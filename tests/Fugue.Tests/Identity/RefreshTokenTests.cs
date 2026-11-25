using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fugue.Application.Features.Auth;
using Fugue.Domain.Identity;
using Fugue.Tests.Persistence;
using Npgsql;

namespace Fugue.Tests.Identity;

/// <summary>Refresh-token gates: rotation, reuse revokes the family, expiry, logout, hashing at rest, inactive users, rate limit.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class RefreshTokenTests(PostgresFixture db)
{
    private static readonly DateTimeOffset BaseClock = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private async Task<TokenDto> Login(string email)
    {
        HttpResponseMessage response = await db.Anonymous().PostAsJsonAsync("/api/v1/auth/token", new { email, password = TestAuth.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TokenDto>())!;
    }

    private Task<HttpResponseMessage> Refresh(string token, HttpClient? client = null) =>
        (client ?? db.Anonymous()).PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = token });

    private static async Task AssertInvalidRefresh(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("auth.invalid_refresh_token", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_the_old_one_dies()
    {
        TokenDto first = await Login(DevUsers.Staff);
        Assert.False(string.IsNullOrEmpty(first.RefreshToken));

        HttpResponseMessage rotated = await Refresh(first.RefreshToken!);

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        TokenDto second = (await rotated.Content.ReadFromJsonAsync<TokenDto>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        using HttpClient api = db.Anonymous();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/v1/customers?limit=1")).StatusCode);
        await AssertInvalidRefresh(await Refresh(first.RefreshToken!));
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_family()
    {
        TokenDto first = await Login(DevUsers.Staff);
        TokenDto second = (await (await Refresh(first.RefreshToken!)).Content.ReadFromJsonAsync<TokenDto>())!;
        TokenDto third = (await (await Refresh(second.RefreshToken!)).Content.ReadFromJsonAsync<TokenDto>())!;

        await AssertInvalidRefresh(await Refresh(first.RefreshToken!)); // an attacker replays a stolen, already-rotated token
        await AssertInvalidRefresh(await Refresh(third.RefreshToken!)); // the legitimate newest token died with its family

        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("SELECT count(*) FROM refresh_tokens WHERE family_id = (SELECT family_id FROM refresh_tokens WHERE token_hash = @h) AND revoked_at IS NULL", connection);
        command.Parameters.AddWithValue("h", RefreshTokenSecret.Hash(third.RefreshToken!));
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task A_second_login_is_a_separate_family_unaffected_by_reuse_in_the_first()
    {
        TokenDto device1 = await Login(DevUsers.Staff);
        TokenDto device2 = await Login(DevUsers.Staff);
        await Refresh(device1.RefreshToken!);
        await Refresh(device1.RefreshToken!); // reuse: kills device 1's family only

        Assert.Equal(HttpStatusCode.OK, (await Refresh(device2.RefreshToken!)).StatusCode);
    }

    [Fact]
    public async Task An_expired_refresh_token_is_401()
    {
        TokenDto token = await Login(DevUsers.Staff);
        db.Clock.Now = BaseClock.AddDays(8); // lifetime is 7 days
        try
        {
            await AssertInvalidRefresh(await Refresh(token.RefreshToken!));
        }
        finally
        {
            db.Clock.Now = BaseClock;
        }
    }

    [Fact]
    public async Task Logout_revokes_the_family_and_is_always_204()
    {
        TokenDto token = await Login(DevUsers.Staff);

        Assert.Equal(HttpStatusCode.NoContent, (await db.Anonymous().PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = token.RefreshToken })).StatusCode);
        await AssertInvalidRefresh(await Refresh(token.RefreshToken!));
        Assert.Equal(HttpStatusCode.NoContent, (await db.Anonymous().PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = "never-issued" })).StatusCode);
    }

    [Fact]
    public async Task An_unknown_token_and_a_deactivated_user_cannot_refresh()
    {
        await AssertInvalidRefresh(await Refresh("never-issued"));

        (string email, _) = await db.NewUserAsync([RoleCodes.Staff]);
        TokenDto token = await Login(email);
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("UPDATE users SET is_active = false WHERE email = @e", connection);
        command.Parameters.AddWithValue("e", email);
        await command.ExecuteNonQueryAsync();

        await AssertInvalidRefresh(await Refresh(token.RefreshToken!));
    }

    [Fact]
    public async Task Only_the_sha256_digest_is_stored_never_the_token()
    {
        TokenDto token = await Login(DevUsers.Staff);
        string expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.RefreshToken!))).ToLowerInvariant();

        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("SELECT count(*) FILTER (WHERE token_hash = @hash), count(*) FILTER (WHERE token_hash = @raw) FROM refresh_tokens", connection);
        command.Parameters.AddWithValue("hash", expected);
        command.Parameters.AddWithValue("raw", token.RefreshToken!);
        await using NpgsqlDataReader row = await command.ExecuteReaderAsync();
        await row.ReadAsync();
        Assert.Equal(1L, row.GetInt64(0));
        Assert.Equal(0L, row.GetInt64(1));
    }

    [Fact]
    public async Task Refresh_is_rate_limited_like_login()
    {
        using var factory = db.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimiting:Auth:PerIpPermits", "2");
            b.UseSetting("RateLimiting:Auth:WindowSeconds", "600");
        });
        using HttpClient client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (int i = 0; i < 3; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "x" + i })).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }

    /// <summary>Pins the Codex build-review HIGH: revoking a family must wait for an in-flight rotation of that family.</summary>
    [Fact]
    public async Task Family_revocation_waits_for_an_in_flight_rotation_holding_the_family_lock()
    {
        TokenDto token = await Login(DevUsers.Staff);
        await using NpgsqlConnection holder = new(db.ConnectionString);
        await holder.OpenAsync();
        await using NpgsqlCommand family = new("SELECT family_id FROM refresh_tokens WHERE token_hash = @h", holder);
        family.Parameters.AddWithValue("h", RefreshTokenSecret.Hash(token.RefreshToken!));
        var familyId = (Guid)(await family.ExecuteScalarAsync())!;

        await using NpgsqlTransaction rotation = await holder.BeginTransactionAsync();
        await using (NpgsqlCommand lockCommand = new("SELECT pg_advisory_xact_lock(hashtextextended(@f, 0))", holder, rotation))
        {
            lockCommand.Parameters.AddWithValue("f", familyId.ToString());
            await lockCommand.ExecuteNonQueryAsync();
        }

        Task<HttpResponseMessage> logout = db.Anonymous().PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = token.RefreshToken });
        Task finished = await Task.WhenAny(logout, Task.Delay(1500));
        Assert.NotSame(logout, finished); // still blocked behind the rotation

        await rotation.CommitAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await logout).StatusCode);
    }
}
