using Encurtador.Api.Http;
using Encurtador.Application.Links;
using Encurtador.Application.Links.Results;

namespace Encurtador.Api.Endpoints;

/// <summary>Link curto público, na raiz (GET /{code}).</summary>
public static class RedirectEndpoints
{
    public static IEndpointRouteBuilder MapRedirectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{code}", ResolveAsync)
            .WithTags("Redirecionamento")
            .WithName("ResolveLink")
            .WithSummary("Abre o link curto")
            .WithDescription("Redireciona com 302 e conta o acesso. Link expirado responde 410.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone);
        return app;
    }

    private static async Task<IResult> ResolveAsync(string code, LinkService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.ResolveAsync(code, ct);

        switch (result)
        {
            case ResolveLinkResult.Redirect redirect:
                // 302 e no-store: um redirecionamento em cache não seria contado nem respeitaria a expiração (D9).
                context.Response.Headers.CacheControl = "no-store";
                return TypedResults.Redirect(redirect.DestinationUrl, permanent: false);
            case ResolveLinkResult.NotFound:
                return Problems.LinkNotFound(code);
            case ResolveLinkResult.Expired expired:
                return Problems.LinkExpired(expired.Code, expired.ExpiresAt);
            default:
                throw new InvalidOperationException($"Resultado não mapeado: {result.GetType().Name}");
        }
    }
}
