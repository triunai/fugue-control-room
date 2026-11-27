using System.Globalization;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Claims;

/// <summary>Where a claim is in its life. Rejected and Settled are terminal.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClaimStatus
{
    Submitted,
    UnderReview,
    InfoRequested,
    Approved,
    Rejected,
    Settled,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LineOfBusiness
{
    Motor,
    Property,
}

/// <summary>The claim number the claimant quotes: <c>CLM-&lt;market&gt;-&lt;yyyy&gt;-&lt;sequence&gt;</c>. The sequence is one Postgres sequence, so numbers never collide.</summary>
public static class ClaimNumber
{
    public static string Format(Market market, int year, long sequence) =>
        string.Create(CultureInfo.InvariantCulture, $"CLM-{market}-{year:D4}-{sequence:D6}");
}

/// <summary>The six APAC markets. The code is the ISO 3166 alpha-2 country; a market has exactly one currency (<see cref="Markets.CurrencyOf"/>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Market
{
    SG,
    MY,
    TH,
    HK,
    ID,
    PH,
}

/// <summary>Per-market facts. Money is bigint minor units plus the market's currency; amounts of different currencies are never added together.</summary>
public static class Markets
{
    /// <summary>ISO 4217 currency of the market.</summary>
    public static string CurrencyOf(Market market) => market switch
    {
        Market.SG => "SGD",
        Market.MY => "MYR",
        Market.TH => "THB",
        Market.HK => "HKD",
        Market.ID => "IDR",
        Market.PH => "PHP",
        _ => throw new ArgumentOutOfRangeException(nameof(market)),
    };

    /// <summary>The market's fixed UTC offset in hours (none of the six observes daylight saving). "Today" for an incident date is the market's today.</summary>
    public static int UtcOffsetHours(Market market) => market == Market.TH || market == Market.ID ? 7 : 8;

    public static DateOnly LocalToday(Market market, DateTimeOffset now) =>
        DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(UtcOffsetHours(market))).DateTime);
}
