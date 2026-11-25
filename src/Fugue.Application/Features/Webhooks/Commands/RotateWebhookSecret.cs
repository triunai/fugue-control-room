using FluentResults;
using FluentValidation;
using Fugue.Domain.Ids;
using Fugue.Domain.Webhooks;
using Mediator;

namespace Fugue.Application.Features.Webhooks.Commands;

/// <summary>
/// Contract. Replaces the secret immediately (no overlap window: deliveries signed after this call use the new secret, so update the receiver first
/// or accept a short failure window). <paramref name="ExpectedVersion"/> from <c>If-Match</c> is optional here.
/// </summary>
public sealed record RotateWebhookSecretCommand(WebhookSubscriptionId Id, long? ExpectedVersion) : ICommand<Result<WebhookSubscriptionSecretDto>>;

public sealed class RotateWebhookSecretValidator : AbstractValidator<RotateWebhookSecretCommand>
{
    public RotateWebhookSecretValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class RotateWebhookSecretHandler(IWebhookSubscriptionStore subscriptions, ISecretProtector protector)
    : ICommandHandler<RotateWebhookSecretCommand, Result<WebhookSubscriptionSecretDto>>
{
    public async ValueTask<Result<WebhookSubscriptionSecretDto>> Handle(RotateWebhookSecretCommand command, CancellationToken cancellationToken)
    {
        WebhookSubscription? subscription = await subscriptions.FindForUpdateAsync(command.Id, cancellationToken);
        if (subscription is null)
        {
            return Result.Fail<WebhookSubscriptionSecretDto>(WebhookErrors.SubscriptionNotFound(command.Id));
        }

        string secret = WebhookSecretFormat.Generate();
        subscription.SecretCiphertext = protector.Protect(secret);
        Result<WebhookSubscriptionDto> saved = await subscriptions.SaveAsync(subscription, command.ExpectedVersion, cancellationToken);
        return saved.IsFailed
            ? Result.Fail<WebhookSubscriptionSecretDto>(saved.Errors)
            : Result.Ok(WebhookSubscriptionSecretDto.From(saved.Value, secret));
    }
}
