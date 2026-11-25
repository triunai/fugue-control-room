namespace Fugue.Application.Features.Auth;

/// <summary>The token response. <see cref="RefreshToken"/> is shown once and never stored in clear.</summary>
public sealed record TokenDto(string AccessToken, string TokenType, int ExpiresIn, string? RefreshToken);
