using System.Buffers.Text;
using System.Security.Cryptography;

namespace Encurtador.Application.Links;

/// <summary>
/// Chave de gerenciamento: 256 bits aleatórios em Base64Url. Só o SHA-256 é persistido (research.md D3).
/// </summary>
public readonly record struct ManagementKey(string PlainText, byte[] Hash)
{
    private const int KeySizeInBytes = 32;

    public static ManagementKey Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(KeySizeInBytes);
        return new ManagementKey(Base64Url.EncodeToString(bytes), SHA256.HashData(bytes));
    }

    public static byte[] ComputeHash(string plainText) => SHA256.HashData(Base64Url.DecodeFromChars(plainText));

    /// <summary>Compara em tempo constante; chave ausente ou malformada é só uma chave errada.</summary>
    public static bool Verify(string? plainText, byte[] expectedHash)
    {
        if (string.IsNullOrEmpty(plainText) || plainText.Length > Base64Url.GetEncodedLength(KeySizeInBytes))
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[KeySizeInBytes];
        var status = Base64Url.DecodeFromChars(plainText, bytes, out _, out var written);
        if (status != System.Buffers.OperationStatus.Done || written != KeySizeInBytes)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), expectedHash);
    }
}
