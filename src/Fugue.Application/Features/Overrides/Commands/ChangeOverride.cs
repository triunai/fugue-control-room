using FluentResults;
using FluentValidation;
using Fugue.Application.Caching;
using Fugue.Application.Features.Overrides.Models;
using Fugue.Application.Features.Overrides.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Overrides.Commands;

/// <summary>Contract.</summary>
public sealed record SetOverrideCommand(UserId UserId, string Permission, bool IsAllowed, string? Reason, DateTimeOffset? ExpiresAt, long? ExpectedVersion = null)
    : ICommand<Result<OverrideDto>>;

public sealed class SetOverrideValidator : AbstractValidator<SetOverrideCommand>
{
    public SetOverrideValidator()
    {
        RuleFor(x => x.UserId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Permission).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class SetOverrideHandler(IOverrideAdmin overrides, ICurrentUser currentUser, TimeProvider time, IPermissionCacheInvalidator permissionCache)
    : ICommandHandler<SetOverrideCommand, Result<OverrideDto>>
{
    public async ValueTask<Result<OverrideDto>> Handle(SetOverrideCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId == command.UserId.Value)
        {
            return Result.Fail<OverrideDto>(OverrideErrors.SelfTarget());
        }

        if (command.ExpiresAt is { } expiry && expiry <= time.GetUtcNow())
        {
            return Result.Fail<OverrideDto>(OverrideErrors.ExpiryInPast());
        }

        Result<OverrideDto> result = await overrides.SetAsync(
            command.UserId, command.Permission.Trim().ToUpperInvariant(), command.IsAllowed, command.Reason, command.ExpiresAt?.ToUniversalTime(), command.ExpectedVersion, cancellationToken);
        if (result.IsSuccess)
        {
            await permissionCache.UserChangedAsync(command.UserId);
        }

        return result;
    }
}

/// <summary>Contract.</summary>
public sealed record RemoveOverrideCommand(UserId UserId, string Permission, long? ExpectedVersion = null) : ICommand<Result>;

public sealed class RemoveOverrideValidator : AbstractValidator<RemoveOverrideCommand>
{
    public RemoveOverrideValidator()
    {
        RuleFor(x => x.UserId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Permission).NotEmpty().MaximumLength(128);
    }
}

public sealed class RemoveOverrideHandler(IOverrideAdmin overrides, ICurrentUser currentUser, IPermissionCacheInvalidator permissionCache)
    : ICommandHandler<RemoveOverrideCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveOverrideCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId == command.UserId.Value)
        {
            return Result.Fail(OverrideErrors.SelfTarget());
        }

        Result result = await overrides.RemoveAsync(command.UserId, command.Permission.Trim().ToUpperInvariant(), command.ExpectedVersion, cancellationToken);
        if (result.IsSuccess)
        {
            await permissionCache.UserChangedAsync(command.UserId);
        }

        return result;
    }
}
