using FluentResults;
using FluentValidation;
using Fugue.Application.Common.Behaviors;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Tests.Application;

public sealed class ValidationBehaviorTests
{
    public sealed record CreateWidget(string Name, int Quantity, Address Address) : ICommand<Result<string>>;

    public sealed record Address(string Street);

    public sealed record DeleteWidget(int Id) : ICommand<Result>;

    private sealed class NameValidator : AbstractValidator<CreateWidget>
    {
        public NameValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
    }

    private sealed class QuantityAndAddressValidator : AbstractValidator<CreateWidget>
    {
        public QuantityAndAddressValidator()
        {
            RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be positive.");
            RuleFor(x => x.Address.Street).NotEmpty().WithMessage("Street is required.");
        }
    }

    private static readonly IValidator<CreateWidget>[] AllValidators = [new NameValidator(), new QuantityAndAddressValidator()];

    private static async Task<(Result<string> Result, int Calls)> Run(CreateWidget message)
    {
        int calls = 0;
        var behavior = new ValidationBehavior<CreateWidget, Result<string>>(AllValidators);

        Result<string> result = await behavior.Handle(
            message,
            (_, _) =>
            {
                calls++;
                return ValueTask.FromResult(Result.Ok("created"));
            },
            CancellationToken.None);
        return (result, calls);
    }

    [Fact]
    public async Task Aggregates_failures_from_all_validators_keyed_by_camel_case_property()
    {
        (Result<string> result, int calls) = await Run(new CreateWidget(string.Empty, 0, new Address(string.Empty)));

        Assert.Equal(0, calls);
        Assert.True(result.IsFailed);
        FugueError error = Assert.IsType<FugueError>(Assert.Single(result.Errors));
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(["Name is required."], error.Fields!["name"]);
        Assert.Equal(["Quantity must be positive."], error.Fields["quantity"]);
        Assert.Equal(["Street is required."], error.Fields["address.street"]);
        Assert.Equal(3, error.Fields.Count);
    }

    [Fact]
    public async Task Passes_through_to_the_handler_when_valid()
    {
        (Result<string> result, int calls) = await Run(new CreateWidget("gizmo", 2, new Address("1 Main St")));

        Assert.Equal(1, calls);
        Assert.True(result.IsSuccess);
        Assert.Equal("created", result.Value);
    }

    [Fact]
    public async Task Passes_through_when_no_validator_is_registered()
    {
        var behavior = new ValidationBehavior<DeleteWidget, Result>([]);

        Result result = await behavior.Handle(
            new DeleteWidget(1), (_, _) => ValueTask.FromResult(Result.Ok()), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Works_for_the_non_generic_result_type()
    {
        var behavior = new ValidationBehavior<DeleteWidget, Result>([new DeleteValidator()]);

        Result result = await behavior.Handle(
            new DeleteWidget(0), (_, _) => ValueTask.FromResult(Result.Ok()), CancellationToken.None);

        FugueError error = Assert.IsType<FugueError>(Assert.Single(result.Errors));
        Assert.Equal(["Id must be positive."], error.Fields!["id"]);
    }

    private sealed class DeleteValidator : AbstractValidator<DeleteWidget>
    {
        public DeleteValidator() => RuleFor(x => x.Id).GreaterThan(0).WithMessage("Id must be positive.");
    }
}
