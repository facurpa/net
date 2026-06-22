namespace Sacc.Api.Core;

/// <summary>
/// Carregador mínimo de <c>.env</c> (KEY=VALUE) para reaproveitar o arquivo legado.
/// Define variáveis de ambiente apenas se ainda não existirem. Chamar antes de criar o builder.
/// </summary>
public static class DotEnv
{
    public static void Load()
    {
        foreach (var dir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var path = Path.Combine(dir, ".env");
            if (File.Exists(path))
            {
                LoadFile(path);
                return;
            }
        }
    }

    private static void LoadFile(string path)
    {
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var idx = line.IndexOf('=');
            if (idx <= 0) continue;

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];

            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
