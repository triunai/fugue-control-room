using FluentResults;
using FluentValidation;
using Fugue.Application.Common;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Application.Features.Webhooks.Queries;

/// <summary>Contract. Keyset paging only: <paramref name="Cursor"/> is the opaque token from the previous page.</summary>
public sealed record ListWebhookSubscriptionsQuery(int? Limit, string? Cursor) : IQuery<Result<PagedResult<WebhookSubscriptionDto>>>;

public sealed class ListWebhookSubscriptionsValidator : AbstractValidator<ListWebhookSubscriptionsQuery>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public ListWebhookSubscriptionsValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit).When(x => x.Limit is not null);
        RuleFor(x => x.Cursor).Must(c => KeysetCursor.TryDecode(c, out _)).When(x => !string.IsNullOrEmpty(x.Cursor)).WithMessage("Cursor is not valid.");
    }
}

public sealed class ListWebhookSubscriptionsHandler(IWebhookSubscriptionStore subscriptions)
    : IQueryHandler<ListWebhookSubscriptionsQuery, Result<PagedResult<WebhookSubscriptionDto>>>
{
    public async ValueTask<Result<PagedResult<WebhookSubscriptionDto>>> Handle(ListWebhookSubscriptionsQuery query, CancellationToken cancellationToken)
    {
        int limit = query.Limit ?? ListWebhookSubscriptionsValidator.DefaultLimit;
        KeysetCursor? after = KeysetCursor.TryDecode(query.Cursor, out KeysetCursor decoded) ? decoded : null;
        IReadOnlyList<WebhookSubscriptionDto> rows = await subscriptions.ListAsync(after, limit + 1, cancellationToken);
        if (rows.Count <= limit)
        {
            return Result.Ok(new PagedResult<WebhookSubscriptionDto>(rows, null));
        }

        WebhookSubscriptionDto last = rows[limit - 1];
        return Result.Ok(new PagedResult<WebhookSubscriptionDto>(rows.Take(limit).ToList(), new KeysetCursor(last.CreatedAt, last.Id.Value).Encode()));
    }
}

/// <summary>Contract.</summary>
public sealed record GetWebhookSubscriptionQuery(WebhookSubscriptionId Id) : IQuery<Result<WebhookSubscriptionDto>>;

public sealed class GetWebhookSubscriptionValidator : AbstractValidator<GetWebhookSubscriptionQuery>
{
    public GetWebhookSubscriptionValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class GetWebhookSubscriptionHandler(IWebhookSubscriptionStore subscriptions) : IQueryHandler<GetWebhookSubscriptionQuery, Result<WebhookSubscriptionDto>>
{
    public async ValueTask<Result<WebhookSubscriptionDto>> Handle(GetWebhookSubscriptionQuery query, CancellationToken cancellationToken) =>
        await subscriptions.GetAsync(query.Id, cancellationToken) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<WebhookSubscriptionDto>(WebhookErrors.SubscriptionNotFound(query.Id));
}
