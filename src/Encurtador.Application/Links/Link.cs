namespace Encurtador.Application.Links;

/// <summary>
/// Link curto. Código, destino, datas e chave são imutáveis; o contador só é
/// incrementado pelo banco, via update atômico (research.md D2).
/// </summary>
public sealed class Link
{
    public long Id { get; private set; }

    public string Code { get; private set; } = null!;

    public string DestinationUrl { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    public DateTime? ExpiresAt { get; private set; }

    public long AccessCount { get; private set; }

    public byte[] ManagementKeyHash { get; private set; } = null!;

    public Link(string code, string destinationUrl, DateTime createdAt, DateTime? expiresAt, byte[] managementKeyHash)
    {
        Code = code;
        DestinationUrl = destinationUrl;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        ManagementKeyHash = managementKeyHash;
        AccessCount = 0;
    }

    // Usado pelo EF Core.
    private Link()
    {
    }

    /// <summary>O instante exato da expiração já conta como expirado.</summary>
    public bool IsExpiredAt(DateTime nowUtc) => ExpiresAt is not null && nowUtc >= ExpiresAt;

    /// <summary>Monta um link com contador; existe só para testes unitários.</summary>
    internal static Link Restore(
        long id,
        string code,
        string destinationUrl,
        DateTime createdAt,
        DateTime? expiresAt,
        long accessCount,
        byte[] managementKeyHash) =>
        new(code, destinationUrl, createdAt, expiresAt, managementKeyHash)
        {
            Id = id,
            AccessCount = accessCount,
        };
}
