using System.Reflection;
using Fugue.Application.Abstractions;
using Fugue.Domain.Audit;

namespace Fugue.Infrastructure.Audit;

/// <summary>Reads the public properties of an entity as "added" audit fields, for write paths the change tracker does not see (Dapper).</summary>
internal static class AuditSnapshot
{
    public static AuditEntry Created(object entity, string entityId, string? defaultAction = null, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        Type type = entity.GetType();
        var fields = new Dictionary<string, AuditFieldChange>();
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0
                || AuditRedactor.NoiseFields.Contains(property.Name)
                || Attribute.IsDefined(property, typeof(NoAuditAttribute)))
            {
                continue;
            }

            if (property.GetValue(entity) is { } value)
            {
                fields[property.Name] = AuditFieldChange.Added(value);
            }
        }

        return new AuditEntry(type.Name, entityId, fields, type, defaultAction, metadata);
    }
}
