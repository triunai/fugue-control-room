using System.Text.Json.Serialization;

namespace Fugue.Domain.Customers;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomerStatus
{
    Active,
    Suspended,
}
