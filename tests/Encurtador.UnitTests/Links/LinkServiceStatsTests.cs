using Encurtador.Application.Links;
using Encurtador.Application.Links.Results;
using Encurtador.UnitTests.Fakes;

namespace Encurtador.UnitTests.Links;

public class LinkServiceStatsTests
{
    private readonly FakeLinkRepository repository = new();
    private readonly FakeTimeProvider time = new();
    private readonly ManagementKey key = ManagementKey.Generate();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unknown_code_is_not_found_before_checking_the_key()
    {
        var result = await CreateService().GetStatsAsync("naoexiste", key.PlainText, Ct);

        Assert.IsType<LinkStatsResult.NotFound>(result);
    }

    [Fact]
    public async Task Malformed_code_is_not_found()
    {
        var result = await CreateService().GetStatsAsync("a b", key.PlainText, Ct);

        Assert.IsType<LinkStatsResult.NotFound>(result);
        Assert.Equal(0, repository.GetByCodeCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("chave-errada")]
    public async Task Missing_or_wrong_key_is_unauthorized(string? candidate)
    {
        SeedLink();

        var result = await CreateService().GetStatsAsync("abcdefg", candidate, Ct);

        Assert.IsType<LinkStatsResult.Unauthorized>(result);
    }

    [Fact]
    public async Task Key_of_another_link_is_unauthorized()
    {
        SeedLink();
        var other = ManagementKey.Generate();
        repository.Seed(new Link("outro12", "https://outro.com", time.UtcNow.UtcDateTime, null, other.Hash));

        var result = await CreateService().GetStatsAsync("abcdefg", other.PlainText, Ct);

        Assert.IsType<LinkStatsResult.Unauthorized>(result);
    }

    [Fact]
    public void Unauthorized_and_not_found_results_carry_no_link_data()
    {
        Assert.DoesNotContain(
            typeof(LinkStatsResult.Unauthorized).GetProperties(),
            p => p.DeclaringType == typeof(LinkStatsResult.Unauthorized));
        Assert.DoesNotContain(
            typeof(LinkStatsResult.NotFound).GetProperties(),
            p => p.DeclaringType == typeof(LinkStatsResult.NotFound));
    }

    [Fact]
    public async Task Correct_key_returns_the_stats()
    {
        var expiresAt = time.UtcNow.UtcDateTime.AddDays(1);
        repository.Seed(new Link("abcdefg", "https://exemplo.com", time.UtcNow.UtcDateTime, expiresAt, key.Hash));
        repository.SeedAccessCount("abcdefg", 5);

        var result = await CreateService().GetStatsAsync("abcdefg", key.PlainText, Ct);

        var stats = Assert.IsType<LinkStatsResult.Stats>(result);
        Assert.Equal("abcdefg", stats.Code);
        Assert.Equal("https://exemplo.com", stats.DestinationUrl);
        Assert.Equal(time.UtcNow.UtcDateTime, stats.CreatedAt);
        Assert.Equal(expiresAt, stats.ExpiresAt);
        Assert.False(stats.IsExpired);
        Assert.Equal(5, stats.AccessCount);
    }

    private void SeedLink() =>
        repository.Seed(new Link("abcdefg", "https://exemplo.com", time.UtcNow.UtcDateTime, null, key.Hash));

    private LinkService CreateService() =>
        new(repository, new CreateLinkValidator(time), new ShortCodeGenerator(), time);
}
