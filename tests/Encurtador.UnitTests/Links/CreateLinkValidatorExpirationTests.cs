using Encurtador.Application.Links;
using Encurtador.UnitTests.Fakes;

namespace Encurtador.UnitTests.Links;

public class CreateLinkValidatorExpirationTests
{
    private const string Url = "https://exemplo.com";
    private const string Future = "A data de expiração precisa estar no futuro.";
    private const string Format =
        "A data de expiração deve estar no formato ISO 8601 com fuso horário (ex.: 2026-12-31T23:59:00-03:00).";

    // Relógio em 2026-10-30T12:00:00Z.
    private readonly CreateLinkValidator validator = new(new FakeTimeProvider());

    [Theory]
    [InlineData("2026-10-30T12:00:01Z")]
    [InlineData("2026-10-30T09:00:01-03:00")]
    public void Future_expiration_is_accepted_and_normalized_to_utc(string expiresAt)
    {
        var result = validator.Validate(new CreateLinkRequest(Url, null, expiresAt));

        Assert.True(result.IsValid);
        var utc = result.Value!.ExpiresAtUtc!.Value;
        Assert.Equal(new DateTime(2026, 10, 30, 12, 0, 1, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Theory]
    [InlineData("2026-10-30T12:00:00Z")]
    [InlineData("2020-01-01T00:00:00Z")]
    public void Expiration_now_or_in_the_past_is_rejected(string expiresAt) => AssertExpirationError(expiresAt, Future);

    [Theory]
    [InlineData("2026-12-31T23:59:00")]
    [InlineData("31/12/2026")]
    [InlineData("amanhã")]
    [InlineData("")]
    public void Expiration_without_offset_or_in_other_format_is_rejected(string expiresAt) =>
        AssertExpirationError(expiresAt, Format);

    [Fact]
    public void Missing_expiration_is_accepted()
    {
        var result = validator.Validate(new CreateLinkRequest(Url, null, null));

        Assert.True(result.IsValid);
        Assert.Null(result.Value!.ExpiresAtUtc);
    }

    [Fact]
    public void All_invalid_fields_are_reported_together()
    {
        var result = validator.Validate(new CreateLinkRequest("ftp://x.com", "a b", "2020-01-01T00:00:00Z"));

        Assert.Equal(["customCode", "destinationUrl", "expiresAt"], result.Errors.Keys.Order());
        Assert.All(result.Errors.Values, messages => Assert.Single(messages));
    }

    private void AssertExpirationError(string expiresAt, string message)
    {
        var result = validator.Validate(new CreateLinkRequest(Url, null, expiresAt));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("expiresAt", error.Key);
        Assert.Equal([message], error.Value);
    }
}
