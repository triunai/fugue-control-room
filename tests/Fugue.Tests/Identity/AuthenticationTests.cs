using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Fugue.Api.Auth;
using Fugue.Application.Features.Auth;
using Fugue.Domain.Identity;
using Fugue.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Fugue.Tests.Identity;

/// <summary>Authentication gates (docs/plans/s3-identity.md section 7): login, uniform failure, JWT rejection, canonical email.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AuthenticationTests(PostgresFixture db)
{
    private static async Task<JsonElement> Problem(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private Task<HttpResponseMessage> Login(string email, string password) =>
        db.Anonymous().PostAsJsonAsync("/api/v1/auth/token", new { email, password });

    [Fact]
    public async Task Login_returns_a_bearer_token_that_works_and_carries_identity_claims_only()
    {
        HttpResponseMessage response = await Login(DevUsers.Admin, TestAuth.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        TokenDto token = (await response.Content.ReadFromJsonAsync<TokenDto>())!;
        Assert.Equal("Bearer", token.TokenType);
        Assert.InRange(token.ExpiresIn, 14 * 60, 15 * 60);
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);
        Assert.Equal("HS256", jwt.Header.Alg);
        Assert.Equal(["aud", "exp", "iat", "iss", "jti", "nbf", "sub"], jwt.Claims.Select(c => c.Type).Order().ToArray()); // no permissions in the token

        using HttpClient client = db.Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/customers?limit=1")).StatusCode);
    }

    [Fact]
    public async Task Every_login_failure_is_the_same_401_with_the_same_body()
    {
        (string inactive, _) = await db.NewUserAsync([RoleCodes.Staff]);
        await Sql("UPDATE users SET is_active = false WHERE email = @v", inactive);
        (string noLocal, _) = await db.NewUserAsync([RoleCodes.Staff]);
        await Sql("UPDATE users SET local_login_enabled = false WHERE email = @v", noLocal);
        (string deleted, _) = await db.NewUserAsync([RoleCodes.Staff]);
        await Sql("UPDATE users SET is_deleted = true WHERE email = @v", deleted);

        var bodies = new List<string>();
        foreach ((string email, string password) in new[]
        {
            ("nobody@example.test", TestAuth.Password), (DevUsers.Admin, "wrong-password"),
            (inactive, TestAuth.Password), (noLocal, TestAuth.Password), (deleted, TestAuth.Password),
        })
        {
            HttpResponseMessage response = await Login(email, password);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            JsonElement problem = await Problem(response);
            Assert.Equal("auth.invalid_credentials", problem.GetProperty("code").GetString());
            bodies.Add(problem.GetProperty("title").GetString()! + problem.GetProperty("type").GetString() + problem.GetProperty("status").GetInt32());
        }

        Assert.Single(bodies.Distinct());
    }

    [Fact]
    public async Task Email_is_canonical_trimmed_and_case_insensitive()
    {
        HttpResponseMessage response = await Login("  ADMIN@Fugue.TEST ", TestAuth.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Case_variant_duplicates_are_rejected_but_a_deleted_email_can_be_reused()
    {
        string email = $"dup{Guid.NewGuid():N}@example.test";
        string canonical = EmailKey.Canonicalize(email.ToUpperInvariant());
        await InsertUser(canonical, deleted: false);

        await Assert.ThrowsAsync<DbUpdateException>(() => InsertUser(EmailKey.Canonicalize(" " + email.ToUpperInvariant() + " "), deleted: false));

        await Sql("UPDATE users SET is_deleted = true WHERE email = @v", canonical);
        await InsertUser(canonical, deleted: false); // reuse after delete: the unique index only covers live rows
        Assert.Equal(HttpStatusCode.OK, (await Login(canonical, TestAuth.Password)).StatusCode); // only the live row authenticates
    }

    [Fact]
    public async Task No_token_is_401_problem_json_and_names_nothing()
    {
        HttpResponseMessage response = await db.Anonymous().GetAsync("/api/v1/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        JsonElement problem = await Problem(response);
        Assert.Equal("auth.unauthenticated", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("correlationId", out _));
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    public static TheoryData<string> BadTokens() => new() { "expired", "bad-signature", "wrong-issuer", "wrong-audience", "wrong-algorithm", "garbage", "unknown-user" };

    [Theory]
    [MemberData(nameof(BadTokens))]
    public async Task A_token_that_fails_validation_is_401(string kind)
    {
        DateTimeOffset now = db.Clock.GetUtcNow();
        (_, Fugue.Domain.Ids.UserId real) = await db.NewUserAsync([RoleCodes.Admin]);
        Guid sub = real.Value;
        string token = kind switch
        {
            "expired" => TestTokens.Make(db.SigningKey, sub, now, lifetime: TimeSpan.FromHours(-1)),
            "bad-signature" => TestTokens.Make(db.SigningKey, sub, now, keyBytes: RandomNumberGenerator.GetBytes(32)),
            "wrong-issuer" => TestTokens.Make(db.SigningKey, sub, now, issuer: "someone-else"),
            "wrong-audience" => TestTokens.Make(db.SigningKey, sub, now, audience: "another-api"),
            // HS512 needs a 64-byte key to sign, so this token is also badly signed; the validator pins HS256 either way.
            "wrong-algorithm" => TestTokens.Make(db.SigningKey, sub, now, algorithm: SecurityAlgorithms.HmacSha512, keyBytes: [.. Convert.FromBase64String(db.SigningKey), .. Convert.FromBase64String(db.SigningKey)]),
            "unknown-user" => TestTokens.Make(db.SigningKey, Guid.NewGuid(), now),
            _ => "not.a.jwt",
        };
        using HttpClient client = db.Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.GetAsync("/api/v1/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.unauthenticated", (await Problem(response)).GetProperty("code").GetString());

        // Sanity: the same helper with valid inputs is accepted, so the rejections above are about the one thing that differs.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Make(db.SigningKey, sub, now));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/customers?limit=1")).StatusCode);
    }

    [Fact]
    public async Task Permission_claims_smuggled_into_a_token_are_discarded()
    {
        (_, Fugue.Domain.Ids.UserId nobody) = await db.NewUserAsync(); // holds no permission at all
        using HttpClient client = db.Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Make(db.SigningKey, nobody.Value, db.Clock.GetUtcNow(), extraPermClaim: Permissions.CustomersView));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/customers?limit=1")).StatusCode);
    }

    [Fact]
    public async Task A_user_deactivated_mid_session_is_401_on_the_next_request()
    {
        (string email, _) = await db.NewUserAsync([RoleCodes.Staff]);
        using HttpClient client = db.ClientAs(email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/customers?limit=1")).StatusCode);

        await Sql("UPDATE users SET is_active = false WHERE email = @v", email);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/customers?limit=1")).StatusCode);
    }

    [Fact]
    public async Task Missing_permission_is_403_problem_json_that_names_no_permission()
    {
        using HttpClient self = db.ClientAs(DevUsers.Self);

        HttpResponseMessage response = await self.DeleteAsync($"/api/v1/customers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal("auth.forbidden", JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("CUSTOMERS_DELETE", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_is_rate_limited_per_email_before_any_password_work()
    {
        var counter = new CountingVerifier();
        using var factory = db.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimiting:Auth:PerEmailPermits", "3");
            b.UseSetting("RateLimiting:Auth:WindowSeconds", "600");
            b.ConfigureTestServices(s => s.AddSingleton<Fugue.Application.Abstractions.IPasswordVerifier>(counter));
        });
        using HttpClient client = factory.CreateClient();
        string email = $"victim{Guid.NewGuid():N}@example.test";

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage last = null!;
        for (int i = 0; i < 6; i++)
        {
            last = await client.PostAsJsonAsync("/api/v1/auth/token", new { email = i % 2 == 0 ? email : email.ToUpperInvariant(), password = "x" });
            statuses.Add(last.StatusCode);
        }

        Assert.Equal([.. Enumerable.Repeat(HttpStatusCode.Unauthorized, 3), .. Enumerable.Repeat(HttpStatusCode.TooManyRequests, 3)], statuses);
        Assert.Equal(3, counter.Calls); // the 429s never reached the verifier
        Assert.NotNull(last.Headers.RetryAfter);
        Assert.Equal("auth.rate_limited", (await Problem(last)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_is_rate_limited_per_ip()
    {
        using var factory = db.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimiting:Auth:PerIpPermits", "2");
            b.UseSetting("RateLimiting:Auth:WindowSeconds", "600");
        });
        using HttpClient client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (int i = 0; i < 4; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/v1/auth/token", new { email = $"a{i}@example.test", password = "x" })).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], statuses);
    }

    private async Task Sql(string sql, string value)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("v", value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertUser(string email, bool deleted)
    {
        using IServiceScope scope = db.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Fugue.Infrastructure.Persistence.FugueDbContext>();
        context.Users.Add(new User
        {
            Id = Fugue.Domain.Ids.UserId.New(),
            DisplayName = "dup",
            Email = email,
            IsDeleted = deleted,
            PasswordHash = scope.ServiceProvider.GetRequiredService<Fugue.Infrastructure.Identity.PasswordVerifier>().Hash(TestAuth.Password),
        });
        await context.SaveChangesAsync();
    }

    private sealed class CountingVerifier : Fugue.Application.Abstractions.IPasswordVerifier
    {
        public int Calls { get; private set; }

        public bool Verify(string? hash, string password)
        {
            Calls++;
            return false;
        }
    }
}
