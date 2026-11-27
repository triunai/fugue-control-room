using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Roles.Models;
using Fugue.Application.Features.Roles.Repos;
using Fugue.Application.Features.Users.Models;
using Fugue.Application.Features.Users.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Mediator;

namespace Fugue.Application.Features.Users.Commands;

/// <summary>
/// Contract. The administrator sets the initial password and hands it over out of band; it is hashed in the handler and never stored,
/// logged or returned (LoggingBehavior logs names and outcomes only, a failure audit row carries field names only).
/// </summary>
public sealed record CreateUserCommand(string Email, string DisplayName, string Password, IReadOnlyList<string> Roles) : ICommand<Result<UserDto>>
{
    /// <summary>Never print the password (or the email) if something formats the command.</summary>
    public override string ToString() => nameof(CreateUserCommand);
}

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public const int PasswordMinLength = 12;
    public const int PasswordMaxLength = 128;

    public CreateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(PasswordMinLength).MaximumLength(PasswordMaxLength);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Pick at least one role.");
        RuleForEach(x => x.Roles).NotEmpty().MaximumLength(64);
    }
}

public sealed class CreateUserHandler(IUserAdmin users, IRoleCatalog roles, IPasswordHasher hasher, ICurrentUser currentUser)
    : ICommandHandler<CreateUserCommand, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        string[] codes = [.. command.Roles.Select(r => r.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        Dictionary<string, RoleDto> catalog = (await roles.ListAsync(cancellationToken)).Where(r => r.IsActive).ToDictionary(r => r.Code, StringComparer.Ordinal);

        string[] unknown = [.. codes.Where(c => !catalog.ContainsKey(c))];
        if (unknown.Length > 0)
        {
            return Result.Fail<UserDto>(UserErrors.UnknownRoles(unknown));
        }

        // Holding USERS_MANAGE is not enough to mint an administrator: every permission of every requested role must already be the caller's.
        string[] beyond = [.. codes.Where(c => catalog[c].Permissions.Any(p => !currentUser.HasPermission(p.Code)))];
        if (beyond.Length > 0)
        {
            return Result.Fail<UserDto>(UserErrors.RoleBeyondCaller(beyond));
        }

        return await users.CreateAsync(
            new NewUser(EmailKey.Canonicalize(command.Email), command.DisplayName.Trim(), hasher.Hash(command.Password), codes), cancellationToken);
    }
}
