using System.Text.RegularExpressions;
using Fugue.Infrastructure.Persistence;

namespace Fugue.Tests.Infrastructure;

/// <summary>Runtime SQL lives in embedded .sql files (D-054); this pins that the files and their <c>SqlScripts.Get</c> callers agree both ways.</summary>
public sealed partial class SqlScriptsTests
{
    [GeneratedRegex("""SqlScripts\.Get\("(?<name>[a-z0-9/-]+)"\)""")]
    private static partial Regex GetCall();

    [Fact]
    public void Every_SqlScripts_Get_literal_in_src_resolves_to_an_embedded_script()
    {
        string[] missing = [.. Referenced().Where(n => !SqlScripts.Names.Contains(n)).Order(StringComparer.Ordinal)];

        Assert.True(missing.Length == 0, "No embedded script for: " + string.Join(", ", missing));
        Assert.All(Referenced(), n => Assert.False(string.IsNullOrWhiteSpace(SqlScripts.Get(n))));
    }

    [Fact]
    public void No_embedded_script_is_unreferenced()
    {
        HashSet<string> referenced = Referenced();
        string[] orphans = [.. SqlScripts.Names.Where(n => !referenced.Contains(n)).Order(StringComparer.Ordinal)];

        Assert.True(orphans.Length == 0, "Unreferenced scripts: " + string.Join(", ", orphans));
    }

    [Fact]
    public void A_missing_script_throws_naming_it()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => SqlScripts.Get("nope/missing"));

        Assert.Contains("nope/missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_script_file_on_disk_is_embedded()
    {
        string root = Path.Combine(RepoRoot(), "src", "Fugue.Infrastructure", "Persistence", "Sql");
        string[] onDisk = [.. Directory.GetFiles(root, "*.sql", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')[..^".sql".Length]).Order(StringComparer.Ordinal)];

        Assert.Equal(onDisk, SqlScripts.Names.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_script_header_names_a_caller_file_that_exists()
    {
        string src = Path.Combine(RepoRoot(), "src");
        string[] cs = [.. Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(Path.GetFileName).OfType<string>()];
        string[] bad = [.. Directory.GetFiles(Path.Combine(src, "Fugue.Infrastructure", "Persistence", "Sql"), "*.sql", SearchOption.AllDirectories)
            .Where(f =>
            {
                Match m = CallerHeader().Match(File.ReadLines(f).First());
                return !m.Success || !cs.Contains(m.Groups["caller"].Value + ".cs");
            })
            .Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal)];

        Assert.True(bad.Length == 0, "First line must be '-- <what>. Caller: <ClassName>.' naming an existing <ClassName>.cs: " + string.Join(", ", bad));
    }

    [GeneratedRegex(@"^-- .+ Caller: (?<caller>\w+)\.$")]
    private static partial Regex CallerHeader();

    private static HashSet<string> Referenced() =>
        [.. Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => GetCall().Matches(File.ReadAllText(f)).Select(m => m.Groups["name"].Value))];

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Fugue.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Fugue.sln not found above the test binaries.");
    }
}
