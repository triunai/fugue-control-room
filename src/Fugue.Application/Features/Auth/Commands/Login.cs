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

public sealed class LoginHandler(IAuthStore store, IPasswordVerifier verifier, IAccessTokenIssuer issuer, IRefreshTokenStore refreshTokens, IAccessAuditWriter audit, TimeProvider time)
    : ICommandHandler<LoginCommand, Result<TokenDto>>
{
    public async ValueTask<Result<TokenDto>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        LoginUser? user = await store.FindLoginUserAsync(EmailKey.Canonicalize(command.Email), cancellationToken);

        // Always one verification (a dummy hash when there is no user) so timing does not reveal existence.
        bool passwordOk = verifier.Verify(user?.PasswordHash, command.Password);
        if (user is null || !passwordOk || !user.IsActive || !user.LocalLoginEnabled)
        {
            // The client sees one uniform failure; the audit row keeps the real reason for operators.
            string reason = user is null ? "unknown_user" : !passwordOk ? "bad_password" : !user.IsActive ? "inactive" : "local_login_disabled";
            await audit.WriteAsync(new AccessAuditEntry("login", "failure", reason, user?.Id, command.Email, command.Client), cancellationToken);
            return Result.Fail<TokenDto>(AuthErrors.InvalidCredentials());
        }

        (string refresh, string refreshHash) = RefreshTokenSecret.Generate();
        await refreshTokens.CreateAsync(
            new NewRefreshToken(user.Id, refreshHash, Guid.NewGuid(), time.GetUtcNow() + issuer.RefreshLifetime, command.Client), cancellationToken);

        await audit.WriteAsync(new AccessAuditEntry("login", "success", null, user.Id, command.Email, command.Client), cancellationToken);
        IssuedToken token = issuer.Issue(user.Id);
        int expiresIn = (int)Math.Max(0, (token.ExpiresAt - time.GetUtcNow()).TotalSeconds);
        return Result.Ok(new TokenDto(token.Value, "Bearer", expiresIn, refresh));
    }
}
