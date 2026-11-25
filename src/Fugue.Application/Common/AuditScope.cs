using Fugue.Application.Abstractions;

namespace Fugue.Application.Common;

/// <summary>Scoped holder of the ambient audit context (see <see cref="IAuditScope"/>).</summary>
public sealed class AuditScope : IAuditScope
{
    private static readonly IReadOnlyDictionary<string, object?> NoMetadata = new Dictionary<string, object?>();

    public string? Action { get; private set; }

    public string? ActorDisplay { get; private set; }

    public IReadOnlyDictionary<string, object?> Metadata { get; private set; } = NoMetadata;

    public void Begin(string action, string? actorDisplay, IReadOnlyDictionary<string, object?> metadata)
    {
        Action = action;
        ActorDisplay = actorDisplay;
        Metadata = metadata;
    }

    public void End()
    {
        Action = null;
        ActorDisplay = null;
        Metadata = NoMetadata;
    }
}
