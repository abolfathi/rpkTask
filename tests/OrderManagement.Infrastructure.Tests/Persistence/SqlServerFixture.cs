using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace OrderManagement.Infrastructure.Tests.Persistence;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"OrderManagement_SoftDeleteTests_{Guid.NewGuid():N}";
    private MsSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        ConnectionString = WithDatabase(await ResolveBaseConnectionStringAsync(), _databaseName);

        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            await using var db = CreateContext();
            await db.Database.EnsureDeletedAsync();
        }
        finally
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    public OrderPersistence CreatePersistence(AppDbContext db) => new(db);

    private async Task<string> ResolveBaseConnectionStringAsync()
    {
        var fromEnv = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        if (OperatingSystem.IsWindows())
        {
            return "Server=(localdb)\\mssqllocaldb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
        }

        _container = new MsSqlBuilder().Build();
        await _container.StartAsync();
        return _container.GetConnectionString();
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServerSoftDelete";
}
