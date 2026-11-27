using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Fugue.Application.Abstractions;

/// <summary>
/// The audit health signal (ADR-010). Audit rows are never sampled, so a row that could not be written is a loss an operator must
/// see: <c>fugue.audit.write_failures</c> counts them, tagged with <c>table</c> (<c>audit_log</c> or <c>access_audit</c>) and a short
/// <c>reason</c> (the exception type name, or <c>suppressed</c> for a 401 row dropped by the per-IP cap). Subscribe to the
/// <see cref="MeterName"/> meter (OpenTelemetry, dotnet-counters). Tags never carry row content.
/// </summary>
public static class AuditMetrics
{
    public const string MeterName = "Fugue.Audit";
    public const string WriteFailuresName = "fugue.audit.write_failures";
    public const string AuditLogTable = "audit_log";
    public const string AccessAuditTable = "access_audit";
    public const string SuppressedReason = "suppressed";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> WriteFailures =
        Meter.CreateCounter<long>(WriteFailuresName, unit: "{row}", description: "Audit rows that could not be written (or were deliberately not written).");

    public static void RecordWriteFailure(string table, string reason) =>
        WriteFailures.Add(1, new TagList { { "table", table }, { "reason", reason } });
}
