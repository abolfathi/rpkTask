# Order Management

.NET 10 solution for a technical assessment: an order-management API built with Clean Architecture. The domain model, SQL Server persistence (EF Core migrations and Development seed data), create/get/search orders, status transitions (including inventory deduct on confirm), and JWT Bearer authentication are implemented. Order deletion is not implemented (planned later as Admin-only).

## Tech stack

| Technology | Version / notes |
|------------|-----------------|
| .NET | 10.0 (`net10.0`) |
| ASP.NET Core | Web API host (`OrderManagement.API`) |
| Entity Framework Core | 10.0.13 (SQL Server provider) |
| Bogus | 35.6.5 (Development seed data) |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` 10.0.9 |
| Swagger UI | `Swashbuckle.AspNetCore.SwaggerUI` (Development; consumes `/openapi/v1.json`; Bearer authorize) |
| JWT Bearer | `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.9 |
| Serilog | `Serilog.AspNetCore` 10.0.0; Console + rolling File sinks |
| xUnit / Moq | Application unit tests; API auth tests (`Microsoft.AspNetCore.Mvc.Testing`) |
| Database (Development) | SQL Server LocalDB (`OrderManagement`) |

## Implementation status

| Area | Status | Details |
|------|--------|---------|
| Domain model | **Implemented** | `Customer`, `Product`, `Inventory`, `Order`, `OrderItem`; `OrderStatus` enum (`Pending`, `Confirmed`, `Shipped`, `Delivered`) |
| EF Core persistence | **Implemented** | `AppDbContext`, Fluent API configurations, `InitialCreate` migration |
| Development seeding | **Implemented** | Bogus-based `DevelopmentDataSeeder` via `UseSeeding` / `UseAsyncSeeding` (Development only) |
| Create-order use case | **Implemented** | `CreateOrderService` + `IOrderPersistence`; creates `Pending` order; prices from DB; no inventory check/deduct on create |
| Get order by ID | **Implemented** | `GetOrderService`; `GET /api/orders/{id}` |
| Search orders | **Implemented** | `SearchOrdersService`; filters, pagination, sort by `CreatedAt` desc then `Id` desc |
| Status transitions | **Implemented** | `UpdateOrderStatusService`; `PATCH /api/orders/{id}/status`; forward-only `Pending → Confirmed → Shipped → Delivered` |
| Inventory on confirm | **Implemented** | Confirm validates stock for all lines and deducts atomically in a DB transaction |
| Order deletion | **Not present** | No delete endpoint (future: Admin-only) |
| Authentication / authorization | **Implemented** | JWT Bearer; roles `Admin` and `User`; all current order endpoints require any authenticated role (`[Authorize]`) |
| Structured logging | **Implemented** | Serilog on the API host; Console + rolling File; HTTP request logging |
| Unit tests | **Implemented** | xUnit + Moq for create, get, search, and status-update application services |
| API auth tests | **Implemented** | `OrderManagement.API.Tests` — 401 without token, Admin/User access, Dev token endpoint |

## Clean Architecture

The solution follows Clean Architecture layering. Dependencies point **inward** toward the Domain. Outer layers may reference inner layers; the Domain has no project references.

| Layer | Project | Responsibility |
|-------|---------|----------------|
| Presentation | `OrderManagement.API` | ASP.NET Core host, JWT auth, Serilog, `OrdersController`, Dev token endpoint |
| Infrastructure | `OrderManagement.Infrastructure` | EF Core, SQL Server, migrations, Development seeding, `OrderPersistence` |
| Application | `OrderManagement.Application` | Order use cases (create/get/search/status), DTOs/results, `IOrderPersistence` port |
| Domain | `OrderManagement.Domain` | Entities, enums, status transition policy |

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
│   ├── Auth/                        # JwtOptions, token service, roles
│   ├── Controllers/
│   │   ├── OrdersController.cs
│   │   └── DevTokensController.cs   # Development test JWTs
│   ├── OpenApi/                     # Bearer security scheme transformers
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
├── OrderManagement.Application/
│   ├── DependencyInjection.cs
│   └── Orders/                      # Create/Get/Search/Status services, DTOs, IOrderPersistence
├── OrderManagement.Domain/
│   ├── Entities/
│   ├── Enums/
│   └── Orders/                      # OrderStatusTransitionPolicy
└── OrderManagement.Infrastructure/
    ├── DependencyInjection.cs
    └── Persistence/
        ├── AppDbContext.cs
        ├── OrderPersistence.cs
        ├── DevelopmentDataSeeder.cs
        ├── Configurations/
        └── Migrations/
tests/
├── OrderManagement.Application.Tests/
│   └── Orders/
│       ├── CreateOrderServiceTests.cs
│       ├── GetOrderServiceTests.cs
│       ├── SearchOrdersServiceTests.cs
│       └── UpdateOrderStatusServiceTests.cs
└── OrderManagement.API.Tests/
    └── Auth/
        ├── OrderApiFactory.cs
        └── AuthenticationAuthorizationTests.cs
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

### JWT signing key (required)

`Jwt:Key` is **not** stored in source control. Configure it before running the API:

```bash
dotnet user-secrets set "Jwt:Key" "<at-least-32-character-secret>" --project src/OrderManagement.API
```

Or set environment variables (useful for CI / non-Dev hosts):

| Variable | Purpose |
|----------|---------|
| `Jwt__Key` | HMAC signing key (min 32 characters) |
| `Jwt__Issuer` | Optional override (default `OrderManagement`) |
| `Jwt__Audience` | Optional override (default `OrderManagement`) |

`appsettings.json` / `appsettings.Development.json` only contain non-secret JWT defaults (`Issuer`, `Audience`, `ExpirationMinutes`). The host fails fast at startup if `Jwt:Key` is missing or too short.

### Logging (Serilog)

The API host uses **Serilog** (not Application/Infrastructure packages):

| Sink | Details |
|------|---------|
| Console | Timestamp, level, source context, message, exception |
| File | `Logs/order-management-.log`, daily rolling, 14 files retained (`Logs/` is gitignored) |

| Environment | Default level | Notes |
|-------------|---------------|-------|
| Base / Production-oriented | `Information` | `Microsoft.AspNetCore` and `Microsoft.EntityFrameworkCore` overridden to `Warning` |
| Development | `Debug` | Same AspNetCore/EF overrides |

HTTP requests are logged once via `UseSerilogRequestLogging` (method, path, status, elapsed, optional `RequestId`). Request/response bodies, Authorization headers, JWT keys, and connection strings are not logged. Startup failures use `Log.Fatal`; there is no separate exception middleware that changes HTTP responses.

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

`ASPNETCORE_ENVIRONMENT` is `Development` in both launch profiles. Launch profiles open Swagger UI (`/swagger`).

### OpenAPI and Swagger UI (Development)

| Resource | URL |
|----------|-----|
| OpenAPI document | `/openapi/v1.json` |
| Swagger UI | `/swagger` |

Swagger UI is enabled only in Development and uses the built-in OpenAPI document (no second document generator). The OpenAPI document declares an HTTP Bearer JWT scheme. In Swagger UI, use **Authorize**, paste the access token (without the `Bearer ` prefix), then call protected endpoints. Authorization is persisted in the browser session for convenience.

### Authentication and authorization

| Item | Detail |
|------|--------|
| Scheme | JWT Bearer (`Authorization: Bearer <token>`) |
| Roles | `Admin`, `User` (role claim for future Admin-only features such as delete) |
| Order endpoints | Require authentication; **either role** may call all current order APIs |
| Identity / refresh tokens | Not implemented |

#### Endpoint permissions

| Method | Route | Auth |
|--------|-------|------|
| `POST` | `/api/orders` | Authenticated (`Admin` or `User`) |
| `GET` | `/api/orders/{id}` | Authenticated (`Admin` or `User`) |
| `GET` | `/api/orders` | Authenticated (`Admin` or `User`) |
| `PATCH` | `/api/orders/{id}/status` | Authenticated (`Admin` or `User`) |
| `POST` | `/api/dev/tokens` | Anonymous; **Development only** (404 otherwise) |

Unauthenticated calls to order endpoints return **401 Unauthorized**.

#### Development test tokens — `POST /api/dev/tokens`

Available only when `ASPNETCORE_ENVIRONMENT=Development`. Issues a signed JWT for local testing (no user store / Identity).

**Request:**

```json
{
  "role": "User",
  "subject": "optional-subject"
}
```

`role` must be `Admin` or `User`. `subject` is optional (defaults to `dev-user` / `dev-admin`).

**Response:**

```json
{
  "accessToken": "<jwt>",
  "tokenType": "Bearer",
  "expiresAt": "2026-10-25T12:00:00+00:00",
  "role": "User"
}
```

**Example:**

```http
POST /api/dev/tokens
Content-Type: application/json

{ "role": "Admin" }
```

Then call orders with `Authorization: Bearer <accessToken>`, or paste the token into Swagger UI Authorize.

### API endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/orders` | Create a new order (`Pending`); requires auth |
| `GET` | `/api/orders/{id}` | Get a single order by ID (includes line items); requires auth |
| `GET` | `/api/orders` | Search orders with optional filters and pagination; requires auth |
| `PATCH` | `/api/orders/{id}/status` | Advance order status (inventory deducted on confirm); requires auth |
| `POST` | `/api/dev/tokens` | Development: issue test JWT (`Admin` or `User`) |

#### Create order — `POST /api/orders`

**Request body:**

```json
{
  "customerId": "00000000-0000-4000-8000-000000000001",
  "items": [
    { "productId": "00000000-0000-4000-8001-000000000001", "quantity": 1 }
  ]
}
```

| Status | When |
|--------|------|
| **201 Created** | Success; body is order detail; `Location: /api/orders/{orderId}` |
| **400 Bad Request** | Validation failure (empty items, non-positive quantity, duplicate product IDs); body `{ "error": "..." }` |
| **401 Unauthorized** | Missing or invalid bearer token |
| **404 Not Found** | Customer or product not found; body `{ "error": "..." }` |

Unit prices come from current `Product.Price`; inventory is not checked or updated.

#### Get order — `GET /api/orders/{id}`

| Status | When |
|--------|------|
| **200 OK** | Order found (includes items) |
| **401 Unauthorized** | Missing or invalid bearer token |
| **404 Not Found** | No order with that ID; body `{ "error": "..." }` |

#### Search orders — `GET /api/orders`

**Query parameters (all optional except defaults for paging):**

| Parameter | Description |
|-----------|-------------|
| `customerId` | Filter by customer |
| `status` | Filter by `OrderStatus` (`Pending`, `Confirmed`, `Shipped`, `Delivered`) |
| `createdFrom` | Inclusive lower bound on `CreatedAt` |
| `createdTo` | Inclusive upper bound on `CreatedAt` |
| `page` | Page number (default `1`, must be ≥ 1) |
| `pageSize` | Page size (default `20`, must be 1–100) |

Results are sorted by **`CreatedAt` descending**, then **`Id` descending**. Queries use EF Core `AsNoTracking` with server-side filter/sort/page.

**Example:**

```http
GET /api/orders?customerId=00000000-0000-4000-8000-000000000001&status=Pending&page=1&pageSize=20
```

**Response shape:**

```json
{
  "items": [ /* order detail objects */ ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0
}
```

| Status | When |
|--------|------|
| **200 OK** | Success (empty `items` when no matches) |
| **400 Bad Request** | Invalid pagination or `createdFrom` > `createdTo`; body `{ "error": "..." }` |
| **401 Unauthorized** | Missing or invalid bearer token |

```mermaid
sequenceDiagram
  participant Client
  participant Api as OrdersController
  participant Search as SearchOrdersService
  participant Persist as IOrderPersistence

  Client->>Api: GET /api/orders?filters&page
  Api->>Search: SearchAsync(request)
  Search->>Search: Validate page/pageSize/dates
  Search->>Persist: SearchOrdersAsync(criteria)
  Persist-->>Search: items + totalCount
  Search-->>Api: SearchOrdersResponse
  Api-->>Client: 200 or 400
```

### Order status workflow

Orders move forward one step at a time. There is no `Cancelled` status and no reverse transitions.

```mermaid
stateDiagram-v2
  [*] --> Pending
  Pending --> Confirmed: inventory OK and deducted
  Confirmed --> Shipped
  Shipped --> Delivered
  Delivered --> [*]
```

| From | To | Side effects |
|------|----|--------------|
| `Pending` | `Confirmed` | Re-check stock for all lines; deduct inventory; set status — all in one DB transaction |
| `Confirmed` | `Shipped` | Status only (conditional update) |
| `Shipped` | `Delivered` | Status only (conditional update) |

Any other target (including skipping steps) is rejected with **400**. Creating an order still does **not** touch inventory; stock is enforced only when confirming.

Concurrency: confirm uses conditional inventory updates (`Quantity >= required`) and a conditional status update (`Status == Pending`) so concurrent confirms cannot drive stock negative or double-confirm. Non-confirm steps update only when the expected current status still matches.

#### Update status — `PATCH /api/orders/{id}/status`

**Request body:**

```json
{
  "status": "Confirmed"
}
```

`status` accepts enum names (`Pending`, `Confirmed`, `Shipped`, `Delivered`) or their integer values. Cancellation is not supported.

| Status | When |
|--------|------|
| **200 OK** | Transition applied; body is order detail |
| **400 Bad Request** | Invalid transition or invalid status value; body `{ "error": "..." }` |
| **401 Unauthorized** | Missing or invalid bearer token |
| **404 Not Found** | Order not found; body `{ "error": "..." }` |
| **409 Conflict** | Insufficient inventory or concurrent modification; body `{ "error": "..." }` |

**Example (confirm):**

```http
PATCH /api/orders/{orderId}/status
Content-Type: application/json

{ "status": "Confirmed" }
```

## Testing

| Project | Coverage |
|---------|----------|
| `tests/OrderManagement.Application.Tests` | Application services (xUnit + Moq; `IOrderPersistence` mocked) |
| `tests/OrderManagement.API.Tests` | JWT auth: 401 without token, Admin/User can reach orders, Dev token create/validation |

API auth tests use `WebApplicationFactory` with an in-memory `Jwt:Key` and a stubbed `IOrderPersistence` (no SQL Server required for those cases).

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
    Pipeline[Configure pipeline: OpenAPI and Swagger UI if Development, MapControllers]
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
