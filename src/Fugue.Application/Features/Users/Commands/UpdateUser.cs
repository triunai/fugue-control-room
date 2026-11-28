using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Roles.Models;
using Fugue.Application.Features.Roles.Repos;
using Fugue.Application.Features.Users.Models;
using Fugue.Application.Features.Users.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Users.Commands;

/// <summary>
/// Contract. Replaces a user's display name, active flag and direct roles. The email and the password are not part of an edit.
/// <paramref name="ExpectedVersion"/> comes from <c>If-Match</c> and is mandatory.
/// </summary>
public sealed record UpdateUserCommand(UserId Id, string DisplayName, bool IsActive, IReadOnlyList<string> Roles, long? ExpectedVersion)
    : ICommand<Result<UserDto>>;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Pick at least one role.");
        RuleForEach(x => x.Roles).NotEmpty().MaximumLength(64);
    }
}

public sealed class UpdateUserHandler(IUserAdmin admin, IUserDirectory directory, IRoleCatalog roles, ICurrentUser currentUser)
    : ICommandHandler<UpdateUserCommand, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        if (command.ExpectedVersion is not { } expected)
        {
            return Result.Fail<UserDto>(UserErrors.IfMatchRequired());
        }

        UserDto? current = await directory.GetAsync(command.Id, cancellationToken);
        if (current is null)
        {
            return Result.Fail<UserDto>(UserErrors.NotFound(command.Id));
        }

        string[] codes = [.. command.Roles.Select(r => r.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        Dictionary<string, RoleDto> catalog = (await roles.ListAsync(cancellationToken)).Where(r => r.IsActive).ToDictionary(r => r.Code, StringComparer.Ordinal);

        string[] unknown = [.. codes.Where(c => !catalog.ContainsKey(c))];
        if (unknown.Length > 0)
        {
            return Result.Fail<UserDto>(UserErrors.UnknownRoles(unknown));
        }

        // The create rule, applied to every role this edit ADDS: each of its permissions must already be the caller's.
        string[] beyond = [.. codes.Except(current.Roles, StringComparer.Ordinal).Where(c => catalog[c].Permissions.Any(p => !currentUser.HasPermission(p.Code)))];
        if (beyond.Length > 0)
        {
            return Result.Fail<UserDto>(UserErrors.RoleBeyondCaller(beyond));
        }

        if (currentUser.UserId == command.Id.Value && LocksOut(current.Roles, codes, command.IsActive, catalog))
        {
            return Result.Fail<UserDto>(UserErrors.SelfLockout());
        }

        Result saved = await admin.UpdateAsync(new UserUpdate(command.Id, command.DisplayName.Trim(), command.IsActive, codes), expected, cancellationToken);
        if (saved.IsFailed)
        {
            return Result.Fail<UserDto>(saved.Errors);
        }

        return await directory.GetAsync(command.Id, cancellationToken) is { } dto ? Result.Ok(dto) : Result.Fail<UserDto>(UserErrors.NotFound(command.Id));
    }

    /// <summary>Editing yourself: no deactivation, and no dropping the last direct role that grants USERS_MANAGE if you had one.</summary>
    private static bool LocksOut(IReadOnlyList<string> before, string[] after, bool isActive, Dictionary<string, RoleDto> catalog)
    {
        bool Manages(string code) => catalog.TryGetValue(code, out RoleDto? role) && role.Permissions.Any(p => p.Code == Permissions.UsersManage);
        return !isActive || (before.Any(Manages) && !after.Any(Manages));
    }
}
