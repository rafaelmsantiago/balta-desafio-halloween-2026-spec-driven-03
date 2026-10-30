using System.Net.Http.Json;
using System.Text.Json;

namespace Encurtador.IntegrationTests.Support;

public static class HttpExtensions
{
    public const string ManagementKeyHeader = "X-Management-Key";

    public static Task<HttpResponseMessage> CreateLinkAsync(
        this HttpClient client,
        string destinationUrl,
        string? customCode = null,
        string? expiresAt = null) =>
        client.PostAsJsonAsync("/api/links", new { destinationUrl, customCode, expiresAt }, TestContext.Current.CancellationToken);

    public static async Task<CreatedLink> CreateLinkOkAsync(
        this HttpClient client,
        string destinationUrl = "https://exemplo.com/destino",
        string? customCode = null,
        string? expiresAt = null)
    {
        var response = await client.CreateLinkAsync(destinationUrl, customCode, expiresAt);
        response.EnsureSuccessStatusCode();
        var body = await response.ReadJsonAsync();
        return new CreatedLink(body.GetProperty("code").GetString()!, body.GetProperty("managementKey").GetString()!);
    }

    public static Task<HttpResponseMessage> GetStatsAsync(this HttpClient client, string code, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/links/{code}/stats");
        if (key is not null)
        {
            request.Headers.Add(ManagementKeyHeader, key);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }
}

public sealed record CreatedLink(string Code, string ManagementKey);
