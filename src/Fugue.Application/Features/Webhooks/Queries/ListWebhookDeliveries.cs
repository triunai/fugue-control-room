using FluentResults;
using FluentValidation;
using Fugue.Application.Common;
using Fugue.Application.Features.Webhooks.Models;
using Fugue.Application.Features.Webhooks.Repos;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;
using Fugue.Domain.Webhooks;
using Mediator;

namespace Fugue.Application.Features.Webhooks.Queries;

/// <summary>Contract. Newest first, keyset paged. Filters are exact matches: <c>status</c> (pending, in_flight, succeeded, dead), subscription id, event type.</summary>
public sealed record ListWebhookDeliveriesQuery(string? Status, Guid? SubscriptionId, string? EventType, int? Limit, string? Cursor)
    : IQuery<Result<PagedResult<WebhookDeliveryDto>>>;

public sealed class ListWebhookDeliveriesValidator : AbstractValidator<ListWebhookDeliveriesQuery>
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    private static readonly string[] Statuses =
        [WebhookDeliveryStatuses.Pending, WebhookDeliveryStatuses.InFlight, WebhookDeliveryStatuses.Succeeded, WebhookDeliveryStatuses.Dead];

    public ListWebhookDeliveriesValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit).When(x => x.Limit is not null);
        RuleFor(x => x.Status).Must(s => Statuses.Contains(s)).When(x => !string.IsNullOrEmpty(x.Status)).WithMessage("Status must be pending, in_flight, succeeded or dead.");
        RuleFor(x => x.EventType).MaximumLength(100);
        RuleFor(x => x.SubscriptionId).Must(id => id != Guid.Empty).When(x => x.SubscriptionId is not null);
        RuleFor(x => x.Cursor).Must(c => KeysetCursor.TryDecode(c, out _)).When(x => !string.IsNullOrEmpty(x.Cursor)).WithMessage("Cursor is not valid.");
    }
}

public sealed class ListWebhookDeliveriesHandler(IWebhookDeliveryStore deliveries)
    : IQueryHandler<ListWebhookDeliveriesQuery, Result<PagedResult<WebhookDeliveryDto>>>
{
    public async ValueTask<Result<PagedResult<WebhookDeliveryDto>>> Handle(ListWebhookDeliveriesQuery query, CancellationToken cancellationToken)
    {
        int limit = query.Limit ?? ListWebhookDeliveriesValidator.DefaultLimit;
        KeysetCursor? after = KeysetCursor.TryDecode(query.Cursor, out KeysetCursor decoded) ? decoded : null;
        var filter = new WebhookDeliveryFilter(
            string.IsNullOrEmpty(query.Status) ? null : query.Status,
            query.SubscriptionId is { } id ? WebhookSubscriptionId.From(id) : null,
            string.IsNullOrWhiteSpace(query.EventType) ? null : query.EventType.Trim());

        IReadOnlyList<WebhookDeliveryDto> rows = await deliveries.ListAsync(filter, after, limit + 1, cancellationToken);
        if (rows.Count <= limit)
        {
            return Result.Ok(new PagedResult<WebhookDeliveryDto>(rows, null));
        }

        WebhookDeliveryDto last = rows[limit - 1];
        return Result.Ok(new PagedResult<WebhookDeliveryDto>(rows.Take(limit).ToList(), new KeysetCursor(last.OccurredAt, last.Id.Value).Encode()));
    }
}
