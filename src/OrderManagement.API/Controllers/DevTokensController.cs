using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.API.Auth;

namespace OrderManagement.API.Controllers;

[ApiController]
[Route("api/dev/tokens")]
[Tags("Development")]
[AllowAnonymous]
public sealed class DevTokensController : ControllerBase
{
    private readonly JwtTokenService _tokenService;
    private readonly IHostEnvironment _environment;

    public DevTokensController(JwtTokenService tokenService, IHostEnvironment environment)
    {
        _tokenService = tokenService;
        _environment = environment;
    }

    [HttpPost]
    [EndpointSummary("Create Development test JWT")]
    [EndpointDescription(
        "Development only. Issues a signed JWT for local testing with role Admin or User. " +
        "Returns 404 outside the Development environment.")]
    [ProducesResponseType(typeof(DevTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Create([FromBody] DevTokenRequest request)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (!AppRoles.IsKnownRole(request.Role))
        {
            return BadRequest(new { error = "Role must be 'Admin' or 'User'." });
        }

        var role = AppRoles.Normalize(request.Role);
        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? $"dev-{role.ToLowerInvariant()}"
            : request.Subject.Trim();

        var (accessToken, expiresAt) = _tokenService.CreateToken(subject, role);

        return Ok(new DevTokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresAt = expiresAt,
            Role = role
        });
    }
}
