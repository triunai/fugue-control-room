using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Customers.Helpers;
using Fugue.Application.Features.Customers.Models;
using Fugue.Application.Features.Customers.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Customers;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Customers.Commands;

/// <summary>Contract.</summary>
/// <summary>
/// Contract. <paramref name="OwnerUserId"/> is honoured only for a caller with effective <c>CUSTOMERS_ALL</c>; everyone else
/// owns what they create (Codex finding 1), so the field can never assign a record to someone else.
/// </summary>
public sealed record CreateCustomerCommand(string Name, string Email, string? Phone, Guid? OwnerUserId = null) : ICommand<Result<CustomerDto>>;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name).CustomerName();
        RuleFor(x => x.Email).CustomerEmail();
        RuleFor(x => x.Phone).CustomerPhone();
    }
}

public sealed class CreateCustomerHandler(ICustomerRepository customers, ICurrentUser currentUser)
    : ICommandHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    public async ValueTask<Result<CustomerDto>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken) =>
        await customers.AddAsync(
            Customer.Create(command.Name, command.Email, command.Phone, ResolveOwner(command)), cancellationToken);

    private UserId? ResolveOwner(CreateCustomerCommand command)
    {
        Guid? owner = OwnerScope.From(currentUser).All ? command.OwnerUserId : currentUser.UserId;
        return owner is { } id ? UserId.From(id) : null;
    }
}
