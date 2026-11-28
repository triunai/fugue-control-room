using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Customers.Helpers;
using Fugue.Application.Features.Customers.Models;
using Fugue.Application.Features.Customers.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Customers.Commands;

/// <summary>Contract. <paramref name="ExpectedVersion"/> comes from <c>If-Match</c> and is mandatory.</summary>
public sealed record UpdateCustomerCommand(CustomerId Id, string Name, string Email, string? Phone, long? ExpectedVersion)
    : ICommand<Result<CustomerDto>>;

public sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerValidator()
    {
        RuleFor(x => x.Name).CustomerName();
        RuleFor(x => x.Email).CustomerEmail();
        RuleFor(x => x.Phone).CustomerPhone();
    }
}

public sealed class UpdateCustomerHandler(ICustomerRepository customers, ICurrentUser currentUser)
    : ICommandHandler<UpdateCustomerCommand, Result<CustomerDto>>
{
    public async ValueTask<Result<CustomerDto>> Handle(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        if (command.ExpectedVersion is null)
        {
            return Result.Fail<CustomerDto>(CustomerErrors.IfMatchRequired());
        }

        Domain.Customers.Customer? customer = await customers.FindForUpdateAsync(command.Id, OwnerScope.From(currentUser), cancellationToken);
        if (customer is null)
        {
            return Result.Fail<CustomerDto>(CustomerErrors.NotFound(command.Id));
        }

        // Email change is staff-only (Codex finding 5): a self-service edit must not probe the global email index.
        OwnerScope scope = OwnerScope.From(currentUser);
        if (!scope.All && EmailKey.Canonicalize(command.Email) != customer.Email)
        {
            return Result.Fail<CustomerDto>(CustomerErrors.EmailChangeForbidden());
        }

        customer.Update(command.Name, command.Email, command.Phone);
        return await customers.SaveAsync(customer, command.ExpectedVersion, cancellationToken);
    }
}
