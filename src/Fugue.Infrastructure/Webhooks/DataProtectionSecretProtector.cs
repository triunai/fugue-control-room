using Fugue.Application.Features.Webhooks.Repos;
using Microsoft.AspNetCore.DataProtection;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>Encrypts subscription secrets with ASP.NET Core Data Protection (ADR-011). The purpose string is versioned: change it and every stored secret becomes unreadable.</summary>
internal sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    public const string Purpose = "Fugue.Webhooks.Secret.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
