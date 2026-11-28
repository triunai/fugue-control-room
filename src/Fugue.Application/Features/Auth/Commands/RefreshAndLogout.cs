using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Auth.Helpers;
using Fugue.Application.Features.Auth.Models;
using Fugue.Domain.Audit;
using Mediator;

namespace Fugue.Application.Features.Auth.Commands;

/// <summary>Contract. Rotation: the presented token dies, a new one in the same family is issued; reusing a dead one revokes the family.</summary>
[NoAudit] // recorded in access_audit, not audit_log (ADR-010)
public sealed record RefreshTokenCommand(string RefreshToken, ClientInfo Client) : ICommand<Result<TokenDto>>;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(256);
}

public sealed class RefreshTokenHandler(IRefreshTokenStore store, IAccessTokenIssuer issuer, IAccessAuditWriter audit, TimeProvider time)
    : ICommandHandler<RefreshTokenCommand, Result<TokenDto>>
{
    public async ValueTask<Result<TokenDto>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        (string next, string nextHash) = RefreshTokenSecret.Generate();

        // The store takes the user and family from the token it claims; only the replacement's secret, lifetime and client come from here.
        RotationResult result = await store.RotateAsync(
            RefreshTokenSecret.Hash(command.RefreshToken), new RotationRequest(nextHash, now + issuer.RefreshLifetime, command.Client), now, cancellationToken);
        if (result.Outcome != RotationOutcome.Rotated || result.UserId is not { } userId)
        {
            string name = result.Outcome == RotationOutcome.Reused ? "refresh_reuse" : "refresh";
            await audit.WriteAsync(new AccessAuditEntry(name, "failure", result.Outcome.ToString().ToLowerInvariant(), null, null, command.Client), cancellationToken);
            return Result.Fail<TokenDto>(AuthErrors.InvalidRefreshToken());
        }

        await audit.WriteAsync(new AccessAuditEntry("refresh", "success", null, userId, null, command.Client), cancellationToken);
        IssuedToken access = issuer.Issue(userId);
        return Result.Ok(new TokenDto(access.Value, "Bearer", (int)Math.Max(0, (access.ExpiresAt - now).TotalSeconds), next));
    }
}

/// <summary>Contract. Ends the session: the token's whole family is revoked. Always succeeds, so it reveals nothing about the token.</summary>
[NoAudit] // recorded in access_audit, not audit_log (ADR-010)
public sealed record LogoutCommand(string RefreshToken, ClientInfo Client) : ICommand<Result>;

public sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(256);
}

public sealed class LogoutHandler(IRefreshTokenStore store, IAccessAuditWriter audit, TimeProvider time) : ICommandHandler<LogoutCommand, Result>
{
    public async ValueTask<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        await store.RevokeFamilyAsync(RefreshTokenSecret.Hash(command.RefreshToken), time.GetUtcNow(), cancellationToken);
        await audit.WriteAsync(new AccessAuditEntry("logout", "success", null, null, null, command.Client), cancellationToken);
        return Result.Ok();
    }
}
