using System.Text.RegularExpressions;

namespace Encurtador.Application.Links;

/// <summary>Formato aceito para qualquer código: 4 a 20 caracteres, A–Z, a–z, 0–9, '-' e '_'.</summary>
public static partial class ShortCodeFormat
{
    public const int MinLength = 4;
    public const int MaxLength = 20;

    [GeneratedRegex("^[A-Za-z0-9_-]{4,20}$")]
    private static partial Regex Pattern();

    public static bool IsWellFormed(string? code) => code is not null && Pattern().IsMatch(code);

    /// <summary>Só os caracteres, sem checar o tamanho.</summary>
    public static bool HasOnlyAllowedCharacters(string code) => AllowedCharacters().IsMatch(code);

    [GeneratedRegex("^[A-Za-z0-9_-]*$")]
    private static partial Regex AllowedCharacters();
}
