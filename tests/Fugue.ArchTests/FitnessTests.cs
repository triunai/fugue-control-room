// Fitness functions for the critical modules: architecture catches pinned as tests (AGENTS.md "Pin every catch", D-012).
// Rules enforced: ADR-002 (controllers over ApiControllerBase), ADR-006 (EF reads, Dapper writes through SqlExecutor),
// ADR-004 (handlers return FluentResults), D-012 (migration catalog same-commit rule, CLAUDE.md).
// Ratchet: each rule reads Baselines/<rule>.txt. A NEW violation fails; a baseline entry that no longer violates ALSO
// fails, so the allowlist can only shrink. Naming: Fitness_<module>_<rule>.
using System.Reflection;
using System.Text.Json.Serialization;
using FluentValidation;
using Fugue.Api.Auth;
using Fugue.Api.Controllers;
using Fugue.Api.Modules;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;

namespace Fugue.ArchTests;

public sealed class FitnessTests
{
    private static readonly Assembly Domain = typeof(ICurrentUser).Assembly;
    private static readonly Assembly Application = typeof(Fugue.Application.AssemblyMarker).Assembly;
    private static readonly Assembly Api = typeof(ApiControllerBase).Assembly;
    private static readonly Assembly Infrastructure = typeof(FugueDbContext).Assembly;

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Fugue.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Fugue.sln not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Shrink-only ratchet: violations must equal the baseline exactly.</summary>
    private static void AssertRatchet(string rule, IEnumerable<string> violations)
    {
        string file = Path.Combine(RepoRoot(), "tests", "Fugue.ArchTests", "Baselines", rule + ".txt");
        HashSet<string> baseline = File.ReadAllLines(file)
            .Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToHashSet(StringComparer.Ordinal);
        HashSet<string> actual = violations.ToHashSet(StringComparer.Ordinal);

        string[] added = actual.Except(baseline).Order().ToArray();
        string[] stale = baseline.Except(actual).Order().ToArray();
        Assert.True(added.Length == 0, $"[{rule}] new violations: " + string.Join(", ", added));
        Assert.True(stale.Length == 0, $"[{rule}] baseline entries no longer needed, delete them from {rule}.txt: " + string.Join(", ", stale));
    }

    private static IEnumerable<Type> Controllers() =>
        Api.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(ApiControllerBase)));

    [Fact]
    public void Fitness_Controllers_do_not_reference_data_access()
    {
        TestResult result = Types.InAssembly(Api)
            .That().ResideInNamespace("Fugue.Api.Controllers")
            .ShouldNot().HaveDependencyOnAny("Fugue.Infrastructure", "Microsoft.EntityFrameworkCore", "Dapper", "Npgsql")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Fitness_Controllers_every_public_action_returns_an_ActionResult()
    {
        // Send* returns ActionResult<T> (keeps OpenAPI schemas) or IActionResult (SendNoContent); a raw DTO bypasses ApiControllerBase.
        static bool IsActionResult(Type t)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>)) t = t.GetGenericArguments()[0];
            return t == typeof(ActionResult) || t == typeof(IActionResult) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ActionResult<>));
        }

        IEnumerable<string> bad = Controllers().SelectMany(c => c
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !IsActionResult(m.ReturnType))
            .Select(m => $"{c.Name}.{m.Name}"));

        AssertRatchet("controller-actions", bad);
    }

    [Fact]
    public void Fitness_Application_does_not_reference_EF_Npgsql_Dapper_or_AspNetCore()
    {
        TestResult result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql", "Dapper", "Microsoft.AspNetCore", "Microsoft.IdentityModel", "System.IdentityModel")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Fitness_Application_does_not_reference_HttpClient_or_Data_Protection()
    {
        // Transport and key management belong to Infrastructure (ADR-011); Application sees ports (IEventPublisher, ISecretProtector, IWebhookUrlGuard).
        TestResult result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOnAny("System.Net.Http", "Microsoft.AspNetCore.DataProtection", "Microsoft.Extensions.Http", "Polly")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Fitness_Events_every_domain_event_has_a_versioned_unique_name_constant()
    {
        // The event name is a public contract (ADR-011): <aggregate>.<verb>.v<N>, declared once as `public const string Type`.
        var form = new System.Text.RegularExpressions.Regex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+\.v[0-9]+$");
        Type[] events = Domain.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(Fugue.Domain.Events.IDomainEvent).IsAssignableFrom(t))
            .ToArray();
        Assert.NotEmpty(events);

        string?[] names = events.Select(t => t.GetField("Type", BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true } f && f.FieldType == typeof(string) ? (string?)f.GetRawConstantValue() : null).ToArray();
        IEnumerable<string> badForm = events.Zip(names).Where(x => x.Second is null || !form.IsMatch(x.Second)).Select(x => x.First.Name);
        IEnumerable<string> duplicates = events.Zip(names).Where(x => x.Second is not null).GroupBy(x => x.Second).Where(g => g.Count() > 1).SelectMany(g => g.Select(x => x.First.Name));

        AssertRatchet("domain-events", badForm.Concat(duplicates));
    }

    [Fact]
    public void Fitness_Application_every_command_has_exactly_one_validator()
    {
        Type[] commands = Application.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)))
            .ToArray();
        Type[] validators = Application.GetTypes()
            .Where(t => t is { IsAbstract: false } && t.BaseType is { IsGenericType: true } b && b.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            .ToArray();

        Assert.NotEmpty(commands);
        IEnumerable<string> bad = commands
            .Where(c => validators.Count(v => v.BaseType!.GetGenericArguments()[0] == c) != 1)
            .Select(c => c.Name);

        AssertRatchet("command-validators", bad);
    }

    [Fact]
    public void Fitness_Persistence_Dapper_is_used_only_inside_Infrastructure_Persistence()
    {
        IEnumerable<string> bad = new[] { Domain, Application, Api, Infrastructure }
            .SelectMany(a => Types.InAssembly(a).That().HaveDependencyOn("Dapper").GetTypes())
            .Where(t => t.Assembly != Infrastructure || t.Namespace?.StartsWith("Fugue.Infrastructure.Persistence", StringComparison.Ordinal) != true)
            .Select(t => t.FullName ?? t.Name);

        AssertRatchet("dapper-placement", bad);
    }

    private static IEnumerable<MethodInfo> Actions() =>
        Controllers().SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName));

    [Fact]
    public void Fitness_Auth_every_controller_action_declares_HasPermission_or_AllowAnonymous()
    {
        IEnumerable<string> bad = Actions()
            .Where(m => !m.GetCustomAttributes<HasPermissionAttribute>().Any() && m.GetCustomAttribute<AllowAnonymousAttribute>() is null)
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}");

        AssertRatchet("authorized-actions", bad);
    }

    [Fact]
    public void Fitness_Auth_the_anonymous_endpoint_set_is_exactly_token_refresh_logout_ping_and_the_development_only_sink()
    {
        string[] anonymous = Actions().Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}").Order().ToArray();

        Assert.Equal(["AuthController.Logout", "AuthController.Refresh", "AuthController.Token", "DevWebhookSinkController.Receive", "DiagnosticsController.Ping"], anonymous);

        // The one anonymous endpoint that is not auth plumbing must be gated to Development (ADR-011): its credential is the request signature.
        Assert.NotNull(typeof(Fugue.Api.Controllers.DevWebhookSinkController).GetCustomAttribute<DevelopmentOnlyAttribute>());
    }

    [Fact]
    public void Fitness_Auth_every_HasPermission_literal_exists_in_the_seeded_catalog()
    {
        HashSet<string> catalog = Fugue.Domain.Identity.Permissions.All.ToHashSet(StringComparer.Ordinal);
        IEnumerable<string> bad = Actions().SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>().Select(a => (m, a)))
            .Where(x => !catalog.Contains(x.a.Permission)).Select(x => $"{x.m.DeclaringType!.Name}.{x.m.Name}:{x.a.Permission}");

        AssertRatchet("permission-literals", bad);
    }

    [Fact]
    public void Fitness_Modules_every_feature_controller_carries_exactly_one_Module_attribute()
    {
        // System controllers (token issuance, diagnostics, module management itself) are the allowlist baseline.
        IEnumerable<string> bad = Controllers()
            .Where(c => c.GetCustomAttributes<ModuleAttribute>(inherit: false).Count() != 1)
            .Select(c => c.Name);

        AssertRatchet("module-attribute", bad);
    }

    [Fact]
    public void Fitness_Modules_every_Module_attribute_names_a_seeded_module()
    {
        string[] seeded = [Fugue.Domain.Identity.ModuleCodes.System, Fugue.Domain.Identity.ModuleCodes.Customers, Fugue.Domain.Identity.ModuleCodes.Identity, Fugue.Domain.Identity.ModuleCodes.Audit, Fugue.Domain.Identity.ModuleCodes.Webhooks];
        IEnumerable<string> bad = Controllers()
            .SelectMany(c => c.GetCustomAttributes<ModuleAttribute>(inherit: false).Select(a => (c, a)))
            .Where(x => !seeded.Contains(x.a.Code)).Select(x => $"{x.c.Name}:{x.a.Code}");

        AssertRatchet("module-codes", bad);
    }

    [Fact]
    public void Fitness_Auth_password_hashing_lives_only_in_Infrastructure_Identity_and_never_by_hand()
    {
        IEnumerable<string> outside = new[] { Domain, Application, Api, Infrastructure }
            .SelectMany(a => Types.InAssembly(a).That().HaveDependencyOn("Microsoft.AspNetCore.Identity").GetTypes())
            .Where(t => t.Namespace != "Fugue.Infrastructure.Identity")
            .Select(t => t.FullName ?? t.Name);
        IEnumerable<string> byHand = new[] { Domain, Application, Api, Infrastructure }
            .SelectMany(a => Types.InAssembly(a).That().HaveDependencyOn("System.Security.Cryptography.Rfc2898DeriveBytes").GetTypes())
            .Select(t => t.FullName ?? t.Name);

        AssertRatchet("password-hashing", outside.Concat(byHand));
    }

    [Fact]
    public void Fitness_Persistence_every_IAuditable_entity_is_mapped_in_FugueDbContext()
    {
        using var context = new FugueDbContext(new DbContextOptionsBuilder<FugueDbContext>().UseNpgsql("Host=localhost;Database=fitness").Options);
        HashSet<Type> mapped = context.Model.GetEntityTypes().Select(e => e.ClrType).ToHashSet();
        IEnumerable<string> bad = Domain.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IAuditable).IsAssignableFrom(t) && !mapped.Contains(t))
            .Select(t => t.Name);

        AssertRatchet("auditable-mapping", bad);
    }

    [Fact]
    public void Fitness_Persistence_every_entity_is_IAuditable_unless_append_only()
    {
        // Base entity contract (D-026). The baseline IS the append-only allowlist; each listed entity must carry OccurredAt instead.
        using var context = new FugueDbContext(new DbContextOptionsBuilder<FugueDbContext>().UseNpgsql("Host=localhost;Database=fitness").Options);
        Type[] entities = context.Model.GetEntityTypes().Select(e => e.ClrType).ToArray();

        IEnumerable<string> bad = entities.Where(t => !typeof(IAuditable).IsAssignableFrom(t)).Select(t => t.Name);
        AssertRatchet("append-only-entities", bad);

        string[] noOccurredAt = entities
            .Where(t => !typeof(IAuditable).IsAssignableFrom(t) && t.GetProperty("OccurredAt") is null)
            .Select(t => t.Name).ToArray();
        Assert.True(noOccurredAt.Length == 0, "append-only entities need OccurredAt: " + string.Join(", ", noOccurredAt));
    }

    [Fact]
    public void Fitness_Persistence_every_typed_id_is_parsable_json_convertible_and_in_the_EF_convention()
    {
        Type[] ids = Domain.GetTypes()
            .Where(t => t is { IsValueType: true } && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>)))
            .ToArray();
        Type typedIds = Infrastructure.GetType("Fugue.Infrastructure.Persistence.Ids.TypedIds")!;
        Type[] scanned = (Type[])typedIds.GetField("All", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        Assert.NotEmpty(ids);
        IEnumerable<string> bad = ids.SelectMany(id => new[]
        {
            (Ok: id.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IParsable<>)), Why: "IParsable"),
            (Ok: id.GetCustomAttribute<JsonConverterAttribute>() is not null, Why: "JsonConverter"),
            (Ok: scanned.Contains(id), Why: "EF convention"),
        }.Where(c => !c.Ok).Select(c => $"{id.Name} lacks {c.Why}"));

        AssertRatchet("typed-ids", bad);
    }

    [Fact]
    public void Fitness_Migrations_every_migration_has_a_catalog_row()
    {
        string catalog = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "migrations", "migration-catalog.md"));
        IEnumerable<string> bad = Infrastructure.GetTypes()
            .Select(t => t.GetCustomAttribute<Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute>())
            .OfType<Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute>()
            .Select(a => a.Id)
            .Where(id => !catalog.Contains(id, StringComparison.Ordinal));

        AssertRatchet("migration-catalog", bad);
    }

    [Fact]
    public void Fitness_Audit_AuditBehavior_is_registered_between_Logging_and_Validation()
    {
        // Audit wraps validation (validation failures get a failure row) and sits inside logging. Mediator generates its pipeline from this list.
        string program = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Fugue.Api", "Program.cs"));
        System.Text.RegularExpressions.Match list = System.Text.RegularExpressions.Regex.Match(program, @"PipelineBehaviors\s*=\s*\[([^\]]*)\]");

        Assert.True(list.Success, "PipelineBehaviors list not found in Program.cs");
        string[] order = System.Text.RegularExpressions.Regex.Matches(list.Groups[1].Value, @"typeof\((\w+)<,>\)").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(["LoggingBehavior", "AuditBehavior", "ValidationBehavior"], order);
    }

    [Fact]
    public void Fitness_Audit_every_command_passes_through_AuditBehavior_unless_it_is_a_baselined_NoAudit_command()
    {
        Type[] commands = Application.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)))
            .ToArray();
        Assert.NotEmpty(commands);

        // Every command must satisfy the behavior's constraints, or Mediator could not wrap it.
        IEnumerable<string> cannotBeWrapped = commands.Where(c =>
        {
            Type response = c.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)).GetGenericArguments()[0];
            try
            {
                _ = typeof(Fugue.Application.Common.Behaviors.AuditBehavior<,>).MakeGenericType(c, response);
                return false;
            }
            catch (ArgumentException)
            {
                return true;
            }
        }).Select(c => c.Name);
        Assert.Empty(cannotBeWrapped);

        // The exemptions (authentication commands, recorded in access_audit) are an allowlist that may only shrink.
        AssertRatchet("audit-exempt-commands", commands.Where(c => c.IsDefined(typeof(Fugue.Domain.Audit.NoAuditAttribute), inherit: false)).Select(c => c.Name));
    }

    [Fact]
    public void Fitness_Audit_Application_and_Api_never_reference_the_audit_entity()
    {
        // The audit_log entity and its DbSet belong to Infrastructure; the rest of the code sees DTOs and ports (IAuditTrail, IAuditLogReader).
        IEnumerable<string> bad = new[] { Application, Api }
            .SelectMany(a => Types.InAssembly(a).That().HaveDependencyOn("Fugue.Domain.Audit.AuditLog").GetTypes())
            .Select(t => t.FullName ?? t.Name);

        AssertRatchet("audit-entity-references", bad);
    }

    [Fact]
    public void Fitness_Audit_audit_log_has_exactly_one_insert_statement_in_the_source()
    {
        string[] hits = Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("INSERT INTO audit_log", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f)).ToArray();

        Assert.Equal(["AuditTrail.cs"], hits);
    }

    [Fact]
    public void Fitness_Audit_every_raw_SQL_write_goes_through_the_audit_trail_or_is_baselined()
    {
        // A Dapper or raw-SQL write is invisible to the EF change tracker, so its class must call IAuditTrail. The baseline is the list of
        // classes that deliberately do not (session rows are covered by access_audit; access_audit is itself an audit table).
        var write = new System.Text.RegularExpressions.Regex(@"\b(INSERT\s+INTO|UPDATE|DELETE\s+FROM)\s+[a-z_]+");
        IEnumerable<string> bad = Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => (File: Path.GetFileName(f), Text: File.ReadAllText(f)))
            .Where(f => write.IsMatch(f.Text) && !f.Text.Contains("IAuditTrail", StringComparison.Ordinal))
            .Select(f => f.File);

        AssertRatchet("raw-sql-writes", bad);
    }

    [Fact]
    public void Fitness_Domain_depends_only_on_FluentResults_and_the_BCL()
    {
        string[] allowed = ["System", "Microsoft", "netstandard", "FluentResults", "Fugue.Domain"];
        string[] offenders = Domain.GetReferencedAssemblies().Select(a => a.Name ?? "")
            .Where(n => !allowed.Any(p => n.StartsWith(p, StringComparison.Ordinal))).ToArray();

        Assert.True(offenders.Length == 0, "Unexpected Domain references: " + string.Join(", ", offenders));
    }
}
