using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>AccessAudit</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<AccessAuditId>))]
[TypeConverter(typeof(GuidIdTypeConverter<AccessAuditId>))]
public readonly record struct AccessAuditId(Guid Value) : IStronglyTypedId<AccessAuditId>, IParsable<AccessAuditId>
{
    public static AccessAuditId New() => new(Uuid7.NewGuid());

    public static AccessAuditId From(Guid value) => new(value);

    public static AccessAuditId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out AccessAuditId id) ? id : throw new FormatException($"'{s}' is not a valid AccessAuditId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out AccessAuditId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new AccessAuditId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>Action</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<ActionId>))]
[TypeConverter(typeof(GuidIdTypeConverter<ActionId>))]
public readonly record struct ActionId(Guid Value) : IStronglyTypedId<ActionId>, IParsable<ActionId>
{
    public static ActionId New() => new(Uuid7.NewGuid());

    public static ActionId From(Guid value) => new(value);

    public static ActionId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out ActionId id) ? id : throw new FormatException($"'{s}' is not a valid ActionId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ActionId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new ActionId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>AuditLog</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<AuditLogId>))]
[TypeConverter(typeof(GuidIdTypeConverter<AuditLogId>))]
public readonly record struct AuditLogId(Guid Value) : IStronglyTypedId<AuditLogId>, IParsable<AuditLogId>
{
    public static AuditLogId New() => new(Uuid7.NewGuid());

    public static AuditLogId From(Guid value) => new(value);

    public static AuditLogId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out AuditLogId id) ? id : throw new FormatException($"'{s}' is not a valid AuditLogId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out AuditLogId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new AuditLogId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>Claim</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<ClaimId>))]
[TypeConverter(typeof(GuidIdTypeConverter<ClaimId>))]
public readonly record struct ClaimId(Guid Value) : IStronglyTypedId<ClaimId>, IParsable<ClaimId>
{
    public static ClaimId New() => new(Uuid7.NewGuid());

    public static ClaimId From(Guid value) => new(value);

    public static ClaimId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out ClaimId id) ? id : throw new FormatException($"'{s}' is not a valid ClaimId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ClaimId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new ClaimId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>ClaimInfoRequest</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<ClaimInfoRequestId>))]
[TypeConverter(typeof(GuidIdTypeConverter<ClaimInfoRequestId>))]
public readonly record struct ClaimInfoRequestId(Guid Value) : IStronglyTypedId<ClaimInfoRequestId>, IParsable<ClaimInfoRequestId>
{
    public static ClaimInfoRequestId New() => new(Uuid7.NewGuid());

    public static ClaimInfoRequestId From(Guid value) => new(value);

    public static ClaimInfoRequestId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out ClaimInfoRequestId id) ? id : throw new FormatException($"'{s}' is not a valid ClaimInfoRequestId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ClaimInfoRequestId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new ClaimInfoRequestId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>ClaimTimelineEntry</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<ClaimTimelineEntryId>))]
[TypeConverter(typeof(GuidIdTypeConverter<ClaimTimelineEntryId>))]
public readonly record struct ClaimTimelineEntryId(Guid Value) : IStronglyTypedId<ClaimTimelineEntryId>, IParsable<ClaimTimelineEntryId>
{
    public static ClaimTimelineEntryId New() => new(Uuid7.NewGuid());

    public static ClaimTimelineEntryId From(Guid value) => new(value);

    public static ClaimTimelineEntryId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out ClaimTimelineEntryId id) ? id : throw new FormatException($"'{s}' is not a valid ClaimTimelineEntryId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ClaimTimelineEntryId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new ClaimTimelineEntryId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>Customer</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<CustomerId>))]
[TypeConverter(typeof(GuidIdTypeConverter<CustomerId>))]
public readonly record struct CustomerId(Guid Value) : IStronglyTypedId<CustomerId>, IParsable<CustomerId>
{
    public static CustomerId New() => new(Uuid7.NewGuid());

    public static CustomerId From(Guid value) => new(value);

    public static CustomerId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out CustomerId id) ? id : throw new FormatException($"'{s}' is not a valid CustomerId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out CustomerId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new CustomerId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>DataProtectionKey</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<DataProtectionKeyId>))]
[TypeConverter(typeof(GuidIdTypeConverter<DataProtectionKeyId>))]
public readonly record struct DataProtectionKeyId(Guid Value) : IStronglyTypedId<DataProtectionKeyId>, IParsable<DataProtectionKeyId>
{
    public static DataProtectionKeyId New() => new(Uuid7.NewGuid());

    public static DataProtectionKeyId From(Guid value) => new(value);

    public static DataProtectionKeyId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out DataProtectionKeyId id) ? id : throw new FormatException($"'{s}' is not a valid DataProtectionKeyId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out DataProtectionKeyId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new DataProtectionKeyId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>GrantAudit</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<GrantAuditId>))]
[TypeConverter(typeof(GuidIdTypeConverter<GrantAuditId>))]
public readonly record struct GrantAuditId(Guid Value) : IStronglyTypedId<GrantAuditId>, IParsable<GrantAuditId>
{
    public static GrantAuditId New() => new(Uuid7.NewGuid());

    public static GrantAuditId From(Guid value) => new(value);

    public static GrantAuditId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out GrantAuditId id) ? id : throw new FormatException($"'{s}' is not a valid GrantAuditId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out GrantAuditId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new GrantAuditId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>Group</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<GroupId>))]
[TypeConverter(typeof(GuidIdTypeConverter<GroupId>))]
public readonly record struct GroupId(Guid Value) : IStronglyTypedId<GroupId>, IParsable<GroupId>
{
    public static GroupId New() => new(Uuid7.NewGuid());

    public static GroupId From(Guid value) => new(value);

    public static GroupId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out GroupId id) ? id : throw new FormatException($"'{s}' is not a valid GroupId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out GroupId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new GroupId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of an <c>idempotency_keys</c> row. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<IdempotencyKeyId>))]
[TypeConverter(typeof(GuidIdTypeConverter<IdempotencyKeyId>))]
public readonly record struct IdempotencyKeyId(Guid Value) : IStronglyTypedId<IdempotencyKeyId>, IParsable<IdempotencyKeyId>
{
    public static IdempotencyKeyId New() => new(Uuid7.NewGuid());

    public static IdempotencyKeyId From(Guid value) => new(value);

    public static IdempotencyKeyId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out IdempotencyKeyId id) ? id : throw new FormatException($"'{s}' is not a valid IdempotencyKeyId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out IdempotencyKeyId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new IdempotencyKeyId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>Module</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<ModuleId>))]
[TypeConverter(typeof(GuidIdTypeConverter<ModuleId>))]
public readonly record struct ModuleId(Guid Value) : IStronglyTypedId<ModuleId>, IParsable<ModuleId>
{
    public static ModuleId New() => new(Uuid7.NewGuid());

    public static ModuleId From(Guid value) => new(value);

    public static ModuleId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out ModuleId id) ? id : throw new FormatException($"'{s}' is not a valid ModuleId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ModuleId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new ModuleId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>OutboxMessage</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<OutboxMessageId>))]
[TypeConverter(typeof(GuidIdTypeConverter<OutboxMessageId>))]
public readonly record struct OutboxMessageId(Guid Value) : IStronglyTypedId<OutboxMessageId>, IParsable<OutboxMessageId>
{
    public static OutboxMessageId New() => new(Uuid7.NewGuid());

    public static OutboxMessageId From(Guid value) => new(value);

    public static OutboxMessageId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out OutboxMessageId id) ? id : throw new FormatException($"'{s}' is not a valid OutboxMessageId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out OutboxMessageId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new OutboxMessageId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>Permission</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<PermissionId>))]
[TypeConverter(typeof(GuidIdTypeConverter<PermissionId>))]
public readonly record struct PermissionId(Guid Value) : IStronglyTypedId<PermissionId>, IParsable<PermissionId>
{
    public static PermissionId New() => new(Uuid7.NewGuid());

    public static PermissionId From(Guid value) => new(value);

    public static PermissionId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out PermissionId id) ? id : throw new FormatException($"'{s}' is not a valid PermissionId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out PermissionId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new PermissionId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>RefreshToken</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<RefreshTokenId>))]
[TypeConverter(typeof(GuidIdTypeConverter<RefreshTokenId>))]
public readonly record struct RefreshTokenId(Guid Value) : IStronglyTypedId<RefreshTokenId>, IParsable<RefreshTokenId>
{
    public static RefreshTokenId New() => new(Uuid7.NewGuid());

    public static RefreshTokenId From(Guid value) => new(value);

    public static RefreshTokenId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out RefreshTokenId id) ? id : throw new FormatException($"'{s}' is not a valid RefreshTokenId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out RefreshTokenId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new RefreshTokenId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>Resource</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<ResourceId>))]
[TypeConverter(typeof(GuidIdTypeConverter<ResourceId>))]
public readonly record struct ResourceId(Guid Value) : IStronglyTypedId<ResourceId>, IParsable<ResourceId>
{
    public static ResourceId New() => new(Uuid7.NewGuid());

    public static ResourceId From(Guid value) => new(value);

    public static ResourceId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out ResourceId id) ? id : throw new FormatException($"'{s}' is not a valid ResourceId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ResourceId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new ResourceId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>Role</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<RoleId>))]
[TypeConverter(typeof(GuidIdTypeConverter<RoleId>))]
public readonly record struct RoleId(Guid Value) : IStronglyTypedId<RoleId>, IParsable<RoleId>
{
    public static RoleId New() => new(Uuid7.NewGuid());

    public static RoleId From(Guid value) => new(value);

    public static RoleId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out RoleId id) ? id : throw new FormatException($"'{s}' is not a valid RoleId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out RoleId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new RoleId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>Tenant</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<TenantId>))]
[TypeConverter(typeof(GuidIdTypeConverter<TenantId>))]
public readonly record struct TenantId(Guid Value) : IStronglyTypedId<TenantId>, IParsable<TenantId>
{
    public static TenantId New() => new(Uuid7.NewGuid());

    public static TenantId From(Guid value) => new(value);

    public static TenantId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out TenantId id) ? id : throw new FormatException($"'{s}' is not a valid TenantId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out TenantId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new TenantId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>User</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<UserId>))]
[TypeConverter(typeof(GuidIdTypeConverter<UserId>))]
public readonly record struct UserId(Guid Value) : IStronglyTypedId<UserId>, IParsable<UserId>
{
    public static UserId New() => new(Uuid7.NewGuid());

    public static UserId From(Guid value) => new(value);

    public static UserId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out UserId id) ? id : throw new FormatException($"'{s}' is not a valid UserId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out UserId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new UserId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>UserPermissionOverride</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<UserPermissionOverrideId>))]
[TypeConverter(typeof(GuidIdTypeConverter<UserPermissionOverrideId>))]
public readonly record struct UserPermissionOverrideId(Guid Value) : IStronglyTypedId<UserPermissionOverrideId>, IParsable<UserPermissionOverrideId>
{
    public static UserPermissionOverrideId New() => new(Uuid7.NewGuid());

    public static UserPermissionOverrideId From(Guid value) => new(value);

    public static UserPermissionOverrideId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out UserPermissionOverrideId id) ? id : throw new FormatException($"'{s}' is not a valid UserPermissionOverrideId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out UserPermissionOverrideId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new UserPermissionOverrideId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>UserRole</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<UserRoleId>))]
[TypeConverter(typeof(GuidIdTypeConverter<UserRoleId>))]
public readonly record struct UserRoleId(Guid Value) : IStronglyTypedId<UserRoleId>, IParsable<UserRoleId>
{
    public static UserRoleId New() => new(Uuid7.NewGuid());

    public static UserRoleId From(Guid value) => new(value);

    public static UserRoleId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out UserRoleId id) ? id : throw new FormatException($"'{s}' is not a valid UserRoleId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out UserRoleId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new UserRoleId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>WebhookDelivery</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<WebhookDeliveryId>))]
[TypeConverter(typeof(GuidIdTypeConverter<WebhookDeliveryId>))]
public readonly record struct WebhookDeliveryId(Guid Value) : IStronglyTypedId<WebhookDeliveryId>, IParsable<WebhookDeliveryId>
{
    public static WebhookDeliveryId New() => new(Uuid7.NewGuid());

    public static WebhookDeliveryId From(Guid value) => new(value);

    public static WebhookDeliveryId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out WebhookDeliveryId id) ? id : throw new FormatException($"'{s}' is not a valid WebhookDeliveryId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out WebhookDeliveryId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new WebhookDeliveryId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Identifier of a <c>WebhookSubscription</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<WebhookSubscriptionId>))]
[TypeConverter(typeof(GuidIdTypeConverter<WebhookSubscriptionId>))]
public readonly record struct WebhookSubscriptionId(Guid Value) : IStronglyTypedId<WebhookSubscriptionId>, IParsable<WebhookSubscriptionId>
{
    public static WebhookSubscriptionId New() => new(Uuid7.NewGuid());

    public static WebhookSubscriptionId From(Guid value) => new(value);

    public static WebhookSubscriptionId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out WebhookSubscriptionId id) ? id : throw new FormatException($"'{s}' is not a valid WebhookSubscriptionId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out WebhookSubscriptionId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new WebhookSubscriptionId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
