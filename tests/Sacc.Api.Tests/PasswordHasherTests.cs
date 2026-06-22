using Sacc.Api.Core;
using Sacc.Api.Core.Errors;
using Sacc.Api.Core.Security;
using Xunit;

namespace Sacc.Api.Tests;

public sealed class PasswordHasherTests
{
    private static PasswordHasher Make(out string blocklistPath, int minLength = 12)
    {
        blocklistPath = Path.Combine(Path.GetTempPath(), $"blocklist_{Guid.NewGuid():N}.txt");
        File.WriteAllText(blocklistPath, "# teste\nSenhaComum123\nbrasil2026\n");
        var settings = new AppSettings { PasswordMinLength = minLength };
        return new PasswordHasher(settings, blocklistPath);
    }

    [Fact]
    public void Hash_gera_phc_argon2id_e_verifica()
    {
        var hasher = Make(out _);
        var hash = hasher.Hash("SenhaForte123");

        Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=4$", hash);
        Assert.True(hasher.Verify(hash, "SenhaForte123"));
        Assert.False(hasher.Verify(hash, "SenhaErrada999"));
    }

    [Fact]
    public void NeedsRehash_false_para_hash_proprio()
    {
        var hasher = Make(out _);
        var hash = hasher.Hash("SenhaForte123");
        Assert.False(hasher.NeedsRehash(hash));
    }

    [Fact]
    public void NeedsRehash_true_para_parametros_divergentes()
    {
        var hasher = Make(out _);
        Assert.True(hasher.NeedsRehash("$argon2id$v=19$m=4096,t=2,p=1$YWFhYWFhYWE$YWFhYWFhYWFhYWFhYWFhYQ"));
        Assert.True(hasher.NeedsRehash("não-é-um-hash"));
    }

    [Theory]
    [InlineData("curta1A", "pelo menos 12")]
    [InlineData("semmaiuscula123", "letra maiúscula")]
    [InlineData("SEMMINUSCULA123", "letra minúscula")]
    [InlineData("SemNumeroAqui", "número")]
    [InlineData("SenhaComum123", "muito comum")]
    public void Politica_rejeita_senha_invalida(string senha, string trechoMensagem)
    {
        var hasher = Make(out _);
        var ex = Assert.Throws<ValidationException>(() => hasher.ValidarSenhaPolitica(senha));
        Assert.Contains(trechoMensagem, ex.Detail);
    }

    [Fact]
    public void Politica_aceita_senha_forte()
    {
        var hasher = Make(out _);
        hasher.ValidarSenhaPolitica("SenhaMuitoForte2026");
    }
}
