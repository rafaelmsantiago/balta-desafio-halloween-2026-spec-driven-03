using Encurtador.Application.Links.Results;

namespace Encurtador.Application.Links;

/// <summary>Dono das regras de negócio dos links curtos.</summary>
public sealed class LinkService(
    ILinkRepository repository,
    CreateLinkValidator validator,
    ShortCodeGenerator generator,
    TimeProvider time)
{
    private const int MaxGenerationAttempts = 5;

    public async Task<CreateLinkResult> CreateAsync(CreateLinkRequest request, CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return new CreateLinkResult.ValidationFailed(validation.Errors);
        }

        var input = validation.Value!;
        var now = time.GetUtcNow().UtcDateTime;
        var key = ManagementKey.Generate();

        if (input.CustomCode is { } customCode)
        {
            // A checagem dá a resposta amigável no caso comum; o índice único decide a corrida (D1).
            if (await repository.CodeExistsAsync(customCode, ct))
            {
                return new CreateLinkResult.CodeInUse(customCode);
            }

            var custom = new Link(customCode, input.DestinationUrl, now, input.ExpiresAtUtc, key.Hash);
            return await repository.AddAsync(custom, ct) == AddLinkResult.Added
                ? Created(custom, key)
                : new CreateLinkResult.CodeInUse(customCode);
        }

        for (var attempt = 1; attempt <= MaxGenerationAttempts; attempt++)
        {
            var code = generator.Generate();
            if (await repository.CodeExistsAsync(code, ct))
            {
                continue;
            }

            var link = new Link(code, input.DestinationUrl, now, input.ExpiresAtUtc, key.Hash);
            if (await repository.AddAsync(link, ct) == AddLinkResult.Added)
            {
                return Created(link, key);
            }
        }

        throw new InvalidOperationException("Não foi possível gerar um código único.");
    }

    public async Task<ResolveLinkResult> ResolveAsync(string code, CancellationToken ct)
    {
        if (!ShortCodeFormat.IsWellFormed(code))
        {
            return new ResolveLinkResult.NotFound();
        }

        var link = await repository.GetByCodeAsync(code, ct);
        if (link is null)
        {
            return new ResolveLinkResult.NotFound();
        }

        var now = time.GetUtcNow().UtcDateTime;
        if (link.IsExpiredAt(now))
        {
            return new ResolveLinkResult.Expired(code, link.ExpiresAt!.Value);
        }

        if (await repository.TryRegisterAccessAsync(code, now, ct))
        {
            return new ResolveLinkResult.Redirect(link.DestinationUrl);
        }

        // O update atômico recusou: o link expirou entre a leitura e o incremento (D2).
        return new ResolveLinkResult.Expired(code, link.ExpiresAt ?? now);
    }

    public async Task<LinkStatsResult> GetStatsAsync(string code, string? managementKey, CancellationToken ct)
    {
        if (!ShortCodeFormat.IsWellFormed(code))
        {
            return new LinkStatsResult.NotFound();
        }

        // O 404 vem antes da chave (US2 cenário 5); com chave inválida nada do link é revelado.
        var link = await repository.GetByCodeAsync(code, ct);
        if (link is null)
        {
            return new LinkStatsResult.NotFound();
        }

        if (!ManagementKey.Verify(managementKey, link.ManagementKeyHash))
        {
            return new LinkStatsResult.Unauthorized();
        }

        var now = time.GetUtcNow().UtcDateTime;
        return new LinkStatsResult.Stats(
            link.Code,
            link.DestinationUrl,
            link.CreatedAt,
            link.ExpiresAt,
            link.IsExpiredAt(now),
            link.AccessCount);
    }

    private static CreateLinkResult.Created Created(Link link, ManagementKey key) =>
        new(link.Code, link.DestinationUrl, link.CreatedAt, link.ExpiresAt, key.PlainText);
}
