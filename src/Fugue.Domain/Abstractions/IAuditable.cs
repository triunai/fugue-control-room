namespace Fugue.Domain.Abstractions;

/// <summary>
/// An entity whose creation and last change are recorded. Values are never set by domain code: the EF
/// <c>SaveChangesInterceptor</c> stamps tracked entities and Dapper SQL binds <see cref="IAuditStamp"/> explicitly.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }

    string CreatedBy { get; set; }

    DateTimeOffset UpdatedAt { get; set; }

    string UpdatedBy { get; set; }
}
