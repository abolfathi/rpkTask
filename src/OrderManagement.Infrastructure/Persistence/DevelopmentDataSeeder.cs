using Bogus;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.Persistence;

public static class DevelopmentDataSeeder
{
    private const int SeedValue = 42;
    private static readonly DateTimeOffset SeedTimestamp = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<Customer>? _customers;
    private static IReadOnlyList<ProductSeed>? _products;

    public static void Seed(AppDbContext context)
    {
        var customers = GetCustomers();
        var products = GetProducts();

        var existingCustomerIds = context.Customers.Select(c => c.Id).ToHashSet();
        var customersToAdd = customers.Where(c => !existingCustomerIds.Contains(c.Id)).ToList();
        if (customersToAdd.Count > 0)
        {
            context.Customers.AddRange(customersToAdd);
            context.SaveChanges();
        }

        var existingProductIds = context.Products.Select(p => p.Id).ToHashSet();
        var productsToAdd = products
            .Where(p => !existingProductIds.Contains(p.Product.Id))
            .Select(p => p.Product)
            .ToList();
        if (productsToAdd.Count > 0)
        {
            context.Products.AddRange(productsToAdd);
            context.SaveChanges();
        }

        var existingInventoryProductIds = context.Inventories.Select(i => i.ProductId).ToHashSet();
        var inventoriesToAdd = products
            .Where(p => !existingInventoryProductIds.Contains(p.Product.Id))
            .Select(p => p.Inventory)
            .ToList();
        if (inventoriesToAdd.Count > 0)
        {
            context.Inventories.AddRange(inventoriesToAdd);
            context.SaveChanges();
        }
    }

    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var customers = GetCustomers();
        var products = GetProducts();

        var existingCustomerIds = await context.Customers.Select(c => c.Id).ToHashSetAsync(cancellationToken);
        var customersToAdd = customers.Where(c => !existingCustomerIds.Contains(c.Id)).ToList();
        if (customersToAdd.Count > 0)
        {
            context.Customers.AddRange(customersToAdd);
            await context.SaveChangesAsync(cancellationToken);
        }

        var existingProductIds = await context.Products.Select(p => p.Id).ToHashSetAsync(cancellationToken);
        var productsToAdd = products
            .Where(p => !existingProductIds.Contains(p.Product.Id))
            .Select(p => p.Product)
            .ToList();
        if (productsToAdd.Count > 0)
        {
            context.Products.AddRange(productsToAdd);
            await context.SaveChangesAsync(cancellationToken);
        }

        var existingInventoryProductIds = await context.Inventories.Select(i => i.ProductId).ToHashSetAsync(cancellationToken);
        var inventoriesToAdd = products
            .Where(p => !existingInventoryProductIds.Contains(p.Product.Id))
            .Select(p => p.Inventory)
            .ToList();
        if (inventoriesToAdd.Count > 0)
        {
            context.Inventories.AddRange(inventoriesToAdd);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static IReadOnlyList<Customer> GetCustomers()
    {
        if (_customers is not null)
        {
            return _customers;
        }

        Randomizer.Seed = new Random(SeedValue);

        var faker = new Faker<Customer>()
            .StrictMode(true)
            .RuleFor(c => c.Id, (_, _) => Guid.Empty)
            .RuleFor(c => c.Name, f => f.Name.FullName())
            .RuleFor(c => c.Email, (f, c) => f.Internet.Email(c.Name).ToLowerInvariant());

        var customers = new List<Customer>(50);
        for (var i = 1; i <= 50; i++)
        {
            var customer = faker.Generate();
            customer.Id = CreateStableGuid(0x8000, i);
            customer.Email = $"customer{i:D3}@example.com";
            customers.Add(customer);
        }

        _customers = customers;
        return _customers;
    }

    private static IReadOnlyList<ProductSeed> GetProducts()
    {
        if (_products is not null)
        {
            return _products;
        }

        Randomizer.Seed = new Random(SeedValue);

        var productFaker = new Faker<Product>()
            .StrictMode(true)
            .RuleFor(p => p.Id, _ => Guid.Empty)
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
            .RuleFor(p => p.Price, f => Math.Round(f.Random.Decimal(5.99m, 499.99m), 2))
            .RuleFor(p => p.CreatedAt, _ => SeedTimestamp)
            .RuleFor(p => p.UpdatedAt, _ => SeedTimestamp)
            .RuleFor(p => p.Inventory, _ => null);

        var quantityFaker = new Faker();
        quantityFaker.Random = new Randomizer(SeedValue);

        var products = new List<ProductSeed>(200);
        for (var i = 1; i <= 200; i++)
        {
            var product = productFaker.Generate();
            product.Id = CreateStableGuid(0x8001, i);
            product.Name = $"{product.Name} {i:D3}";
            if (product.Description.Length > 2000)
            {
                product.Description = product.Description[..2000];
            }

            var inventory = new Inventory
            {
                Id = CreateStableGuid(0x8002, i),
                ProductId = product.Id,
                Quantity = quantityFaker.Random.Int(10, 500),
                UpdatedAt = SeedTimestamp
            };

            products.Add(new ProductSeed(product, inventory));
        }

        _products = products;
        return _products;
    }

    private static Guid CreateStableGuid(int discriminator, int index) =>
        Guid.Parse($"00000000-0000-4000-{discriminator:x4}-{index:D12}");

    private sealed record ProductSeed(Product Product, Inventory Inventory);
}
