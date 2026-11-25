using Fugue.Application.Abstractions;

namespace Fugue.Infrastructure.Audit;

/// <summary>The request info outside a web host (tools, tests of the persistence layer): nothing to add.</summary>
internal sealed class NullAuditRequestInfo : IAuditRequestInfo
{
    public AuditRequestInfo Current => AuditRequestInfo.None;
}
