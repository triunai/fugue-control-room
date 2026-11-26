namespace Fugue.Tests.OpenApi;

/// <summary>
/// The OpenAPI document is a contract the console generates its client from (D-042). The committed snapshot <c>docs/api/openapi.v1.json</c>
/// must equal what the code produces; a change to a route, a DTO, a status or a header fails here until the snapshot is refreshed on purpose
/// (<c>npm run api:openapi</c>) and reviewed in the diff.
/// </summary>
public sealed class OpenApiContractTests
{
    public const string UpdateVariable = "FUGUE_OPENAPI_UPDATE";

    [Fact]
    public void The_committed_snapshot_equals_the_generated_document()
    {
        string actual = OpenApiSnapshot.Render();
        string path = OpenApiSnapshot.SnapshotPath();

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual, new System.Text.UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(path), $"{OpenApiSnapshot.RelativePath} is missing. Create it with: npm run api:openapi");
        string committed = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.True(
            committed == actual,
            $"The API contract changed and {OpenApiSnapshot.RelativePath} is out of date. If the change is intended, run `npm run api:openapi` "
            + "(or set FUGUE_OPENAPI_UPDATE=1 and run this test), review the diff of the snapshot, and commit it with the code. "
            + "The console generates its client from that file.");
    }
}
