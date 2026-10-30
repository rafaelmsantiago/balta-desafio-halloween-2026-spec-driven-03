namespace Encurtador.Api.Contracts;

public sealed record LinkCreatedResponse(
    string Code,
    string ShortUrl,
    string DestinationUrl,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    string ManagementKey);
