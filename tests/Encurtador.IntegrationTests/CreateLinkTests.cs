using System.Net;
using System.Text;
using Encurtador.IntegrationTests.Support;

namespace Encurtador.IntegrationTests;

public class CreateLinkTests(LinksApiFactory factory) : IClassFixture<LinksApiFactory>
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";
    private const string Url = "https://exemplo.com/artigos/2026/um-texto-bem-longo";

    private readonly HttpClient client = factory.CreateNoRedirectClient();

    [Fact]
    public async Task Creates_link_with_generated_code()
    {
        var response = await client.CreateLinkAsync(Url);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        var code = body.GetProperty("code").GetString()!;
        Assert.Equal(7, code.Length);
        Assert.All(code, c => Assert.Contains(c, Alphabet));
        Assert.Equal($"/api/links/{code}/stats", response.Headers.Location?.OriginalString);
        Assert.EndsWith($"/{code}", body.GetProperty("shortUrl").GetString());
        Assert.Equal(Url, body.GetProperty("destinationUrl").GetString());
        Assert.EndsWith("Z", body.GetProperty("createdAt").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("expiresAt").ValueKind);
        Assert.Equal(43, body.GetProperty("managementKey").GetString()!.Length);
    }

    [Fact]
    public async Task Non_http_scheme_returns_validation_problem()
    {
        var response = await client.CreateLinkAsync("ftp://exemplo.com");

        await AssertValidationProblem(response, "destinationUrl", "A URL de destino deve usar o esquema http ou https.");
    }

    [Fact]
    public async Task Url_over_2048_characters_returns_validation_problem()
    {
        var response = await client.CreateLinkAsync("https://exemplo.com/" + new string('a', 2049 - 20));

        await AssertValidationProblem(response, "destinationUrl", "A URL de destino deve ter no máximo 2048 caracteres.");
    }

    [Fact]
    public async Task Malformed_json_returns_problem_details()
    {
        var response = await client.PostAsync(
            "/api/links",
            new StringContent("{ não é json", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.ReadJsonAsync();
        Assert.Equal("A requisição não pôde ser lida.", body.GetProperty("title").GetString());
    }

    internal static async Task AssertValidationProblem(HttpResponseMessage response, string field, string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.ReadJsonAsync();
        Assert.Equal("Um ou mais campos são inválidos.", body.GetProperty("title").GetString());
        var messages = body.GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString());
        Assert.Contains(message, messages);
    }
}
