namespace Fugue.Application.Abstractions;

/// <summary>
/// The hard module kill switch: configuration <c>Modules:CODE:Enabled</c> (default true), read live so a reload takes effect.
/// A module is effectively enabled only when this AND the <c>modules.is_active</c> row agree (ADR-009).
/// </summary>
public interface IModuleConfig
{
    /// <summary>Codes explicitly switched off in configuration, upper-case.</summary>
    IReadOnlySet<string> DisabledCodes();

    bool IsEnabledInConfig(string code) => !DisabledCodes().Contains(code.ToUpperInvariant());
}
