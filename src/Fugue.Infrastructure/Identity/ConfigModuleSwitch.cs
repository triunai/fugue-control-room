using Fugue.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Fugue.Infrastructure.Identity;

/// <summary>Reads <c>Modules:CODE:Enabled</c> from configuration on every call, so env or file reloads apply immediately.</summary>
internal sealed class ConfigModuleSwitch(IConfiguration configuration) : IModuleConfig
{
    public IReadOnlySet<string> DisabledCodes() =>
        configuration.GetSection("Modules").GetChildren()
            .Where(s => string.Equals(s["Enabled"], "false", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Key.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
}
