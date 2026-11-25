namespace Fugue.Domain.Results;

/// <summary>Factories for <see cref="FugueError"/>, one per <see cref="ErrorType"/>.</summary>
public static class FugueErrors
{
    public static FugueError Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static FugueError Validation(IReadOnlyDictionary<string, string[]> fields) =>
        Validation("validation.failed", "One or more validation errors occurred.", fields);

    public static FugueError Validation(string code, string message, IReadOnlyDictionary<string, string[]> fields) =>
        new(code, message, ErrorType.Validation, fields);

    public static FugueError NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static FugueError Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static FugueError Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static FugueError Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static FugueError PreconditionFailed(string code, string message) =>
        new(code, message, ErrorType.PreconditionFailed);

    public static FugueError PreconditionRequired(string code, string message) =>
        new(code, message, ErrorType.PreconditionRequired);

    public static FugueError Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);
}
