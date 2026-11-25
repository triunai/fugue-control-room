using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Application.Common.Behaviors;

/// <summary>
/// Runs every <see cref="IValidator{T}"/> registered for the message before the handler. Failures are
/// aggregated into one <see cref="FugueErrors.Validation(IReadOnlyDictionary{string, string[]})"/> keyed by
/// camelCase property name; the handler is not called.
/// </summary>
/// <remarks>
/// <typeparamref name="TResponse"/> is constrained to <c>ResultBase, new()</c>: FluentResults' <c>Result</c> and
/// <c>Result&lt;T&gt;</c> both have a public parameterless constructor, so the failure result is built with
/// <c>new TResponse()</c> (a cached generic activator, no per-call reflection on our side) and the error added
/// to <see cref="ResultBase.Reasons"/>.
/// </remarks>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
    where TResponse : ResultBase, new()
{
    private readonly IValidator<TMessage>[] _validators = validators.ToArray();

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Length == 0)
        {
            return await next(message, cancellationToken);
        }

        var context = new ValidationContext<TMessage>(message);
        List<ValidationFailure> failures = [];
        foreach (IValidator<TMessage> validator in _validators)
        {
            ValidationResult result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        Dictionary<string, string[]> fields = failures
            .GroupBy(f => ToCamelCase(f.PropertyName), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray(), StringComparer.Ordinal);

        var failed = new TResponse();
        failed.Reasons.Add(FugueErrors.Validation(fields));
        return failed;
    }

    /// <summary>"Address.Street" becomes "address.street"; "Items[0].Name" becomes "items[0].name".</summary>
    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return string.Empty;
        }

        string[] segments = propertyName.Split('.');
        for (int i = 0; i < segments.Length; i++)
        {
            string segment = segments[i];
            if (segment.Length > 0 && char.IsUpper(segment[0]))
            {
                segments[i] = char.ToLowerInvariant(segment[0]) + segment[1..];
            }
        }

        return string.Join('.', segments);
    }
}
