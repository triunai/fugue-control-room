using System.Text.Json;
using Fugue.Application.Abstractions;
using Fugue.Domain.Audit;
using Fugue.Infrastructure.Audit;
using Microsoft.Extensions.Options;

namespace Fugue.Tests.Audit;

/// <summary>Masking, partial masking, attributes and the size bound of the stored <c>changes</c> document (ADR-010).</summary>
public sealed class AuditRedactorTests
{
    private sealed class Sample
    {
        public string Name { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string PasswordHash { get; set; } = string.Empty;

        [AuditMask]
        public string Nickname { get; set; } = string.Empty;

        [NoAudit]
        public string Scratch { get; set; } = string.Empty;

        public string Custom { get; set; } = string.Empty;
    }

    private static AuditRedactor Redactor(AuditOptions? options = null) => new(Options.Create(options ?? new AuditOptions()));

    private static JsonElement Build(AuditRedactor redactor, params (string Field, object? Old, object? New)[] fields) =>
        JsonDocument.Parse(redactor.Build(typeof(Sample), fields.ToDictionary(f => f.Field, f => AuditFieldChange.Changed(f.Old, f.New)))!).RootElement;

    [Fact]
    public void Built_in_secrets_are_masked_with_no_configuration_and_the_field_still_shows_as_changed()
    {
        JsonElement json = Build(Redactor(), ("PasswordHash", "AQAAAAEAACcQold", "AQAAAAEAACcQnew"));

        Assert.Equal("***", json.GetProperty("passwordHash").GetProperty("old").GetString());
        Assert.Equal("***", json.GetProperty("passwordHash").GetProperty("new").GetString());
    }

    [Fact]
    public void Configured_names_the_mask_attribute_and_the_no_audit_attribute_are_honoured()
    {
        AuditRedactor redactor = Redactor(new AuditOptions { MaskedProperties = ["custom"] });

        JsonElement json = Build(redactor, ("Custom", "a", "b"), ("Nickname", "n1", "n2"), ("Scratch", "s1", "s2"), ("Name", "x", "y"));

        Assert.Equal("***", json.GetProperty("custom").GetProperty("new").GetString());
        Assert.Equal("***", json.GetProperty("nickname").GetProperty("new").GetString());
        Assert.False(json.TryGetProperty("scratch", out _));
        Assert.Equal("y", json.GetProperty("name").GetProperty("new").GetString());
    }

    [Fact]
    public void Email_and_phone_are_partially_masked_by_default_and_the_option_turns_it_off()
    {
        JsonElement masked = Build(Redactor(), ("Email", "john@example.test", "jane@example.test"), ("Phone", "+14155550123", null));
        JsonElement raw = Build(Redactor(new AuditOptions { PartialMaskPersonalData = false }), ("Email", "john@example.test", "jane@example.test"));

        Assert.Equal("j***@example.test", masked.GetProperty("email").GetProperty("old").GetString());
        Assert.Equal("***23", masked.GetProperty("phone").GetProperty("old").GetString());
        Assert.Equal(JsonValueKind.Null, masked.GetProperty("phone").GetProperty("new").ValueKind);
        Assert.Equal("john@example.test", raw.GetProperty("email").GetProperty("old").GetString());
    }

    [Fact]
    public void A_document_over_the_byte_bound_is_truncated_with_a_marker_and_stays_valid_json()
    {
        AuditRedactor redactor = Redactor(new AuditOptions { MaxChangesBytes = 300 });
        (string, object?, object?)[] fields = Enumerable.Range(0, 12).Select(i => ($"Field{i}", (object?)new string('a', 30), (object?)new string('b', 30))).ToArray();

        string stored = redactor.Build(typeof(Sample), fields.ToDictionary(f => f.Item1, f => AuditFieldChange.Changed(f.Item2, f.Item3)))!;

        using JsonDocument json = JsonDocument.Parse(stored);
        Assert.True(json.RootElement.GetProperty("_truncated").GetBoolean());
        Assert.True(json.RootElement.GetProperty("_omittedFields").GetInt32() > 0);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(stored) <= 300, stored.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(json.RootElement.TryGetProperty("field0", out _)); // the leading fields survive
    }

    [Fact]
    public void A_document_within_the_bound_has_no_marker_and_a_huge_string_is_cut_and_flagged()
    {
        JsonElement small = Build(Redactor(), ("Name", "a", "b"));
        JsonElement huge = Build(Redactor(), ("Name", "a", new string('z', 5000)));

        Assert.False(small.TryGetProperty("_truncated", out _));
        Assert.Equal(1024, huge.GetProperty("name").GetProperty("new").GetString()!.Length);
        Assert.True(huge.GetProperty("_truncated").GetBoolean());
    }

    [Fact]
    public void A_change_set_with_nothing_to_store_produces_no_document()
    {
        Assert.Null(Redactor().Build(typeof(Sample), new Dictionary<string, AuditFieldChange> { ["Scratch"] = AuditFieldChange.Added("x") }));
    }
}
