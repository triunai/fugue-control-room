namespace Fugue.Infrastructure.Persistence.Seeding;

public interface IDevSinkSeeder
{
    /// <summary>
    /// Makes sure a <c>claim.*</c> webhook subscription points at the Development sink (<paramref name="sinkUrl"/>) and that the sink trusts its secret, so the
    /// sink tab shows claim events during a demo. Idempotent: an existing subscription is reused (its URL is corrected when the API moved port) and its
    /// secret is read back, because the sink forgets its secret on every restart. Returns true when the subscription was created.
    /// </summary>
    Task<bool> SeedAsync(string sinkUrl, CancellationToken ct);
}
