using System.Net;
using Encurtador.IntegrationTests.Support;

namespace Encurtador.IntegrationTests;

/// <summary>Interface de testes (Scalar) e documento OpenAPI, disponíveis em Development (research.md D12).</summary>
public class ApiReferenceTests(LinksApiFactory factory) : IClassFixture<LinksApiFactory>
{
    private readonly HttpClient client = factory.CreateNoRedirectClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OpenApi_document_describes_the_three_endpoints()
    {
        var response = await client.GetAsync("/openapi/v1.json", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = (await response.ReadJsonAsync()).GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/links", out var create));
        Assert.True(create.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/links/{code}/stats", out _));
        Assert.True(paths.TryGetProperty("/{code}", out _));
    }

    [Fact]
    public async Task Scalar_interface_is_served()
    {
        // "/scalar" redireciona para "/scalar/", que serve a página.
        var response = await client.GetAsync("/scalar/", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Scalar_cannot_be_taken_as_custom_code()
    {
        var response = await client.CreateLinkAsync("https://exemplo.com", "scalar");

        await CreateLinkTests.AssertValidationProblem(response, "customCode", "O código \"scalar\" está indisponível.");
    }
}
