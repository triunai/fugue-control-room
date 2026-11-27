using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Idempotency;

/// <summary>
/// Opts a create POST into <c>Idempotency-Key</c> handling (ADR-013): a client that retries with the same key gets the first response instead of a
/// second execution. Implemented as a resource filter (<see cref="IdempotencyFilter"/>) resolved from DI. Without the header the action behaves as before.
/// The OpenAPI operation filter documents the header and the 409/422 responses from this marker.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute() : TypeFilterAttribute(typeof(IdempotencyFilter));
