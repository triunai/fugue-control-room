using Fugue.Application.Abstractions;
using Fugue.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Identity;

/// <summary>Binds <c>Auth:PasswordHashIterations</c>. The default is the OWASP PBKDF2-HMAC-SHA512 figure; tests lower it.</summary>
public sealed class PasswordOptions
{
    public const int DefaultIterations = 210_000;

    public int PasswordHashIterations { get; set; } = DefaultIterations;
}

/// <summary>
/// The only place passwords are hashed or checked: <see cref="PasswordHasher{TUser}"/> (PBKDF2-HMAC-SHA512, v3 format, salt inside
/// the string). Verifying with no stored hash checks a dummy produced with the same settings, so timing does not reveal
/// whether an account exists.
/// </summary>
public sealed class PasswordVerifier : IPasswordVerifier
{
    private readonly PasswordHasher<User> _hasher;
    private readonly string _dummyHash;

    public PasswordVerifier(IOptions<PasswordOptions> options)
    {
        _hasher = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
        {
            IterationCount = options.Value.PasswordHashIterations,
        }));
        _dummyHash = _hasher.HashPassword(null!, Guid.NewGuid().ToString("N"));
    }

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string? hash, string password)
    {
        PasswordVerificationResult result = _hasher.VerifyHashedPassword(null!, hash ?? _dummyHash, password);
        return hash is not null && result != PasswordVerificationResult.Failed;
    }
}
