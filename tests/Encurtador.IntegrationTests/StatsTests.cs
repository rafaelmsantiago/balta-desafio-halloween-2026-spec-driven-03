using System.Net;
using Encurtador.IntegrationTests.Support;

namespace Encurtador.IntegrationTests;

public class StatsTests(LinksApiFactory factory) : IClassFixture<LinksApiFactory>
{
    private static readonly string[] LinkDataProperties = ["accessCount", "destinationUrl", "createdAt", "expiresAt"];

    private readonly HttpClient client = factory.CreateNoRedirectClient();

    [Fact]
    public async Task New_link_has_zero_accesses()
    {
        var link = await client.CreateLinkOkAsync("https://exemplo.com/stats");

        var response = await client.GetStatsAsync(link.Code, link.ManagementKey);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(link.Code, body.GetProperty("code").GetString());
        Assert.Equal("https://exemplo.com/stats", body.GetProperty("destinationUrl").GetString());
        Assert.EndsWith("Z", body.GetProperty("createdAt").GetString());
        Assert.False(body.GetProperty("isExpired").GetBoolean());
        Assert.Equal(0, body.GetProperty("accessCount").GetInt64());
    }

    [Fact]
    public async Task Counts_each_redirect()
    {
        var link = await client.CreateLinkOkAsync();
        for (var i = 0; i < 5; i++)
        {
            await client.GetAsync($"/{link.Code}", TestContext.Current.CancellationToken);
        }

        var body = await (await client.GetStatsAsync(link.Code, link.ManagementKey)).ReadJsonAsync();

        Assert.Equal(5, body.GetProperty("accessCount").GetInt64());
    }

    [Fact]
    public async Task Missing_key_is_unauthorized_without_link_data()
    {
        var link = await client.CreateLinkOkAsync();

        var response = await client.GetStatsAsync(link.Code, null);

        await AssertUnauthorized(response);
        Assert.Equal("ManagementKey", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Wrong_key_is_unauthorized()
    {
        var link = await client.CreateLinkOkAsync();

        await AssertUnauthorized(await client.GetStatsAsync(link.Code, "chave-errada"));
    }

    [Fact]
    public async Task Key_of_another_link_is_unauthorized()
    {
        var first = await client.CreateLinkOkAsync();
        var second = await client.CreateLinkOkAsync();

        await AssertUnauthorized(await client.GetStatsAsync(second.Code, first.ManagementKey));
    }

    [Fact]
    public async Task Code_used_as_key_is_unauthorized()
    {
        var link = await client.CreateLinkOkAsync();

        await AssertUnauthorized(await client.GetStatsAsync(link.Code, link.Code));
    }

    [Fact]
    public async Task Key_used_as_code_is_not_found_without_link_data()
    {
        var link = await client.CreateLinkOkAsync();

        var response = await client.GetStatsAsync(link.ManagementKey, link.ManagementKey);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertNoLinkData(await response.ReadJsonAsync());
    }

    [Fact]
    public async Task Unknown_code_is_not_found()
    {
        var response = await client.GetStatsAsync("naoexiste1", "qualquer");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Key_in_query_string_is_not_accepted()
    {
        var link = await client.CreateLinkOkAsync();

        var response = await client.GetAsync(
            $"/api/links/{link.Code}/stats?managementKey={link.ManagementKey}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task AssertUnauthorized(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.ReadJsonAsync();
        Assert.Equal("/problems/invalid-management-key", body.GetProperty("type").GetString());
        AssertNoLinkData(body);
    }

    private static void AssertNoLinkData(System.Text.Json.JsonElement body)
    {
        foreach (var property in LinkDataProperties)
        {
            Assert.False(body.TryGetProperty(property, out _), $"A resposta não deveria conter '{property}'.");
        }
    }
}
