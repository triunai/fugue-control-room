using FluentResults;
using FluentValidation;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Customers.Queries;

/// <summary>Contract.</summary>
public sealed record GetCustomerByIdQuery(CustomerId Id) : IQuery<Result<CustomerDto>>;

public sealed class GetCustomerByIdValidator : AbstractValidator<GetCustomerByIdQuery>
{
    public GetCustomerByIdValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class GetCustomerByIdHandler(ICustomerRepository customers)
    : IQueryHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    public async ValueTask<Result<CustomerDto>> Handle(GetCustomerByIdQuery query, CancellationToken cancellationToken) =>
        await customers.GetAsync(query.Id, cancellationToken) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<CustomerDto>(CustomerErrors.NotFound(query.Id));
}
