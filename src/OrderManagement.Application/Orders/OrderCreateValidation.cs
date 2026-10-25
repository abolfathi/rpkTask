namespace OrderManagement.Application.Orders;

internal static class OrderCreateValidation
{
    /// <summary>
    /// Validates items for create/bulk create. Returns an error message, or null if valid.
    /// </summary>
    public static string? ValidateItems(IReadOnlyList<CreateOrderItemRequest>? items)
    {
        if (items is null || items.Count == 0)
        {
            return "Order must contain at least one item.";
        }

        if (items.Any(i => i.Quantity <= 0))
        {
            return "All item quantities must be greater than zero.";
        }

        var productIds = items.Select(i => i.ProductId).ToList();
        if (productIds.Count != productIds.Distinct().Count())
        {
            return "Duplicate product IDs are not allowed.";
        }

        return null;
    }
}
