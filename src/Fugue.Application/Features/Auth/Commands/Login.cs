using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Domain.Identity;
using Mediator;

namespace Fugue.Application.Features.Auth.Commands;

/// <summary>Contract. Password is never logged (LoggingBehavior logs names and outcomes only).</summary>
public sealed record LoginCommand(string Email, string Password, ClientInfo Client) : ICommand<Result<TokenDto>>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        // Presence and size only: format rules would turn validation into an account oracle.
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}

public sealed class LoginHandler(IAuthStore store, IPasswordVerifier verifier, IAccessTokenIssuer issuer, IRefreshTokenStore refreshTokens, TimeProvider time)
    : ICommandHandler<LoginCommand, Result<TokenDto>>
{
    public async ValueTask<Result<TokenDto>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        LoginUser? user = await store.FindLoginUserAsync(EmailKey.Canonicalize(command.Email), cancellationToken);

        // Always one verification (a dummy hash when there is no user) so timing does not reveal existence.
        bool passwordOk = verifier.Verify(user?.PasswordHash, command.Password);
        if (user is null || !passwordOk || !user.IsActive || !user.LocalLoginEnabled)
        {
            return Result.Fail<TokenDto>(AuthErrors.InvalidCredentials());
        }

        (string refresh, string refreshHash) = RefreshTokenSecret.Generate();
        await refreshTokens.CreateAsync(
            new NewRefreshToken(user.Id, refreshHash, Guid.NewGuid(), time.GetUtcNow() + issuer.RefreshLifetime, command.Client), cancellationToken);

        IssuedToken token = issuer.Issue(user.Id);
        int expiresIn = (int)Math.Max(0, (token.ExpiresAt - time.GetUtcNow()).TotalSeconds);
        return Result.Ok(new TokenDto(token.Value, "Bearer", expiresIn, refresh));
    }
}
