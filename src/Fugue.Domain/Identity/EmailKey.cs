namespace Fugue.Domain.Identity;

/// <summary>The one canonical form of an email: trimmed and lower-invariant. Storage, uniqueness and login all use it.</summary>
public static class EmailKey
{
    public static string Canonicalize(string email) => email.Trim().ToLowerInvariant();
}
