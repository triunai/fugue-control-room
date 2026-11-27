using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Idempotency;

/// <summary>
/// Opts a create POST into <c>Idempotency-Key</c> handling (ADR-013): a client that retries with the same key gets the first response instead of a
/// second execution. Implemented as a resource filter (<see cref="IdempotencyFilter"/>) resolved from DI. Without the header the action behaves as before,
/// unless <see cref="Required"/> is set: then a missing header is 400 <c>idempotency.key_required</c> (claim submit, where a duplicate is real harm).
/// The OpenAPI operation filter documents the header and the 409/422 responses from this marker.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute() : TypeFilterAttribute(typeof(IdempotencyFilter))
{
    /// <summary>True when the action refuses a request without an <c>Idempotency-Key</c>. Optional (false) is the ADR-013 default.</summary>
    public bool Required { get; init; }
}
