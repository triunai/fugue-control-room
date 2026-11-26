using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.OpenApi;

/// <summary>
/// Declares one problem status an action can return. Every error the API sends is an RFC 9457 <c>application/problem+json</c> body
/// (400 carries the per-field <c>errors</c> map), so the content type and schema are fixed here and an action lists only the statuses.
/// Statuses that follow from the pipeline are added by <see cref="FugueOperationFilter"/> and need no declaration:
/// 400 (any action with input), 401 and 403 (authorization), 404 (a module gate), 412 and 428 (<see cref="IfMatchAttribute"/>), 429 (rate limiting) and 503.
/// Declare what the action itself decides: 404 for a missing resource, 409 for a state conflict, 401 for the token endpoints.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ProblemResponseAttribute(int statusCode, params string[] codes)
    : ProducesResponseTypeAttribute(
        statusCode == StatusCodes.Status400BadRequest ? typeof(ValidationProblemDetails) : typeof(ProblemDetails),
        statusCode,
        ProblemContentType)
{
    public const string ProblemContentType = "application/problem+json";

    /// <summary>The machine-readable <c>code</c> values this action can send with the status (for example <c>customer.not_found</c>); published in the response description.</summary>
    public IReadOnlyList<string> Codes { get; } = codes;
}

/// <summary>
/// The action reads the <c>If-Match</c> header (<c>ApiControllerBase.IfMatchVersion</c>): the header is documented, a successful response
/// carries the new <c>ETag</c>, and 412 and 428 are added to the responses. <see cref="Required"/> is false for an action that needs the header
/// only for part of its behaviour (an override PUT needs it to change an existing override, not to create one).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IfMatchAttribute : Attribute
{
    public bool Required { get; init; } = true;
}
