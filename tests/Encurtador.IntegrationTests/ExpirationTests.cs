using System.Net;
using Encurtador.IntegrationTests.Support;

namespace Encurtador.IntegrationTests;

public class ExpirationTests(LinksApiFactory factory) : IClassFixture<LinksApiFactory>
{
    private readonly HttpClient client = factory.CreateNoRedirectClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Link_stops_redirecting_and_counting_after_expiration()
    {
        var expiresAt = factory.Time.GetUtcNow().AddMinutes(1).ToOffset(TimeSpan.FromHours(-3));
        var response = await client.CreateLinkAsync("https://exemplo.com/campanha", "expira-1", expiresAt.ToString("yyyy-MM-ddTHH:mm:sszzz"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadJsonAsync();
        Assert.Equal(expiresAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"), created.GetProperty("expiresAt").GetString());
        var key = created.GetProperty("managementKey").GetString()!;

        Assert.Equal(HttpStatusCode.Found, (await client.GetAsync("/expira-1", Ct)).StatusCode);

        factory.Time.Advance(TimeSpan.FromMinutes(1)); // instante exato da expiração

        var expired = await client.GetAsync("/expira-1", Ct);
        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);
        Assert.Equal("application/problem+json", expired.Content.Headers.ContentType?.MediaType);
        Assert.Equal("/problems/link-expired", (await expired.ReadJsonAsync()).GetProperty("type").GetString());
        Assert.Equal(HttpStatusCode.Gone, (await client.GetAsync("/expira-1", Ct)).StatusCode);

        var stats = await (await client.GetStatsAsync("expira-1", key)).ReadJsonAsync();
        Assert.True(stats.GetProperty("isExpired").GetBoolean());
        Assert.Equal(1, stats.GetProperty("accessCount").GetInt64());

        // O código de um link expirado continua ocupado (FR-004).
        var reuse = await client.CreateLinkAsync("https://exemplo.com/outro", "expira-1");
        Assert.Equal(HttpStatusCode.Conflict, reuse.StatusCode);
        Assert.Equal("/problems/code-in-use", (await reuse.ReadJsonAsync()).GetProperty("type").GetString());
    }

    [Fact]
    public async Task Past_expiration_returns_validation_problem()
    {
        var response = await client.CreateLinkAsync("https://exemplo.com", expiresAt: "2020-01-01T00:00:00Z");

        await CreateLinkTests.AssertValidationProblem(response, "expiresAt", "A data de expiração precisa estar no futuro.");
    }

    [Fact]
    public async Task All_invalid_fields_are_listed_in_one_validation_problem()
    {
        var response = await client.CreateLinkAsync("ftp://x.com", "a b", "2020-01-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.Equal(
            ["customCode", "destinationUrl", "expiresAt"],
            errors.EnumerateObject().Select(p => p.Name).Order());
    }
}
