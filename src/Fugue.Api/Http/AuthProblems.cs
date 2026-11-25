using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Http;

/// <summary>Writes framework-level auth failures (no handler ran) as the same problem+json the handlers produce, with code and correlation id.</summary>
public static class AuthProblems
{
    /// <summary>The caller's address and user agent, truncated; recorded on audit rows only.</summary>
    public static Fugue.Application.Abstractions.ClientInfo ClientOf(HttpContext context)
    {
        string ua = context.Request.Headers.UserAgent.ToString();
        return new(context.Connection.RemoteIpAddress?.ToString(), ua.Length == 0 ? null : ua[..Math.Min(ua.Length, 256)]);
    }

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
