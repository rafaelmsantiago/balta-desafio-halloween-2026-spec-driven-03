namespace Encurtador.Application.Links.Results;

public abstract record ResolveLinkResult
{
    private ResolveLinkResult()
    {
    }

    public sealed record Redirect(string DestinationUrl) : ResolveLinkResult;

    public sealed record NotFound : ResolveLinkResult;

    public sealed record Expired(string Code, DateTime ExpiresAt) : ResolveLinkResult;
}
