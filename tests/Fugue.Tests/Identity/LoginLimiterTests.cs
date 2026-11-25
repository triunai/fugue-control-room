using System.Net;
using System.Net.Http.Json;
using Fugue.Tests.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Fugue.Tests.Identity;

/// <summary>
/// D-035: login is throttled per (client IP, canonical email) and by a loose per-email ceiling, so one IP cannot lock a victim out.
/// TestServer has no remote address, so the host reads it from <c>X-Test-Ip</c>.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class LoginLimiterTests(PostgresFixture db)
{
    private WebApplicationFactoryHandle Host(params (string Key, string Value)[] settings)
    {
        var factory = db.Factory.WithWebHostBuilder(b =>
        {
            foreach ((string key, string value) in settings)
            {
                b.UseSetting(key, value);
            }

            b.UseSetting("RateLimiting:Auth:WindowSeconds", "600");
            b.ConfigureTestServices(s => s.AddTransient<IStartupFilter, TestIpStartupFilter>());
        });
        return new WebApplicationFactoryHandle(factory, factory.CreateClient());
    }

    private static Task<HttpResponseMessage> Login(HttpClient client, string ip, string email, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/token") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-Test-Ip", ip);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task One_ip_hammering_a_victim_is_throttled_while_the_victim_from_another_ip_still_logs_in()
    {
        (string victim, _) = await db.NewUserAsync();
        using WebApplicationFactoryHandle host = Host(("RateLimiting:Auth:PerEmailPermits", "3"));

        var attacker = new List<HttpStatusCode>();
        for (int i = 0; i < 5; i++)
        {
            attacker.Add((await Login(host.Client, "10.0.0.1", i % 2 == 0 ? victim : victim.ToUpperInvariant(), "wrong")).StatusCode);
        }

        Assert.Equal([.. Enumerable.Repeat(HttpStatusCode.Unauthorized, 3), HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], attacker);
        HttpResponseMessage limited = await Login(host.Client, "10.0.0.1", victim, TestAuth.Password);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode); // the attacker's own bucket stays closed, even with the right password
        Assert.NotNull(limited.Headers.RetryAfter);

        HttpResponseMessage victimLogin = await Login(host.Client, "10.0.0.2", victim, TestAuth.Password);
        Assert.Equal(HttpStatusCode.OK, victimLogin.StatusCode);
    }

    [Fact]
    public async Task The_per_email_ceiling_bounds_failures_spread_over_many_ips()
    {
        (string victim, _) = await db.NewUserAsync();
        using WebApplicationFactoryHandle host = Host(("RateLimiting:Auth:EmailCeilingPermits", "3"));

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(host.Client, $"10.1.0.{i}", victim, "wrong")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login(host.Client, "10.1.0.99", victim, TestAuth.Password)).StatusCode);
        // Another account is unaffected.
        (string other, _) = await db.NewUserAsync();
        Assert.Equal(HttpStatusCode.OK, (await Login(host.Client, "10.1.0.99", other, TestAuth.Password)).StatusCode);
    }

    [Fact]
    public async Task Successful_logins_never_charge_the_ceiling()
    {
        (string victim, _) = await db.NewUserAsync();
        using WebApplicationFactoryHandle host = Host(("RateLimiting:Auth:EmailCeilingPermits", "2"));

        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await Login(host.Client, $"10.2.0.{i}", victim, TestAuth.Password)).StatusCode);
        }
    }

    [Fact]
    public async Task The_per_ip_limit_still_bites_across_different_emails()
    {
        using WebApplicationFactoryHandle host = Host(("RateLimiting:Auth:PerIpPermits", "2"));
        // The per-IP policy partitions on the connection address; the startup filter sets it, so one header value is one IP.
        var statuses = new List<HttpStatusCode>();
        for (int i = 0; i < 4; i++)
        {
            statuses.Add((await Login(host.Client, "10.3.0.1", $"a{i}@example.test", "x")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], statuses);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(host.Client, "10.3.0.2", "b@example.test", "x")).StatusCode);
    }

    private sealed class TestIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nxt) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Ip", out Microsoft.Extensions.Primitives.StringValues ip) && System.Net.IPAddress.TryParse(ip.ToString(), out System.Net.IPAddress? address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                return nxt(context);
            });
            next(app);
        };
    }

    private sealed class WebApplicationFactoryHandle(IDisposable factory, HttpClient client) : IDisposable
    {
        public HttpClient Client { get; } = client;

        public void Dispose()
        {
            Client.Dispose();
            factory.Dispose();
        }
    }
}
