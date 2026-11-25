namespace Fugue.Api.Modules;

/// <summary>
/// Puts a feature controller in a module (ADR-009). When the module is not effectively enabled (switched off in configuration
/// or inactive in the registry) <see cref="ModuleGateMiddleware"/> answers 404 <c>module.disabled</c> before authorization.
/// Every feature controller carries exactly one (fitness function); system controllers are allowlisted.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ModuleAttribute(string code) : Attribute
{
    public string Code { get; } = code;
}
