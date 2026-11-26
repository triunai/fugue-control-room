using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>
/// Connect-time half of the SSRF guard (ADR-011, D-038): the <see cref="SocketsHttpHandler.ConnectCallback"/> resolves the host itself, refuses the
/// connection when ANY answer is a blocked address, and dials the verified address directly. Because the connection goes to the address that was
/// checked, a name that answered with a public address at subscribe time and a private one now (DNS rebinding) cannot reach the private network.
/// TLS still validates the certificate against the host name of the request, so the pinned address does not weaken the handshake.
/// </summary>
internal sealed class GuardedConnect(IHostResolver resolver, IOptions<WebhookOptions> options)
{
    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        string host = context.DnsEndPoint.Host;
        bool allowLocal = options.Value.AllowInsecureLocal;
        IPAddress[] addresses = IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? literal) ? [literal] : await resolver.ResolveAsync(host, ct);

        if (addresses.Length == 0 || addresses.Any(a => NetworkGuard.IsBlocked(a, allowLocal)))
        {
            throw new HttpRequestException("The destination is not allowed.");
        }

        Exception? last = null;
        foreach (IPAddress address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException)
            {
                socket.Dispose();
                last = ex;
            }
        }

        throw new HttpRequestException("Could not connect to the destination.", last);
    }
}
