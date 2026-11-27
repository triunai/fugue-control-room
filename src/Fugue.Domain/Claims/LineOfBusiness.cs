using System.Text.Json.Serialization;

namespace Fugue.Domain.Claims;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LineOfBusiness
{
    Motor,
    Property,
}
