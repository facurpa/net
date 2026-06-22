using System.Net.Mail;
using System.Text.RegularExpressions;
using Sacc.Api.Core.Errors;

namespace Sacc.Api.Core.Validation;

/// <summary>Validação de formato <c>local@dominio.tld</c> (equivalente ao <c>EmailStr</c> do Pydantic).</summary>
public static partial class EmailValidator
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !Pattern().IsMatch(email))
            return false;
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Valida e normaliza (trim). Lança 422 se inválido.</summary>
    public static string Require(string? email)
    {
        var value = (email ?? "").Trim();
        if (!IsValid(value))
            throw new ValidationException("E-mail inválido");
        return value;
    }
}
