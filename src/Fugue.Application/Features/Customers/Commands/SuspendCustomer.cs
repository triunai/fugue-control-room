using FluentResults;
using FluentValidation;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Customers.Commands;

/// <summary>Contract. <paramref name="ExpectedVersion"/> (from <c>If-Match</c>) is optional here: the state machine already guards the transition.</summary>
public sealed record SuspendCustomerCommand(CustomerId Id, long? ExpectedVersion) : ICommand<Result<CustomerDto>>;

public sealed class SuspendCustomerValidator : AbstractValidator<SuspendCustomerCommand>
{
    public SuspendCustomerValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class SuspendCustomerHandler(ICustomerRepository customers, ICurrentUser currentUser)
    : ICommandHandler<SuspendCustomerCommand, Result<CustomerDto>>
{
    public async ValueTask<Result<CustomerDto>> Handle(SuspendCustomerCommand command, CancellationToken cancellationToken)
    {
        Domain.Customers.Customer? customer = await customers.FindForUpdateAsync(command.Id, OwnerScope.From(currentUser), cancellationToken);
        if (customer is null)
        {
            return Result.Fail<CustomerDto>(CustomerErrors.NotFound(command.Id));
        }

        Result transition = customer.Suspend();
        return transition.IsFailed
            ? Result.Fail<CustomerDto>(transition.Errors)
            : await customers.SaveAsync(customer, command.ExpectedVersion, cancellationToken);
    }
}
