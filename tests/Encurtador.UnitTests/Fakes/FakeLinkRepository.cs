using Encurtador.Application.Links;

namespace Encurtador.UnitTests.Fakes;

/// <summary>Repositório em memória que imita as regras do banco (índice único ordinal, update condicionado).</summary>
internal sealed class FakeLinkRepository : ILinkRepository
{
    private readonly Dictionary<string, Link> links = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> accessCounts = new(StringComparer.Ordinal);
    private long nextId = 1;

    public IReadOnlyCollection<Link> Links => links.Values;

    /// <summary>Resultados forçados para as próximas chamadas de AddAsync (simula a corrida do índice).</summary>
    public Queue<AddLinkResult> ForcedAddResults { get; } = new();

    /// <summary>Códigos que <see cref="CodeExistsAsync"/> deve acusar como existentes, além dos salvos.</summary>
    public HashSet<string> ExistingCodes { get; } = new(StringComparer.Ordinal);

    public int AddCalls { get; private set; }

    public int CodeExistsCalls { get; private set; }

    public int GetByCodeCalls { get; private set; }

    public int TryRegisterAccessCalls { get; private set; }

    /// <summary>Força o próximo TryRegisterAccessAsync a falhar, como se o link expirasse entre leitura e update.</summary>
    public bool FailNextRegisterAccess { get; set; }

    public long AccessCountOf(string code) => accessCounts.GetValueOrDefault(code);

    public void SeedAccessCount(string code, long count) => accessCounts[code] = count;

    public void Seed(Link link)
    {
        links[link.Code] = link;
        accessCounts.TryAdd(link.Code, 0);
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct)
    {
        CodeExistsCalls++;
        return Task.FromResult(links.ContainsKey(code) || ExistingCodes.Contains(code));
    }

    public Task<AddLinkResult> AddAsync(Link link, CancellationToken ct)
    {
        AddCalls++;
        if (ForcedAddResults.TryDequeue(out var forced) && forced == AddLinkResult.DuplicateCode)
        {
            return Task.FromResult(AddLinkResult.DuplicateCode);
        }

        if (links.ContainsKey(link.Code))
        {
            return Task.FromResult(AddLinkResult.DuplicateCode);
        }

        Seed(link);
        return Task.FromResult(AddLinkResult.Added);
    }

    public Task<Link?> GetByCodeAsync(string code, CancellationToken ct)
    {
        GetByCodeCalls++;
        if (!links.TryGetValue(code, out var stored))
        {
            return Task.FromResult<Link?>(null);
        }

        var snapshot = Link.Restore(
            nextId++,
            stored.Code,
            stored.DestinationUrl,
            stored.CreatedAt,
            stored.ExpiresAt,
            accessCounts.GetValueOrDefault(code),
            stored.ManagementKeyHash);
        return Task.FromResult<Link?>(snapshot);
    }

    public Task<bool> TryRegisterAccessAsync(string code, DateTime nowUtc, CancellationToken ct)
    {
        TryRegisterAccessCalls++;
        if (FailNextRegisterAccess)
        {
            FailNextRegisterAccess = false;
            return Task.FromResult(false);
        }

        if (!links.TryGetValue(code, out var link) || !(link.ExpiresAt is null || link.ExpiresAt > nowUtc))
        {
            return Task.FromResult(false);
        }

        accessCounts[code] = accessCounts.GetValueOrDefault(code) + 1;
        return Task.FromResult(true);
    }
}
