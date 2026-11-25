using Fugue.Api.Auth;
using Fugue.Domain.Abstractions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
builder.Services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

WebApplication app = builder.Build();

app.MapControllers();

app.Run();

/// <summary>Exposes the entry point to <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
