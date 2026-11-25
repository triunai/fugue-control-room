using Mediator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fugue.Tests.Api;

/// <summary>Builds a client over the real pipeline with the test-only probe controller and a fake sender.</summary>
internal static class ProbeFactory
{
    public static HttpClient CreateClient(WebApplicationFactory<Program> factory, string? environment = null)
    {
        WebApplicationFactory<Program> configured = factory.WithWebHostBuilder(builder =>
        {
            if (environment is not null)
            {
                builder.UseEnvironment(environment);
            }

            TestAuth.Apply(builder, TestAuth.NewKey());
            builder.ConfigureTestServices(services =>
            {
                TestAuth.OpenFallback(services);
                services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
                services.RemoveAll<ISender>();
                services.AddSingleton<ISender, FakeSender>();
            });
        });
        return configured.CreateClient();
    }
}
