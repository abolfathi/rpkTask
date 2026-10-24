# Order Management

.NET 10 solution for a technical assessment: an order-management API built with Clean Architecture. The domain model, SQL Server persistence (EF Core migrations and Development seed data), create-order use case, and `POST /api/orders` endpoint are in place. Other API resources, inventory enforcement, and authentication are not implemented yet.

## Tech stack

| Technology | Version / notes |
|------------|-----------------|
| .NET | 10.0 (`net10.0`) |
| ASP.NET Core | Web API host (`OrderManagement.API`) |
| Entity Framework Core | 10.0.13 (SQL Server provider) |
| Bogus | 35.6.5 (Development seed data) |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` 10.0.9 |
| xUnit / Moq | Unit tests (`OrderManagement.Application.Tests`) |
| Database (Development) | SQL Server LocalDB (`OrderManagement`) |

## Implementation status

| Area | Status | Details |
|------|--------|---------|
| Domain model | **Implemented** | `Customer`, `Product`, `Inventory`, `Order`, `OrderItem`; `OrderStatus` enum (`Pending`, `Confirmed`, `Shipped`, `Delivered`) |
| EF Core persistence | **Implemented** | `AppDbContext`, Fluent API configurations, `InitialCreate` migration |
| Development seeding | **Implemented** | Bogus-based `DevelopmentDataSeeder` via `UseSeeding` / `UseAsyncSeeding` (Development only) |
| Create-order use case | **Implemented** | `CreateOrderService` + `IOrderPersistence` port; Infrastructure `OrderPersistence` adapter |
| `POST /api/orders` | **Implemented** | Creates a `Pending` order; prices from DB; does **not** check or decrement inventory |
| Other HTTP endpoints | **Not present** | No GET/list/update endpoints (including no GET for the `Location` URI returned on create) |
| Inventory enforcement | **Not present** | Stock is not validated or updated when creating an order |
| Authentication / authorization policies | **Not present** | Pipeline calls `UseAuthorization()`; no auth configuration or policies |
| Unit tests | **Implemented** | xUnit + Moq tests for `CreateOrderService` |
| API / integration tests | **Not present** | No test project for the API host |

## Clean Architecture

The solution follows Clean Architecture layering. Dependencies point **inward** toward the Domain. Outer layers may reference inner layers; the Domain has no project references.

| Layer | Project | Responsibility |
|-------|---------|----------------|
| Presentation | `OrderManagement.API` | ASP.NET Core host, HTTP pipeline, `OrdersController` |
| Infrastructure | `OrderManagement.Infrastructure` | EF Core, SQL Server, migrations, Development seeding, `OrderPersistence` |
| Application | `OrderManagement.Application` | Create-order use case, DTOs/result types, `IOrderPersistence` port |
| Domain | `OrderManagement.Domain` | Entities and enums |

```mermaid
flowchart TB
  subgraph presentation [Presentation]
    API[OrderManagement.API]
  end
  subgraph infrastructure [Infrastructure]
    Infra[OrderManagement.Infrastructure]
  end
  subgraph application [Application]
    App[OrderManagement.Application]
  end
  subgraph domain [Domain]
    Dom[OrderManagement.Domain]
  end
  API --> Infra
  API --> App
  Infra --> App
  Infra --> Dom
  App --> Dom
```

## Project structure

```
OrderManagement.sln
src/
├── OrderManagement.API/
│   ├── Controllers/
│   │   └── OrdersController.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
├── OrderManagement.Application/
│   ├── DependencyInjection.cs
│   └── Orders/                      # CreateOrderService, DTOs, IOrderPersistence
├── OrderManagement.Domain/
│   ├── Entities/
│   └── Enums/
└── OrderManagement.Infrastructure/
    ├── DependencyInjection.cs
    └── Persistence/
        ├── AppDbContext.cs
        ├── OrderPersistence.cs
        ├── DevelopmentDataSeeder.cs
        ├── Configurations/
        └── Migrations/
tests/
└── OrderManagement.Application.Tests/
    └── Orders/
        └── CreateOrderServiceTests.cs
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [EF Core command-line tools](https://learn.microsoft.com/ef/core/cli/dotnet) (`dotnet-ef`)
- SQL Server LocalDB (default Development connection), or another SQL Server instance

Install the EF tools if needed:

```bash
dotnet tool install --global dotnet-ef
```

## Local setup

From the repository root:

```bash
dotnet restore
dotnet build
```

## Database: migrations and seed data

Migrations and Development seeding are **not** applied automatically when the API starts. `Program.cs` does not call `Database.Migrate()`, `EnsureCreated()`, or any startup seeding. You must run the EF CLI explicitly.

### Connection string

Development uses `DefaultConnection` in `src/OrderManagement.API/appsettings.Development.json`:

```text
Server=(localdb)\mssqllocaldb;Database=OrderManagement;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

Update that value if you use a different SQL Server instance.

### Apply migrations and seed (Development)

```bash
dotnet ef database update --project src/OrderManagement.Infrastructure --startup-project src/OrderManagement.API
```

This command:

1. Applies pending EF Core migrations (currently `InitialCreate`)
2. In the Development environment, executes the `UseSeeding` / `UseAsyncSeeding` callbacks registered in Infrastructure DI, which call `DevelopmentDataSeeder`

Seeding is registered **only** when the host environment is Development.

### Seed data summary

| Entity | Count | Notes |
|--------|------:|-------|
| Customers | 50 | Deterministic Bogus data; emails `customer001@example.com` … `customer050@example.com` |
| Products | 200 | Deterministic Bogus commerce data |
| Inventories | 200 | One row per product |
| Orders | 0 | Not seeded |
| OrderItems | 0 | Not seeded |

- Bogus `Randomizer.Seed` is fixed at `42` for reproducible data.
- Seeding is idempotent: existing rows with the same Ids are skipped.

## Running the API

Ensure the database has been updated (see above), then from the repository root:

```bash
dotnet run --project src/OrderManagement.API
```

HTTPS profile (HTTP + HTTPS):

```bash
dotnet run --project src/OrderManagement.API --launch-profile https
```

| Profile | URLs |
|---------|------|
| `http` (default) | `http://localhost:5184` |
| `https` | `https://localhost:7038`, `http://localhost:5184` |

`ASPNETCORE_ENVIRONMENT` is `Development` in both launch profiles.

In Development, the OpenAPI document is mapped via `MapOpenApi()`.

### API endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/orders` | Create a new order (`Pending`) |

**Request body:**

```json
{
  "customerId": "00000000-0000-0000-0000-000000000000",
  "items": [
    { "productId": "00000000-0000-0000-0000-000000000000", "quantity": 1 }
  ]
}
```

| Status | When |
|--------|------|
| **201 Created** | Success; body is `CreateOrderResponse`; `Location: /api/orders/{orderId}` |
| **400 Bad Request** | Validation failure (e.g. empty items, non-positive quantity, duplicate product IDs); body `{ "error": "..." }` |
| **404 Not Found** | Customer or product not found; body `{ "error": "..." }` |

There is **no** `GET /api/orders/{id}` (or other order endpoints) despite the `Location` header on create. Unit prices come from current `Product.Price`; inventory is not checked or updated.

## Testing

Application-layer unit tests live in `tests/OrderManagement.Application.Tests` (xUnit + Moq; `IOrderPersistence` is mocked). There are no API or integration tests.

From the repository root:

```bash
dotnet test
```

## Entity relationship diagram

Relationships match the Fluent API configurations (delete behaviors noted below).

| Relationship | Cardinality | Delete behavior |
|--------------|-------------|-----------------|
| Customer → Order | 1 : N | Restrict |
| Order → OrderItem | 1 : N | Cascade |
| Product → OrderItem | 1 : N | Restrict |
| Product → Inventory | 1 : 0..1 | Restrict (unique `ProductId`) |

`Order.Status` is stored as `int` and maps to `OrderStatus`: `Pending`, `Confirmed`, `Shipped`, `Delivered`.

```mermaid
erDiagram
  Customer ||--o{ Order : places
  Order ||--|{ OrderItem : contains
  Product ||--o{ OrderItem : "ordered as"
  Product ||--o| Inventory : has

  Customer {
    guid Id PK
    string Name
    string Email UK
  }

  Product {
    guid Id PK
    string Name
    string Description
    decimal Price
    datetimeoffset CreatedAt
    datetimeoffset UpdatedAt
  }

  Inventory {
    guid Id PK
    guid ProductId FK_UK
    int Quantity
    datetimeoffset UpdatedAt
  }

  Order {
    guid Id PK
    guid CustomerId FK
    int Status
    decimal TotalAmount
    datetimeoffset CreatedAt
    datetimeoffset UpdatedAt
  }

  OrderItem {
    guid Id PK
    guid OrderId FK
    guid ProductId FK
    int Quantity
    decimal UnitPrice
  }
```

## Application startup and database initialization

Migrations and seeding run only through the EF CLI path below—not during `dotnet run`.

```mermaid
flowchart TD
  subgraph apiStart [API startup: dotnet run]
    Build[Build WebApplication]
    DI[Register AddApplication and AddInfrastructure]
    SeedReg{"Is Development?"}
    RegisterSeed[Register UseSeeding and UseAsyncSeeding callbacks]
    SkipSeed[Skip seeding registration]
    Pipeline[Configure pipeline: OpenAPI if Development, MapControllers]
    Listen[Listen for HTTP requests]
    Build --> DI
    DI --> SeedReg
    SeedReg -->|yes| RegisterSeed
    SeedReg -->|no| SkipSeed
    RegisterSeed --> Pipeline
    SkipSeed --> Pipeline
    Pipeline --> Listen
  end

  subgraph dbInit [Explicit DB setup: dotnet ef database update]
    EfCli[EF Core CLI]
    Migrate[Apply pending migrations]
    InvokeSeed[Invoke UseSeeding / UseAsyncSeeding in Development]
    EfCli --> Migrate
    Migrate --> InvokeSeed
  end

  Note[Program.cs does not call Migrate or EnsureCreated]
  Note -.-> Listen
  Note -.-> EfCli
```
