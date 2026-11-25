using System.Net;
using FluentResults;
using Fugue.Application.Features.Webhooks;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>
/// Subscribe-time half of the SSRF guard (ADR-011): https only (http only with <c>AllowInsecureLocal</c>), no credentials in the URL, and every address the
/// host resolves to must be allowed by <see cref="NetworkGuard"/>. The connect-time half (`GuardedConnect`) repeats the check on the address it
/// actually dials, which is what defeats DNS rebinding; this one gives the admin an immediate, early error. Reasons never echo resolved addresses.
/// </summary>
internal sealed class WebhookUrlGuard(IHostResolver resolver, IOptions<WebhookOptions> options) : IWebhookUrlGuard
{
    public async Task<Result> CheckAsync(string url, CancellationToken ct)
    {
        bool allowLocal = options.Value.AllowInsecureLocal;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0)
        {
            return Result.Fail(WebhookErrors.UrlRejected("Use an absolute URL without credentials or a fragment."));
        }

        if (uri.Scheme != Uri.UriSchemeHttps && !(allowLocal && uri.Scheme == Uri.UriSchemeHttp))
        {
            return Result.Fail(WebhookErrors.UrlRejected("Only https URLs are allowed."));
        }

        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(uri.IdnHost.Trim('[', ']'), out IPAddress? literal) ? [literal] : await resolver.ResolveAsync(uri.IdnHost, ct);
        }
        catch (System.Net.Sockets.SocketException)
        {
            return Result.Fail(WebhookErrors.UrlRejected("The host name does not resolve."));
        }

        if (addresses.Length == 0)
        {
            return Result.Fail(WebhookErrors.UrlRejected("The host name does not resolve."));
        }

        return addresses.Any(a => NetworkGuard.IsBlocked(a, allowLocal))
            ? Result.Fail(WebhookErrors.UrlRejected("The host resolves to a private, loopback, link-local or otherwise non-public address."))
            : Result.Ok();
    }
}
