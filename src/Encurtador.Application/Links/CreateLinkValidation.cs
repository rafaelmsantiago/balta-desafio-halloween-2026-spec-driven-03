namespace Encurtador.Application.Links;

public sealed record CreateLinkValidation(IReadOnlyDictionary<string, string[]> Errors, ValidatedLink? Value)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record ValidatedLink(string DestinationUrl, string? CustomCode, DateTime? ExpiresAtUtc);
