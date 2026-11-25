using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Fugue.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every <c>AbstractValidator&lt;T&gt;</c> in this assembly. Pipeline behaviors are registered
    /// by the composition root through the Mediator options (<c>PipelineBehaviors</c>).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services.AddValidatorsFromAssembly(typeof(AssemblyMarker).Assembly, includeInternalTypes: true);
}
