using FluentResults;
using FluentValidation;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Customers.Commands;

/// <summary>Contract. <paramref name="ExpectedVersion"/> comes from <c>If-Match</c> and is mandatory.</summary>
public sealed record DeleteCustomerCommand(CustomerId Id, long? ExpectedVersion) : ICommand<Result>;

public sealed class DeleteCustomerValidator : AbstractValidator<DeleteCustomerCommand>
{
    public DeleteCustomerValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class DeleteCustomerHandler(ICustomerRepository customers, ICurrentUser currentUser) : ICommandHandler<DeleteCustomerCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteCustomerCommand command, CancellationToken cancellationToken)
    {
        if (command.ExpectedVersion is not { } expected)
        {
            return Result.Fail(CustomerErrors.IfMatchRequired());
        }

        Domain.Customers.Customer? customer = await customers.FindForUpdateAsync(command.Id, OwnerScope.From(currentUser), cancellationToken);
        return customer is null
            ? Result.Fail(CustomerErrors.NotFound(command.Id))
            : await customers.DeleteAsync(customer, expected, cancellationToken);
    }
}
