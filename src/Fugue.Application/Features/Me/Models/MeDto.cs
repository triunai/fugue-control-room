using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Me.Models;

/// <summary>
/// The caller's own identity and what they may do, for the console's navigation and permission checks. Everything here is about the
/// authenticated user only; nothing identifies anyone else.
/// </summary>
/// <param name="UserId">The signed-in user.</param>
/// <param name="Email">Canonical (lower-case) sign-in email.</param>
/// <param name="DisplayName">Name to show in the UI.</param>
/// <param name="TenantId">The tenant, or null when tenancy is off or the user has none.</param>
/// <param name="Roles">Codes of the active roles the user holds, directly or through an active group. Sorted.</param>
/// <param name="Groups">Codes of the active groups the user belongs to. Sorted.</param>
/// <param name="Permissions">Effective permission codes, the same set the API enforces: role grants plus unexpired allow overrides, minus unexpired deny overrides, with permissions of disabled modules removed. Sorted.</param>
/// <param name="Modules">The modules that are effectively enabled right now (switched on in configuration and in the database).</param>
public sealed record MeDto(
    UserId UserId,
    string Email,
    string DisplayName,
    TenantId? TenantId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<MeModuleDto> Modules);

/// <summary>An enabled module as the console's navigation needs it.</summary>
/// <param name="Code">Module code, such as <c>CUSTOMERS</c>.</param>
/// <param name="Name">Display name.</param>
public sealed record MeModuleDto(string Code, string Name);
