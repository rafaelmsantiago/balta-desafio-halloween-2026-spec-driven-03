using System.Security.Cryptography;

namespace Encurtador.Application.Links;

/// <summary>
/// Gera códigos de 7 caracteres sem os símbolos ambíguos 0 O o 1 I l (research.md D4).
/// </summary>
public class ShortCodeGenerator
{
    public const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";
    public const int Length = 7;

    // GetItems é uniforme (sem viés de módulo) e usa o gerador criptográfico.
    public virtual string Generate() => new(RandomNumberGenerator.GetItems<char>(Alphabet, Length));
}
