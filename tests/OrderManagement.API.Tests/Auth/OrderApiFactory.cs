using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Entities;

namespace OrderManagement.API.Tests.Auth;

public sealed class OrderApiFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "integration-test-signing-key-at-least-32-chars!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "OrderManagement.Tests",
                ["Jwt:Audience"] = "OrderManagement.Tests",
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:ExpirationMinutes"] = "60",
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\mssqllocaldb;Database=OrderManagement_AuthTests;Trusted_Connection=True;TrustServerCertificate=True",
                // Quiet Serilog in tests: raise level and avoid rolling files under the repo.
                ["Serilog:MinimumLevel:Default"] = "Warning",
                ["Serilog:WriteTo:0:Name"] = "Console",
                ["Serilog:WriteTo:1:Name"] = "Console"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOrderPersistence>();

            var persistence = new Mock<IOrderPersistence>();
            persistence
                .Setup(p => p.GetOrderByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order?)null);
            persistence
                .Setup(p => p.GetOrderForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order?)null);

            services.AddSingleton(persistence.Object);
        });
    }
}
