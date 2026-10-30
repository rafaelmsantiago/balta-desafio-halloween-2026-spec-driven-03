namespace Encurtador.Application.Links.Results;

public abstract record LinkStatsResult
{
    private LinkStatsResult()
    {
    }

    public sealed record Stats(
        string Code,
        string DestinationUrl,
        DateTime CreatedAt,
        DateTime? ExpiresAt,
        bool IsExpired,
        long AccessCount) : LinkStatsResult;

    public sealed record NotFound : LinkStatsResult;

    /// <summary>Sem dados do link (FR-017).</summary>
    public sealed record Unauthorized : LinkStatsResult;
}
