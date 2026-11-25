using System.Xml.Linq;
using Fugue.Domain.Ids;
using Fugue.Domain.Webhooks;
using Fugue.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>
/// The Data Protection key ring, stored in <c>data_protection_keys</c> so every API instance (and every test host) shares one ring (ADR-011, D-038).
/// <see cref="IXmlRepository"/> is synchronous, so this blocks on the database; Data Protection caches the ring, so the calls are rare. Each call uses its
/// own scope and context: the key ring must never share a change tracker or a transaction with a request. The keys are NOT encrypted at rest here;
/// production should add <c>ProtectKeysWith*</c> (certificate or a key service), see the runbook.
/// </summary>
internal sealed class DatabaseXmlRepository(IServiceScopeFactory scopes) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using IServiceScope scope = scopes.CreateScope();
        FugueDbContext db = scope.ServiceProvider.GetRequiredService<FugueDbContext>();
        return db.DataProtectionKeys.AsNoTracking().OrderBy(k => k.CreatedAt).ThenBy(k => k.Id).Select(k => k.Xml).ToList()
            .Select(xml => XElement.Parse(xml)).ToList();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using IServiceScope scope = scopes.CreateScope();
        FugueDbContext db = scope.ServiceProvider.GetRequiredService<FugueDbContext>();
        db.DataProtectionKeys.Add(new DataProtectionKeyRecord
        {
            Id = DataProtectionKeyId.New(),
            FriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? "key-" + Guid.NewGuid().ToString("N") : friendlyName,
            Xml = element.ToString(SaveOptions.DisableFormatting),
        });
        db.SaveChanges(); // stamped by the auditable interceptor; the entity is [NoAudit]
    }
}
