using Fugue.Domain.Abstractions;
using Fugue.Domain.Audit;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Webhooks;

/// <summary>
/// One XML element of the ASP.NET Core Data Protection key ring, kept in the database so every API instance decrypts the same
/// webhook secrets (ADR-011). <c>[NoAudit]</c>: key material must never reach <c>audit_log</c>.
/// </summary>
[NoAudit]
public sealed class DataProtectionKeyRecord : IAuditable
{
    public DataProtectionKeyId Id { get; set; }

    public string FriendlyName { get; set; } = string.Empty;

    public string Xml { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}
