using System.Text;
using System.Text.Json;
using Fugue.Application.Abstractions;
using Fugue.Domain.Audit;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Audit;
using Microsoft.Extensions.Options;

namespace Fugue.Tests.Audit;

/// <summary>
/// The edges of <see cref="AuditRedactor"/>: exact byte budgets, the 1024-character cap, the partial-mask length rule, configured lists and the
/// outbox-payload entry point. Found by mutation testing (docs/quality/mutation), where the happy paths in <see cref="AuditRedactorTests"/> left
/// every boundary unpinned.
/// </summary>
public sealed class AuditRedactorBoundaryTests
{
    [NoAudit]
    private sealed class Skipped;

    private sealed class Plain
    {
        public string Name { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Custom { get; set; } = string.Empty;

        [NoAudit]
        public string Scratch { get; set; } = string.Empty;
    }

    private static AuditRedactor Redactor(AuditOptions? options = null) => new(Options.Create(options ?? new AuditOptions()));

    private static string Build(AuditRedactor redactor, params (string Field, string Old, string New)[] fields) =>
        redactor.Build(typeof(Plain), fields.ToDictionary(f => f.Field, f => AuditFieldChange.Changed(f.Old, f.New)))!;

    [Fact]
    public void The_stamp_and_token_columns_are_noise_whatever_their_casing()
    {
        Assert.Equal(5, AuditRedactor.NoiseFields.Count);
        foreach (string name in new[] { "createdat", "CREATEDBY", "UpdatedAt", "updatedby", "rowversion" })
        {
            Assert.Contains(name, AuditRedactor.NoiseFields);
        }
    }

    [Fact]
    public void Entities_are_excluded_by_built_in_name_configured_name_or_attribute_and_not_otherwise()
    {
        AuditRedactor redactor = Redactor(new AuditOptions { ExcludedEntities = ["Plain"] });

        Assert.True(redactor.IsExcluded(typeof(Plain))); // configured
        Assert.True(redactor.IsExcluded(typeof(Skipped))); // [NoAudit] on the type
        Assert.False(Redactor().IsExcluded(typeof(Plain)));
        Assert.True(Redactor().IsExcluded(typeof(GrantAudit))); // built in, by CLR name
    }

    [Fact]
    public void Configured_partial_mask_names_are_added_to_the_built_in_ones()
    {
        AuditRedactor redactor = Redactor(new AuditOptions { PartialMaskedProperties = ["Custom"] });

        using JsonDocument json = JsonDocument.Parse(Build(redactor, ("Custom", "abcdefgh", "ijklmnop")));

        Assert.Equal("***gh", json.RootElement.GetProperty("custom").GetProperty("old").GetString());
    }

    [Theory]
    [InlineData("abcde", "***")] // five characters: too short to show the tail
    [InlineData("abcdef", "***ef")] // six: shows the last two
    [InlineData("@x.test", "***st")] // an @ in position 0 is not an email
    [InlineData("a@x.test", "a***@x.test")]
    public void Partial_masking_follows_the_length_and_at_sign_rules(string input, string expected) =>
        Assert.Equal(expected, AuditRedactor.PartialMask(input));

    [Fact]
    public void A_masked_field_that_is_null_stays_null()
    {
        using JsonDocument json = JsonDocument.Parse(
            Redactor().Build(typeof(Plain), new Dictionary<string, AuditFieldChange> { ["PasswordHash"] = AuditFieldChange.Changed(null, "secret") })!);

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("passwordHash").GetProperty("old").ValueKind);
        Assert.Equal("***", json.RootElement.GetProperty("passwordHash").GetProperty("new").GetString());
    }

    [Fact]
    public void A_string_of_exactly_1024_characters_is_kept_whole_and_one_more_is_cut()
    {
        using JsonDocument exact = JsonDocument.Parse(Build(Redactor(), ("Name", "a", new string('z', 1024))));
        using JsonDocument over = JsonDocument.Parse(Build(Redactor(), ("Name", "a", new string('z', 1025))));

        Assert.Equal(1024, exact.RootElement.GetProperty("name").GetProperty("new").GetString()!.Length);
        Assert.False(exact.RootElement.TryGetProperty("_truncated", out _));
        Assert.Equal(1024, over.RootElement.GetProperty("name").GetProperty("new").GetString()!.Length);
        Assert.True(over.RootElement.GetProperty("_truncated").GetBoolean());
    }

    [Fact]
    public void A_document_exactly_at_the_budget_fits_without_a_marker()
    {
        // Budget is max(MaxChangesBytes, 96) = 96. Cost per field = key + value json + 5; value json = 19 + len(old) + len(new). Costs 31, 31, 32; with the 2
        // braces the document needs exactly 96.
        string stored = Build(Redactor(new AuditOptions { MaxChangesBytes = 96 }), ("a", "aaa", "bbb"), ("b", "aaa", "bbb"), ("c", "aaaa", "bbb"));

        using JsonDocument json = JsonDocument.Parse(stored);
        Assert.False(json.RootElement.TryGetProperty("_truncated", out _));
        Assert.True(json.RootElement.TryGetProperty("c", out _));
    }

    [Fact]
    public void A_field_that_leaves_exactly_the_marker_reserve_is_kept()
    {
        // First field: 2 + cost 46 + 48 reserve = 96 = the budget, so it stays; the large second field does not fit and is counted as omitted.
        string stored = Build(
            Redactor(new AuditOptions { MaxChangesBytes = 96 }),
            ("a", new string('x', 10), new string('y', 11)),
            ("b", new string('x', 20), new string('y', 20)));

        using JsonDocument json = JsonDocument.Parse(stored);
        Assert.True(json.RootElement.TryGetProperty("a", out _));
        Assert.False(json.RootElement.TryGetProperty("b", out _));
        Assert.Equal(1, json.RootElement.GetProperty("_omittedFields").GetInt32());
        Assert.True(Encoding.UTF8.GetByteCount(stored) <= 96);
    }

    [Fact]
    public void A_tiny_configured_bound_is_raised_to_the_marker_floor_and_the_leading_field_survives()
    {
        string stored = Build(Redactor(new AuditOptions { MaxChangesBytes = 1 }), ("a", "x", "y"), ("b", "x", "y"), ("c", "x", "y"), ("d", "x", "y"));

        using JsonDocument json = JsonDocument.Parse(stored);
        Assert.True(json.RootElement.TryGetProperty("a", out _));
        Assert.True(json.RootElement.GetProperty("_truncated").GetBoolean());
        Assert.True(Encoding.UTF8.GetByteCount(stored) <= 96);
    }

    [Fact]
    public void When_not_even_one_field_fits_the_document_is_only_the_marker_and_still_valid_json()
    {
        string stored = Build(Redactor(new AuditOptions { MaxChangesBytes = 1 }), ("a", new string('x', 60), new string('y', 60)));

        using JsonDocument json = JsonDocument.Parse(stored);
        Assert.Equal("{\"_truncated\":true,\"_omittedFields\":1}", stored);
        Assert.True(json.RootElement.GetProperty("_truncated").GetBoolean());
    }

    [Fact]
    public void An_empty_field_name_does_not_throw_and_is_written_as_an_empty_key()
    {
        string stored = Build(Redactor(), ("", "a", "b"));

        Assert.StartsWith("{\"\":", stored, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRedact_leaves_out_no_audit_properties_and_masks_the_rest_like_an_audit_value()
    {
        AuditRedactor redactor = Redactor();

        Assert.False(redactor.TryRedact(typeof(Plain), "Scratch", "x", out object? scratch));
        Assert.Null(scratch);

        Assert.True(redactor.TryRedact(typeof(Plain), "passwordhash", "secret", out object? secret)); // name lookup ignores case
        Assert.Equal("***", secret);

        Assert.True(redactor.TryRedact(typeof(Plain), "Name", "Ada", out object? name));
        Assert.Equal("Ada", name);

        Assert.True(redactor.TryRedact(typeof(Plain), "Unknown", 42, out object? number)); // a name the type does not have is kept as is
        Assert.Equal(42, number);

        Assert.True(redactor.TryRedact(typeof(Plain), "Name", new string('q', 2000), out object? capped));
        Assert.Equal(1024, ((string)capped!).Length);
    }
}
