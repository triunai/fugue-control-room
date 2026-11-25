using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Writes a strongly-typed id as a plain JSON string (the guid) and reads it back.</summary>
public sealed class GuidIdJsonConverter<TId> : JsonConverter<TId>
    where TId : struct, IStronglyTypedId<TId>
{
    public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && Guid.TryParse(reader.GetString(), out Guid value) && value != Guid.Empty)
        {
            return TId.From(value);
        }

        throw new JsonException($"Expected a non-empty guid string for {typeof(TId).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);

    public override TId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Read(ref reader, typeToConvert, options);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TId value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value.ToString());
}
