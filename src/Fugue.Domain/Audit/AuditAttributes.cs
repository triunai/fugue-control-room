namespace Fugue.Domain.Audit;

/// <summary>
/// On a property: its value is replaced by <c>***</c> in every audit row (the field still shows as changed). Use for secrets and
/// hashes. The configurable list in <c>Audit:MaskedProperties</c> does the same by name.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditMaskAttribute : Attribute;

/// <summary>
/// On a property: the field is left out of audit rows entirely. On a class: changes to that entity are not audited at all
/// (use only where another record already covers it, such as refresh tokens in <c>access_audit</c>). On a command: the command
/// is not wrapped by the failure-audit behavior (authentication commands, which belong to <c>access_audit</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public sealed class NoAuditAttribute : Attribute;
