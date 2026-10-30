using System.Globalization;

namespace Encurtador.Api.Http;

/// <summary>Respostas de erro do contrato (contracts/http-api.md), com mensagens em português.</summary>
internal static class Problems
{
    public static IResult LinkNotFound(string code) =>
        TypedResults.Problem(
            type: "/problems/link-not-found",
            title: "Link não encontrado.",
            statusCode: StatusCodes.Status404NotFound,
            detail: $"Não existe link com o código \"{code}\".");

    public static IResult LinkExpired(string code, DateTime expiresAtUtc) =>
        TypedResults.Problem(
            type: "/problems/link-expired",
            title: "Link expirado.",
            statusCode: StatusCodes.Status410Gone,
            detail: $"O link \"{code}\" expirou em {expiresAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}.");

    public static IResult CodeInUse(string code) =>
        TypedResults.Problem(
            type: "/problems/code-in-use",
            title: "Código já está em uso.",
            statusCode: StatusCodes.Status409Conflict,
            detail: $"O código \"{code}\" já está em uso. Escolha outro código.");

    public static IResult InvalidManagementKey(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "ManagementKey";
        return TypedResults.Problem(
            type: "/problems/invalid-management-key",
            title: "Chave de gerenciamento inválida.",
            statusCode: StatusCodes.Status401Unauthorized,
            detail: "Informe no cabeçalho X-Management-Key a chave entregue na criação do link.");
    }

    public static IResult Validation(IDictionary<string, string[]> errors) =>
        TypedResults.ValidationProblem(errors, title: "Um ou mais campos são inválidos.");
}
