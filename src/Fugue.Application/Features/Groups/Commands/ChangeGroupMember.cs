using FluentResults;
using FluentValidation;
using Fugue.Application.Caching;
using Fugue.Application.Features.Groups.Models;
using Fugue.Application.Features.Groups.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Groups.Commands;

/// <summary>Contract.</summary>
public sealed record AddGroupMemberCommand(string GroupCode, UserId UserId) : ICommand<Result>;

public sealed class AddGroupMemberValidator : AbstractValidator<AddGroupMemberCommand>
{
    public AddGroupMemberValidator()
    {
        RuleFor(x => x.GroupCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.UserId).Must(id => id.Value != Guid.Empty);
    }
}

public sealed class AddGroupMemberHandler(IGroupAdmin groups, ICurrentUser currentUser, IPermissionCacheInvalidator permissionCache)
    : ICommandHandler<AddGroupMemberCommand, Result>
{
    public async ValueTask<Result> Handle(AddGroupMemberCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId == command.UserId.Value)
        {
            return Result.Fail(GroupErrors.SelfTarget());
        }

        Result result = await groups.AddMemberAsync(command.GroupCode.Trim().ToUpperInvariant(), command.UserId, cancellationToken);
        if (result.IsSuccess)
        {
            await permissionCache.UserChangedAsync(command.UserId);
        }

        return result;
    }
}

/// <summary>Contract.</summary>
public sealed record RemoveGroupMemberCommand(string GroupCode, UserId UserId) : ICommand<Result>;

public sealed class RemoveGroupMemberValidator : AbstractValidator<RemoveGroupMemberCommand>
{
    public RemoveGroupMemberValidator()
    {
        RuleFor(x => x.GroupCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.UserId).Must(id => id.Value != Guid.Empty);
    }
}

public sealed class RemoveGroupMemberHandler(IGroupAdmin groups, ICurrentUser currentUser, IPermissionCacheInvalidator permissionCache)
    : ICommandHandler<RemoveGroupMemberCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveGroupMemberCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId == command.UserId.Value)
        {
            return Result.Fail(GroupErrors.SelfTarget());
        }

        Result result = await groups.RemoveMemberAsync(command.GroupCode.Trim().ToUpperInvariant(), command.UserId, cancellationToken);
        if (result.IsSuccess)
        {
            await permissionCache.UserChangedAsync(command.UserId);
        }

        return result;
    }
}
