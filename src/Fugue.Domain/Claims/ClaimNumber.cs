using System.Globalization;

namespace Fugue.Domain.Claims;

/// <summary>The claim number the claimant quotes: <c>CLM-&lt;market&gt;-&lt;yyyy&gt;-&lt;sequence&gt;</c>. The sequence is one Postgres sequence, so numbers never collide.</summary>
public static class ClaimNumber
{
    public static string Format(Market market, int year, long sequence) =>
        string.Create(CultureInfo.InvariantCulture, $"CLM-{market}-{year:D4}-{sequence:D6}");
}
