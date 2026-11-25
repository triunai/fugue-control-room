using System.Net;
using Fugue.Infrastructure.Webhooks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Fugue.Tests.Webhooks;

/// <summary>The address rules and the subscribe-time resolution of the SSRF guard, without a database (ADR-011).</summary>
public sealed class NetworkGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.1")]
    [InlineData("::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("::ffff:127.0.0.1")]
    public void Loopback_and_private_ranges_are_blocked_unless_local_is_allowed(string ip)
    {
        Assert.True(NetworkGuard.IsBlocked(IPAddress.Parse(ip), allowLocal: false));
        Assert.False(NetworkGuard.IsBlocked(IPAddress.Parse(ip), allowLocal: true));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("93.184.216.34")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("172.32.0.1")] // just outside 172.16/12
    [InlineData("172.15.255.255")]
    public void Public_addresses_pass_either_way(string ip)
    {
        Assert.False(NetworkGuard.IsBlocked(IPAddress.Parse(ip), allowLocal: false));
        Assert.False(NetworkGuard.IsBlocked(IPAddress.Parse(ip), allowLocal: true));
    }

    [Theory]
    [InlineData("169.254.169.254")] // cloud metadata
    [InlineData("169.254.1.1")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("ff02::1")]
    [InlineData("64:ff9b::a9fe:a9fe")] // NAT64 of 169.254.169.254
    [InlineData("2002:a9fe:a9fe::1")]  // 6to4 of 169.254.169.254
    [InlineData("2001:db8::1")]
    public void Link_local_metadata_cgnat_multicast_and_embedded_forms_are_blocked_even_when_local_is_allowed(string ip)
    {
        Assert.True(NetworkGuard.IsBlocked(IPAddress.Parse(ip), allowLocal: true));
        Assert.True(NetworkGuard.IsBlocked(IPAddress.Parse(ip), allowLocal: false));
    }

    [Fact]
    public async Task A_name_that_resolves_to_a_private_address_is_rejected_at_subscribe_time()
    {
        var guard = new WebhookUrlGuard(new FakeResolver("10.0.0.7"), Options.Create(new WebhookOptions()));

        FluentResults.Result result = await guard.CheckAsync("https://hooks.example.test/x", default);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task One_private_answer_among_public_ones_is_enough_to_reject()
    {
        var guard = new WebhookUrlGuard(new FakeResolver("93.184.216.34", "10.0.0.7"), Options.Create(new WebhookOptions()));

        Assert.True((await guard.CheckAsync("https://hooks.example.test/x", default)).IsFailed);
    }

    [Fact]
    public async Task AllowInsecureLocal_permits_http_and_loopback_but_never_the_metadata_address()
    {
        var local = Options.Create(new WebhookOptions { AllowInsecureLocal = true });
        var guard = new WebhookUrlGuard(new FakeResolver("127.0.0.1"), local);

        Assert.True((await guard.CheckAsync("http://localhost:5080/api/v1/dev/webhook-sink", default)).IsSuccess);
        Assert.True((await guard.CheckAsync("https://169.254.169.254/", default)).IsFailed);
    }

    [Theory]
    [InlineData("Production", true, false)]
    [InlineData("Staging", true, false)]
    [InlineData("Development", true, true)]
    [InlineData("Production", false, true)]
    public void AllowInsecureLocal_is_refused_at_startup_outside_Development(string environment, bool flag, bool valid)
    {
        var validator = new WebhookOptionsValidator(new FakeEnvironment(environment));

        Assert.Equal(valid, validator.Validate(null, new WebhookOptions { AllowInsecureLocal = flag }).Succeeded);
    }

    private sealed class FakeResolver(params string[] answers) : IHostResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Task.FromResult(answers.Select(IPAddress.Parse).ToArray());
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = "/";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
