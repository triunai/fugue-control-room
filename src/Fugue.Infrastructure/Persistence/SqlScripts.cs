using System.Collections.Frozen;
using System.Reflection;

namespace Fugue.Infrastructure.Persistence;

/// <summary>
/// The runtime SQL of this assembly, kept as <c>Persistence/Sql/&lt;area&gt;/&lt;name&gt;.sql</c> embedded resources and addressed as
/// <c>"area/name"</c>. Each script is read once. Schema migrations are not here: they stay EF Core.
/// </summary>
internal static class SqlScripts
{
    private const string Prefix = "sql/";
    private const string Extension = ".sql";

    private static readonly Assembly Source = typeof(SqlScripts).Assembly;

    private static readonly Lazy<FrozenDictionary<string, string>> Scripts = new(Load);

    /// <summary>Every script name (<c>area/name</c>) embedded in the assembly.</summary>
    public static IReadOnlyCollection<string> Names => Scripts.Value.Keys;

    /// <summary>The text of script <paramref name="name"/> (for example <c>claims/insert</c>), without its trailing newline.</summary>
    public static string Get(string name) =>
        Scripts.Value.TryGetValue(name, out string? sql) ? sql : throw new InvalidOperationException($"No embedded SQL script named '{name}'.");

    private static FrozenDictionary<string, string> Load()
    {
        var scripts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string resource in Source.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(Extension, StringComparison.Ordinal)))
        {
            using Stream stream = Source.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            string name = resource[Prefix.Length..^Extension.Length].Replace('\\', '/');
            scripts[name] = reader.ReadToEnd().TrimEnd();
        }

        return scripts.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
