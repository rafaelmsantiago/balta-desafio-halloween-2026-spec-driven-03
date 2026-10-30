using System.Net;
using Encurtador.IntegrationTests.Support;

namespace Encurtador.IntegrationTests;

public class PersistenceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Links_and_counters_survive_application_restart()
    {
        var path = LinksApiFactory.NewDatabasePath();
        try
        {
            CreatedLink link;
            string createdAt;
            await using (var first = new LinksApiFactory(path))
            {
                var client = first.CreateNoRedirectClient();
                link = await client.CreateLinkOkAsync("https://exemplo.com/persistente");
                for (var i = 0; i < 3; i++)
                {
                    await client.GetAsync($"/{link.Code}", Ct);
                }

                createdAt = (await (await client.GetStatsAsync(link.Code, link.ManagementKey)).ReadJsonAsync())
                    .GetProperty("createdAt").GetString()!;
            }

            await using var second = new LinksApiFactory(path);
            var restarted = second.CreateNoRedirectClient();

            var redirect = await restarted.GetAsync($"/{link.Code}", Ct);
            Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
            Assert.Equal("https://exemplo.com/persistente", redirect.Headers.Location?.OriginalString);

            var stats = await (await restarted.GetStatsAsync(link.Code, link.ManagementKey)).ReadJsonAsync();
            Assert.Equal(4, stats.GetProperty("accessCount").GetInt64());
            Assert.Equal("https://exemplo.com/persistente", stats.GetProperty("destinationUrl").GetString());
            Assert.Equal(createdAt, stats.GetProperty("createdAt").GetString());
        }
        finally
        {
            LinksApiFactory.DeleteDatabase(path);
        }
    }
}
