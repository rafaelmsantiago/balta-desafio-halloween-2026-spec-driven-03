using Encurtador.Application.Links;
using Encurtador.Application.Links.Results;
using Encurtador.UnitTests.Fakes;

namespace Encurtador.UnitTests.Links;

public class LinkServiceResolveTests
{
    private readonly FakeLinkRepository repository = new();
    private readonly FakeTimeProvider time = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Existing_link_redirects_and_registers_the_access()
    {
        repository.Seed(new Link("abcdefg", "https://exemplo.com", time.UtcNow.UtcDateTime, null, new byte[32]));

        var result = await CreateService().ResolveAsync("abcdefg", Ct);

        Assert.Equal("https://exemplo.com", Assert.IsType<ResolveLinkResult.Redirect>(result).DestinationUrl);
        Assert.Equal(1, repository.TryRegisterAccessCalls);
        Assert.Equal(1, repository.AccessCountOf("abcdefg"));
    }

    [Fact]
    public async Task Unknown_code_is_not_found()
    {
        var result = await CreateService().ResolveAsync("naoexiste", Ct);

        Assert.IsType<ResolveLinkResult.NotFound>(result);
        Assert.Equal(0, repository.TryRegisterAccessCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("abcdefghijklmnopqrstu")]
    [InlineData("x/y")]
    public async Task Malformed_code_is_not_found_without_touching_the_repository(string code)
    {
        var result = await CreateService().ResolveAsync(code, Ct);

        Assert.IsType<ResolveLinkResult.NotFound>(result);
        Assert.Equal(0, repository.GetByCodeCalls);
        Assert.Equal(0, repository.TryRegisterAccessCalls);
    }

    private LinkService CreateService() =>
        new(repository, new CreateLinkValidator(time), new ShortCodeGenerator(), time);
}
