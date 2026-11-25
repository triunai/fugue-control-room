using FluentResults;
using FluentValidation;
using Fugue.Domain.Ids;
using Fugue.Domain.Webhooks;
using Mediator;

namespace Fugue.Application.Features.Webhooks.Commands;

/// <summary>Contract. The secret is generated server-side and returned once in the response; it is never part of a request.</summary>
public sealed record CreateWebhookSubscriptionCommand(string Name, string Url, string[] EventTypes) : ICommand<Result<WebhookSubscriptionSecretDto>>;

public sealed class CreateWebhookSubscriptionValidator : AbstractValidator<CreateWebhookSubscriptionCommand>
{
    public CreateWebhookSubscriptionValidator()
    {
        RuleFor(x => x.Name).SubscriptionName();
        RuleFor(x => x.Url).SubscriptionUrl();
        RuleFor(x => x.EventTypes).SubscriptionEventTypes();
    }
}

public sealed class CreateWebhookSubscriptionHandler(IWebhookSubscriptionStore subscriptions, IWebhookUrlGuard guard, ISecretProtector protector)
    : ICommandHandler<CreateWebhookSubscriptionCommand, Result<WebhookSubscriptionSecretDto>>
{
    public async ValueTask<Result<WebhookSubscriptionSecretDto>> Handle(CreateWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        Result allowed = await guard.CheckAsync(command.Url.Trim(), cancellationToken);
        if (allowed.IsFailed)
        {
            return Result.Fail<WebhookSubscriptionSecretDto>(allowed.Errors);
        }

        string secret = WebhookSecretFormat.Generate();
        var subscription = new WebhookSubscription
        {
            Id = WebhookSubscriptionId.New(),
            Name = command.Name.Trim(),
            Url = command.Url.Trim(),
            EventTypes = command.EventTypes,
            IsActive = true,
            SecretCiphertext = protector.Protect(secret),
        };

        WebhookSubscriptionDto created = await subscriptions.AddAsync(subscription, cancellationToken);
        return Result.Ok(WebhookSubscriptionSecretDto.From(created, secret));
    }
}
