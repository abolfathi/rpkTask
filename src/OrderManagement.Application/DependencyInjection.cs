using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application.Orders;

namespace OrderManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateOrderService>();
        services.AddScoped<BulkCreateOrdersService>();
        services.AddScoped<GetOrderService>();
        services.AddScoped<SearchOrdersService>();
        services.AddScoped<UpdateOrderStatusService>();
        services.AddScoped<DeleteOrderService>();
        return services;
    }
}
