using Encurtador.Application.Links;

namespace Encurtador.UnitTests.Links;

public class LinkTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Link_without_expiration_never_expires()
    {
        var link = new Link("abcdefg", "https://exemplo.com", Now, null, new byte[32]);

        Assert.False(link.IsExpiredAt(Now.AddYears(100)));
    }

    [Fact]
    public void Link_is_not_expired_one_tick_before_expiration()
    {
        var link = new Link("abcdefg", "https://exemplo.com", Now, Now.AddMinutes(1), new byte[32]);

        Assert.False(link.IsExpiredAt(Now.AddMinutes(1).AddTicks(-1)));
    }

    [Fact]
    public void Link_is_expired_at_the_exact_expiration_instant()
    {
        var link = new Link("abcdefg", "https://exemplo.com", Now, Now.AddMinutes(1), new byte[32]);

        Assert.True(link.IsExpiredAt(Now.AddMinutes(1)));
    }

    [Fact]
    public void Link_is_expired_after_expiration()
    {
        var link = new Link("abcdefg", "https://exemplo.com", Now, Now.AddMinutes(1), new byte[32]);

        Assert.True(link.IsExpiredAt(Now.AddMinutes(2)));
    }

    [Fact]
    public void New_link_starts_with_zero_accesses()
    {
        var link = new Link("abcdefg", "https://exemplo.com", Now, null, new byte[32]);

        Assert.Equal(0, link.AccessCount);
    }
}
