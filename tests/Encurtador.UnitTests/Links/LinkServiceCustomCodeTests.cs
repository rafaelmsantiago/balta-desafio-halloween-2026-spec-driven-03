using Encurtador.Application.Links;
using Encurtador.Application.Links.Results;
using Encurtador.UnitTests.Fakes;

namespace Encurtador.UnitTests.Links;

public class LinkServiceCustomCodeTests
{
    private const string Url = "https://exemplo.com";

    private readonly FakeLinkRepository repository = new();
    private readonly FakeTimeProvider time = new();
    private readonly SequenceCodeGenerator generator = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Free_custom_code_is_used_exactly()
    {
        var result = await CreateService().CreateAsync(new CreateLinkRequest(Url, "promo-natal", null), Ct);

        Assert.Equal("promo-natal", Assert.IsType<CreateLinkResult.Created>(result).Code);
        Assert.Equal(1, repository.AddCalls);
        Assert.Equal(0, generator.Calls);
    }

    [Fact]
    public async Task Existing_custom_code_is_in_use_without_inserting()
    {
        repository.ExistingCodes.Add("promo-natal");

        var result = await CreateService().CreateAsync(new CreateLinkRequest(Url, "promo-natal", null), Ct);

        Assert.Equal("promo-natal", Assert.IsType<CreateLinkResult.CodeInUse>(result).Code);
        Assert.Equal(0, repository.AddCalls);
    }

    [Fact]
    public async Task Lost_race_on_the_unique_index_is_in_use_without_retrying()
    {
        repository.ForcedAddResults.Enqueue(AddLinkResult.DuplicateCode);

        var result = await CreateService().CreateAsync(new CreateLinkRequest(Url, "promo-natal", null), Ct);

        Assert.IsType<CreateLinkResult.CodeInUse>(result);
        Assert.Equal(1, repository.AddCalls);
        Assert.Equal(0, generator.Calls);
    }

    [Fact]
    public async Task Codes_differing_only_in_case_are_distinct()
    {
        var service = CreateService();

        Assert.IsType<CreateLinkResult.Created>(await service.CreateAsync(new CreateLinkRequest(Url, "Promo", null), Ct));
        Assert.IsType<CreateLinkResult.Created>(await service.CreateAsync(new CreateLinkRequest(Url, "promo", null), Ct));
        Assert.Equal(2, repository.Links.Count);
    }

    [Fact]
    public async Task Custom_code_may_contain_ambiguous_characters()
    {
        var result = await CreateService().CreateAsync(new CreateLinkRequest(Url, "O0l1-Il", null), Ct);

        Assert.Equal("O0l1-Il", Assert.IsType<CreateLinkResult.Created>(result).Code);
    }

    private LinkService CreateService() => new(repository, new CreateLinkValidator(time), generator, time);
}
