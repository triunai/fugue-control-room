using FluentResults;
using FluentValidation;
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

public sealed class AddGroupMemberHandler(IGroupAdmin groups, ICurrentUser currentUser) : ICommandHandler<AddGroupMemberCommand, Result>
{
    public async ValueTask<Result> Handle(AddGroupMemberCommand command, CancellationToken cancellationToken) =>
        currentUser.UserId == command.UserId.Value
            ? Result.Fail(GroupErrors.SelfTarget())
            : await groups.AddMemberAsync(command.GroupCode.Trim().ToUpperInvariant(), command.UserId, cancellationToken);
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

public sealed class RemoveGroupMemberHandler(IGroupAdmin groups, ICurrentUser currentUser) : ICommandHandler<RemoveGroupMemberCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveGroupMemberCommand command, CancellationToken cancellationToken) =>
        currentUser.UserId == command.UserId.Value
            ? Result.Fail(GroupErrors.SelfTarget())
            : await groups.RemoveMemberAsync(command.GroupCode.Trim().ToUpperInvariant(), command.UserId, cancellationToken);
}
