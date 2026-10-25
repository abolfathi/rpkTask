using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using OrderManagement.API.Auth;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.API.Tests.Auth;

[Collection(OrderApiCollection.Name)]
public sealed class SearchOrdersApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly OrderApiFactory _factory;
    private readonly HttpClient _client;

    public SearchOrdersApiTests(OrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SearchOrders_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SearchOrders_WithUserToken_EmptyResults_ReturnsOkWithDefaults()
    {
        _factory.Persistence
            .Setup(p => p.SearchOrdersAsync(It.IsAny<OrderSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Order>(), 0));

        var token = await CreateDevTokenAsync(AppRoles.User);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SearchOrdersResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body.Items);
        Assert.Equal(1, body.Page);
        Assert.Equal(20, body.PageSize);
        Assert.Equal(0, body.TotalCount);
    }

    [Theory]
    [InlineData("/api/orders?page=0")]
    [InlineData("/api/orders?pageSize=101")]
    [InlineData("/api/orders?createdFrom=2026-10-10T00:00:00Z&createdTo=2026-10-01T00:00:00Z")]
    public async Task SearchOrders_InvalidQuery_ReturnsBadRequest(string url)
    {
        var token = await CreateDevTokenAsync(AppRoles.User);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("error", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchOrders_WithFilters_PassesCriteriaToPersistence()
    {
        var customerId = Guid.Parse("00000000-0000-4000-8000-000000000001");
        OrderSearchCriteria? captured = null;

        _factory.Persistence
            .Setup(p => p.SearchOrdersAsync(It.IsAny<OrderSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<OrderSearchCriteria, CancellationToken>((c, _) => captured = c)
            .ReturnsAsync((Array.Empty<Order>(), 0));

        var token = await CreateDevTokenAsync(AppRoles.Admin);
        var url =
            $"/api/orders?customerId={customerId}&status=Pending&page=2&pageSize=10";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(captured);
        Assert.Equal(customerId, captured.CustomerId);
        Assert.Equal(OrderStatus.Pending, captured.Status);
        Assert.Equal(2, captured.Page);
        Assert.Equal(10, captured.PageSize);

        _factory.Persistence.Verify(
            p => p.SearchOrdersAsync(It.IsAny<OrderSearchCriteria>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    private async Task<string> CreateDevTokenAsync(string role)
    {
        var response = await _client.PostAsJsonAsync("/api/dev/tokens", new DevTokenRequest
        {
            Role = role,
            Subject = $"search-test-{role.ToLowerInvariant()}"
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DevTokenResponse>(JsonOptions);
        Assert.NotNull(body);
        return body.AccessToken;
    }
}
