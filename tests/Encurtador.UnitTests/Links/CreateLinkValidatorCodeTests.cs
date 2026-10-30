using Encurtador.Application.Links;
using Encurtador.UnitTests.Fakes;

namespace Encurtador.UnitTests.Links;

public class CreateLinkValidatorCodeTests
{
    private const string Url = "https://exemplo.com";
    private const string Length = "O código deve ter entre 4 e 20 caracteres.";
    private const string Characters =
        "O código só pode conter letras sem acento (A–Z, a–z), dígitos (0–9), hífen (-) e sublinhado (_).";

    private readonly CreateLinkValidator validator = new(new FakeTimeProvider());

    [Theory]
    [InlineData("abc")]
    [InlineData("abcdefghijklmnopqrstu")]
    [InlineData("")]
    public void Code_outside_4_to_20_characters_is_rejected(string code) => AssertCodeError(code, Length);

    [Theory]
    [InlineData("abcd")]
    [InlineData("abcdefghijklmnopqrst")]
    [InlineData("promo-natal")]
    [InlineData("promo_natal")]
    [InlineData("O0l1I0oo")]
    public void Well_formed_code_is_accepted(string code)
    {
        var result = validator.Validate(new CreateLinkRequest(Url, code, null));

        Assert.True(result.IsValid);
        Assert.Equal(code, result.Value!.CustomCode);
    }

    [Theory]
    [InlineData("promo natal")]
    [InlineData("a/bcd")]
    [InlineData("promoção")]
    public void Code_with_invalid_characters_is_rejected(string code) => AssertCodeError(code, Characters);

    [Theory]
    [InlineData("health")]
    [InlineData("HEALTH")]
    [InlineData("Swagger")]
    [InlineData("openapi")]
    [InlineData("scalar")]
    [InlineData("Scalar")]
    public void Reserved_code_is_unavailable(string code) => AssertCodeError(code, $"O código \"{code}\" está indisponível.");

    [Fact]
    public void Missing_code_means_generated_code()
    {
        var result = validator.Validate(new CreateLinkRequest(Url, null, null));

        Assert.True(result.IsValid);
        Assert.Null(result.Value!.CustomCode);
    }

    [Fact]
    public void Code_errors_accumulate_with_url_errors()
    {
        var result = validator.Validate(new CreateLinkRequest("ftp://exemplo.com", "a b", null));

        Assert.Equal(["customCode", "destinationUrl"], result.Errors.Keys.Order());
    }

    private void AssertCodeError(string code, string message)
    {
        var result = validator.Validate(new CreateLinkRequest(Url, code, null));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("customCode", error.Key);
        Assert.Equal([message], error.Value);
    }
}
