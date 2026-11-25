using Fugue.Domain.Abstractions;

namespace Fugue.Infrastructure.Persistence;

/// <summary>Now (from <see cref="TimeProvider"/>) and actor (<see cref="ICurrentUser"/>, else <c>"system"</c>).</summary>
internal sealed class AuditStamp(ICurrentUser user, TimeProvider time) : IAuditStamp
{
    public AuditStampValue Capture() =>
        new(time.GetUtcNow(), user.UserId?.ToString() ?? IAuditStamp.SystemActor);
}
