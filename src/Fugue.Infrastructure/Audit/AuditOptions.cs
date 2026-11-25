namespace Fugue.Infrastructure.Audit;

/// <summary>
/// <c>Audit:</c> configuration (ADR-010). Audit rows are NEVER sampled and cannot be switched off outside Development
/// (startup validation in the API host). Masking is applied at write time, so stored rows and the read API cannot un-mask.
/// </summary>
public sealed class AuditOptions
{
    public const string Section = "Audit";

    /// <summary>Property names that are always masked, whatever the configuration says.</summary>
    public static readonly IReadOnlyList<string> BuiltInMasked = ["PasswordHash", "TokenHash", "SigningKey"];

    /// <summary>Property names that are partially masked while <see cref="PartialMaskPersonalData"/> is on.</summary>
    public static readonly IReadOnlyList<string> BuiltInPartiallyMasked = ["Email", "Phone", "ContactNumber"];

    /// <summary>Entities that are never audited here: the audit tables themselves and refresh tokens (<c>access_audit</c> covers sessions).</summary>
    public static readonly IReadOnlyList<string> BuiltInExcludedEntities = ["AuditLog", "GrantAudit", "AccessAudit", "RefreshToken"];

    /// <summary>False only in Development, for debugging; any other environment refuses to start with it off.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Extra property names whose values become <c>***</c> (case-insensitive), on top of <see cref="BuiltInMasked"/>.</summary>
    public string[] MaskedProperties { get; set; } = [];

    /// <summary>Extra property names that are partially masked (<c>j***@example.test</c>, <c>***42</c>), on top of the built-in list.</summary>
    public string[] PartialMaskedProperties { get; set; } = [];

    /// <summary>Turns the built-in partial masking of email and phone fields on or off. Explicit names in <see cref="PartialMaskedProperties"/> always apply.</summary>
    public bool PartialMaskPersonalData { get; set; } = true;

    /// <summary>Entity type names (CLR names, for example <c>Customer</c>) that are not audited, on top of <see cref="BuiltInExcludedEntities"/>.</summary>
    public string[] ExcludedEntities { get; set; } = [];

    /// <summary>Upper bound for the serialized <c>changes</c> document; past it fields are dropped and <c>"_truncated": true</c> is added.</summary>
    public int MaxChangesBytes { get; set; } = 16_384;

    /// <summary>False stores no client IP on audit rows (the user agent is still stored).</summary>
    public bool CaptureClientIp { get; set; } = true;
}
