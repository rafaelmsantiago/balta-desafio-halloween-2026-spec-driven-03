namespace Encurtador.Application.Links;

public enum AddLinkResult
{
    Added,
    DuplicateCode,
}

public interface ILinkRepository
{
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);

    /// <summary>
    /// Insere o link. Retorna <see cref="AddLinkResult.DuplicateCode"/> quando o índice único
    /// do código é violado, inclusive numa corrida que a checagem prévia não pegou (D1).
    /// </summary>
    Task<AddLinkResult> AddAsync(Link link, CancellationToken ct);

    Task<Link?> GetByCodeAsync(string code, CancellationToken ct);

    /// <summary>
    /// Incrementa o contador de forma atômica, condicionado a
    /// <c>ExpiresAt IS NULL OR ExpiresAt &gt; nowUtc</c>, num único comando (D2).
    /// Retorna <c>true</c> só se uma linha foi afetada.
    /// </summary>
    Task<bool> TryRegisterAccessAsync(string code, DateTime nowUtc, CancellationToken ct);
}
