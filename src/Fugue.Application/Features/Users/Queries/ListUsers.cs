using FluentResults;
using FluentValidation;
using Fugue.Application.Common;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Application.Features.Users.Queries;

/// <summary>
/// Contract. Keyset paging only: <paramref name="Cursor"/> is the opaque token from the previous page; order is <c>(created_at, id)</c> ascending.
/// <paramref name="Search"/> matches the email or the display name.
/// </summary>
public sealed record ListUsersQuery(int? Limit, string? Search, string? Cursor) : IQuery<Result<PagedResult<UserDto>>>;

public sealed class ListUsersValidator : AbstractValidator<ListUsersQuery>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public ListUsersValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit).When(x => x.Limit is not null);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Cursor)
            .Must(c => KeysetCursor.TryDecode(c, out _))
            .When(x => !string.IsNullOrEmpty(x.Cursor))
            .WithMessage("Cursor is not valid.");
    }
}

public sealed class ListUsersHandler(IUserDirectory users) : IQueryHandler<ListUsersQuery, Result<PagedResult<UserDto>>>
{
    public async ValueTask<Result<PagedResult<UserDto>>> Handle(ListUsersQuery query, CancellationToken cancellationToken)
    {
        int limit = query.Limit ?? ListUsersValidator.DefaultLimit;
        KeysetCursor? after = KeysetCursor.TryDecode(query.Cursor, out KeysetCursor decoded) ? decoded : null;
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        IReadOnlyList<UserDto> rows = await users.ListAsync(search, after, limit + 1, cancellationToken);

        if (rows.Count <= limit)
        {
            return Result.Ok(new PagedResult<UserDto>(rows, null));
        }

        UserDto last = rows[limit - 1];
        return Result.Ok(new PagedResult<UserDto>(rows.Take(limit).ToList(), new KeysetCursor(last.CreatedAt, last.Id.Value).Encode()));
    }
}
