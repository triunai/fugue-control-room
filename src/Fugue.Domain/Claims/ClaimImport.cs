using System.Text.Json.Serialization;

namespace Fugue.Domain.Claims;

/// <summary>
/// Where an imported claim came from (item 11a). Each is a FILE import standing in for a live connector: a spreadsheet upload for a shared
/// workbook, an <c>.eml</c>/<c>.mbox</c> export for a Microsoft Graph or Gmail inbox, a chat export for WhatsApp Business. No external call is made.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImportSource
{
    Spreadsheet,
    Email,
    WhatsApp,
}
