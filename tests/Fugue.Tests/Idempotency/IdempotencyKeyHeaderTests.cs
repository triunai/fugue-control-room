using Fugue.Api.Idempotency;
using Microsoft.Extensions.Primitives;

namespace Fugue.Tests.Idempotency;

/// <summary>The <c>Idempotency-Key</c> header grammar (ADR-013): one Structured Field String or a bare token, nothing else.</summary>
public sealed class IdempotencyKeyHeaderTests
{
    [Theory]
    [InlineData("\"abc\"", "abc")]
    [InlineData("  \"abc\"  ", "abc")]
    [InlineData("\"with space and ,comma;semi\"", "with space and ,comma;semi")]
    [InlineData("\"quote\\\"inside\"", "quote\"inside")]
    [InlineData("\"back\\\\slash\"", "back\\slash")]
    [InlineData("abc-123_X.y:z", "abc-123_X.y:z")]
    public void A_single_string_or_a_bare_token_is_a_key(string header, string key)
    {
        HeaderParse parsed = IdempotencyKeyHeader.Parse(header);

        Assert.True(parsed.IsPresent);
        Assert.True(parsed.IsValid);
        Assert.Equal(key, parsed.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_header_is_no_key(string header)
    {
        HeaderParse parsed = IdempotencyKeyHeader.Parse(header);

        Assert.False(parsed.IsPresent);
        Assert.True(parsed.IsValid);
    }

    [Fact]
    public void A_missing_header_is_no_key() => Assert.False(IdempotencyKeyHeader.Parse(StringValues.Empty).IsPresent);

    [Theory]
    [InlineData("\"\"")] // empty decoded value
    [InlineData("\"abc")] // no closing quote
    [InlineData("\"a\" , \"b\"")] // a list
    [InlineData("\"a\",\"b\"")]
    [InlineData("a,b")]
    [InlineData("\"abc\";x=1")] // a parameter
    [InlineData("\"bad\\escape\"")] // only \" and \\ are escapes
    [InlineData("\"trailing\\")]
    [InlineData("\"tab\there\"")] // control character
    [InlineData("\"del\u007f\"")]
    [InlineData("\"café\"")] // not ASCII
    [InlineData("has space")] // a bare token has no spaces
    [InlineData("a/b")]
    public void Anything_else_is_invalid(string header)
    {
        HeaderParse parsed = IdempotencyKeyHeader.Parse(header);

        Assert.True(parsed.IsPresent);
        Assert.False(parsed.IsValid);
    }

    [Fact]
    public void Two_header_lines_are_invalid() =>
        Assert.False(IdempotencyKeyHeader.Parse(new StringValues(["\"a\"", "\"b\""])).IsValid);

    [Fact]
    public void The_length_limit_is_255_decoded_characters()
    {
        Assert.True(IdempotencyKeyHeader.Parse($"\"{new string('k', 255)}\"").IsValid);
        Assert.False(IdempotencyKeyHeader.Parse($"\"{new string('k', 256)}\"").IsValid);
        Assert.True(IdempotencyKeyHeader.Parse(new string('k', 255)).IsValid);
        Assert.False(IdempotencyKeyHeader.Parse(new string('k', 256)).IsValid);
        Assert.Equal(255, IdempotencyKeyHeader.Parse($"\"{string.Concat(Enumerable.Repeat("\\\"", 255))}\"").Key!.Length); // escapes count once
    }
}
