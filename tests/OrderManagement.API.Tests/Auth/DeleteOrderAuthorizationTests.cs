using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using OrderManagement.API.Auth;
using OrderManagement.Application.Orders;

namespace OrderManagement.API.Tests.Auth;

[Collection(OrderApiCollection.Name)]
public sealed class DeleteOrderAuthorizationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly OrderApiFactory _factory;
    private readonly HttpClient _client;

    public DeleteOrderAuthorizationTests(OrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeleteOrder_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrder_WithUserToken_ReturnsForbidden()
    {
        var token = await CreateDevTokenAsync(AppRoles.User);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/orders/{Guid.NewGuid()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrder_WithAdminToken_WhenFound_ReturnsNoContent()
    {
        var orderId = Guid.NewGuid();
        _factory.Persistence
            .Setup(p => p.DeleteOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeleteOrderOutcome.Success());

        var token = await CreateDevTokenAsync(AppRoles.Admin);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/orders/{orderId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrder_WithAdminToken_WhenMissing_ReturnsNotFound()
    {
        var orderId = Guid.NewGuid();
        _factory.Persistence
            .Setup(p => p.DeleteOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeleteOrderOutcome.OrderNotFound());

        var token = await CreateDevTokenAsync(AppRoles.Admin);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/orders/{orderId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrder_WithAdminToken_WhenNotAllowed_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        _factory.Persistence
            .Setup(p => p.DeleteOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeleteOrderOutcome.NotAllowed("Orders with status 'Delivered' cannot be deleted."));

        var token = await CreateDevTokenAsync(AppRoles.Admin);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/orders/{orderId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<string> CreateDevTokenAsync(string role)
    {
        var response = await _client.PostAsJsonAsync("/api/dev/tokens", new DevTokenRequest
        {
            Role = role,
            Subject = $"test-{role.ToLowerInvariant()}"
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DevTokenResponse>(JsonOptions);
        Assert.NotNull(body);
        return body.AccessToken;
    }
}
