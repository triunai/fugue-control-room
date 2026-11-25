using FluentResults;
using FluentValidation;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Customers.Commands;

/// <summary>Contract. <paramref name="ExpectedVersion"/> (from <c>If-Match</c>) is optional here: the state machine already guards the transition.</summary>
public sealed record ReactivateCustomerCommand(CustomerId Id, long? ExpectedVersion) : ICommand<Result<CustomerDto>>;

public sealed class ReactivateCustomerValidator : AbstractValidator<ReactivateCustomerCommand>
{
    public ReactivateCustomerValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class ReactivateCustomerHandler(ICustomerRepository customers, ICurrentUser currentUser)
    : ICommandHandler<ReactivateCustomerCommand, Result<CustomerDto>>
{
    public async ValueTask<Result<CustomerDto>> Handle(ReactivateCustomerCommand command, CancellationToken cancellationToken)
    {
        Domain.Customers.Customer? customer = await customers.FindForUpdateAsync(command.Id, OwnerScope.From(currentUser), cancellationToken);
        if (customer is null)
        {
            return Result.Fail<CustomerDto>(CustomerErrors.NotFound(command.Id));
        }

        Result transition = customer.Reactivate();
        return transition.IsFailed
            ? Result.Fail<CustomerDto>(transition.Errors)
            : await customers.SaveAsync(customer, command.ExpectedVersion, cancellationToken);
    }
}
