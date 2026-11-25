using Fugue.Api.Auth;
using Fugue.Application.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace Fugue.Api.Auditing;

/// <summary>
/// What the current HTTP request adds to an audit row: method, route template (never the concrete URL, which may hold ids and query
/// strings), the caller's address and user agent, and the <c>[HasPermission]</c> codes on the endpoint that authorized the call.
/// </summary>
public sealed class HttpAuditRequestInfo(IHttpContextAccessor accessor) : IAuditRequestInfo
{
    private const int MaxUserAgent = 256;

    public AuditRequestInfo Current
    {
        get
        {
            HttpContext? http = accessor.HttpContext;
            if (http is null)
            {
                return AuditRequestInfo.None;
            }

            Endpoint? endpoint = http.GetEndpoint();
            string[] permissions = endpoint?.Metadata.GetOrderedMetadata<HasPermissionAttribute>().Select(a => a.Permission).Distinct().ToArray() ?? [];
            string agent = http.Request.Headers.UserAgent.ToString();
            return new AuditRequestInfo(
                http.Request.Method,
                (endpoint as RouteEndpoint)?.RoutePattern.RawText?.ToLowerInvariant(), // [controller] expands with its class casing; templates are case-insensitive
                http.Connection.RemoteIpAddress?.ToString(),
                agent.Length == 0 ? null : agent[..Math.Min(agent.Length, MaxUserAgent)],
                permissions.Length == 0 ? null : string.Join(',', permissions));
        }
    }
}
