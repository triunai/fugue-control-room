using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>ClaimTimelineEntry</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<ClaimTimelineEntryId>))]
[TypeConverter(typeof(GuidIdTypeConverter<ClaimTimelineEntryId>))]
public readonly record struct ClaimTimelineEntryId(Guid Value) : IStronglyTypedId<ClaimTimelineEntryId>, IParsable<ClaimTimelineEntryId>
{
    public static ClaimTimelineEntryId New() => new(Uuid7.NewGuid());

    public static ClaimTimelineEntryId From(Guid value) => new(value);

    public static ClaimTimelineEntryId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out ClaimTimelineEntryId id) ? id : throw new FormatException($"'{s}' is not a valid ClaimTimelineEntryId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ClaimTimelineEntryId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new ClaimTimelineEntryId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
