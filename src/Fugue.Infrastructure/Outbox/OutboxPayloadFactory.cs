using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Fugue.Domain.Events;
using Fugue.Infrastructure.Audit;

namespace Fugue.Infrastructure.Outbox;

/// <summary>
/// Turns an event's public properties into the stored payload, camelCased, through the audit redaction rules (<see cref="AuditRedactor"/>):
/// masked fields become <c>***</c>, email and phone are partially masked, <c>[NoAudit]</c> properties are left out. Nothing secret is ever in
/// an event; this is the net under that rule (ADR-011).
/// </summary>
internal sealed class OutboxPayloadFactory(AuditRedactor redactor)
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() }, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public string Build(IDomainEvent evt)
    {
        Type type = evt.GetType();
        var payload = new JsonObject();
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0))
        {
            if (redactor.TryRedact(type, property.Name, property.GetValue(evt), out object? value))
            {
                payload[char.ToLowerInvariant(property.Name[0]) + property.Name[1..]] = JsonSerializer.SerializeToNode(value, Json);
            }
        }

        return payload.ToJsonString();
    }
}
