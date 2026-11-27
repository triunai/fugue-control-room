using Fugue.Application.Features.Import.Repos;
using Fugue.Domain.Claims;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Import;

/// <summary>
/// <c>Import:</c> configuration (item 11a). <see cref="MarketByDomain"/> routes an inbox export's sender domain to a market: the longest matching suffix
/// wins (<c>broker.com.sg</c> before <c>sg</c>). Values are market codes; a wrong one stops the host at start, like the other options.
/// </summary>
public sealed class ImportOptions
{
    public const string Section = "Import";

    public Dictionary<string, string> MarketByDomain { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class ImportOptionsValidator : IValidateOptions<ImportOptions>
{
    public ValidateOptionsResult Validate(string? name, ImportOptions options)
    {
        string[] bad = [.. options.MarketByDomain
            .Where(e => string.IsNullOrWhiteSpace(e.Key) || !Enum.TryParse(e.Value, false, out Market m) || !Enum.IsDefined(m) || e.Value.Length != 2)
            .Select(e => $"{e.Key}={e.Value}")];
        return bad.Length == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Import:MarketByDomain needs a domain and a market code (SG, MY, TH, HK, ID, PH): {string.Join(", ", bad)}.");
    }
}

internal sealed class ConfigImportSettings(IOptions<ImportOptions> options) : IImportSettings
{
    public IReadOnlyDictionary<string, string> MarketByDomain => options.Value.MarketByDomain;
}
