using System.Text.Json.Serialization;

namespace Sacc.Api.Features.Worker;

/// <summary>Item gravado em <c>logs_execucao.contas_viradas</c> (jsonb).</summary>
public sealed class ContaViradaJson
{
    [JsonPropertyName("conta")]
    public string Conta { get; set; } = "";

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = "";
}
