using System.Reflection;
using System.Text.RegularExpressions;

namespace Fugue.Application.Common.Behaviors;

/// <summary>
/// Derives the audit action, entity type and entity id from a mediator message type, once per type.
/// <c>Fugue.Application.Features.Customers.Commands.UpdateCustomerCommand</c> becomes action <c>customers.update</c> and entity
/// type <c>Customer</c>; the entity id is the message's <c>Id</c> property when it has one.
/// </summary>
internal static partial class AuditNaming<TMessage>
{
    public static readonly string Action = BuildAction();
    public static readonly string? EntityType = BuildEntityType();
    private static readonly PropertyInfo? IdProperty = typeof(TMessage).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);

    public static string? EntityId(TMessage message) => IdProperty?.GetValue(message)?.ToString();

    private static string? Feature()
    {
        string[] parts = (typeof(TMessage).Namespace ?? string.Empty).Split('.');
        int at = Array.IndexOf(parts, "Features");
        return at >= 0 && at + 1 < parts.Length ? parts[at + 1] : null;
    }

    private static string BuildAction()
    {
        string name = typeof(TMessage).Name;
        name = name.EndsWith("Command", StringComparison.Ordinal) ? name[..^"Command".Length] : name;
        Match verb = FirstWord().Match(name);
        string action = $"{(Feature() ?? "app").ToLowerInvariant()}.{(verb.Success ? verb.Value : name).ToLowerInvariant()}";
        return action.Length <= 64 ? action : action[..64];
    }

    private static string? BuildEntityType()
    {
        string? feature = Feature();
        if (feature is null)
        {
            return null;
        }

        return feature.Length > 3 && feature.EndsWith('s') && !feature.EndsWith("ss", StringComparison.Ordinal) ? feature[..^1] : feature;
    }

    [GeneratedRegex("^[A-Z][a-z0-9]*")]
    private static partial Regex FirstWord();
}
