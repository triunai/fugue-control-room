using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Persistence;

/// <summary>
/// The seeded reference catalog (modules, resources, actions, permissions, roles, one group), delivered by the
/// <c>AddIdentity</c> migration through <c>HasData</c>. Ids are fixed literals so the migration is deterministic.
/// Users are never seeded here (dev users come from the Development seeder).
/// </summary>
public static class IdentityCatalog
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);
    private const string Actor = "migration";

    private static Guid Seed(int kind, int n) => new($"01900000-0000-7000-8{kind:x3}-{n:D12}");

    public static readonly ModuleId SystemModule = ModuleId.From(Seed(1, 1));
    public static readonly ModuleId CustomersModule = ModuleId.From(Seed(1, 2));
    public static readonly ModuleId IdentityModule = ModuleId.From(Seed(1, 3));
    public static readonly ModuleId AuditModule = ModuleId.From(Seed(1, 4));
    public static readonly ModuleId WebhooksModule = ModuleId.From(Seed(1, 5));
    public static readonly ModuleId ClaimsModule = ModuleId.From(Seed(1, 6));

    public static readonly RoleId AdminRole = RoleId.From(Seed(5, 1));
    public static readonly RoleId StaffRole = RoleId.From(Seed(5, 2));
    public static readonly RoleId SelfServiceRole = RoleId.From(Seed(5, 3));
    public static readonly RoleId ManagerRole = RoleId.From(Seed(5, 4));

    public static readonly GroupId SupportGroup = GroupId.From(Seed(7, 1));

    private static readonly (string Code, string Name, ModuleId Module)[] ResourceDefs =
    [
        ("MODULES", "Modules", SystemModule),
        ("CUSTOMERS", "Customers", CustomersModule),
        ("GROUPS", "Groups", IdentityModule),
        ("OVERRIDES", "PermissionDefinition overrides", IdentityModule),
        ("AUDIT", "Audit log", AuditModule),
        ("WEBHOOKS", "Webhooks", WebhooksModule),
        ("CLAIMS", "Claims", ClaimsModule),
    ];

    private static readonly string[] ActionCodes = ["VIEW", "INSERT", "EDIT", "DELETE", "SUSPEND", "ALL", "MANAGE", "READ", "SUBMIT", "RESPOND", "HANDLE", "SETTLE", "WORKLOAD", "EXPOSURE"];

    public static ResourceId ResourceIdFor(string code) =>
        ResourceId.From(Seed(2, Array.FindIndex(ResourceDefs, r => r.Code == code) + 1));

    public static ActionId ActionIdFor(string code) => ActionId.From(Seed(3, Array.IndexOf(ActionCodes, code) + 1));

    public static PermissionId PermissionIdFor(string permissionCode) =>
        PermissionId.From(Seed(4, Array.IndexOf(Permissions.All.ToArray(), permissionCode) + 1));

    public static RoleId RoleIdFor(string roleCode) => roleCode switch
    {
        RoleCodes.Admin => AdminRole,
        RoleCodes.Staff => StaffRole,
        RoleCodes.SelfService => SelfServiceRole,
        RoleCodes.Manager => ManagerRole,
        _ => throw new ArgumentOutOfRangeException(nameof(roleCode)),
    };

    private static string[] RolePermissionCodes(string role) => role switch
    {
        RoleCodes.Admin => [.. Permissions.All],
        RoleCodes.Staff => [.. StaffPermissions],
        RoleCodes.Manager => [.. StaffPermissions, Permissions.ClaimsSettle, Permissions.ClaimsExposure],
        _ => [Permissions.CustomersView, Permissions.CustomersEdit, Permissions.ClaimsSubmit, Permissions.ClaimsRead, Permissions.ClaimsRespond],
    };

    /// <summary>Customers (every action) plus the claims staff set (decision G of the claims window): read, all, handle, workload.</summary>
    private static IEnumerable<string> StaffPermissions =>
        Permissions.All.Where(p => p.StartsWith("CUSTOMERS_", StringComparison.Ordinal))
            .Concat([Permissions.ClaimsRead, Permissions.ClaimsAll, Permissions.ClaimsHandle, Permissions.ClaimsWorkload]);

    public static void HasData(ModelBuilder mb)
    {
        mb.Entity<Module>().HasData(
            Module(SystemModule, ModuleCodes.System, "System"),
            Module(CustomersModule, ModuleCodes.Customers, "Customers"),
            Module(IdentityModule, ModuleCodes.Identity, "Identity"),
            Module(AuditModule, ModuleCodes.Audit, "Audit"),
            Module(WebhooksModule, ModuleCodes.Webhooks, "Webhooks"),
            Module(ClaimsModule, ModuleCodes.Claims, "Claims"));

        mb.Entity<Resource>().HasData(ResourceDefs.Select(r => new Resource
        {
            Id = ResourceIdFor(r.Code),
            ModuleId = r.Module,
            Code = r.Code,
            Name = r.Name,
            CreatedAt = Stamp,
            CreatedBy = Actor,
            UpdatedAt = Stamp,
            UpdatedBy = Actor,
        }));

        mb.Entity<PermissionAction>().HasData(ActionCodes.Select(a => new PermissionAction
        {
            Id = ActionIdFor(a),
            Code = a,
            Name = a[0] + a[1..].ToLowerInvariant(),
            CreatedAt = Stamp,
            CreatedBy = Actor,
            UpdatedAt = Stamp,
            UpdatedBy = Actor,
        }));

        mb.Entity<PermissionDefinition>().HasData(Permissions.All.Select(code =>
        {
            int split = code.LastIndexOf('_');
            return new PermissionDefinition
            {
                Id = PermissionIdFor(code),
                ResourceId = ResourceIdFor(code[..split]),
                ActionId = ActionIdFor(code[(split + 1)..]),
                Code = code,
                CreatedAt = Stamp,
                CreatedBy = Actor,
                UpdatedAt = Stamp,
                UpdatedBy = Actor,
            };
        }));

        mb.Entity<Role>().HasData(
            Role(AdminRole, RoleCodes.Admin, "Administrator"),
            Role(StaffRole, RoleCodes.Staff, "Staff"),
            Role(SelfServiceRole, RoleCodes.SelfService, "Self service"),
            Role(ManagerRole, RoleCodes.Manager, "Manager"));

        mb.Entity<RoleGrant>().HasData(new[] { RoleCodes.Admin, RoleCodes.Staff, RoleCodes.SelfService, RoleCodes.Manager }
            .SelectMany(role => RolePermissionCodes(role).Select(code => new RoleGrant
            {
                RoleId = RoleIdFor(role),
                PermissionId = PermissionIdFor(code),
                CreatedAt = Stamp,
                CreatedBy = Actor,
                UpdatedAt = Stamp,
                UpdatedBy = Actor,
            })));

        mb.Entity<Group>().HasData(new Group
        {
            Id = SupportGroup,
            Code = "SUPPORT",
            Name = "Support",
            CreatedAt = Stamp,
            CreatedBy = Actor,
            UpdatedAt = Stamp,
            UpdatedBy = Actor,
        });
        mb.Entity<GroupRole>().HasData(new GroupRole
        {
            GroupId = SupportGroup,
            RoleId = StaffRole,
            CreatedAt = Stamp,
            CreatedBy = Actor,
            UpdatedAt = Stamp,
            UpdatedBy = Actor,
        });
    }

    private static Module Module(ModuleId id, string code, string name) => new()
    {
        Id = id,
        Code = code,
        Name = name,
        CreatedAt = Stamp,
        CreatedBy = Actor,
        UpdatedAt = Stamp,
        UpdatedBy = Actor,
    };

    private static Role Role(RoleId id, string code, string name) => new()
    {
        Id = id,
        Code = code,
        Name = name,
        IsSystem = true,
        CreatedAt = Stamp,
        CreatedBy = Actor,
        UpdatedAt = Stamp,
        UpdatedBy = Actor,
    };
}
