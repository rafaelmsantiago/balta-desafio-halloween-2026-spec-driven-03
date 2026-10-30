namespace Encurtador.Application.Links;

/// <summary>Pedido de criação; tudo chega como texto para que todos os erros sejam listados juntos (D8).</summary>
public sealed record CreateLinkRequest(string? DestinationUrl, string? CustomCode, string? ExpiresAt);
