using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Http;

/// <summary>
/// Writes a <see cref="ProblemDetails"/> as <c>application/problem+json</c> through MVC's formatters.
/// The inner <see cref="ObjectResult"/> is created at execution time, after result filters have run:
/// <c>[Produces("application/json")]</c> on the controller would otherwise overwrite the content type
/// of an <see cref="ObjectResult"/> returned from an action. Deriving from <see cref="ActionResult"/>
/// (not just implementing <see cref="IActionResult"/>) lets it convert implicitly to <c>ActionResult&lt;T&gt;</c>.
/// </summary>
internal sealed class ProblemResult(ProblemDetails problem) : ActionResult
{
    public override Task ExecuteResultAsync(ActionContext context)
    {
        var inner = new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { "application/problem+json" },
        };
        return inner.ExecuteResultAsync(context);
    }
}
