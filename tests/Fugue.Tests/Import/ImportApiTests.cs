using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fugue.Application.Features.Claims.Models;
using Fugue.Application.Features.Import;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Claims;
using Fugue.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Fugue.Tests.Import;

/// <summary>
/// The import endpoints over HTTP against real Postgres (item 11a): a reviewed draft becomes a claim through the normal submit path, the same item
/// twice never becomes two claims (the ledger primary key, also under a race), the checks are Report an incident's own rules, and only staff get in.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ImportApiTests(PostgresFixture db)
{
    private const string Header = "Claimant email,Market,Line,Policy no.,Date of incident,Where,What happened";

    private readonly ClaimsHarness _h = new(db);

    private string Yesterday => _h.Yesterday().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string source, string fileName, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", fileName);
        return await client.PostAsync($"/api/v1/import/files?source={source}", form);
    }

    private static async Task<ImportFileDto> ReadAsync(HttpClient client, string source, string fileName, string text)
    {
        HttpResponseMessage response = await UploadAsync(client, source, fileName, Encoding.UTF8.GetBytes(text));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImportFileDto>())!;
    }

    private static async Task<IReadOnlyList<ImportCheckDto>> CheckAsync(HttpClient client, params object[] drafts)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/import/check", new { drafts });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<ImportCheckDto>>())!;
    }

    private static object Draft(ImportSheetRowDto row) => new
    {
        key = row.Key,
        claimantEmail = row.Cells[0],
        market = row.Cells[1],
        line = row.Cells[2],
        policyNumber = row.Cells[3],
        incidentDate = row.Cells[4],
        incidentLocation = row.Cells[5],
        incidentDescription = row.Cells[6],
    };

    private static object Create(ImportFileDto file, string item, ImportSheetRowDto row, ImportCheckDto verdict) => new
    {
        source = file.Source.ToString(),
        key = row.Key,
        fileName = file.FileName,
        fileSha256 = file.FileSha256,
        item,
        market = verdict.Market.ToString(),
        line = verdict.Line.ToString(),
        policyNumber = row.Cells[3],
        incidentDate = verdict.IncidentDate!.Value.Iso(),
        incidentLocation = row.Cells[5],
        incidentDescription = row.Cells[6],
        claimantEmail = row.Cells[0],
    };

    [Fact]
    public async Task A_spreadsheet_row_becomes_one_claim_owned_by_the_claimant_and_the_same_file_twice_creates_nothing_more()
    {
        using HttpClient staff = _h.Officer();
        string policy = ClaimsHarness.UniquePolicy();
        string csv = $"{Header}\n{DevUsers.Self},Singapore,Motor,{policy},{Yesterday},Orchard Road,Rear-ended at the lights.\n";

        ImportFileDto file = await ReadAsync(staff, "Spreadsheet", "intake.csv", csv);
        ImportSheetRowDto row = Assert.Single(file.Sheet!.Rows);
        Assert.Null(row.Imported);
        ImportCheckDto verdict = Assert.Single(await CheckAsync(staff, Draft(row)));
        Assert.True(verdict.Ready, string.Join("; ", verdict.Errors.SelectMany(e => e.Value)));
        Assert.Equal(Market.SG, verdict.Market);
        Assert.Equal("Dev Self-Service One", verdict.Claimant!.DisplayName);

        HttpResponseMessage created = await staff.PostAsJsonAsync("/api/v1/import/claims", Create(file, "Row 2", row, verdict));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        ClaimDto claim = (await created.Content.ReadFromJsonAsync<ClaimDto>())!;
        Assert.EndsWith($"/api/v1/claims/{claim.Id}", created.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal("Dev Self-Service One", claim.ClaimantName);

        // The claimant owns it and sees the import on the timeline, signed by the team; the normal submit event and the audit rows exist.
        using HttpClient self = _h.Claimant();
        Assert.Equal(HttpStatusCode.OK, (await self.GetAsync($"/api/v1/claims/{claim.Id}")).StatusCode);
        List<ClaimTimelineEntryDto> timeline = (await self.GetFromJsonAsync<List<ClaimTimelineEntryDto>>($"/api/v1/claims/{claim.Id}/timeline"))!;
        Assert.Equal(2, timeline.Count);
        Assert.Equal("Claims team", timeline[1].ActorDisplay);
        Assert.StartsWith("Imported by the claims team from a spreadsheet file import: intake.csv, Row 2.", timeline[1].Note, StringComparison.Ordinal);
        Assert.Equal(1, await _h.ScalarAsync<long>("SELECT count(*) FROM outbox_messages WHERE aggregate_id = @id AND event_type = 'claim.submitted.v1'", ("id", claim.Id.ToString())));
        Assert.Equal(1, await _h.ScalarAsync<long>("SELECT count(*) FROM audit_log WHERE action = 'import.create' AND entity_type = 'Claim' AND entity_id = @id", ("id", claim.Id.ToString())));
        Assert.Equal(1, await _h.ScalarAsync<long>("SELECT count(*) FROM audit_log WHERE action = 'import.create' AND entity_type = 'ClaimImport' AND entity_id = @key", ("key", row.Key)));

        // The same file again: the row is marked, the check is not ready, and a create is refused without a second claim.
        ImportFileDto again = await ReadAsync(staff, "Spreadsheet", "intake (1).csv", csv);
        Assert.Equal(claim.Number, again.Sheet!.Rows[0].Imported!.ClaimNumber);
        ImportCheckDto second = Assert.Single(await CheckAsync(staff, Draft(again.Sheet.Rows[0])));
        Assert.False(second.Ready);
        Assert.Equal(claim.Id, second.Imported!.ClaimId);
        HttpResponseMessage duplicate = await staff.PostAsJsonAsync("/api/v1/import/claims", Create(again, "Row 2", again.Sheet.Rows[0], verdict));
        await ClaimsHarness.AssertProblemAsync(duplicate, HttpStatusCode.Conflict, "import.already_imported");
        Assert.Equal(1, await _h.ScalarAsync<long>("SELECT count(*) FROM claims WHERE policy_number = @p", ("p", policy)));
        Assert.True(await _h.ScalarAsync<long>(
            "SELECT count(*) FROM audit_log WHERE action = 'import.create' AND outcome = 'failure' AND error_code = 'import.already_imported'") >= 1);
    }

    [Fact]
    public async Task Two_creates_of_the_same_item_at_once_make_one_claim()
    {
        using HttpClient staff = _h.Officer();
        string policy = ClaimsHarness.UniquePolicy();
        ImportFileDto file = await ReadAsync(staff, "Spreadsheet", "race.csv", $"{Header}\n{DevUsers.Self2},MY,Property,{policy},{Yesterday},KL,Pipe burst.\n");
        ImportSheetRowDto row = file.Sheet!.Rows[0];
        ImportCheckDto verdict = (await CheckAsync(staff, Draft(row)))[0];

        HttpResponseMessage[] answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => staff.PostAsJsonAsync("/api/v1/import/claims", Create(file, "Row 2", row, verdict))));

        Assert.Equal(1, answers.Count(a => a.StatusCode == HttpStatusCode.Created));
        Assert.All(answers.Where(a => a.StatusCode != HttpStatusCode.Created), a => Assert.Equal(HttpStatusCode.Conflict, a.StatusCode));
        Assert.Equal(1, await _h.ScalarAsync<long>("SELECT count(*) FROM claims WHERE policy_number = @p", ("p", policy)));
    }

    [Fact]
    public async Task The_check_uses_the_report_an_incident_rules_and_names_the_claimant_problems()
    {
        using HttpClient staff = _h.Officer();
        string future = _h.Yesterday().AddDays(5).Iso();
        IReadOnlyList<ImportCheckDto> verdicts = await CheckAsync(
            staff,
            new { key = "a", claimantEmail = "nobody@example.com", market = "Atlantis", line = "Marine", policyNumber = "", incidentDate = "soon", incidentDescription = "x" },
            new { key = "b", claimantEmail = DevUsers.Staff, market = "hk", line = "property", policyNumber = "P-1", incidentDate = future, incidentDescription = "Leak." },
            new { key = "c", claimantEmail = "", market = "Hong Kong", line = "Motor", policyNumber = "P-2", incidentDate = "02/03/2029", incidentDescription = "" });

        ImportCheckDto a = verdicts[0];
        Assert.False(a.Ready);
        Assert.StartsWith("Not a market: \"Atlantis\"", a.Errors["market"][0], StringComparison.Ordinal);
        Assert.StartsWith("Not a line of cover: \"Marine\"", a.Errors["line"][0], StringComparison.Ordinal);
        Assert.StartsWith("Not a date: \"soon\"", Assert.Single(a.Errors["incidentDate"]), StringComparison.Ordinal);
        Assert.True(a.Errors.ContainsKey("policyNumber")); // NotEmpty from the submit validator
        Assert.StartsWith("No live account has this email.", a.Errors["claimantEmail"][0], StringComparison.Ordinal);

        ImportCheckDto b = verdicts[1];
        Assert.Equal(Market.HK, b.Market);
        Assert.Equal(LineOfBusiness.Property, b.Line);
        Assert.Equal(["The incident date cannot be in the future."], b.Errors["incidentDate"]); // the submit validator's own message
        Assert.Equal(["This account cannot own claims (it does not hold CLAIMS_SUBMIT)."], b.Errors["claimantEmail"]);
        Assert.Null(b.Claimant);

        ImportCheckDto c = verdicts[2];
        Assert.Equal(new DateOnly(2029, 3, 2), c.IncidentDate); // day-first
        Assert.Equal(["Give the email of the claimant's account."], c.Errors["claimantEmail"]);
        Assert.True(c.Errors.ContainsKey("incidentDescription"));
        Assert.False(c.Errors.ContainsKey("incidentDate"));
    }

    [Fact]
    public async Task A_create_fails_with_exactly_the_messages_report_an_incident_gives()
    {
        using HttpClient staff = _h.Officer();
        using HttpClient self = _h.Claimant();
        DateOnly future = _h.Yesterday().AddDays(5);
        HttpResponseMessage submit = await ClaimsHarness.PostSubmitAsync(self, new
        {
            market = "SG",
            line = "Motor",
            policyNumber = "",
            incidentDate = future.Iso(),
            incidentDescription = "x",
            incidentLocation = (string?)null,
        });
        HttpResponseMessage import = await staff.PostAsJsonAsync("/api/v1/import/claims", new
        {
            source = "Spreadsheet",
            key = new string('a', 64),
            fileName = "f.csv",
            fileSha256 = new string('b', 64),
            item = "Row 2",
            market = "SG",
            line = "Motor",
            policyNumber = "",
            incidentDate = future.Iso(),
            incidentDescription = "x",
            claimantEmail = DevUsers.Self,
        });

        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, import.StatusCode);
        Dictionary<string, string[]> submitted = await ErrorsAsync(submit);
        Dictionary<string, string[]> imported = await ErrorsAsync(import);
        Assert.Equal(submitted["incidentDate"], imported["incidentDate"]);
        Assert.Equal(submitted["policyNumber"], imported["policyNumber"]);
    }

    [Fact]
    public async Task A_create_for_an_unknown_claimant_is_refused_on_the_claimant_field()
    {
        using HttpClient staff = _h.Officer();
        HttpResponseMessage response = await staff.PostAsJsonAsync("/api/v1/import/claims", new
        {
            source = "Email",
            key = new string('c', 64),
            fileName = "inbox.mbox",
            fileSha256 = new string('d', 64),
            item = "Email 1 of 1: x",
            market = "SG",
            line = "Motor",
            policyNumber = "P-9",
            incidentDate = _h.Yesterday().Iso(),
            incidentDescription = "Dent.",
            claimantEmail = "ghost@example.com",
        });

        await ClaimsHarness.AssertProblemAsync(response, HttpStatusCode.BadRequest, "import.claimant_unknown");
        Assert.True((await ErrorsAsync(response)).ContainsKey("claimantEmail"));
    }

    [Fact]
    public async Task A_whatsapp_chat_becomes_a_claim_with_the_transcript_on_its_timeline()
    {
        using HttpClient staff = _h.Officer();
        string day = _h.Yesterday().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        string policy = ClaimsHarness.UniquePolicy();
        string chat = $"{day}, 21:40 - +60 12 345 6789: Hi, my car was scratched in the mall carpark today\n{day}, 21:42 - Fugue Claims: Policy number please?\n"
            + $"{day}, 21:43 - +60 12 345 6789: {policy}\nself2@fugue.test\n";

        ImportFileDto file = await ReadAsync(staff, "WhatsApp", "WhatsApp Chat with +60 12 345 6789.txt", chat);
        ImportDraftDto draft = Assert.Single(file.Drafts);
        Assert.Equal("MY", draft.Market);
        Assert.Equal(policy, draft.PolicyNumber);
        Assert.Equal(DevUsers.Self2, draft.ClaimantEmail);
        ImportCheckDto verdict = (await CheckAsync(staff, new
        {
            key = draft.Key,
            claimantEmail = draft.ClaimantEmail,
            market = draft.Market,
            line = draft.Line,
            policyNumber = draft.PolicyNumber,
            incidentDate = draft.IncidentDate,
            incidentDescription = draft.IncidentDescription,
            incidentLocation = "Mid Valley Megamall carpark",
        }))[0];
        Assert.True(verdict.Ready, string.Join("; ", verdict.Errors.SelectMany(e => e.Value)));

        HttpResponseMessage created = await staff.PostAsJsonAsync("/api/v1/import/claims", new
        {
            source = "WhatsApp",
            key = draft.Key,
            fileName = file.FileName,
            fileSha256 = file.FileSha256,
            item = draft.Item,
            market = verdict.Market.ToString(),
            line = verdict.Line.ToString(),
            policyNumber = draft.PolicyNumber,
            incidentDate = verdict.IncidentDate!.Value.Iso(),
            incidentDescription = draft.IncidentDescription,
            incidentLocation = "Mid Valley Megamall carpark",
            claimantEmail = draft.ClaimantEmail,
            note = draft.Note,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        ClaimDto claim = (await created.Content.ReadFromJsonAsync<ClaimDto>())!;
        List<ClaimTimelineEntryDto> timeline = (await staff.GetFromJsonAsync<List<ClaimTimelineEntryDto>>($"/api/v1/claims/{claim.Id}/timeline"))!;
        Assert.Contains("WhatsApp chat export file import (standing in for a WhatsApp Business connector)", timeline[1].Note, StringComparison.Ordinal);
        Assert.Contains("Fugue Claims: Policy number please?", timeline[1].Note, StringComparison.Ordinal);
        Assert.Equal("Dev Staff", timeline[1].ActorDisplay); // staff read the officer's name; a claimant reads "Claims team"

        List<ImportedClaimDto> recent = (await staff.GetFromJsonAsync<List<ImportedClaimDto>>("/api/v1/import/claims?limit=50"))!;
        ImportedClaimDto listed = Assert.Single(recent, r => r.ClaimId == claim.Id);
        Assert.Equal(ImportSource.WhatsApp, listed.Source);
        Assert.Equal("Dev Staff", listed.ImportedBy);
    }

    [Fact]
    public async Task An_inbox_export_comes_back_as_drafts_routed_by_the_configured_domain_table()
    {
        using HttpClient staff = _h.Officer();
        byte[] mbox = await File.ReadAllBytesAsync(Path.Combine(FixtureDir(), "inbox.mbox"));

        HttpResponseMessage response = await UploadAsync(staff, "Email", "inbox.mbox", mbox);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ImportFileDto file = (await response.Content.ReadFromJsonAsync<ImportFileDto>())!;
        Assert.Null(file.Sheet);
        Assert.Equal(2, file.Drafts.Count);
        Assert.Equal("HK", file.Drafts[0].Market); // appsettings.json Import:MarketByDomain "harbourbrokers.example": "HK"
    }

    [Fact]
    public async Task A_file_that_is_not_what_the_source_expects_is_a_400_with_the_reason()
    {
        using HttpClient staff = _h.Officer();

        HttpResponseMessage wrong = await UploadAsync(staff, "WhatsApp", "notes.txt", Encoding.UTF8.GetBytes("just some notes\n"));
        HttpResponseMessage empty = await UploadAsync(staff, "Spreadsheet", "empty.csv", []);
        HttpResponseMessage big = await UploadAsync(staff, "Spreadsheet", "big.csv", new byte[(2 * 1024 * 1024) + 10]);

        await ClaimsHarness.AssertProblemAsync(wrong, HttpStatusCode.BadRequest, "import.unreadable");
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        await ClaimsHarness.AssertProblemAsync(big, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task A_claimant_gets_403_on_every_import_endpoint()
    {
        using HttpClient self = _h.Claimant();

        HttpResponseMessage read = await UploadAsync(self, "Spreadsheet", "x.csv", Encoding.UTF8.GetBytes("a,b\n1,2\n"));
        HttpResponseMessage check = await self.PostAsJsonAsync("/api/v1/import/check", new { drafts = new[] { new { key = "a" } } });
        HttpResponseMessage create = await self.PostAsJsonAsync("/api/v1/import/claims", new { source = "Spreadsheet" });
        HttpResponseMessage list = await self.GetAsync("/api/v1/import/claims");

        Assert.All([read, check, create, list], r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }

    private static string FixtureDir([System.Runtime.CompilerServices.CallerFilePath] string here = "") => Path.Combine(Path.GetDirectoryName(here)!, "Fixtures");
}

/// <summary>The claim_imports migration applies, rolls back to AddUsersManagePermission without touching the claims tables, and applies again.</summary>
public sealed class ClaimImportsMigrationRollbackTests
{
    [Fact]
    public async Task Down_drops_claim_imports_only_and_up_applies_again()
    {
        await using PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();
        var builder = new DbContextOptionsBuilder<FugueDbContext>();
        PersistenceOptions.Configure(builder, container.GetConnectionString());
        await using var context = new FugueDbContext(builder.Options);
        IMigrator migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync();
        Assert.True(await ExistsAsync(container, "SELECT to_regclass('claim_imports') IS NOT NULL"));
        Assert.True(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_claim_imports_key_sha256')"));

        await migrator.MigrateAsync("AddUsersManagePermission");
        Assert.False(await ExistsAsync(container, "SELECT to_regclass('claim_imports') IS NOT NULL"));
        Assert.True(await ExistsAsync(container, "SELECT to_regclass('claims') IS NOT NULL AND to_regclass('claim_timeline') IS NOT NULL"));

        await migrator.MigrateAsync();
        Assert.True(await ExistsAsync(container, "SELECT to_regclass('ux_claim_imports_claim') IS NOT NULL"));
    }

    private static async Task<bool> ExistsAsync(PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
