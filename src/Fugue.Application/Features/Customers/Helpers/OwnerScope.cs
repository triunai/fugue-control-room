using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Customers.Helpers;

/// <summary>
/// Which customers the caller may touch. <see cref="All"/> (effective <c>CUSTOMERS_ALL</c>) means every row;
/// otherwise only rows whose owner is <see cref="OwnerId"/>. A caller with neither sees nothing.
/// </summary>
public readonly record struct OwnerScope(bool All, UserId? OwnerId)
{
    public static OwnerScope From(ICurrentUser user) =>
        new(user.HasPermission(Permissions.CustomersAll), user.UserId is { } id ? UserId.From(id) : null);
}
