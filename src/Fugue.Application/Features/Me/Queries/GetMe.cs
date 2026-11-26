using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Application.Features.Me.Queries;

/// <summary>Contract. No parameters: the subject is always the authenticated caller, never an id from the request.</summary>
public sealed record GetMeQuery : IQuery<Result<MeDto>>;

public sealed class GetMeValidator : AbstractValidator<GetMeQuery>;

public sealed class GetMeHandler(ICurrentUser user, IUserAccessReader access, IMeReader profiles, Modules.IModuleRegistry modules)
    : IQueryHandler<GetMeQuery, Result<MeDto>>
{
    public async ValueTask<Result<MeDto>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        if (user.UserId is not { } id)
        {
            return Result.Fail<MeDto>(FugueErrors.Unauthorized("auth.unauthenticated", "Authentication is required."));
        }

        UserId userId = UserId.From(id);
        UserAccess? effective = await access.GetAsync(userId, cancellationToken);
        MeProfile? profile = effective is null ? null : await profiles.GetProfileAsync(userId, cancellationToken);
        if (effective is null || profile is null)
        {
            // The token was valid a moment ago but the user was deactivated or deleted since: the same answer as any invalid token.
            return Result.Fail<MeDto>(FugueErrors.Unauthorized("auth.unauthenticated", "Authentication is required."));
        }

        IReadOnlyList<MeModuleDto> enabled = [.. (await modules.ListAsync(cancellationToken))
            .Where(m => m.Effective).OrderBy(m => m.Code, StringComparer.Ordinal).Select(m => new MeModuleDto(m.Code, m.Name))];

        return Result.Ok(new MeDto(
            userId, profile.Email, profile.DisplayName, effective.TenantId, profile.Roles, profile.Groups,
            [.. effective.Permissions.Order(StringComparer.Ordinal)], enabled));
    }
}
