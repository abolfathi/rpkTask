using Microsoft.Extensions.Options;

namespace OrderManagement.API.Auth;

internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            return ValidateOptionsResult.Fail("Jwt:Issuer must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            return ValidateOptionsResult.Fail("Jwt:Audience must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Key) || options.Key.Length < 32)
        {
            return ValidateOptionsResult.Fail(
                "Jwt:Key must be configured with at least 32 characters. " +
                "Use user secrets (Development) or environment variable Jwt__Key; do not commit secrets.");
        }

        if (options.ExpirationMinutes <= 0)
        {
            return ValidateOptionsResult.Fail("Jwt:ExpirationMinutes must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}
