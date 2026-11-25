namespace Fugue.Domain.Identity;

/// <summary>Permission codes (<c>RESOURCE_ACTION</c>). The seeded catalog and every <c>[HasPermission]</c> literal come from here.</summary>
public static class Permissions
{
    public const string CustomersView = "CUSTOMERS_VIEW";
    public const string CustomersInsert = "CUSTOMERS_INSERT";
    public const string CustomersEdit = "CUSTOMERS_EDIT";
    public const string CustomersDelete = "CUSTOMERS_DELETE";
    public const string CustomersSuspend = "CUSTOMERS_SUSPEND";

    /// <summary>Widens every customers permission from "own rows" to "all rows".</summary>
    public const string CustomersAll = "CUSTOMERS_ALL";

    public const string GroupsView = "GROUPS_VIEW";
    public const string GroupsManage = "GROUPS_MANAGE";
    public const string OverridesView = "OVERRIDES_VIEW";
    public const string OverridesManage = "OVERRIDES_MANAGE";
    public const string ModulesView = "MODULES_VIEW";
    public const string ModulesManage = "MODULES_MANAGE";
    public const string AuditRead = "AUDIT_READ";

    public static readonly IReadOnlyList<string> All =
    [
        CustomersView, CustomersInsert, CustomersEdit, CustomersDelete, CustomersSuspend, CustomersAll,
        GroupsView, GroupsManage, OverridesView, OverridesManage, ModulesView, ModulesManage,
        AuditRead,
    ];
}

/// <summary>Module codes of the seeded registry.</summary>
public static class ModuleCodes
{
    public const string System = "SYSTEM";
    public const string Customers = "CUSTOMERS";
    public const string Identity = "IDENTITY";
    public const string Audit = "AUDIT";
}

/// <summary>Role codes of the seeded roles.</summary>
public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string Staff = "STAFF";
    public const string SelfService = "SELF_SERVICE";
}
