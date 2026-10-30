using System.Net;
using Encurtador.IntegrationTests.Support;

namespace Encurtador.IntegrationTests;

public class CustomCodeTests(LinksApiFactory factory) : IClassFixture<LinksApiFactory>
{
    private readonly HttpClient client = factory.CreateNoRedirectClient();

    [Fact]
    public async Task Custom_code_is_used_and_redirects()
    {
        var link = await client.CreateLinkOkAsync("https://exemplo.com/natal", "promo-natal");

        Assert.Equal("promo-natal", link.Code);
        var response = await client.GetAsync("/promo-natal", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    [Fact]
    public async Task Repeated_custom_code_is_in_use_and_original_is_kept()
    {
        await client.CreateLinkOkAsync("https://exemplo.com/original", "repetido");

        var response = await client.CreateLinkAsync("https://exemplo.com/outro", "repetido");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.ReadJsonAsync();
        Assert.Equal("/problems/code-in-use", body.GetProperty("type").GetString());
        Assert.Contains("\"repetido\"", body.GetProperty("detail").GetString());

        var redirect = await client.GetAsync("/repetido", TestContext.Current.CancellationToken);
        Assert.Equal("https://exemplo.com/original", redirect.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Codes_differing_only_in_case_are_both_created()
    {
        Assert.Equal(HttpStatusCode.Created, (await client.CreateLinkAsync("https://exemplo.com", "Caixa")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.CreateLinkAsync("https://exemplo.com", "caixa")).StatusCode);
    }

    [Theory]
    [InlineData("health", "O código \"health\" está indisponível.")]
    [InlineData("abc", "O código deve ter entre 4 e 20 caracteres.")]
    [InlineData("a b c", "O código só pode conter letras sem acento (A–Z, a–z), dígitos (0–9), hífen (-) e sublinhado (_).")]
    public async Task Invalid_custom_code_returns_validation_problem(string code, string message)
    {
        var response = await client.CreateLinkAsync("https://exemplo.com", code);

        await CreateLinkTests.AssertValidationProblem(response, "customCode", message);
    }
}
