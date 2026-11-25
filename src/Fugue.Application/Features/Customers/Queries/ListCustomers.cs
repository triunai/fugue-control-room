using FluentResults;
using FluentValidation;
using Fugue.Application.Common;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Customers;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Application.Features.Customers.Queries;

/// <summary>Contract. Keyset paging only: <paramref name="Cursor"/> is the opaque token from the previous page.</summary>
public sealed record ListCustomersQuery(int? Limit, string? Status, string? Search, string? Cursor)
    : IQuery<Result<PagedResult<CustomerDto>>>;

public sealed class ListCustomersValidator : AbstractValidator<ListCustomersQuery>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public ListCustomersValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit).When(x => x.Limit is not null);
        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse(s, ignoreCase: true, out CustomerStatus v) && Enum.IsDefined(v))
            .When(x => !string.IsNullOrEmpty(x.Status))
            .WithMessage("Status must be 'active' or 'suspended'.");
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Cursor)
            .Must(c => KeysetCursor.TryDecode(c, out _))
            .When(x => !string.IsNullOrEmpty(x.Cursor))
            .WithMessage("Cursor is not valid.");
    }
}

public sealed class ListCustomersHandler(ICustomerRepository customers, ICurrentUser currentUser)
    : IQueryHandler<ListCustomersQuery, Result<PagedResult<CustomerDto>>>
{
    public async ValueTask<Result<PagedResult<CustomerDto>>> Handle(ListCustomersQuery query, CancellationToken cancellationToken)
    {
        int limit = query.Limit ?? ListCustomersValidator.DefaultLimit;
        KeysetCursor? after = KeysetCursor.TryDecode(query.Cursor, out KeysetCursor decoded) ? decoded : null;
        CustomerStatus? status = Enum.TryParse(query.Status, ignoreCase: true, out CustomerStatus parsed) && Enum.IsDefined(parsed) ? parsed : null;

        IReadOnlyList<CustomerDto> rows = await customers.ListAsync(after, status, query.Search?.Trim(), limit + 1, OwnerScope.From(currentUser), cancellationToken);

        if (rows.Count <= limit)
        {
            return Result.Ok(new PagedResult<CustomerDto>(rows, null));
        }

        CustomerDto last = rows[limit - 1];
        return Result.Ok(new PagedResult<CustomerDto>(
            rows.Take(limit).ToList(), new KeysetCursor(last.CreatedAt, last.Id.Value).Encode()));
    }
}
