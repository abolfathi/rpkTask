namespace OrderManagement.API.Auth;

public sealed class DevTokenRequest
{
    public string Role { get; set; } = string.Empty;
    public string? Subject { get; set; }
}

public sealed class DevTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTimeOffset ExpiresAt { get; set; }
    public string Role { get; set; } = string.Empty;
}
