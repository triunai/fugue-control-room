using System.Net;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>Resolves a host name to addresses. A seam so tests can simulate a name that changes its answer (DNS rebinding).</summary>
internal interface IHostResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct);
}

internal sealed class DnsHostResolver : IHostResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Dns.GetHostAddressesAsync(host, ct);
}
