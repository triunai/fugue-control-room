using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Modules.Models;
using Fugue.Application.Features.Modules.Repos;
using Fugue.Domain.Identity;
using Mediator;

namespace Fugue.Application.Features.Modules.Commands;

/// <summary>Contract. <paramref name="ExpectedVersion"/> comes from <c>If-Match</c> and is mandatory.</summary>
public sealed record SetModuleActiveCommand(string Code, bool Active, long? ExpectedVersion) : ICommand<Result<ModuleDto>>;

public sealed class SetModuleActiveValidator : AbstractValidator<SetModuleActiveCommand>
{
    public SetModuleActiveValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
}

public sealed class SetModuleActiveHandler(IModuleRegistry registry) : ICommandHandler<SetModuleActiveCommand, Result<ModuleDto>>
{
    public async ValueTask<Result<ModuleDto>> Handle(SetModuleActiveCommand command, CancellationToken cancellationToken)
    {
        string code = command.Code.Trim().ToUpperInvariant();
        if (command.ExpectedVersion is not { } expected)
        {
            return Result.Fail<ModuleDto>(ModuleErrors.IfMatchRequired());
        }

        if (!command.Active && code == ModuleCodes.System)
        {
            return Result.Fail<ModuleDto>(ModuleErrors.SystemProtected());
        }

        return await registry.SetActiveAsync(code, command.Active, expected, cancellationToken);
    }
}
