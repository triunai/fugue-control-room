using FluentResults;
using FluentValidation;
using Mediator;

namespace Fugue.Application.Features.Webhooks.Sink;

/// <summary>Contract. Tells the Development sink which signing secret to trust (the one returned when the subscription was created).</summary>
public sealed record SetDevWebhookSinkSecretRequest(string Secret) : IRequest<Result>;

public sealed class SetDevWebhookSinkSecretValidator : AbstractValidator<SetDevWebhookSinkSecretRequest>
{
    public SetDevWebhookSinkSecretValidator() => RuleFor(x => x.Secret).NotEmpty().MaximumLength(200);
}

public sealed class SetDevWebhookSinkSecretHandler(IDevWebhookSink sink) : IRequestHandler<SetDevWebhookSinkSecretRequest, Result>
{
    public ValueTask<Result> Handle(SetDevWebhookSinkSecretRequest request, CancellationToken cancellationToken)
    {
        sink.Secret = request.Secret.Trim();
        return ValueTask.FromResult(Result.Ok());
    }
}

/// <summary>Contract. The last accepted webhooks, newest first.</summary>
public sealed record ListDevWebhookSinkRequest(int? Limit) : IRequest<Result<IReadOnlyList<ReceivedWebhookDto>>>;

public sealed class ListDevWebhookSinkValidator : AbstractValidator<ListDevWebhookSinkRequest>
{
    public ListDevWebhookSinkValidator() => RuleFor(x => x.Limit).InclusiveBetween(1, 100).When(x => x.Limit is not null);
}

public sealed class ListDevWebhookSinkHandler(IDevWebhookSink sink) : IRequestHandler<ListDevWebhookSinkRequest, Result<IReadOnlyList<ReceivedWebhookDto>>>
{
    public ValueTask<Result<IReadOnlyList<ReceivedWebhookDto>>> Handle(ListDevWebhookSinkRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Ok(sink.Latest(request.Limit ?? 20)));
}

/// <summary>Contract. Forgets everything the sink received (and the event ids it had seen), for a repeatable demo.</summary>
public sealed record ClearDevWebhookSinkRequest : IRequest<Result>;

public sealed class ClearDevWebhookSinkValidator : AbstractValidator<ClearDevWebhookSinkRequest>;

public sealed class ClearDevWebhookSinkHandler(IDevWebhookSink sink) : IRequestHandler<ClearDevWebhookSinkRequest, Result>
{
    public ValueTask<Result> Handle(ClearDevWebhookSinkRequest request, CancellationToken cancellationToken)
    {
        sink.Clear();
        return ValueTask.FromResult(Result.Ok());
    }
}
