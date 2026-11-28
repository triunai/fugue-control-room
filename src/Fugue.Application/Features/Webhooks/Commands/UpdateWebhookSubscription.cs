using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Webhooks.Helpers;
using Fugue.Application.Features.Webhooks.Models;
using Fugue.Application.Features.Webhooks.Repos;
using Fugue.Domain.Ids;
using Fugue.Domain.Webhooks;
using Mediator;

namespace Fugue.Application.Features.Webhooks.Commands;

/// <summary>Contract. <paramref name="ExpectedVersion"/> comes from <c>If-Match</c> and is mandatory. The secret is not changed here (rotate it).</summary>
public sealed record UpdateWebhookSubscriptionCommand(
    WebhookSubscriptionId Id, string Name, string Url, string[] EventTypes, bool IsActive, long? ExpectedVersion) : ICommand<Result<WebhookSubscriptionDto>>;

public sealed class UpdateWebhookSubscriptionValidator : AbstractValidator<UpdateWebhookSubscriptionCommand>
{
    public UpdateWebhookSubscriptionValidator()
    {
        RuleFor(x => x.Name).SubscriptionName();
        RuleFor(x => x.Url).SubscriptionUrl();
        RuleFor(x => x.EventTypes).SubscriptionEventTypes();
    }
}

public sealed class UpdateWebhookSubscriptionHandler(IWebhookSubscriptionStore subscriptions, IWebhookUrlGuard guard)
    : ICommandHandler<UpdateWebhookSubscriptionCommand, Result<WebhookSubscriptionDto>>
{
    public async ValueTask<Result<WebhookSubscriptionDto>> Handle(UpdateWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (command.ExpectedVersion is null)
        {
            return Result.Fail<WebhookSubscriptionDto>(WebhookErrors.IfMatchRequired());
        }

        WebhookSubscription? subscription = await subscriptions.FindForUpdateAsync(command.Id, cancellationToken);
        if (subscription is null)
        {
            return Result.Fail<WebhookSubscriptionDto>(WebhookErrors.SubscriptionNotFound(command.Id));
        }

        string url = command.Url.Trim();
        if (url != subscription.Url)
        {
            Result allowed = await guard.CheckAsync(url, cancellationToken);
            if (allowed.IsFailed)
            {
                return Result.Fail<WebhookSubscriptionDto>(allowed.Errors);
            }
        }

        subscription.Name = command.Name.Trim();
        subscription.Url = url;
        subscription.EventTypes = command.EventTypes;
        subscription.IsActive = command.IsActive;
        return await subscriptions.SaveAsync(subscription, command.ExpectedVersion, cancellationToken);
    }
}
