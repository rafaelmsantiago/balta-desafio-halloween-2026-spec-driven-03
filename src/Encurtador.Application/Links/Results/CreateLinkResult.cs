namespace Encurtador.Application.Links.Results;

public abstract record CreateLinkResult
{
    private CreateLinkResult()
    {
    }

    public sealed record Created(
        string Code,
        string DestinationUrl,
        DateTime CreatedAt,
        DateTime? ExpiresAt,
        string ManagementKey) : CreateLinkResult;

    public sealed record ValidationFailed(IReadOnlyDictionary<string, string[]> Errors) : CreateLinkResult;

    public sealed record CodeInUse(string Code) : CreateLinkResult;
}
