using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Customers;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Tests.Persistence;

namespace Fugue.Tests.Identity;

/// <summary>
/// The ownership gates (plan section 7). A "restricted staff" user is STAFF with an explicit CUSTOMERS_ALL deny: it may
/// create, edit and suspend, but only on rows it owns. A miss is always 404, never 403.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class OwnershipTests(PostgresFixture db)
{
    private static string Email() => $"{Guid.NewGuid():N}@example.test";

    private static (string, bool, DateTimeOffset?)[] DenyAll() => [(Permissions.CustomersAll, false, null)];

    private async Task<(HttpClient Client, UserId Id)> Restricted()
    {
        (string email, UserId id) = await db.NewUserAsync([RoleCodes.Staff], DenyAll());
        return (db.ClientAs(email), id);
    }

    private static async Task<CustomerDto> Create(HttpClient client, object? body = null)
    {
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/v1/customers", body ?? new { name = "Owned", email = Email() });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<CustomerDto>())!;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, long? version = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (version is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static async Task AssertCustomerNotFound(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("customer.not_found", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Restricted_staff_creating_a_customer_with_another_owner_still_owns_it()
    {
        (HttpClient alice, UserId aliceId) = await Restricted();
        (_, UserId bobId) = await Restricted();

        CustomerDto created = await Create(alice, new { name = "Mine", email = Email(), userId = bobId.Value });

        Assert.Equal(aliceId, created.OwnerId);
    }

    [Fact]
    public async Task Staff_with_all_may_assign_an_owner_and_sees_every_row()
    {
        (_, UserId bobId) = await Restricted();
        using HttpClient admin = db.ClientAs(DevUsers.Admin);

        CustomerDto created = await Create(admin, new { name = "For Bob", email = Email(), userId = bobId.Value });

        Assert.Equal(bobId, created.OwnerId);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/customers/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Cross_owner_get_update_suspend_reactivate_and_delete_are_all_404()
    {
        (HttpClient alice, _) = await Restricted();
        (HttpClient bob, _) = await Restricted();
        CustomerDto aliceCustomer = await Create(alice);
        string path = $"/api/v1/customers/{aliceCustomer.Id}";

        await AssertCustomerNotFound(await bob.GetAsync(path));
        await AssertCustomerNotFound(await bob.SendAsync(Request(HttpMethod.Put, path, aliceCustomer.Version, new { name = "Hijack", email = aliceCustomer.Email })));
        await AssertCustomerNotFound(await bob.PostAsync(path + "/suspend", null));
        await AssertCustomerNotFound(await bob.PostAsync(path + "/reactivate", null));
        await AssertCustomerNotFound(await bob.SendAsync(Request(HttpMethod.Delete, path, aliceCustomer.Version)));

        // Nothing changed, and the owner can still act.
        CustomerDto after = (await alice.GetFromJsonAsync<CustomerDto>(path))!;
        Assert.Equal("Owned", after.Name);
        Assert.Equal(aliceCustomer.Version, after.Version);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync(path + "/suspend", null)).StatusCode);
    }

    [Fact]
    public async Task A_cross_owner_miss_is_indistinguishable_from_an_absent_id()
    {
        (HttpClient alice, _) = await Restricted();
        (HttpClient bob, _) = await Restricted();
        CustomerDto aliceCustomer = await Create(alice);

        HttpResponseMessage foreign = await bob.GetAsync($"/api/v1/customers/{aliceCustomer.Id}");
        HttpResponseMessage absent = await bob.GetAsync($"/api/v1/customers/{Guid.NewGuid()}");

        Assert.Equal(absent.StatusCode, foreign.StatusCode);
        string Normalize(string s) => System.Text.RegularExpressions.Regex.Replace(s, "[0-9a-f]{8}-[0-9a-f-]{27}|\"(traceId|correlationId)\":\"[^\"]*\"", "X");
        Assert.Equal(Normalize(await absent.Content.ReadAsStringAsync()), Normalize(await foreign.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Cursor_pages_only_ever_contain_the_callers_rows()
    {
        (HttpClient alice, UserId aliceId) = await Restricted();
        (HttpClient bob, _) = await Restricted();
        string token = "own" + Guid.NewGuid().ToString("N")[..8];
        for (int i = 0; i < 5; i++)
        {
            db.Clock.Now = db.Clock.Now.AddSeconds(1);
            await Create(i % 2 == 0 ? alice : bob, new { name = $"{token}-{i}", email = Email() });
        }

        var seen = new List<CustomerDto>();
        string? cursor = null;
        do
        {
            HttpResponseMessage page = await alice.GetAsync($"/api/v1/customers?limit=1&search={token}" + (cursor is null ? "" : $"&cursor={cursor}"));
            seen.AddRange((await page.Content.ReadFromJsonAsync<CustomerDto[]>())!);
            cursor = page.Headers.TryGetValues("X-Next-Cursor", out IEnumerable<string>? next) ? next.Single() : null;
        }
        while (cursor is not null);

        Assert.Equal(3, seen.Count); // alice created #0, #2, #4; bob's rows never appear on any page
        Assert.All(seen, c => Assert.Equal(aliceId, c.OwnerId));
    }

    [Fact]
    public async Task Self_service_sees_and_edits_only_their_own_customer()
    {
        using HttpClient self = db.ClientAs(DevUsers.Self);
        CustomerDto[] mine = (await self.GetFromJsonAsync<CustomerDto[]>("/api/v1/customers?limit=100"))!;
        Assert.NotEmpty(mine);
        Assert.All(mine, c => Assert.NotNull(c.OwnerId));
        Assert.Single(mine.Select(c => c.OwnerId).Distinct());

        using HttpClient self2 = db.ClientAs(DevUsers.Self2);
        await AssertCustomerNotFound(await self2.GetAsync($"/api/v1/customers/{mine[0].Id}"));

        HttpResponseMessage edit = await self.SendAsync(Request(HttpMethod.Put, $"/api/v1/customers/{mine[0].Id}", mine[0].Version, new { name = "Self Edited", email = mine[0].Email.ToUpperInvariant(), phone = (string?)null }));
        Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode); // same canonical email is not a change
    }

    [Fact]
    public async Task Self_service_cannot_change_the_email_staff_can()
    {
        using HttpClient self = db.ClientAs(DevUsers.Self);
        CustomerDto mine = (await self.GetFromJsonAsync<CustomerDto[]>("/api/v1/customers?limit=1"))![0];

        HttpResponseMessage denied = await self.SendAsync(Request(HttpMethod.Put, $"/api/v1/customers/{mine.Id}", mine.Version, new { name = mine.Name, email = Email() }));

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
        Assert.Equal("customer.email_change_forbidden", json.RootElement.GetProperty("code").GetString());

        using HttpClient staff = db.ClientAs(DevUsers.Staff);
        HttpResponseMessage allowed = await staff.SendAsync(Request(HttpMethod.Put, $"/api/v1/customers/{mine.Id}", mine.Version, new { name = mine.Name, email = mine.Email }));
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
    }

    [Fact]
    public async Task Self_service_cannot_create_suspend_or_delete()
    {
        using HttpClient self = db.ClientAs(DevUsers.Self);

        Assert.Equal(HttpStatusCode.Forbidden, (await self.PostAsJsonAsync("/api/v1/customers", new { name = "x", email = Email() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await self.PostAsync($"/api/v1/customers/{Guid.NewGuid()}/suspend", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await self.DeleteAsync($"/api/v1/customers/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task A_support_user_with_the_staff_role_only_through_a_group_has_staff_access()
    {
        using HttpClient support = db.ClientAs(DevUsers.Support);

        Assert.Equal(HttpStatusCode.Created, (await support.PostAsJsonAsync("/api/v1/customers", new { name = "Via group", email = Email() })).StatusCode);
    }
}
