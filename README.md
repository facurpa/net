# SACC Backend — réplica .NET 8

Réplica fiel da API SACC (originalmente FastAPI/Python) em **.NET 8 + ASP.NET Core (Controllers) + Dapper + JWT**.
Aponta para o **mesmo PostgreSQL `sacc_db`** (schema gerido pelo Alembic — esta app **não** cria/altera schema)
e para a **view read-only do ERP TOTVS** (SQL Server).

## Como rodar

```bash
cp .env.example .env       # ajuste os valores
dotnet run --project src/Sacc.Api
```

- `dotnet build Sacc.sln` — compila (0 warnings).
- `dotnet test` — 33 testes.
- Swagger em `/docs` (somente `ENV=dev`). Health em `/health`.

O `.env` é lido por `Core/DotEnv.cs` (mesmos nomes do `.env` legado Python). A `DATABASE_URL`/`ERP_DATABASE_URL`
no formato SQLAlchemy são convertidas automaticamente para connection strings Npgsql / SqlClient
(senha URL-decoded). Config inválida **falha o boot**.

## Estrutura

```
src/Sacc.Api/
  Core/            AppSettings, Db (Dapper/jsonb/Clock), Security (Argon2/JWT/CurrentUser), Errors, Logging, RateLimiting, Validation, Web
  Features/        Auth, PlanoContas, Destinatarios, EmailTemplates, Periodos, Logs, Usuarios, Balancete, Worker
tests/Sacc.Api.Tests/
```

## Paridade — verificado

- **Argon2id** `time=3, mem=65536, p=4, hash=32, salt=16` → PHC `$argon2id$v=19$m=65536,t=3,p=4$…`
  (Isopoh; aceita hashes do `argon2-cffi`). `NeedsRehash` + política PT-BR + blocklist.
- **JWT HS256** com claims exatas `sub,email,role,display_name,type,jti,iat,exp` (+`scope` no fluxo restrito);
  refresh `sub,type,jti,iat,exp`. Sem `nbf`, sem `kid`. Mesma `SECRET_KEY` ⇒ tokens mútuos.
- **Erros** no estilo FastAPI: `{ "detail": ... }`; 401 com `WWW-Authenticate: Bearer`.
- Rotas/métodos/status/shapes JSON em `snake_case` conforme Seção 8; `versao = MAX+1`; jsonb p/ `contas_viradas`/`detalhes`.
- `DateTime.Kind`: `timestamptz` → UTC; `timestamp` sem tz → UTC "naive" (`Clock.UtcNowNaive`).
- Worker: `detectar_contas_viradas` (puro, atrás de `IVerificacaoSaldoService`, **TODO saldo/movimento preservado**),
  scheduler Quartz só com `RUN_SCHEDULER=true` (cron `America/Sao_Paulo`), trigger manual 202, classificação de
  incidentes + textos de ação exatos, e-mail (negócio e falha) via MailKit (nunca lança).

## Paridade vs Segurança (Seção 13) — lacunas MANTIDAS de propósito

Marcadas com `// TODO(security)` no código, para não divergir silenciosamente da origem:

- `plano-contas`, `destinatarios`, `email-templates`, `periodos`, `logs`, `worker/trigger` — **públicos** (sem auth).
- `balancete` — público via `BALANCETE_PUBLIC=true`; o **guard de boot aborta em produção**.
- `usuarios/*` exige `require_admin`; `auth/{logout,me,change-password}` exigem token.

Para fechar essas lacunas, aplique `RequireAuthorization()` aos grupos acima **em um commit isolado** — isso
**diverge** da origem (decisão intencional do operador).

## Pendente / requer ambiente real

- Validação contra o **banco e a view reais** (CRUD, login com usuário criado pelo Python, refresh rotation,
  jsonb, leitura ERP). Os testes atuais cobrem as unidades puras/seguras; o I/O depende de Postgres/SQL Server vivos.
- **Vetor de hash Argon2 gerado pelo Python**: quando disponível, adicionar um teste fixando esse hash e
  verificando `Verify(...) == true` (interop). Hoje validamos round-trip + parsing de parâmetros PHC.
- `senhas_proibidas.txt` e o template HTML são **recriações** (a origem não estava disponível) — substituir
  pelos arquivos reais quando possível.
