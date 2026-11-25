namespace Fugue.Domain.Abstractions;

/// <summary>The "now" and "who" written to audit columns.</summary>
public readonly record struct AuditStampValue(DateTimeOffset Now, string Actor);

/// <summary>
/// The single source of audit values, shared by both write paths: the EF interceptor and Dapper SQL both call
/// <see cref="Capture"/>, so a row looks the same whichever path wrote it. Actor is the authenticated user id,
/// or <see cref="SystemActor"/> when there is no HTTP user (seeder, background work).
/// </summary>
public interface IAuditStamp
{
    public const string SystemActor = "system";

    AuditStampValue Capture();
}
