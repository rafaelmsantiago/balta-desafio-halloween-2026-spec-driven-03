using Encurtador.Api.Contracts;
using Encurtador.Api.Http;
using Encurtador.Application.Links;
using Encurtador.Application.Links.Results;
using Microsoft.AspNetCore.Mvc;

namespace Encurtador.Api.Endpoints;

/// <summary>API de gerenciamento dos links (/api/links).</summary>
public static class LinkEndpoints
{
    public static RouteGroupBuilder MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/links").WithTags("Links");

        group.MapPost("/", CreateAsync)
            .WithName("CreateLink")
            .WithSummary("Cria um link curto")
            .WithDescription("Sem customCode, gera um código de 7 caracteres. A managementKey é devolvida só nesta resposta.")
            .Produces<LinkCreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{code}/stats", GetStatsAsync)
            .WithName("GetLinkStats")
            .WithSummary("Consulta os acessos de um link")
            .WithDescription("Exige a chave entregue na criação, no cabeçalho X-Management-Key.")
            .Produces<LinkStatsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    // A chave só é aceita no cabeçalho, nunca na query string (research.md D3).
    private static async Task<IResult> GetStatsAsync(
        string code,
        [FromHeader(Name = "X-Management-Key")] string? managementKey,
        LinkService service,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.GetStatsAsync(code, managementKey, ct);

        return result switch
        {
            LinkStatsResult.Stats stats => TypedResults.Ok(new LinkStatsResponse(
                stats.Code,
                stats.DestinationUrl,
                stats.CreatedAt,
                stats.ExpiresAt,
                stats.IsExpired,
                stats.AccessCount)),
            LinkStatsResult.NotFound => Problems.LinkNotFound(code),
            LinkStatsResult.Unauthorized => Problems.InvalidManagementKey(context),
            _ => throw new InvalidOperationException($"Resultado não mapeado: {result.GetType().Name}"),
        };
    }

    private static async Task<IResult> CreateAsync(
        CreateLinkHttpRequest body,
        LinkService service,
        IConfiguration configuration,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.CreateAsync(
            new CreateLinkRequest(body.DestinationUrl, body.CustomCode, body.ExpiresAt), ct);

        return result switch
        {
            CreateLinkResult.Created created => TypedResults.Created(
                $"/api/links/{created.Code}/stats",
                new LinkCreatedResponse(
                    created.Code,
                    ShortUrl(configuration, context.Request, created.Code),
                    created.DestinationUrl,
                    created.CreatedAt,
                    created.ExpiresAt,
                    created.ManagementKey)),
            CreateLinkResult.ValidationFailed failed => Problems.Validation(failed.Errors.ToDictionary()),
            CreateLinkResult.CodeInUse inUse => Problems.CodeInUse(inUse.Code),
            _ => throw new InvalidOperationException($"Resultado não mapeado: {result.GetType().Name}"),
        };
    }

    // Base pública configurável; sem ela, usa o host da requisição (research.md D11).
    private static string ShortUrl(IConfiguration configuration, HttpRequest request, string code)
    {
        var baseUrl = configuration["ShortLinks:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = $"{request.Scheme}://{request.Host}";
        }

        return $"{baseUrl.TrimEnd('/')}/{code}";
    }
}
