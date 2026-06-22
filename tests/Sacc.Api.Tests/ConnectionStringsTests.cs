using Sacc.Api.Core;
using Xunit;

namespace Sacc.Api.Tests;

public sealed class ConnectionStringsTests
{
    [Fact]
    public void Postgres_url_sqlalchemy_convertida_com_senha_url_decoded()
    {
        var cs = ConnectionStrings.ToNpgsql("postgresql+asyncpg://sacc:Cast%402026%21@db-host:5432/sacc_db");

        Assert.Contains("Host=db-host", cs);
        Assert.Contains("Port=5432", cs);
        Assert.Contains("Database=sacc_db", cs);
        Assert.Contains("Username=sacc", cs);
        Assert.Contains("Password=Cast@2026!", cs); // %40=@ , %21=!
    }

    [Fact]
    public void SqlServer_url_convertida_com_host_porta_e_flags()
    {
        var cs = ConnectionStrings.ToSqlServer(
            "mssql+aioodbc://leitor:pwd@erp-host:1433/db_integ_intranet?driver=ODBC+Driver+18&TrustServerCertificate=yes&Encrypt=yes");

        Assert.Contains("erp-host,1433", cs);
        Assert.Contains("db_integ_intranet", cs);
        Assert.Contains("leitor", cs);
        Assert.Contains("Trust Server Certificate=True", cs);
        Assert.Contains("Encrypt=True", cs);
    }
}
