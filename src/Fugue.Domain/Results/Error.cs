namespace Fugue.Domain.Results;

/// <summary>A failure description. <paramref name="Fields"/> carries per-field messages for validation errors.</summary>
public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? Fields = null)
{
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? fields = null) =>
        new(code, message, ErrorType.Validation, fields);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error PreconditionFailed(string code, string message) =>
        new(code, message, ErrorType.PreconditionFailed);

    public static Error PreconditionRequired(string code, string message) =>
        new(code, message, ErrorType.PreconditionRequired);
}
