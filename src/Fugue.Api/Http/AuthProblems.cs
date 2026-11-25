using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Http;

/// <summary>Writes framework-level auth failures (no handler ran) as the same problem+json the handlers produce, with code and correlation id.</summary>
public static class AuthProblems
{
    public static async Task WriteAsync(HttpContext context, int status, string code, string detail, string? retryAfterSeconds = null)
    {
        context.Response.StatusCode = status;
        if (retryAfterSeconds is not null)
        {
            context.Response.Headers.RetryAfter = retryAfterSeconds;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = status switch
            {
                StatusCodes.Status401Unauthorized => "Unauthorized",
                StatusCodes.Status403Forbidden => "Forbidden",
                StatusCodes.Status404NotFound => "Not Found",
                StatusCodes.Status429TooManyRequests => "Too Many Requests",
                _ => "Error",
            },
            Detail = detail,
            Type = "https://fugue.dev/errors/" + code.Replace('.', '-').Replace('_', '-'),
        };
        problem.Extensions["code"] = code;
        ProblemDetailsEnrichment.AddCorrelation(problem, context);

        // The response may already have a content type from the challenge; the writer sets problem+json.
        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
        });
    }
}
