using Fugue.Infrastructure.Audit;
using Microsoft.Extensions.Options;

namespace Fugue.Api.Auditing;

/// <summary>
/// Audit is not optional outside Development: with <c>Audit:Enabled=false</c> in any other environment the host refuses to start
/// (a misconfiguration must not silently stop recording). The size bound must leave room for at least one field.
/// </summary>
public sealed class AuditOptionsValidator(IHostEnvironment environment) : IValidateOptions<AuditOptions>
{
    public const int MinChangesBytes = 256;

    public ValidateOptionsResult Validate(string? name, AuditOptions options)
    {
        var failures = new List<string>();
        if (!options.Enabled && !environment.IsDevelopment())
        {
            failures.Add("Audit:Enabled=false is only allowed in the Development environment.");
        }

        if (options.MaxChangesBytes < MinChangesBytes)
        {
            failures.Add($"Audit:MaxChangesBytes must be at least {MinChangesBytes}.");
        }

        if (options.UnauthenticatedRowsPerIpPerMinute < 0)
        {
            failures.Add("Audit:UnauthenticatedRowsPerIpPerMinute must be 0 or more (0 records none).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
