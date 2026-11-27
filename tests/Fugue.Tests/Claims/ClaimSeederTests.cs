using System.Net;
using System.Net.Http.Json;
using Fugue.Application.Features.Claims;
using Fugue.Application.Features.Webhooks.Sink;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Persistence.Seeding;
using Fugue.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fugue.Tests.Claims;

/// <summary>The Development claim seed (decision L), in its own database so the counts are exact: idempotent, consistent and visible through the API.</summary>
public sealed class ClaimSeederTests(PostgresFixture db) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IClaimSeeder>().SeedAsync(default);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<List<T>> RowsAsync<T>(string sql, Func<NpgsqlDataReader, T> read)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        var rows = new List<T>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    [Fact]
    public async Task Seeded_questions_can_be_answered_in_text_and_never_ask_for_uploads()
    {
        List<string> questions = await RowsAsync("SELECT question FROM claim_info_requests", r => r.GetString(0));

        Assert.NotEmpty(questions);
        Assert.DoesNotContain(questions, q => q.Contains("photo", StringComparison.OrdinalIgnoreCase) || q.Contains("upload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_dev_sink_subscription_for_claim_events_is_seeded_once_and_the_sink_trusts_its_secret()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        IDevSinkSeeder seeder = scope.ServiceProvider.GetRequiredService<IDevSinkSeeder>();
        IDevWebhookSink sink = scope.ServiceProvider.GetRequiredService<IDevWebhookSink>();

        bool first = await seeder.SeedAsync("http://localhost:5080/api/v1/dev/webhook-sink", default);
        string? secret = sink.Secret;
        sink.Secret = null; // a restart: the sink forgets, the subscription stays
        bool second = await seeder.SeedAsync("http://localhost:5199/api/v1/dev/webhook-sink", default);

        Assert.True(first || (await RowsAsync("SELECT count(*) FROM webhook_subscriptions WHERE name = 'dev sink (claim events)'", r => r.GetInt64(0))).Single() == 1);
        Assert.False(second);
        Assert.NotNull(sink.Secret);
        Assert.StartsWith("whsec_", sink.Secret, StringComparison.Ordinal);
        Assert.True(!first || secret == sink.Secret); // the secret read back is the one generated on create
        List<(string Url, string Types, bool Active)> rows = await RowsAsync(
            "SELECT url, array_to_string(event_types, ','), is_active FROM webhook_subscriptions WHERE name = 'dev sink (claim events)'",
            r => (r.GetString(0), r.GetString(1), r.GetBoolean(2)));
        Assert.Equal(("http://localhost:5199/api/v1/dev/webhook-sink", "claim.*", true), Assert.Single(rows)); // the moved port is corrected, never duplicated
    }

    [Fact]
    public async Task Seeding_twice_writes_twenty_claims_once()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();

        int again = await scope.ServiceProvider.GetRequiredService<IClaimSeeder>().SeedAsync(default);

        Assert.Equal(0, again);
        Assert.Equal(20, (await RowsAsync("SELECT count(*) FROM claims", r => r.GetInt64(0))).Single());
        Assert.Equal(20, (await RowsAsync("SELECT count(DISTINCT number) FROM claims", r => r.GetInt64(0))).Single());
    }

    [Fact]
    public async Task The_seed_covers_every_market_both_lines_and_every_status_with_the_markets_currency()
    {
        var rows = await RowsAsync("SELECT market, line, status, currency FROM claims", r => (r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)));

        Assert.Equal(Enum.GetNames<Market>().Order(), rows.Select(r => r.Item1).Distinct().Order());
        Assert.Equal(Enum.GetNames<LineOfBusiness>().Order(), rows.Select(r => r.Item2).Distinct().Order());
        Assert.Equal(Enum.GetNames<ClaimStatus>().Order(), rows.Select(r => r.Item3).Distinct().Order());
        Assert.All(rows, r => Assert.Equal(Markets.CurrencyOf(Enum.Parse<Market>(r.Item1)), r.Item4));
        Assert.All(Enum.GetValues<Market>(), m => Assert.True(rows.Count(r => r.Item1 == m.ToString()) >= 3, $"{m} has at least three claims"));
    }

    [Fact]
    public async Task Every_timeline_is_a_contiguous_chain_ending_at_the_claims_status()
    {
        var claims = await RowsAsync("SELECT id, status FROM claims", r => (r.GetGuid(0), r.GetString(1)));
        var timeline = (await RowsAsync(
            "SELECT claim_id, from_status, to_status, occurred_at FROM claim_timeline ORDER BY claim_id, occurred_at, created_at",
            r => (Claim: r.GetGuid(0), From: r.IsDBNull(1) ? null : r.GetString(1), To: r.GetString(2), At: r.GetFieldValue<DateTimeOffset>(3))))
            .GroupBy(e => e.Claim).ToDictionary(g => g.Key, g => g.ToList());

        Assert.Equal(20, claims.Count);
        foreach ((Guid id, string status) in claims)
        {
            var chain = timeline[id];
            Assert.Null(chain[0].From);
            Assert.Equal("Submitted", chain[0].To);
            Assert.Equal(status, chain[^1].To);
            for (int i = 1; i < chain.Count; i++)
            {
                Assert.Equal(chain[i - 1].To, chain[i].From);
                Assert.True(chain[i].At >= chain[i - 1].At);
            }
        }
    }

    [Fact]
    public async Task Money_and_dates_agree_with_the_status()
    {
        var rows = await RowsAsync(
            "SELECT status, reserve_minor, approved_amount_minor, decided_at, settled_at, assigned_officer_id FROM claims",
            r => (Status: r.GetString(0), Reserve: r.IsDBNull(1) ? (long?)null : r.GetInt64(1), Approved: r.IsDBNull(2) ? (long?)null : r.GetInt64(2),
                Decided: !r.IsDBNull(3), Settled: !r.IsDBNull(4), Assigned: !r.IsDBNull(5)));

        Assert.All(rows.Where(r => r.Status == "Submitted"), r => Assert.False(r.Assigned));
        Assert.All(rows.Where(r => r.Status != "Submitted"), r => Assert.True(r.Assigned));
        Assert.All(rows.Where(r => r.Status is "Approved" or "Settled"), r =>
        {
            Assert.True(r.Approved > 0 && r.Approved <= r.Reserve);
            Assert.True(r.Decided);
        });
        Assert.All(rows.Where(r => r.Status == "Settled"), r => Assert.True(r.Settled));
        Assert.All(rows.Where(r => r.Status != "Settled"), r => Assert.False(r.Settled));
        Assert.All(rows.Where(r => r.Status is "Submitted" or "UnderReview" or "InfoRequested" or "Rejected"), r => Assert.Null(r.Approved));
    }

    [Fact]
    public async Task Self_owns_three_claims_one_of_them_waiting_for_an_answer()
    {
        using HttpClient self = db.ClientAs(DevUsers.Self);

        ClaimDto[] mine = (await self.GetFromJsonAsync<ClaimDto[]>("/api/v1/claims"))!;

        Assert.Equal(3, mine.Length);
        ClaimDto waiting = Assert.Single(mine, c => c.Status == ClaimStatus.InfoRequested);
        ClaimDto detail = (await self.GetFromJsonAsync<ClaimDto>($"/api/v1/claims/{waiting.Id}"))!;
        Assert.False(string.IsNullOrWhiteSpace(detail.OpenInfoRequest!.Question));
        Assert.All(mine, c => Assert.Null(c.ReserveMinor));
    }

    [Fact]
    public async Task Staff_and_manager_see_the_queue_and_the_manager_sees_exposure_in_separate_currencies()
    {
        using HttpClient staff = db.ClientAs(DevUsers.Staff);
        using HttpClient manager = db.ClientAs(DevUsers.Manager);

        ClaimDto[] queue = (await staff.GetFromJsonAsync<ClaimDto[]>("/api/v1/claims?status=Submitted&limit=100"))!;
        ClaimExposureDto exposure = (await manager.GetFromJsonAsync<ClaimExposureDto>("/api/v1/claims/exposure"))!;
        ClaimWorkloadDto workload = (await staff.GetFromJsonAsync<ClaimWorkloadDto>("/api/v1/claims/workload"))!;

        Assert.Equal(4, queue.Length);
        Assert.True(queue.Select(c => c.SubmittedAt).SequenceEqual(queue.Select(c => c.SubmittedAt).Order())); // oldest first
        Assert.Equal(6, exposure.Totals.Count); // one per currency, none merged
        Assert.Equal(4, workload.UnassignedCount);
        Assert.Contains(workload.Officers, o => o.OfficerName == "Dev Staff");
        Assert.Contains(workload.Officers, o => o.OfficerName == "Dev Manager");
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/claims/exposure")).StatusCode);
    }

    [Fact]
    public async Task Seeding_fires_no_outbox_events_and_leaves_an_audit_row_per_claim()
    {
        Assert.Equal(0, (await RowsAsync("SELECT count(*) FROM outbox_messages WHERE aggregate_type = 'claim'", r => r.GetInt64(0))).Single());
        Assert.Equal(20, (await RowsAsync("SELECT count(*) FROM audit_log WHERE action = 'claims.seed' AND entity_type = 'Claim'", r => r.GetInt64(0))).Single());
    }
}
