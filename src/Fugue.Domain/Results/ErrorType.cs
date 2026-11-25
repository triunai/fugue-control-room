namespace Fugue.Domain.Results;

/// <summary>Transport-agnostic classification of a failure. The API layer maps it to a status.</summary>
public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    PreconditionFailed,
    PreconditionRequired,
}
