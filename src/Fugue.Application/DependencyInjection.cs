using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Fugue.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every <c>AbstractValidator&lt;T&gt;</c> in this assembly. Pipeline behaviors are registered
    /// by the composition root through the Mediator options (<c>PipelineBehaviors</c>).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuditScope, AuditScope>();
        return services.AddValidatorsFromAssembly(typeof(AssemblyMarker).Assembly, includeInternalTypes: true);
    }
}
