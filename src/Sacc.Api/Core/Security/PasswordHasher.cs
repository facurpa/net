using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Isopoh.Cryptography.Argon2;
using Sacc.Api.Core.Errors;

namespace Sacc.Api.Core.Security;

/// <summary>
/// Hash e verificação de senha com Argon2id (compatível com <c>argon2-cffi</c>) e a política de senha.
/// Parâmetros: time_cost=3, memory_cost=65536 KiB, parallelism=4, hash_len=32, salt_len=16, variante argon2id.
/// A string gerada/validada é PHC: <c>$argon2id$v=19$m=65536,t=3,p=4$&lt;salt&gt;$&lt;hash&gt;</c>.
/// </summary>
public sealed class PasswordHasher
{
    public const int TimeCost = 3;
    public const int MemoryCost = 65536; // KiB = 64 MB
    public const int Parallelism = 4;
    public const int HashLength = 32;
    public const int SaltLength = 16;

    private readonly int _minLength;
    private readonly HashSet<string> _blocklist;

    private static readonly Regex PhcParams =
        new(@"\$argon2(id|i|d)\$v=(?<v>\d+)\$m=(?<m>\d+),t=(?<t>\d+),p=(?<p>\d+)\$", RegexOptions.Compiled);

    public PasswordHasher(AppSettings settings, string? blocklistPath = null)
    {
        _minLength = settings.PasswordMinLength;
        _blocklist = LoadBlocklist(blocklistPath ?? DefaultBlocklistPath());
    }

    private static string DefaultBlocklistPath() =>
        Path.Combine(AppContext.BaseDirectory, "Core", "Security", "senhas_proibidas.txt");

    private static HashSet<string> LoadBlocklist(string path)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return set;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            set.Add(line);
        }
        return set;
    }

    /// <summary>Gera um hash PHC argon2id com os parâmetros do baseline.</summary>
    public string Hash(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing, // argon2id
            Version = Argon2Version.Nineteen,
            TimeCost = TimeCost,
            MemoryCost = MemoryCost,
            Lanes = Parallelism,
            Threads = Parallelism,
            Password = Encoding.UTF8.GetBytes(senha),
            Salt = salt,
            HashLength = HashLength,
        };
        using var argon2 = new Argon2(config);
        using var hash = argon2.Hash();
        return config.EncodeString(hash.Buffer);
    }

    /// <summary>Verifica uma senha contra um hash PHC (aceita os hashes gravados pela versão Python).</summary>
    public bool Verify(string hash, string senha)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        try
        {
            return Argon2.Verify(hash, Encoding.UTF8.GetBytes(senha));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>true se o hash não usa exatamente os parâmetros do baseline (deve ser regravado no login).</summary>
    public bool NeedsRehash(string hash)
    {
        var m = PhcParams.Match(hash);
        if (!m.Success) return true;
        if (m.Groups[1].Value != "id") return true;
        return m.Groups["v"].Value != "19"
            || m.Groups["m"].Value != MemoryCost.ToString()
            || m.Groups["t"].Value != TimeCost.ToString()
            || m.Groups["p"].Value != Parallelism.ToString();
    }

    /// <summary>
    /// Valida a política de senha. Lança <see cref="ValidationException"/> (→ 422) com mensagem PT-BR
    /// na primeira regra violada.
    /// </summary>
    public void ValidarSenhaPolitica(string senha)
    {
        if (senha.Length < _minLength)
            throw new ValidationException($"A senha deve ter pelo menos {_minLength} caracteres");
        if (!senha.Any(char.IsUpper))
            throw new ValidationException("A senha deve conter pelo menos uma letra maiúscula");
        if (!senha.Any(char.IsLower))
            throw new ValidationException("A senha deve conter pelo menos uma letra minúscula");
        if (!senha.Any(char.IsDigit))
            throw new ValidationException("A senha deve conter pelo menos um número");
        if (_blocklist.Contains(senha))
            throw new ValidationException("Essa senha é muito comum. Escolha uma senha mais segura");
    }
}
