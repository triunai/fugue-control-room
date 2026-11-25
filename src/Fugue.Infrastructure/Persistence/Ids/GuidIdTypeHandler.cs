using System.Data;
using Dapper;
using Fugue.Domain.Ids;

namespace Fugue.Infrastructure.Persistence.Ids;

/// <summary>Dapper handler: binds a typed id as a <see cref="Guid"/> parameter and reads it back from a uuid column.</summary>
internal sealed class GuidIdTypeHandler<TId> : SqlMapper.TypeHandler<TId>
    where TId : struct, IStronglyTypedId<TId>
{
    public override void SetValue(IDbDataParameter parameter, TId value)
    {
        parameter.DbType = DbType.Guid;
        parameter.Value = value.Value;
    }

    public override TId Parse(object value) => TId.From((Guid)value);
}
