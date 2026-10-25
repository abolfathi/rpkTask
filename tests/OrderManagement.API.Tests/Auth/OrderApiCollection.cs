namespace OrderManagement.API.Tests.Auth;

[CollectionDefinition(Name)]
public sealed class OrderApiCollection : ICollectionFixture<OrderApiFactory>
{
    public const string Name = "OrderApi";
}
