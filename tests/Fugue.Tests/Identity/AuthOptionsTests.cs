using System.Security.Cryptography;
using Fugue.Api.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace Fugue.Tests.Identity;

/// <summary>Signing-key hygiene (Codex finding 10): base64 of at least 32 random bytes; placeholders and weak keys stop the host.</summary>
public sealed class AuthOptionsTests
{
    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes);

    public static TheoryData<string?, string> BadKeys() => new()
    {
        { null, "not set" },
        { "", "not set" },
        { "not base64 !!", "base64" },
        { B64(RandomNumberGenerator.GetBytes(16)), "16 bytes" },
        { B64(RandomNumberGenerator.GetBytes(31)), "31 bytes" },
        { B64(System.Text.Encoding.ASCII.GetBytes("REPLACE_ME_with_a_real_random_key_please")), "placeholder" },
        { B64(System.Text.Encoding.ASCII.GetBytes("changeme-changeme-changeme-changeme")), "placeholder" },
        { B64(new byte[32]), "entropy" },
        { B64(Enumerable.Repeat((byte)7, 40).ToArray()), "entropy" },
    };

    [Theory]
    [MemberData(nameof(BadKeys))]
    public void A_bad_signing_key_fails_validation(string? key, string _)
    {
        ValidateOptionsResult result = new AuthOptionsValidator().Validate(null, new AuthOptions { SigningKey = key! });

        Assert.True(result.Failed);
    }

    [Fact]
    public void Thirty_two_random_bytes_pass()
    {
        Assert.True(new AuthOptionsValidator().Validate(null, new AuthOptions { SigningKey = B64(RandomNumberGenerator.GetBytes(32)) }).Succeeded);
    }

    [Fact]
    public void The_host_refuses_to_start_without_a_valid_key()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Auth:SigningKey", "short"));

        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}
