using System.Reflection;
using Fugue.Api.Controllers;
using Fugue.Domain.Abstractions;
using Fugue.Infrastructure;
using NetArchTest.Rules;

namespace Fugue.ArchTests;

public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(ICurrentUser).Assembly;
    private static readonly Assembly Application = typeof(Fugue.Application.AssemblyMarker).Assembly;
    private static readonly Assembly Api = typeof(ApiControllerBase).Assembly;

    private static IEnumerable<Type> Controllers() =>
        Types.InAssembly(Api)
            .That().ResideInNamespace("Fugue.Api.Controllers")
            .GetTypes()
            .Where(t => t != typeof(ApiControllerBase) && !t.IsNested && !t.Name.Contains('<', StringComparison.Ordinal));

    [Fact]
    public void Domain_does_not_depend_on_other_layers_or_aspnet()
    {
        TestResult result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOnAny("Fugue.Application", "Fugue.Api", "Fugue.Infrastructure", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Domain_references_only_FluentResults_among_third_party_assemblies()
    {
        string[] allowedPrefixes = ["System", "Microsoft", "netstandard", "FluentResults", "Fugue.Domain"];
        string[] offenders = Domain.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => !allowedPrefixes.Any(p => n.StartsWith(p, StringComparison.Ordinal)))
            .ToArray();

        Assert.True(offenders.Length == 0, "Unexpected Domain references: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Application_does_not_depend_on_aspnet_api_or_infrastructure()
    {
        TestResult result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOnAny("Fugue.Api", "Fugue.Infrastructure", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Controllers_inherit_ApiControllerBase()
    {
        Type[] controllers = Controllers().ToArray();

        Assert.All(controllers, t => Assert.True(t.IsSubclassOf(typeof(ApiControllerBase)), t.FullName));
    }

    [Fact]
    public void Controllers_have_no_public_constructor_parameters()
    {
        Type[] controllers = Controllers().ToArray();

        Assert.All(controllers, t =>
            Assert.All(
                t.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
                c => Assert.Empty(c.GetParameters())));
    }

    [Fact]
    public void Controllers_do_not_reference_Infrastructure()
    {
        TestResult result = Types.InAssembly(Api)
            .That().ResideInNamespace("Fugue.Api.Controllers")
            .ShouldNot().HaveDependencyOn(typeof(AssemblyMarker).Namespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
