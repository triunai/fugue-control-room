using Fugue.Api.Idempotency;
using Microsoft.Extensions.Primitives;

namespace Fugue.Tests.Idempotency;

/// <summary>Edges of the header grammar that the grammar tests leave open (found by mutation testing, docs/quality/mutation).</summary>
public sealed class IdempotencyKeyHeaderBoundaryTests
{
    [Theory]
    [InlineData("\"a\"", "a")] // one decoded character is the minimum
    [InlineData("\"~\"", "~")] // the last printable ASCII character
    [InlineData("\" \"", " ")] // the first printable ASCII character
    public void The_printable_ascii_range_and_the_one_character_minimum_are_inclusive(string header, string key)
    {
        HeaderParse parsed = IdempotencyKeyHeader.Parse(header);

        Assert.True(parsed.IsValid);
        Assert.Equal(key, parsed.Key);
    }

    [Fact]
    public void An_absent_header_is_a_valid_no_key_result()
    {
        HeaderParse absent = IdempotencyKeyHeader.Parse(StringValues.Empty);

        Assert.False(absent.IsPresent);
        Assert.True(absent.IsValid);
        Assert.Null(absent.Key);
    }

    [Fact]
    public void A_null_header_line_counts_as_blank_and_is_invalid()
    {
        HeaderParse parsed = IdempotencyKeyHeader.Parse(new StringValues([null]));

        Assert.True(parsed.IsPresent);
        Assert.False(parsed.IsValid);
    }
}
