using Microsoft.EntityFrameworkCore;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Infrastructure.Tests.Persistence;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"OrderManagement_SoftDeleteTests_{Guid.NewGuid():N}";

    public string ConnectionString =>
        $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    public OrderPersistence CreatePersistence(AppDbContext db) => new(db);
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServerSoftDelete";
}
