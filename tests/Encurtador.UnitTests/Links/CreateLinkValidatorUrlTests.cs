using Encurtador.Application.Links;
using Encurtador.UnitTests.Fakes;

namespace Encurtador.UnitTests.Links;

public class CreateLinkValidatorUrlTests
{
    private const string Required = "A URL de destino é obrigatória.";
    private const string Malformed = "A URL de destino deve ser um endereço absoluto válido, com domínio.";
    private const string Scheme = "A URL de destino deve usar o esquema http ou https.";
    private const string TooLong = "A URL de destino deve ter no máximo 2048 caracteres.";

    private readonly CreateLinkValidator validator = new(new FakeTimeProvider());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_url_is_required(string? url) => AssertSingleError(url, Required);

    [Theory]
    [InlineData("https://")]
    [InlineData("exemplo")]
    [InlineData("http://")]
    [InlineData(" https://exemplo.com")]
    [InlineData("https://exemplo.com ")]
    public void Malformed_url_is_rejected(string url) => AssertSingleError(url, Malformed);

    [Theory]
    [InlineData("ftp://exemplo.com")]
    [InlineData("javascript:alert(1)")]
    public void Non_http_scheme_is_rejected(string url) => AssertSingleError(url, Scheme);

    [Fact]
    public void Url_with_2049_characters_is_rejected() => AssertSingleError(UrlOfLength(2049), TooLong);

    [Fact]
    public void Url_with_exactly_2048_characters_is_accepted() => AssertValid(UrlOfLength(2048));

    [Theory]
    [InlineData("http://exemplo.com")]
    [InlineData("https://exemplo.com/artigos/2026/um-texto-bem-longo")]
    public void Http_and_https_urls_with_host_are_accepted(string url) => AssertValid(url);

    internal static string UrlOfLength(int length)
    {
        const string prefix = "https://exemplo.com/";
        return prefix + new string('a', length - prefix.Length);
    }

    private void AssertSingleError(string? url, string message)
    {
        var result = validator.Validate(new CreateLinkRequest(url, null, null));

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        var error = Assert.Single(result.Errors);
        Assert.Equal("destinationUrl", error.Key);
        Assert.Equal([message], error.Value);
    }

    private void AssertValid(string url)
    {
        var result = validator.Validate(new CreateLinkRequest(url, null, null));

        Assert.True(result.IsValid);
        Assert.Equal(url, result.Value!.DestinationUrl);
    }
}
