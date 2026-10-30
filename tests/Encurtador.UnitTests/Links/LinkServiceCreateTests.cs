using System.Text;
using Encurtador.Application.Links;
using Encurtador.Application.Links.Results;
using Encurtador.UnitTests.Fakes;

namespace Encurtador.UnitTests.Links;

public class LinkServiceCreateTests
{
    private const string Url = "https://exemplo.com/artigos/2026/um-texto-bem-longo";

    private readonly FakeLinkRepository repository = new();
    private readonly FakeTimeProvider time = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creates_link_with_generated_code_and_management_key()
    {
        var result = await CreateService().CreateAsync(new CreateLinkRequest(Url, null, null), Ct);

        var created = Assert.IsType<CreateLinkResult.Created>(result);
        Assert.Equal(7, created.Code.Length);
        Assert.All(created.Code, c => Assert.Contains(c, ShortCodeGenerator.Alphabet));
        Assert.Equal(Url, created.DestinationUrl);
        Assert.Equal(time.UtcNow.UtcDateTime, created.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.Kind);
        Assert.Null(created.ExpiresAt);
        Assert.False(string.IsNullOrEmpty(created.ManagementKey));
    }

    [Fact]
    public async Task Stores_only_the_hash_of_the_management_key()
    {
        var created = Assert.IsType<CreateLinkResult.Created>(
            await CreateService().CreateAsync(new CreateLinkRequest(Url, null, null), Ct));

        var stored = Assert.Single(repository.Links);
        Assert.Equal(ManagementKey.ComputeHash(created.ManagementKey), stored.ManagementKeyHash);
        Assert.NotEqual(Encoding.UTF8.GetBytes(created.ManagementKey), stored.ManagementKeyHash);
        Assert.DoesNotContain(created.ManagementKey, stored.Code + stored.DestinationUrl);
        Assert.Equal(0, stored.AccessCount);
    }

    [Fact]
    public async Task Generates_another_code_when_the_first_already_exists()
    {
        repository.ExistingCodes.Add("AAAAAAA");
        var generator = new SequenceCodeGenerator("AAAAAAA", "BBBBBBB");

        var result = await CreateService(generator).CreateAsync(new CreateLinkRequest(Url, null, null), Ct);

        Assert.Equal("BBBBBBB", Assert.IsType<CreateLinkResult.Created>(result).Code);
        Assert.Equal(1, repository.AddCalls);
    }

    [Fact]
    public async Task Generates_another_code_when_the_unique_index_rejects_the_insert()
    {
        repository.ForcedAddResults.Enqueue(AddLinkResult.DuplicateCode);
        var generator = new SequenceCodeGenerator("AAAAAAA", "BBBBBBB");

        var result = await CreateService(generator).CreateAsync(new CreateLinkRequest(Url, null, null), Ct);

        Assert.Equal("BBBBBBB", Assert.IsType<CreateLinkResult.Created>(result).Code);
        Assert.Equal(2, repository.AddCalls);
    }

    [Fact]
    public async Task Gives_up_after_five_consecutive_collisions()
    {
        for (var i = 0; i < 5; i++)
        {
            repository.ForcedAddResults.Enqueue(AddLinkResult.DuplicateCode);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().CreateAsync(new CreateLinkRequest(Url, null, null), Ct));
        Assert.Equal(5, repository.AddCalls);
    }

    [Fact]
    public async Task Invalid_request_does_not_touch_the_repository()
    {
        var result = await CreateService().CreateAsync(new CreateLinkRequest("ftp://exemplo.com", null, null), Ct);

        Assert.IsType<CreateLinkResult.ValidationFailed>(result);
        Assert.Equal(0, repository.AddCalls);
        Assert.Equal(0, repository.CodeExistsCalls);
    }

    [Fact]
    public async Task Same_url_twice_creates_two_distinct_links()
    {
        var service = CreateService();

        var first = Assert.IsType<CreateLinkResult.Created>(await service.CreateAsync(new CreateLinkRequest(Url, null, null), Ct));
        var second = Assert.IsType<CreateLinkResult.Created>(await service.CreateAsync(new CreateLinkRequest(Url, null, null), Ct));

        Assert.NotEqual(first.Code, second.Code);
        Assert.Equal(2, repository.Links.Count);
    }

    private LinkService CreateService(ShortCodeGenerator? generator = null) =>
        new(repository, new CreateLinkValidator(time), generator ?? new ShortCodeGenerator(), time);
}
