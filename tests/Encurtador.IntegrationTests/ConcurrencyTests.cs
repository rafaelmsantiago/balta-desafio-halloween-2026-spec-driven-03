using System.Diagnostics;
using System.Net;
using Encurtador.IntegrationTests.Support;

namespace Encurtador.IntegrationTests;

public class ConcurrencyTests(LinksApiFactory factory) : IClassFixture<LinksApiFactory>
{
    private readonly HttpClient client = factory.CreateNoRedirectClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Thousand_concurrent_accesses_are_all_counted()
    {
        var link = await client.CreateLinkOkAsync();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 1000).Select(_ => client.GetAsync($"/{link.Code}", Ct)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Found, r.StatusCode));
        Assert.Equal(1000, await AccessCountAsync(link));
    }

    [Fact]
    public async Task Counter_never_decreases_while_accesses_happen()
    {
        var link = await client.CreateLinkOkAsync();
        var accesses = Task.WhenAll(Enumerable.Range(0, 200).Select(_ => client.GetAsync($"/{link.Code}", Ct)));

        var previous = 0L;
        while (!accesses.IsCompleted)
        {
            var current = await AccessCountAsync(link);
            Assert.True(current >= previous, $"Contador diminuiu de {previous} para {current}.");
            previous = current;
        }

        await accesses;
        Assert.Equal(200, await AccessCountAsync(link));
    }

    [Fact]
    public async Task Redirect_p95_latency_is_under_one_second()
    {
        // O TestServer mede só aplicação e banco, sem rede: serve como limite de regressão.
        // O SC-002 completo é validado no cenário manual do quickstart.
        var link = await client.CreateLinkOkAsync();
        var durations = new List<TimeSpan>();
        for (var i = 0; i < 200; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await client.GetAsync($"/{link.Code}", Ct);
            durations.Add(stopwatch.Elapsed);
            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        }

        var p95 = durations.Order().ElementAt((int)Math.Ceiling(durations.Count * 0.95) - 1);
        Assert.True(p95 < TimeSpan.FromSeconds(1), $"p95 = {p95.TotalMilliseconds} ms");
    }

    [Fact]
    public async Task Concurrent_requests_for_the_same_custom_code_have_exactly_one_winner()
    {
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(i => client.CreateLinkAsync($"https://exemplo.com/{i}", "corrida-1")));

        var winners = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToList();
        Assert.Single(winners);
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var winnerUrl = (await winners[0].ReadJsonAsync()).GetProperty("destinationUrl").GetString();
        var redirect = await client.GetAsync("/corrida-1", Ct);
        Assert.Equal(winnerUrl, redirect.Headers.Location?.OriginalString);
    }

    private async Task<long> AccessCountAsync(CreatedLink link)
    {
        var body = await (await client.GetStatsAsync(link.Code, link.ManagementKey)).ReadJsonAsync();
        return body.GetProperty("accessCount").GetInt64();
    }
}
