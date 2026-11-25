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

    /// <summary>A dependency (database, downstream service) is temporarily unreachable; the caller may retry.</summary>
    Unavailable,
}
