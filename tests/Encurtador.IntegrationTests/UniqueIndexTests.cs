using Encurtador.Application.Links;
using Encurtador.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Encurtador.IntegrationTests;

/// <summary>
/// Prova, no SQLite real, que o índice único recusa o segundo insert mesmo sem a checagem
/// prévia do serviço, e que a violação chega como DuplicateCode (research.md D1).
/// </summary>
public class UniqueIndexTests(LinksApiFactory factory) : IClassFixture<LinksApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Second_insert_with_same_code_is_reported_as_duplicate()
    {
        _ = factory.CreateClient(); // sobe o host e aplica as migrations
        var now = factory.Time.GetUtcNow().UtcDateTime;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ILinkRepository>();
            var first = await repository.AddAsync(new Link("indice-1", "https://a.com", now, null, new byte[32]), Ct);
            Assert.Equal(AddLinkResult.Added, first);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ILinkRepository>();
            var second = await repository.AddAsync(new Link("indice-1", "https://b.com", now, null, new byte[32]), Ct);
            Assert.Equal(AddLinkResult.DuplicateCode, second);

            // O insert recusado foi desanexado: outro insert no mesmo escopo funciona.
            var third = await repository.AddAsync(new Link("indice-2", "https://c.com", now, null, new byte[32]), Ct);
            Assert.Equal(AddLinkResult.Added, third);
        }
    }

    [Fact]
    public async Task Unique_index_is_case_sensitive()
    {
        _ = factory.CreateClient();
        var now = factory.Time.GetUtcNow().UtcDateTime;
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILinkRepository>();

        Assert.Equal(AddLinkResult.Added, await repository.AddAsync(new Link("Indice-C", "https://a.com", now, null, new byte[32]), Ct));
        Assert.Equal(AddLinkResult.Added, await repository.AddAsync(new Link("indice-c", "https://a.com", now, null, new byte[32]), Ct));
    }
}
