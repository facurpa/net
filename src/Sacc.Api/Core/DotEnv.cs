namespace Sacc.Api.Core;

/// <summary>
/// Carregador mínimo de <c>.env</c> (KEY=VALUE) para reaproveitar o arquivo legado.
/// Define variáveis de ambiente apenas se ainda não existirem. Chamar antes de criar o builder.
/// </summary>
public static class DotEnv
{
    public static void Load()
    {
        // Procura o .env subindo a árvore de diretórios a partir do diretório atual e do binário.
        // Cobre tanto `dotnet run` da raiz do repo quanto o debug no VS (CWD = pasta do projeto).
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var path = FindUpwards(start);
            if (path is not null)
            {
                LoadFile(path);
                return;
            }
        }
    }

    private static string? FindUpwards(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
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
