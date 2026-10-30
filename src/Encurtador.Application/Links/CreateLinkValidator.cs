using System.Globalization;
using System.Text.RegularExpressions;

namespace Encurtador.Application.Links;

/// <summary>
/// Valida o pedido de criação acumulando todos os erros por campo, com mensagens em português (D8).
/// </summary>
public sealed partial class CreateLinkValidator(TimeProvider time)
{
    public const int MaxUrlLength = 2048;

    // ISO 8601 com fuso obrigatório: a expiração é um instante absoluto.
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex IsoWithOffset();

    public CreateLinkValidation Validate(CreateLinkRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var url = request.DestinationUrl;
        if (ValidateDestinationUrl(url) is { } urlError)
        {
            errors["destinationUrl"] = [urlError];
        }

        var code = request.CustomCode;
        if (code is not null && ValidateCustomCode(code) is { } codeError)
        {
            errors["customCode"] = [codeError];
        }

        DateTime? expiresAtUtc = null;
        if (request.ExpiresAt is { } expiresAt)
        {
            if (ValidateExpiration(expiresAt, out var parsed) is { } expirationError)
            {
                errors["expiresAt"] = [expirationError];
            }
            else
            {
                expiresAtUtc = parsed;
            }
        }

        if (errors.Count > 0)
        {
            return new CreateLinkValidation(errors, null);
        }

        return new CreateLinkValidation(errors, new ValidatedLink(url!, code, expiresAtUtc));
    }

    private string? ValidateExpiration(string value, out DateTime utc)
    {
        utc = default;
        if (!IsoWithOffset().IsMatch(value)
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return "A data de expiração deve estar no formato ISO 8601 com fuso horário (ex.: 2026-12-31T23:59:00-03:00).";
        }

        utc = parsed.UtcDateTime;
        if (utc <= time.GetUtcNow().UtcDateTime)
        {
            return "A data de expiração precisa estar no futuro.";
        }

        return null;
    }

    private static string? ValidateCustomCode(string code)
    {
        if (code.Length is < ShortCodeFormat.MinLength or > ShortCodeFormat.MaxLength)
        {
            return "O código deve ter entre 4 e 20 caracteres.";
        }

        if (!ShortCodeFormat.HasOnlyAllowedCharacters(code))
        {
            return "O código só pode conter letras sem acento (A–Z, a–z), dígitos (0–9), hífen (-) e sublinhado (_).";
        }

        if (ReservedCodes.IsReserved(code))
        {
            return $"O código \"{code}\" está indisponível.";
        }

        return null;
    }

    private static string? ValidateDestinationUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return "A URL de destino é obrigatória.";
        }

        if (url.Length > MaxUrlLength)
        {
            return "A URL de destino deve ter no máximo 2048 caracteres.";
        }

        if (url.Trim().Length != url.Length || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "A URL de destino deve ser um endereço absoluto válido, com domínio.";
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return "A URL de destino deve usar o esquema http ou https.";
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            return "A URL de destino deve ser um endereço absoluto válido, com domínio.";
        }

        return null;
    }
}
