using FluentResults;
using Fugue.Application.Features.Roles.Models;
using Fugue.Application.Features.Roles.Repos;
using Fugue.Application.Features.Users.Commands;
using Fugue.Application.Features.Users.Models;
using Fugue.Application.Features.Users.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Tests.Application;

/// <summary>
/// <see cref="UpdateUserHandler"/> with hand-written fakes: If-Match is mandatory, unknown users are 404, a stale version is the repository's
/// 412, only ADDED roles face the escalation rule, and an administrator cannot lock themselves out.
/// </summary>
public sealed class UpdateUserHandlerTests
{
    private static readonly UserId Target = UserId.From(Guid.Parse("01900000-0000-7000-8000-000000000001"));
    private static readonly Guid Caller = Guid.Parse("01900000-0000-7000-8000-000000000002");

    private static readonly RoleDto Admin = new("ADMIN", "Admin", true, [new(Permissions.UsersManage, "IDENTITY"), new("WEBHOOKS_MANAGE", "WEBHOOKS")]);
    private static readonly RoleDto Staff = new("STAFF", "Staff", true, [new("CLAIMS_READ", "CLAIMS")]);
    private static readonly RoleDto UserAdmin = new("USER_ADMIN", "User admin", true, [new(Permissions.UsersManage, "IDENTITY")]);

    private static UserDto Existing(params string[] roles) =>
        new(Target, "t@example.test", "Target", true, roles, [], DateTimeOffset.UnixEpoch, 7);

    private static (UpdateUserHandler Handler, FakeAdmin Admin) Build(UserDto? existing, Guid callerId, params string[] callerPermissions)
    {
        var admin = new FakeAdmin();
        var handler = new UpdateUserHandler(admin, new FakeDirectory(existing), new FakeRoles([Admin, Staff, UserAdmin]), new FakeCaller(callerId, callerPermissions));
        return (handler, admin);
    }

    private static string Code(IResultBase result) => Assert.IsType<FugueError>(Assert.Single(result.Errors)).Code;

    [Fact]
    public async Task Happy_path_saves_the_trimmed_name_and_canonical_roles_at_the_if_match_version()
    {
        var (handler, admin) = Build(Existing("STAFF"), Caller, Permissions.UsersManage, "CLAIMS_READ");

        Result<UserDto> result = await handler.Handle(new UpdateUserCommand(Target, "  Renamed ", false, [" staff "], 7), default);

        Assert.True(result.IsSuccess);
        Assert.Equal((Target, "Renamed", false, 7L), (admin.Saved!.Id, admin.Saved.DisplayName, admin.Saved.IsActive, admin.Expected));
        Assert.Equal(["STAFF"], admin.Saved.RoleCodes);
    }

    [Fact]
    public async Task Missing_if_match_is_428_before_anything_is_read()
    {
        var (handler, admin) = Build(Existing("STAFF"), Caller, Permissions.UsersManage);

        Result<UserDto> result = await handler.Handle(new UpdateUserCommand(Target, "X", true, ["STAFF"], null), default);

        Assert.Equal("user.if_match_required", Code(result));
        Assert.Null(admin.Saved);
    }

    [Fact]
    public async Task Unknown_user_is_404()
    {
        var (handler, admin) = Build(null, Caller, Permissions.UsersManage);

        Result<UserDto> result = await handler.Handle(new UpdateUserCommand(Target, "X", true, ["STAFF"], 7), default);

        Assert.Equal("user.not_found", Code(result));
        Assert.Null(admin.Saved);
    }

    [Fact]
    public async Task Stale_version_from_the_repository_is_returned_as_412()
    {
        var (handler, admin) = Build(Existing("STAFF"), Caller, Permissions.UsersManage, "CLAIMS_READ");
        admin.Outcome = Result.Fail(UserErrors.VersionMismatch());

        Result<UserDto> result = await handler.Handle(new UpdateUserCommand(Target, "X", true, ["STAFF"], 6), default);

        Assert.Equal("user.version_mismatch", Code(result));
        Assert.Equal(ErrorType.PreconditionFailed, Assert.IsType<FugueError>(result.Errors[0]).Type);
    }

    [Fact]
    public async Task Adding_a_role_beyond_the_caller_is_403_but_keeping_one_is_not()
    {
        // The caller manages users but holds neither WEBHOOKS_MANAGE nor CLAIMS_READ.
        var (handler, admin) = Build(Existing("STAFF"), Caller, Permissions.UsersManage);

        Result<UserDto> added = await handler.Handle(new UpdateUserCommand(Target, "X", true, ["STAFF", "ADMIN"], 7), default);
        Assert.Equal("user.role_escalation", Code(added));
        Assert.Null(admin.Saved);

        Result<UserDto> kept = await handler.Handle(new UpdateUserCommand(Target, "X", true, ["STAFF"], 7), default);
        Assert.True(kept.IsSuccess); // STAFF was already assigned: not an addition, so the caller need not hold CLAIMS_READ
    }

    [Fact]
    public async Task Unknown_role_and_empty_roles_are_400s()
    {
        var (handler, _) = Build(Existing("STAFF"), Caller, Permissions.UsersManage);

        Result<UserDto> unknown = await handler.Handle(new UpdateUserCommand(Target, "X", true, ["NOPE"], 7), default);
        Assert.Equal(ErrorType.Validation, Assert.IsType<FugueError>(Assert.Single(unknown.Errors)).Type);

        var validator = new UpdateUserValidator();
        Assert.False(validator.Validate(new UpdateUserCommand(Target, "", true, [], 7)).IsValid);
        Assert.Contains(validator.Validate(new UpdateUserCommand(Target, "X", true, [], 7)).Errors, e => e.PropertyName == "Roles");
        Assert.Contains(validator.Validate(new UpdateUserCommand(Target, new string('a', 201), true, ["STAFF"], 7)).Errors, e => e.PropertyName == "DisplayName");
    }

    [Fact]
    public async Task Editing_yourself_cannot_deactivate_you_or_drop_your_last_users_manage_role()
    {
        string[] all = [Permissions.UsersManage, "WEBHOOKS_MANAGE", "CLAIMS_READ"];
        var (handler, admin) = Build(Existing("ADMIN"), Target.Value, all);

        Assert.Equal("user.self_lockout", Code(await handler.Handle(new UpdateUserCommand(Target, "Me", false, ["ADMIN"], 7), default)));
        Assert.Equal("user.self_lockout", Code(await handler.Handle(new UpdateUserCommand(Target, "Me", true, ["STAFF"], 7), default)));
        Assert.Null(admin.Saved);

        // Swapping to another role that still grants USERS_MANAGE is fine.
        Assert.True((await handler.Handle(new UpdateUserCommand(Target, "Me", true, ["USER_ADMIN"], 7), default)).IsSuccess);
    }

    private sealed class FakeAdmin : IUserAdmin
    {
        public UserUpdate? Saved { get; private set; }

        public long? Expected { get; private set; }

        public Result Outcome { get; set; } = Result.Ok();

        public Task<Result<UserDto>> CreateAsync(NewUser user, CancellationToken ct) => throw new NotSupportedException();

        public Task<Result> UpdateAsync(UserUpdate user, long expectedVersion, CancellationToken ct)
        {
            if (Outcome.IsSuccess)
            {
                (Saved, Expected) = (user, expectedVersion);
            }

            return Task.FromResult(Outcome);
        }
    }

    private sealed class FakeDirectory(UserDto? user) : IUserDirectory
    {
        public Task<IReadOnlyList<UserDto>> ListAsync(string? search, Fugue.Application.Common.KeysetCursor? after, int take, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<UserDto?> GetAsync(UserId id, CancellationToken ct) => Task.FromResult(user?.Id == id ? user : null);
    }

    private sealed class FakeRoles(IReadOnlyList<RoleDto> roles) : IRoleCatalog
    {
        public Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct) => Task.FromResult(roles);
    }

    private sealed class FakeCaller(Guid id, string[] permissions) : ICurrentUser
    {
        public Guid? UserId => id;

        public Guid? TenantId => null;

        public bool IsAuthenticated => true;

        public bool HasPermission(string code) => permissions.Contains(code, StringComparer.Ordinal);
    }
}
