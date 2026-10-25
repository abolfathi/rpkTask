using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OrderManagement.API.Auth;

namespace OrderManagement.API.Tests.Auth;

[Collection(OrderApiCollection.Name)]
public sealed class AuthenticationAuthorizationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;

    public AuthenticationAuthorizationTests(OrderApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetOrder_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_WithUserToken_PassesAuthentication()
    {
        var token = await CreateDevTokenAsync(AppRoles.User);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{Guid.NewGuid()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_WithAdminToken_PassesAuthentication()
    {
        var token = await CreateDevTokenAsync(AppRoles.Admin);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{Guid.NewGuid()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(AppRoles.User)]
    [InlineData(AppRoles.Admin)]
    public async Task CreateDevToken_WithKnownRole_ReturnsBearerToken(string role)
    {
        var response = await _client.PostAsJsonAsync("/api/dev/tokens", new DevTokenRequest
        {
            Role = role,
            Subject = $"test-{role}"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DevTokenResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal("Bearer", body.TokenType);
        Assert.Equal(role, body.Role);
    }

    [Fact]
    public async Task CreateDevToken_WithInvalidRole_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/dev/tokens", new DevTokenRequest
        {
            Role = "SuperAdmin"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
