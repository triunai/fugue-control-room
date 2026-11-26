namespace Fugue.Api.Http;

public static class RequestBodyExtensions
{
    /// <summary>
    /// The raw request body, read as bytes (a webhook signature covers the exact bytes, so the body must not be re-serialized). Reads at most
    /// <paramref name="maxBytes"/> + 1 bytes: a longer body comes back one byte over the limit so the validator can refuse it without buffering it all.
    /// </summary>
    public static async Task<byte[]> ReadBodyAsync(this HttpRequest request, int maxBytes, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while (buffer.Length <= maxBytes && (read = await request.Body.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, maxBytes + 1 - buffer.Length)), ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
