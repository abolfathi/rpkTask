using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderManagement.Application.Orders;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));

            if (environment.IsDevelopment())
            {
                options.UseSeeding((context, _) =>
                    DevelopmentDataSeeder.Seed((AppDbContext)context));

                options.UseAsyncSeeding(async (context, _, cancellationToken) =>
                    await DevelopmentDataSeeder.SeedAsync((AppDbContext)context, cancellationToken));
            }
        });

        services.AddScoped<IOrderPersistence, OrderPersistence>();

        return services;
    }
}
