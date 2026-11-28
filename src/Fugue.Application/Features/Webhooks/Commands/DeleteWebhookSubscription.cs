using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Webhooks.Models;
using Fugue.Application.Features.Webhooks.Repos;
using Fugue.Domain.Ids;
using Fugue.Domain.Webhooks;
using Mediator;

namespace Fugue.Application.Features.Webhooks.Commands;

/// <summary>Contract. <paramref name="ExpectedVersion"/> comes from <c>If-Match</c> and is mandatory. Deleting cascades to the subscription's deliveries (the delete is audited).</summary>
public sealed record DeleteWebhookSubscriptionCommand(WebhookSubscriptionId Id, long? ExpectedVersion) : ICommand<Result>;

public sealed class DeleteWebhookSubscriptionValidator : AbstractValidator<DeleteWebhookSubscriptionCommand>
{
    public DeleteWebhookSubscriptionValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class DeleteWebhookSubscriptionHandler(IWebhookSubscriptionStore subscriptions) : ICommandHandler<DeleteWebhookSubscriptionCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (command.ExpectedVersion is not { } expected)
        {
            return Result.Fail(WebhookErrors.IfMatchRequired());
        }

        WebhookSubscription? subscription = await subscriptions.FindForUpdateAsync(command.Id, cancellationToken);
        return subscription is null
            ? Result.Fail(WebhookErrors.SubscriptionNotFound(command.Id))
            : await subscriptions.DeleteAsync(subscription, expected, cancellationToken);
    }
}
