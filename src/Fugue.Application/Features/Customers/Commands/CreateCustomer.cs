using FluentResults;
using FluentValidation;
using Fugue.Domain.Customers;
using Mediator;

namespace Fugue.Application.Features.Customers.Commands;

/// <summary>Contract.</summary>
public sealed record CreateCustomerCommand(string Name, string Email, string? Phone) : ICommand<Result<CustomerDto>>;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name).CustomerName();
        RuleFor(x => x.Email).CustomerEmail();
        RuleFor(x => x.Phone).CustomerPhone();
    }
}

public sealed class CreateCustomerHandler(ICustomerRepository customers)
    : ICommandHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    public async ValueTask<Result<CustomerDto>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken) =>
        await customers.AddAsync(Customer.Create(command.Name, command.Email, command.Phone), cancellationToken);
}
