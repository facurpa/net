namespace Sacc.Api.Core;

/// <summary>
/// Fonte de tempo UTC. A versão Python grava UTC "naive" nas colunas <c>timestamp</c> sem timezone
/// (<c>datetime.now(utc).replace(tzinfo=None)</c>). Para paridade e para evitar o erro do Npgsql
/// "Cannot write DateTime with Kind=Utc to timestamp without time zone", essas colunas usam
/// <see cref="UtcNowNaive"/> (Kind=Unspecified representando UTC).
/// </summary>
public static class Clock
{
    /// <summary>Agora em UTC, <c>Kind=Utc</c> — para colunas <c>timestamptz</c>.</summary>
    public static DateTime UtcNow() => DateTime.UtcNow;

    /// <summary>Agora em UTC "naive", <c>Kind=Unspecified</c> — para colunas <c>timestamp</c> sem tz.</summary>
    public static DateTime UtcNowNaive() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    /// <summary>Converte um instante UTC para o formato "naive" gravado nas colunas sem tz.</summary>
    public static DateTime ToNaiveUtc(DateTime utc) =>
        DateTime.SpecifyKind(utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime(), DateTimeKind.Unspecified);
}
