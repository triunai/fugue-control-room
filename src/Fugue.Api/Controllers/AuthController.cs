using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.OpenApi;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Auth;
using Fugue.Application.Features.Auth.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fugue.Api.Controllers;

/// <summary>Token issuance. Anonymous by necessity; both rate limits run before any password work.</summary>
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Exchange an email and password for an access token and a refresh token. Rate limited per client address and per email.</summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status401Unauthorized, "auth.invalid_credentials")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.IpPolicy)]
    [ServiceFilter(typeof(EmailRateLimitFilter))]
    public Task<ActionResult<TokenDto>> Token(LoginRequest body, CancellationToken ct) =>
        Send(new LoginCommand(body.Email, body.Password, Client()), ct);

    /// <summary>Rotates a refresh token. The refresh token IS the credential, so this is anonymous; the per-IP rate limit applies.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status401Unauthorized, "auth.invalid_refresh_token")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.IpPolicy)]
    public Task<ActionResult<TokenDto>> Refresh(RefreshRequest body, CancellationToken ct) =>
        Send(new RefreshTokenCommand(body.RefreshToken, Client()), ct);

    /// <summary>Ends the session by revoking the token's whole family. Always 204.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.IpPolicy)]
    public Task<IActionResult> Logout(LogoutRequest body, CancellationToken ct) =>
        SendNoContent(new LogoutCommand(body.RefreshToken, Client()), ct);

    private ClientInfo Client() =>
        new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua[..Math.Min(ua.Length, 256)] : null);
}
