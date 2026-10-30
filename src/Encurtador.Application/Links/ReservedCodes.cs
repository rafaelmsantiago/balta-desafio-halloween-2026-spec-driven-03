using System.Collections.Frozen;

namespace Encurtador.Application.Links;

/// <summary>
/// Códigos que colidem com rotas do próprio sistema. A comparação ignora caixa porque o
/// roteamento do ASP.NET Core também ignora (research.md D9).
/// </summary>
public static class ReservedCodes
{
    public static readonly FrozenSet<string> Values =
        new[] { "api", "health", "openapi", "scalar", "swagger" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsReserved(string code) => Values.Contains(code);
}
