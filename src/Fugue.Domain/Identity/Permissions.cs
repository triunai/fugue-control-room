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
    public const string WebhooksManage = "WEBHOOKS_MANAGE";

    // Claims (W-020, D-053). Claimant: submit, read own, respond. Staff: all, handle, workload. Manager: settle, exposure.
    public const string ClaimsSubmit = "CLAIMS_SUBMIT";
    public const string ClaimsRead = "CLAIMS_READ";
    public const string ClaimsRespond = "CLAIMS_RESPOND";

    /// <summary>Widens reading claims from "my own" to "every claim".</summary>
    public const string ClaimsAll = "CLAIMS_ALL";

    /// <summary>Pick up, request information, set the reserve, approve or reject.</summary>
    public const string ClaimsHandle = "CLAIMS_HANDLE";

    /// <summary>Settle an approved claim; also the manager override on who may act on an assigned claim.</summary>
    public const string ClaimsSettle = "CLAIMS_SETTLE";
    public const string ClaimsWorkload = "CLAIMS_WORKLOAD";
    public const string ClaimsExposure = "CLAIMS_EXPOSURE";

    /// <summary>List users and roles in the admin console (read only).</summary>
    public const string UsersView = "USERS_VIEW";

    // New permissions are appended: a permission's seeded id is its position in this list, so inserting earlier would renumber existing rows.
    public static readonly IReadOnlyList<string> All =
    [
        CustomersView, CustomersInsert, CustomersEdit, CustomersDelete, CustomersSuspend, CustomersAll,
        GroupsView, GroupsManage, OverridesView, OverridesManage, ModulesView, ModulesManage,
        AuditRead, WebhooksManage,
        ClaimsSubmit, ClaimsRead, ClaimsRespond, ClaimsAll, ClaimsHandle, ClaimsSettle, ClaimsWorkload, ClaimsExposure,
        UsersView,
    ];
}

/// <summary>Module codes of the seeded registry.</summary>
public static class ModuleCodes
{
    public const string System = "SYSTEM";
    public const string Customers = "CUSTOMERS";
    public const string Identity = "IDENTITY";
    public const string Audit = "AUDIT";
    public const string Webhooks = "WEBHOOKS";
    public const string Claims = "CLAIMS";
}

/// <summary>Role codes of the seeded roles.</summary>
public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string Staff = "STAFF";
    public const string SelfService = "SELF_SERVICE";
    public const string Manager = "MANAGER";
}
