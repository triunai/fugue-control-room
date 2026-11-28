using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Me.Models;
using Fugue.Application.Features.Me.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Application.Features.Me.Queries;

/// <summary>Contract. No parameters: the subject is always the authenticated caller, never an id from the request.</summary>
public sealed record GetMeQuery : IQuery<Result<MeDto>>;

public sealed class GetMeValidator : AbstractValidator<GetMeQuery>;

public sealed class GetMeHandler(ICurrentUser user, IUserAccessReader access, IMeReader profiles, Modules.Repos.IModuleRegistry modules, IMeCache cache)
    : IQueryHandler<GetMeQuery, Result<MeDto>>
{
    public async ValueTask<Result<MeDto>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        if (user.UserId is not { } id)
        {
            return Result.Fail<MeDto>(FugueErrors.Unauthorized("auth.unauthenticated", "Authentication is required."));
        }

        // Keyed by the authenticated caller only; permission writes invalidate it (IPermissionCacheInvalidator).
        UserId userId = UserId.From(id);
        MeDto? me = await cache.GetOrCreateAsync(userId, ct => LoadAsync(userId, ct), cancellationToken);

        // The token was valid a moment ago but the user was deactivated or deleted since: the same answer as any invalid token.
        return me is null
            ? Result.Fail<MeDto>(FugueErrors.Unauthorized("auth.unauthenticated", "Authentication is required."))
            : Result.Ok(me);
    }

    private async ValueTask<MeDto?> LoadAsync(UserId userId, CancellationToken cancellationToken)
    {
        UserAccess? effective = await access.GetAsync(userId, cancellationToken);
        MeProfile? profile = effective is null ? null : await profiles.GetProfileAsync(userId, cancellationToken);
        if (effective is null || profile is null)
        {
            return null;
        }

        IReadOnlyList<MeModuleDto> enabled = [.. (await modules.ListAsync(cancellationToken))
            .Where(m => m.Effective).OrderBy(m => m.Code, StringComparer.Ordinal).Select(m => new MeModuleDto(m.Code, m.Name))];

        return new MeDto(
            userId, profile.Email, profile.DisplayName, effective.TenantId, profile.Roles, profile.Groups,
            [.. effective.Permissions.Order(StringComparer.Ordinal)], enabled);
    }
}
