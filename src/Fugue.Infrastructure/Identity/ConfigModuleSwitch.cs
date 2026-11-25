using Fugue.Application.Abstractions;
using Fugue.Domain.Identity;
using Microsoft.Extensions.Configuration;

namespace Fugue.Infrastructure.Identity;

/// <summary>Reads <c>Modules:CODE:Enabled</c> from configuration on every call, so env or file reloads apply immediately.</summary>
internal sealed class ConfigModuleSwitch(IConfiguration configuration) : IModuleConfig
{
    private static readonly string[] On = ["true", "1", "yes", "on"];

    /// <summary>
    /// Fails closed: a kill switch with a typo (<c>flase</c>, <c>off</c>, <c>0</c>) must not leave the module running, so only an absent
    /// or blank <c>Enabled</c> and an explicit true (<c>true</c>, <c>1</c>, <c>yes</c>, <c>on</c>) keep a module enabled. <c>SYSTEM</c>
    /// holds module management itself and can never be switched off here (the same rule the runtime switch has, ADR-009).
    /// </summary>
    public IReadOnlySet<string> DisabledCodes() =>
        configuration.GetSection("Modules").GetChildren()
            .Where(s => !string.IsNullOrWhiteSpace(s["Enabled"]) && !On.Contains(s["Enabled"]!.Trim(), StringComparer.OrdinalIgnoreCase))
            .Select(s => s.Key.ToUpperInvariant())
            .Where(code => code != ModuleCodes.System)
            .ToHashSet(StringComparer.Ordinal);
}
