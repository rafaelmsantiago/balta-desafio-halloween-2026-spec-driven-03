using System.Net;
using Encurtador.IntegrationTests.Support;

namespace Encurtador.IntegrationTests;

public class RedirectTests(LinksApiFactory factory) : IClassFixture<LinksApiFactory>
{
    private readonly HttpClient client = factory.CreateNoRedirectClient();

    [Fact]
    public async Task Valid_link_redirects_with_302_and_no_store()
    {
        var link = await client.CreateLinkOkAsync("https://exemplo.com/destino");

        var response = await client.GetAsync($"/{link.Code}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://exemplo.com/destino", response.Headers.Location?.OriginalString);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Unknown_code_returns_not_found_problem()
    {
        var response = await client.GetAsync("/naoexiste1", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.ReadJsonAsync();
        Assert.Equal("/problems/link-not-found", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Reserved_path_is_not_found()
    {
        var response = await client.GetAsync("/api", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
