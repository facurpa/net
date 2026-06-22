using System.Globalization;
using Sacc.Api.Core.Errors;

namespace Sacc.Api.Core.Validation;

/// <summary>Validação de datas no formato inteiro <c>YYYYMMDD</c>.</summary>
public static class DateYmd
{
    public static bool TryParse(int yyyymmdd, out DateOnly date) =>
        DateOnly.TryParseExact(yyyymmdd.ToString("D8", CultureInfo.InvariantCulture),
            "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>Valida que é uma data real YYYYMMDD; lança 422 com o nome do campo.</summary>
    public static void Require(int yyyymmdd, string campo)
    {
        if (!TryParse(yyyymmdd, out _))
            throw new ValidationException($"{campo} deve ser uma data válida no formato YYYYMMDD");
    }
}
