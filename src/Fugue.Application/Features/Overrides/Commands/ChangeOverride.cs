using FluentResults;
using FluentValidation;
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

public sealed class SetOverrideHandler(IOverrideAdmin overrides, ICurrentUser currentUser, TimeProvider time) : ICommandHandler<SetOverrideCommand, Result<OverrideDto>>
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

        return await overrides.SetAsync(
            command.UserId, command.Permission.Trim().ToUpperInvariant(), command.IsAllowed, command.Reason, command.ExpiresAt?.ToUniversalTime(), command.ExpectedVersion, cancellationToken);
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

public sealed class RemoveOverrideHandler(IOverrideAdmin overrides, ICurrentUser currentUser) : ICommandHandler<RemoveOverrideCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveOverrideCommand command, CancellationToken cancellationToken) =>
        currentUser.UserId == command.UserId.Value
            ? Result.Fail(OverrideErrors.SelfTarget())
            : await overrides.RemoveAsync(command.UserId, command.Permission.Trim().ToUpperInvariant(), command.ExpectedVersion, cancellationToken);
}
