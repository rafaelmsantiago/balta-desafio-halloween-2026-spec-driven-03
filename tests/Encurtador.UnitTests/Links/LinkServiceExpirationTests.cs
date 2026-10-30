using Encurtador.Application.Links;
using Encurtador.Application.Links.Results;
using Encurtador.UnitTests.Fakes;

namespace Encurtador.UnitTests.Links;

public class LinkServiceExpirationTests
{
    private readonly FakeLinkRepository repository = new();
    private readonly FakeTimeProvider time = new();
    private readonly ManagementKey key = ManagementKey.Generate();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateTime Now => time.UtcNow.UtcDateTime;

    [Theory]
    [InlineData(null)]
    [InlineData("expira-1")]
    public async Task Create_stores_the_expiration_in_utc(string? customCode)
    {
        var result = await CreateService().CreateAsync(
            new CreateLinkRequest("https://exemplo.com", customCode, "2026-10-30T10:00:00-03:00"), Ct);

        var expected = new DateTime(2026, 10, 30, 13, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, Assert.IsType<CreateLinkResult.Created>(result).ExpiresAt);
        Assert.Equal(expected, Assert.Single(repository.Links).ExpiresAt);
    }

    [Fact]
    public async Task Expired_link_is_expired_without_registering_the_access()
    {
        repository.Seed(new Link("expirad", "https://exemplo.com", Now.AddHours(-2), Now.AddHours(-1), key.Hash));

        var result = await CreateService().ResolveAsync("expirad", Ct);

        var expired = Assert.IsType<ResolveLinkResult.Expired>(result);
        Assert.Equal("expirad", expired.Code);
        Assert.Equal(Now.AddHours(-1), expired.ExpiresAt);
        Assert.Equal(0, repository.TryRegisterAccessCalls);
        Assert.Equal(0, repository.AccessCountOf("expirad"));
    }

    [Fact]
    public async Task Link_is_expired_at_the_exact_instant()
    {
        repository.Seed(new Link("instant", "https://exemplo.com", Now.AddHours(-1), Now, key.Hash));

        Assert.IsType<ResolveLinkResult.Expired>(await CreateService().ResolveAsync("instant", Ct));
    }

    [Fact]
    public async Task Link_expiring_between_read_and_update_is_expired()
    {
        repository.Seed(new Link("corrida", "https://exemplo.com", Now, Now.AddMinutes(1), key.Hash));
        repository.FailNextRegisterAccess = true;

        var result = await CreateService().ResolveAsync("corrida", Ct);

        Assert.IsType<ResolveLinkResult.Expired>(result);
        Assert.Equal(0, repository.AccessCountOf("corrida"));
    }

    [Fact]
    public async Task Stats_of_expired_link_report_expired_and_accumulated_total()
    {
        repository.Seed(new Link("expirad", "https://exemplo.com", Now.AddHours(-2), Now.AddHours(-1), key.Hash));
        repository.SeedAccessCount("expirad", 7);

        var result = await CreateService().GetStatsAsync("expirad", key.PlainText, Ct);

        var stats = Assert.IsType<LinkStatsResult.Stats>(result);
        Assert.True(stats.IsExpired);
        Assert.Equal(7, stats.AccessCount);
    }

    [Fact]
    public async Task Link_without_expiration_keeps_redirecting_a_century_later()
    {
        repository.Seed(new Link("eterno1", "https://exemplo.com", Now, null, key.Hash));
        time.Advance(TimeSpan.FromDays(365 * 100));

        Assert.IsType<ResolveLinkResult.Redirect>(await CreateService().ResolveAsync("eterno1", Ct));
    }

    private LinkService CreateService() =>
        new(repository, new CreateLinkValidator(time), new ShortCodeGenerator(), time);
}
