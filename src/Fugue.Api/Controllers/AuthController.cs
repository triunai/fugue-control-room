using Fugue.Api.Auth;
using Fugue.Api.Contracts;
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
    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.IpPolicy)]
    [ServiceFilter(typeof(EmailRateLimitFilter))]
    public Task<ActionResult<TokenDto>> Token(LoginRequest body, CancellationToken ct) =>
        Send(new LoginCommand(body.Email, body.Password, Client()), ct);

    private ClientInfo Client() =>
        new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua[..Math.Min(ua.Length, 256)] : null);
}
