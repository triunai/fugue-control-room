using System.Text;
using FluentResults;
using Fugue.Api.Http;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Results;
using Mediator;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;

namespace Fugue.Api.Controllers;

/// <summary>
/// Base class for every controller. It owns the HTTP plumbing so actions stay one-line dispatches:
/// <c>FluentResults.Result&lt;T&gt;</c> to response or RFC 9457 problem, ETag / <c>If-Match</c>, keyset pagination headers,
/// the idempotency key, the current user, and mediator dispatch.
/// </summary>
/// <remarks>
/// Controllers deriving from this class declare no constructor. Every collaborator is resolved lazily from
/// <c>HttpContext.RequestServices</c>, so adding a cross-cutting concern never touches a derived class.
/// This type is a critical seam (see AGENTS.md review gate matrix).
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private const string ErrorTypeBase = "https://fugue.dev/errors/";

    private ISender? _sender;
    private ICurrentUser? _currentUser;

    /// <summary>The mediator, resolved on first use from the request scope.</summary>
    protected ISender Sender => _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    /// <summary>The authenticated caller, resolved on first use from the request scope.</summary>
    protected ICurrentUser CurrentUser =>
        _currentUser ??= HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

    /// <summary>
    /// The <c>Idempotency-Key</c> request header, or null when absent or blank. This base class only
    /// exposes the value; storing keys and replaying responses is the job of a dedicated idempotency
    /// filter that runs before the action.
    /// </summary>
    protected string? IdempotencyKey
    {
        get
        {
            string? value = Request.Headers["Idempotency-Key"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    /// <summary>
    /// The version in the <c>If-Match</c> header (<c>"5"</c> or <c>W/"5"</c>), or null when the header is
    /// absent, <c>*</c>, or not a number. Handlers decide whether a missing version is a
    /// <see cref="ErrorType.PreconditionRequired"/> failure.
    /// </summary>
    protected long? IfMatchVersion
    {
        get
        {
            string? raw = Request.Headers.IfMatch.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            string tag = raw.Split(',')[0].Trim();
            if (tag.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            {
                tag = tag[2..];
            }

            tag = tag.Trim('"');
            return long.TryParse(tag, out long version) ? version : null;
        }
    }

    // Return types are ActionResult<T> (not IActionResult) so OpenAPI generators infer the 200/201 body
    // schema from the action signature without a [ProducesResponseType] on every action. Failures are
    // ProblemResult, which is an ActionResult, so the one-liner action style is unchanged.
    //
    // Mediator keeps ICommand<T>, IQuery<T> and IRequest<T> as separate hierarchies, so each helper has an
    // overload per hierarchy; all of them funnel into one private mapper.

    /// <summary>200 with the value as the body, plus an <c>ETag</c> when the value is <see cref="IVersioned"/>.</summary>
    protected async Task<ActionResult<T>> Send<T>(IRequest<Result<T>> request, CancellationToken ct) =>
        ToOk(await Sender.Send(request, ct));

    /// <inheritdoc cref="Send{T}(IRequest{Result{T}}, CancellationToken)"/>
    protected async Task<ActionResult<T>> Send<T>(ICommand<Result<T>> command, CancellationToken ct) =>
        ToOk(await Sender.Send(command, ct));

    /// <inheritdoc cref="Send{T}(IRequest{Result{T}}, CancellationToken)"/>
    protected async Task<ActionResult<T>> Send<T>(IQuery<Result<T>> query, CancellationToken ct) =>
        ToOk(await Sender.Send(query, ct));

    /// <summary>201 with a <c>Location</c> built from <paramref name="actionName"/> and an <c>ETag</c> when versioned.</summary>
    protected async Task<ActionResult<T>> SendCreated<T>(
        IRequest<Result<T>> request,
        string actionName,
        Func<T, object> routeValues,
        CancellationToken ct) =>
        ToCreated(await Sender.Send(request, ct), actionName, routeValues);

    /// <inheritdoc cref="SendCreated{T}(IRequest{Result{T}}, string, Func{T, object}, CancellationToken)"/>
    protected async Task<ActionResult<T>> SendCreated<T>(
        ICommand<Result<T>> command,
        string actionName,
        Func<T, object> routeValues,
        CancellationToken ct) =>
        ToCreated(await Sender.Send(command, ct), actionName, routeValues);

    /// <summary>204 on success.</summary>
    protected async Task<IActionResult> SendNoContent(IRequest<Result> request, CancellationToken ct) =>
        ToNoContent(await Sender.Send(request, ct));

    /// <inheritdoc cref="SendNoContent(IRequest{Result}, CancellationToken)"/>
    protected async Task<IActionResult> SendNoContent(ICommand<Result> command, CancellationToken ct) =>
        ToNoContent(await Sender.Send(command, ct));

    /// <summary>
    /// 200 with the items array as the body. When another page exists, adds <c>X-Next-Cursor</c> and an
    /// RFC 8288 <c>Link: &lt;url&gt;; rel="next"</c> built from the current request URL.
    /// </summary>
    protected async Task<ActionResult<IReadOnlyList<T>>> SendPaged<T>(
        IRequest<Result<PagedResult<T>>> request,
        CancellationToken ct) =>
        ToPage(await Sender.Send(request, ct));

    /// <inheritdoc cref="SendPaged{T}(IRequest{Result{PagedResult{T}}}, CancellationToken)"/>
    protected async Task<ActionResult<IReadOnlyList<T>>> SendPaged<T>(
        IQuery<Result<PagedResult<T>>> query,
        CancellationToken ct) =>
        ToPage(await Sender.Send(query, ct));

    private ActionResult<T> ToOk<T>(Result<T> result)
    {
        if (result.IsFailed)
        {
            return Problem(result.Errors[0]);
        }

        SetETag(result.Value);
        return Ok(result.Value);
    }

    private ActionResult<T> ToCreated<T>(Result<T> result, string actionName, Func<T, object> routeValues)
    {
        if (result.IsFailed)
        {
            return Problem(result.Errors[0]);
        }

        SetETag(result.Value);
        return CreatedAtAction(actionName, routeValues(result.Value), result.Value);
    }

    private IActionResult ToNoContent(Result result) =>
        result.IsFailed ? Problem(result.Errors[0]) : NoContent();

    private ActionResult<IReadOnlyList<T>> ToPage<T>(Result<PagedResult<T>> result)
    {
        if (result.IsFailed)
        {
            return Problem(result.Errors[0]);
        }

        PagedResult<T> page = result.Value;
        if (page.NextCursor is not null)
        {
            Response.Headers["X-Next-Cursor"] = page.NextCursor;
            Response.Headers.Append("Link", $"<{BuildNextLink(page.NextCursor)}>; rel=\"next\"");
        }

        return Ok(page.Items);
    }

    /// <summary>
    /// Maps a failure to an RFC 9457 problem. A <see cref="FugueError"/> supplies its own type, code and
    /// per-field messages; any other <see cref="IError"/> is treated as an unexpected failure and its
    /// message is not exposed.
    /// </summary>
    protected ActionResult Problem(IError error)
    {
        FugueError fugue = error as FugueError
            ?? FugueErrors.Failure("unexpected", "An unexpected error occurred.");
        int status = MapStatus(fugue.Type);
        string type = ErrorTypeBase + ToKebab(fugue.Code);

        ProblemDetails problem;
        if (fugue.Type == ErrorType.Validation)
        {
            var modelState = new ModelStateDictionary();
            foreach ((string field, string[] messages) in fugue.Fields ?? new Dictionary<string, string[]>())
            {
                foreach (string message in messages)
                {
                    modelState.AddModelError(field, message);
                }
            }

            problem = ProblemDetailsFactory.CreateValidationProblemDetails(
                HttpContext, modelState, status, fugue.Message, type);
        }
        else
        {
            problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, fugue.Message, type);
        }

        problem.Extensions["code"] = fugue.Code;
        ProblemDetailsEnrichment.AddCorrelation(problem, HttpContext);
        if (CurrentUser.TenantId is { } tenantId)
        {
            problem.Extensions["tenantId"] = tenantId;
        }

        return new ProblemResult(problem);
    }

    // DECISION (owner): which HTTP status does each ErrorType map to? Suggested mapping:
    //
    //   Failure              -> 500  An unclassified failure is a server fault. Use 400 only if you decide
    //                                that handlers return Failure for bad input (prefer Validation for that).
    //   Validation           -> 400  (422 is the alternative; 400 matches ValidationProblemDetails defaults.)
    //   NotFound             -> 404
    //   Conflict             -> 409
    //   Unauthorized         -> 401  Caller is not authenticated.
    //   Forbidden            -> 403  Caller is authenticated but lacks permission.
    //   PreconditionFailed   -> 412  If-Match did not match the current version.
    //   PreconditionRequired -> 428  The operation demands If-Match and it was missing.
    //
    // Trade-off on NotFound versus cross-tenant access: if tenant A asks for tenant B's resource, answering
    // 403 confirms that the resource exists. Answering 404 leaks nothing, because the response is the same
    // as for an id that never existed. The global query filter already makes B's rows invisible to A, so
    // handlers naturally return NotFound; keep it that way and never turn a cross-tenant miss into Forbidden.
    // Reserve 403 for same-tenant permission checks (role or policy), where existence is not a secret.
    protected static int MapStatus(ErrorType type)
    {
        throw new NotImplementedException("Owner: map ErrorType -> HTTP status");
    }

    private void SetETag<T>(T value)
    {
        if (value is IVersioned versioned)
        {
            Response.Headers.ETag = $"\"{versioned.Version}\"";
        }
    }

    private string BuildNextLink(string cursor)
    {
        var query = new List<KeyValuePair<string, StringValues>>();
        foreach (KeyValuePair<string, StringValues> pair in Request.Query)
        {
            if (!string.Equals(pair.Key, "cursor", StringComparison.OrdinalIgnoreCase))
            {
                query.Add(pair);
            }
        }

        query.Add(new KeyValuePair<string, StringValues>("cursor", cursor));
        return UriHelper.BuildAbsolute(
            Request.Scheme, Request.Host, Request.PathBase, Request.Path, QueryString.Create(query));
    }

    /// <summary>"product.not_found" and "ProductNotFound" both become "product-not-found".</summary>
    private static string ToKebab(string code)
    {
        var builder = new StringBuilder(code.Length + 4);
        bool pendingDash = false;
        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            if (!char.IsLetterOrDigit(c))
            {
                pendingDash = builder.Length > 0;
                continue;
            }

            bool boundary = char.IsUpper(c) && i > 0 && char.IsLetterOrDigit(code[i - 1]) && !char.IsUpper(code[i - 1]);
            if ((pendingDash || boundary) && builder.Length > 0)
            {
                builder.Append('-');
            }

            pendingDash = false;
            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
