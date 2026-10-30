namespace Encurtador.Api.Contracts;

/// <summary>Todos os campos como texto: a validação fica no serviço e lista todos os erros (D8).</summary>
public sealed record CreateLinkHttpRequest(string? DestinationUrl, string? CustomCode, string? ExpiresAt);
