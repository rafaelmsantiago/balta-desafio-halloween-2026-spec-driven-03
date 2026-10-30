namespace Encurtador.Api.Contracts;

public sealed record LinkStatsResponse(
    string Code,
    string DestinationUrl,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    bool IsExpired,
    long AccessCount);
