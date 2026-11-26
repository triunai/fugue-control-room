using FluentResults;
using FluentValidation;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Webhooks.Commands;

/// <summary>Contract. Revives a dead-lettered delivery with a fresh attempt budget; any other state is a 409 <c>webhook.delivery_not_dead</c>.</summary>
public sealed record RedeliverWebhookDeliveryCommand(WebhookDeliveryId Id) : ICommand<Result>;

public sealed class RedeliverWebhookDeliveryValidator : AbstractValidator<RedeliverWebhookDeliveryCommand>
{
    public RedeliverWebhookDeliveryValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class RedeliverWebhookDeliveryHandler(IWebhookDeliveryStore deliveries) : ICommandHandler<RedeliverWebhookDeliveryCommand, Result>
{
    public async ValueTask<Result> Handle(RedeliverWebhookDeliveryCommand command, CancellationToken cancellationToken) =>
        await deliveries.RedeliverAsync(command.Id, cancellationToken);
}
