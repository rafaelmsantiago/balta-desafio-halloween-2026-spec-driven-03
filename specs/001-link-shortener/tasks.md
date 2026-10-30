---

description: "Lista de tarefas da feature Encurtador de Links"
---

# Tasks: Encurtador de Links

**Input**: Design documents from `/specs/001-link-shortener/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/http-api.md](./contracts/http-api.md), [quickstart.md](./quickstart.md)

**Tests**: OBRIGATÓRIOS. A constituição exige teste unitário xUnit para toda regra de negócio
(Princípio III), teste de integração do caminho feliz para todo endpoint (Princípio IV) e
teste do formato 400 `ProblemDetails` (Princípio V). Em cada história, escreva os testes
primeiro e confirme que falham antes de implementar.

**Organization**: tarefas agrupadas por história de usuário, para que cada uma possa ser
implementada e testada de forma independente.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tarefa incompleta)
- **[Story]**: história a que a tarefa pertence (US1, US2, US3, US4)
- Os caminhos são relativos à raiz do repositório (`C:\Dev\_Estudos\encurtador`)

## Convenções que valem para todas as tarefas

- `net10.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`.
- Nomes de tipos, arquivos, pastas, rotas e campos JSON **em inglês**. Toda mensagem de
  erro para o cliente (`title`, `detail`, erros de campo) **em português**, com o texto exato
  de [contracts/http-api.md](./contracts/http-api.md) (Princípio VII).
- Namespaces seguem as pastas: `Encurtador.Application.Links`,
  `Encurtador.Infrastructure.Persistence`, `Encurtador.Api.Endpoints` etc.
- Endpoints injetam **apenas** `LinkService` (e `IConfiguration`, se precisarem); nunca
  `DbContext` ou `ILinkRepository` (Princípio I).
- Nenhum `FromSqlRaw`, `ExecuteSqlRaw`, `SqlQuery` ou `PRAGMA` em lugar nenhum (Princípio II, D7).
- Nenhum pacote NuGet além dos listados em T003 (Princípio VI).
- O relógio é sempre `TimeProvider` injetado; nunca `DateTime.UtcNow`/`DateTimeOffset.UtcNow`
  direto. Instantes no domínio são `DateTime` com `Kind = Utc`, obtidos por
  `timeProvider.GetUtcNow().UtcDateTime` (D6).
- Commits: `tipo(escopo): descrição [FR-xxx]` (ex.: `feat(links): cria link encurtado [FR-001]`);
  sem requisito → `[sem-requisito]`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: criar a solução, os projetos, as referências e as dependências.

- [X] T001 Criar a solução e os projetos na raiz: `dotnet new sln --format slnx -n Encurtador` (gera `Encurtador.slnx`); `dotnet new web -o src/Encurtador.Api`; `dotnet new classlib -o src/Encurtador.Application`; `dotnet new classlib -o src/Encurtador.Infrastructure`; `dotnet new classlib -o tests/Encurtador.UnitTests`; `dotnet new classlib -o tests/Encurtador.IntegrationTests`; adicionar os cinco ao `Encurtador.slnx` com `dotnet sln add`; apagar os `Class1.cs` gerados
- [X] T002 Configurar referências entre projetos: `src/Encurtador.Api/Encurtador.Api.csproj` → Application e Infrastructure; `src/Encurtador.Infrastructure/Encurtador.Infrastructure.csproj` → Application; `tests/Encurtador.UnitTests/Encurtador.UnitTests.csproj` → Application (somente); `tests/Encurtador.IntegrationTests/Encurtador.IntegrationTests.csproj` → Api. `Encurtador.Application` NÃO referencia EF Core nem Infrastructure
- [X] T003 Adicionar os pacotes NuGet de [research.md → Dependências](./research.md#dependências-nuget-princípio-vi) (versão estável mais recente compatível com .NET 10): `Microsoft.EntityFrameworkCore.Sqlite` e `Microsoft.EntityFrameworkCore.Design` (com `<PrivateAssets>all</PrivateAssets>`) em `src/Encurtador.Infrastructure/Encurtador.Infrastructure.csproj`; `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`) em `src/Encurtador.Api/Encurtador.Api.csproj`, para ser startup project do `dotnet ef`; `xunit.v3`, `xunit.runner.visualstudio` e `Microsoft.NET.Test.Sdk` nos dois projetos de teste, com `<OutputType>Exe</OutputType>` e `<IsPackable>false</IsPackable>`; `Microsoft.AspNetCore.Mvc.Testing` em `tests/Encurtador.IntegrationTests/Encurtador.IntegrationTests.csproj`. Nenhum outro pacote
- [X] T004 [P] Criar o manifesto de ferramentas locais com `dotnet new tool-manifest` e `dotnet tool install dotnet-ef` (versão alinhada ao EF Core de T003), gerando `.config/dotnet-tools.json`
- [X] T005 [P] Criar `Directory.Build.props` na raiz com `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable` e `TreatWarningsAsErrors=true`, e remover essas propriedades dos `.csproj`; criar `.gitignore` na raiz ignorando `bin/`, `obj/`, `*.db`, `*.db-shm`, `*.db-wal` e `.vs/`
- [X] T006 Validar o setup: `dotnet build Encurtador.slnx` compila sem erros nem warnings

**Checkpoint**: solução compila, referências respeitam as camadas do plano.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: entidade, persistência, pipeline de erros e infraestrutura de teste que todas as histórias usam.

**⚠️ CRITICAL**: nenhuma história começa antes desta fase terminar.

### Domínio e contrato de persistência (Application)

- [X] T007 [P] Criar a entidade `Link` em `src/Encurtador.Application/Links/Link.cs` conforme [data-model.md](./data-model.md). Propriedades com setter `private` (ou `init`): `long Id`; `string Code` ("Índice único `IX_Links_Code`, case-sensitive", "Imutável"); `string DestinationUrl` ("≤ 2048 caracteres", "Imutável"); `DateTime CreatedAt` (UTC); `DateTime? ExpiresAt` (UTC, "Opcional"); `long AccessCount` ("Começa em 0", nunca alterado pela aplicação); `byte[] ManagementKeyHash` ("SHA-256 da chave de gerenciamento", 32 bytes). Construtor público `Link(string code, string destinationUrl, DateTime createdAt, DateTime? expiresAt, byte[] managementKeyHash)` com `AccessCount = 0`; construtor privado sem parâmetros para o EF Core; método puro `bool IsExpiredAt(DateTime nowUtc) => ExpiresAt is not null && nowUtc >= ExpiresAt`. Nenhum método que altere destino, código, chave ou contador (FR-014). Adicionar `internal static Link Restore(long id, string code, string destinationUrl, DateTime createdAt, DateTime? expiresAt, long accessCount, byte[] managementKeyHash)`, que serve só para os testes montarem uma `Link` com contador. Criar `src/Encurtador.Application/AssemblyInfo.cs` com `[assembly: InternalsVisibleTo("Encurtador.UnitTests")]`. O EF Core não usa esse método
- [X] T008 [P] Criar em `src/Encurtador.Application/Links/ILinkRepository.cs` a interface `ILinkRepository` e o enum `AddLinkResult { Added, DuplicateCode }`, com exatamente: `Task<bool> CodeExistsAsync(string code, CancellationToken ct)`; `Task<AddLinkResult> AddAsync(Link link, CancellationToken ct)`; `Task<Link?> GetByCodeAsync(string code, CancellationToken ct)`; `Task<bool> TryRegisterAccessAsync(string code, DateTime nowUtc, CancellationToken ct)`. Documentar em XML doc que `TryRegisterAccessAsync` faz o incremento atômico condicionado a `ExpiresAt IS NULL OR ExpiresAt > nowUtc` e retorna `true` só se uma linha foi afetada (D2)
- [X] T009 [P] Criar `src/Encurtador.Application/Links/ShortCodeFormat.cs`: classe estática com `Regex` compilado `^[A-Za-z0-9_-]{4,20}$` e método `bool IsWellFormed(string? code)`, usado para rejeitar códigos de formato impossível sem ir ao banco (D9) e para validar códigos personalizados (FR-003)

### Persistência (Infrastructure)

- [X] T010 Criar `src/Encurtador.Infrastructure/Persistence/LinksDbContext.cs` (`DbSet<Link> Links`, aplica as configurações do assembly) e `src/Encurtador.Infrastructure/Persistence/LinkConfiguration.cs` (`IEntityTypeConfiguration<Link>`): tabela `Links`; `Id` chave; `Code` obrigatório, `HasMaxLength(20)`, `HasIndex(l => l.Code).IsUnique().HasDatabaseName("IX_Links_Code")`; `DestinationUrl` obrigatório, `HasMaxLength(2048)`; `CreatedAt` obrigatório e `ExpiresAt` opcional, ambos com value converter que grava como está e lê com `DateTime.SpecifyKind(v, DateTimeKind.Utc)`; `AccessCount` obrigatório, sem token de concorrência; `ManagementKeyHash` obrigatório, `HasMaxLength(32)`. Depende de T007
- [X] T011 [P] Criar `src/Encurtador.Infrastructure/Persistence/UniqueViolation.cs`: classe estática `internal` com `bool IsUniqueViolation(DbUpdateException ex)`, que retorna `true` quando `ex.InnerException` é `Microsoft.Data.Sqlite.SqliteException` com `SqliteErrorCode == 19` e `SqliteExtendedErrorCode == 2067` (`SQLITE_CONSTRAINT_UNIQUE`). Comentar o ponto de extensão para PostgreSQL (`PostgresException.SqlState == "23505"`), sem implementá-lo (D1, D6)
- [X] T012 Criar `src/Encurtador.Infrastructure/Persistence/EfLinkRepository.cs` implementando `ILinkRepository` com `LinksDbContext`. `CodeExistsAsync` → `AnyAsync(l => l.Code == code)`. `GetByCodeAsync` → `AsNoTracking().SingleOrDefaultAsync(l => l.Code == code)`. `AddAsync` → `Add` + `SaveChangesAsync`; em `catch (DbUpdateException ex) when (UniqueViolation.IsUniqueViolation(ex))`, desanexar a entidade (`Entry(link).State = EntityState.Detached`, para um retry no mesmo escopo não reenviar o insert) e retornar `DuplicateCode`. `TryRegisterAccessAsync` → `Links.Where(l => l.Code == code && (l.ExpiresAt == null || l.ExpiresAt > nowUtc)).ExecuteUpdateAsync(s => s.SetProperty(l => l.AccessCount, l => l.AccessCount + 1))` e retorna `affected == 1`. NUNCA ler `AccessCount`, somar e gravar (D2). Depende de T008, T010, T011
- [X] T013 Criar `src/Encurtador.Infrastructure/DependencyInjection.cs` com `IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)`: lê `Database:Provider` (padrão `Sqlite`); se `Sqlite`, registra `LinksDbContext` com `UseSqlite(configuration.GetConnectionString("Links"))`; qualquer outro valor → `InvalidOperationException` com mensagem clara; registra `ILinkRepository` → `EfLinkRepository` (scoped). Criar também a extensão `Task ApplyMigrationsAsync(this IServiceProvider services)`, que abre um scope e chama `Database.MigrateAsync()`, para que `Program.cs` não toque no `DbContext` (D6, D7). Depende de T012
- [X] T014 Gerar a migration inicial: `dotnet ef migrations add InitialCreate --project src/Encurtador.Infrastructure --startup-project src/Encurtador.Api --output-dir Persistence/Migrations`. Conferir em `src/Encurtador.Infrastructure/Persistence/Migrations/*_InitialCreate.cs` o índice único `IX_Links_Code` e os tamanhos 20/2048/32. Depende de T013 e T017

### Application DI e pipeline HTTP (Api)

- [X] T015 [P] Criar `src/Encurtador.Application/DependencyInjection.cs` com `IServiceCollection AddApplication(this IServiceCollection services)`, que registra `TimeProvider.System` como singleton (`TryAddSingleton`) e, nas histórias seguintes, os serviços de `Application`. `LinkService` (scoped) é registrado em T034
- [X] T016 [P] Criar `src/Encurtador.Api/Http/Problems.cs`: classe estática com fábricas `IResult` que usam `TypedResults.Problem(...)` com `type`, `title`, `status` e `detail` exatamente como em [contracts/http-api.md](./contracts/http-api.md): `LinkNotFound(string code)` (404, `/problems/link-not-found`, "Link não encontrado.", "Não existe link com o código \"{code}\"."); `LinkExpired(string code, DateTime expiresAtUtc)` (410, `/problems/link-expired`, "Link expirado.", "O link \"{code}\" expirou em {expiresAt:yyyy-MM-ddTHH:mm:ssZ}."); `CodeInUse(string code)` (409, `/problems/code-in-use`, "Código já está em uso.", "O código \"{code}\" já está em uso. Escolha outro código."); `InvalidManagementKey(HttpContext ctx)` (401, `/problems/invalid-management-key`, "Chave de gerenciamento inválida.", "Informe no cabeçalho X-Management-Key a chave entregue na criação do link.", e grava o cabeçalho `WWW-Authenticate: ManagementKey`); `Validation(IDictionary<string, string[]> errors)` → `TypedResults.ValidationProblem(errors, title: "Um ou mais campos são inválidos.")`
- [X] T017 Escrever `src/Encurtador.Api/Program.cs`: `AddApplication()`; `AddInfrastructure(builder.Configuration)`; `AddProblemDetails(o => o.CustomizeProblemDetails = …)`, que, para status 400 que não seja `HttpValidationProblemDetails`, define `title = "A requisição não pôde ser lida."` e `detail = "O corpo da requisição está ausente ou não é um JSON válido."`, e para 500 define `title = "Erro interno."`; `Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = false)`, para que falha de binding vire 400 vazio e o `UseStatusCodePages` preencha o `ProblemDetails`; `app.UseExceptionHandler()`; `app.UseStatusCodePages()`; `await app.Services.ApplyMigrationsAsync()` antes de `app.Run()`; ao final, `public partial class Program;` para o `WebApplicationFactory`. Os `Map*Endpoints` são adicionados em T036/T037. Depende de T013, T015
- [X] T018 [P] Configurar `src/Encurtador.Api/appsettings.json` com `"Database": { "Provider": "Sqlite" }`, `"ConnectionStrings": { "Links": "Data Source=links.db" }` e `"ShortLinks": { "PublicBaseUrl": "" }`; remover o endpoint "Hello World" do template

### Infraestrutura de teste

- [X] T019 [P] Criar `tests/Encurtador.UnitTests/Fakes/FakeTimeProvider.cs` (subclasse de `TimeProvider` com `DateTimeOffset UtcNow { get; set; }`, `GetUtcNow()` retornando esse valor e `Advance(TimeSpan)`) e `tests/Encurtador.UnitTests/Fakes/FakeLinkRepository.cs` (implementa `ILinkRepository` sobre `Dictionary<string, Link>` com `StringComparer.Ordinal`; `TryRegisterAccessAsync` aplica a mesma regra `ExpiresAt == null || ExpiresAt > nowUtc` e incrementa um contador por código, exposto como `AccessCountOf(code)`; `Links` expõe os links adicionados; `Queue<AddLinkResult> ForcedAddResults` permite forçar `DuplicateCode` nas próximas chamadas de `AddAsync`, simulando a corrida do índice único; contadores `AddCalls`, `CodeExistsCalls` e `TryRegisterAccessCalls`). O fake guarda o total por código em um dicionário próprio. `GetByCodeAsync` devolve `Link.Restore(...)` com o `accessCount` atual, imitando o que o banco faria. Expor `SeedAccessCount(code, n)` para preparar cenários. Depende de T007 e T008
- [X] T020 [P] Criar `tests/Encurtador.IntegrationTests/Support/MutableTimeProvider.cs` (mesmo contrato do fake de T019; projeto independente) e `tests/Encurtador.IntegrationTests/Support/LinksApiFactory.cs`: `WebApplicationFactory<Program>`, `IAsyncLifetime`; construtor opcional `LinksApiFactory(string? databasePath = null)`, que reutiliza um arquivo para o teste de reinício; caminho padrão `Path.Combine(Path.GetTempPath(), $"encurtador-{Guid.NewGuid():N}.db")`; `ConfigureWebHost` com `builder.UseSetting("ConnectionStrings:Links", $"Data Source={path}")` e `ConfigureTestServices`, que substitui `TimeProvider` por uma instância pública `Time` de `MutableTimeProvider` (iniciada em `2026-10-30T12:00:00Z`); `CreateNoRedirectClient()` → `CreateClient(new() { AllowAutoRedirect = false })`; `DisposeAsync` chama `SqliteConnection.ClearAllPools()` e apaga o arquivo e os `-shm`/`-wal`. Depende de T017
- [X] T021 [P] Escrever `tests/Encurtador.UnitTests/Links/LinkTests.cs`: link sem `ExpiresAt` nunca expira; `IsExpiredAt` é `false` 1 tick antes de `ExpiresAt`, `true` exatamente em `ExpiresAt` (Edge Case "instante exato") e `true` depois; novo link tem `AccessCount == 0`. Depende de T007
- [X] T022 Validar a fase: `dotnet build` sem warnings, `dotnet test` (com os testes de T021) verde, e `dotnet run --project src/Encurtador.Api` cria `links.db` com a tabela `Links`

**Checkpoint**: base pronta; as histórias podem começar.

---

## Phase 3: User Story 1 - Criar um link curto e ser redirecionado (Priority: P1) 🎯 MVP

**Goal**: `POST /api/links` com URL válida devolve código gerado de 7 caracteres e a chave de gerenciamento; `GET /{code}` redireciona com 302 e conta o acesso; links sobrevivem a reinício.

**Independent Test**: criar um link a partir de URL válida, abrir o `shortUrl` e chegar ao destino (302 + `Location`); reiniciar o host com o mesmo banco e repetir.

### Tests for User Story 1 ⚠️ (escrever primeiro; devem falhar)

- [X] T023 [P] [US1] Escrever `tests/Encurtador.UnitTests/Links/ShortCodeGeneratorTests.cs`: gera exatamente 7 caracteres; em 10.000 gerações, todo caractere pertence a `23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz` e nenhum é `0`, `O`, `o`, `1`, `I` ou `l` (D4); `ShortCodeGenerator.Alphabet.Length == 56`; gerações sucessivas não são todas iguais
- [X] T024 [P] [US1] Escrever `tests/Encurtador.UnitTests/Links/ManagementKeyTests.cs` (parte de geração): `ManagementKey.Generate()` retorna `PlainText` com 43 caracteres Base64Url (`^[A-Za-z0-9_-]{43}$`, sem `=`) e `Hash` com 32 bytes igual a `SHA256.HashData(Base64Url.DecodeFromChars(PlainText))`; o `Hash` é diferente dos bytes da chave; duas gerações dão chaves diferentes; `ManagementKey.ComputeHash(plain)` é determinístico
- [X] T025 [P] [US1] Escrever `tests/Encurtador.UnitTests/Links/CreateLinkValidatorUrlTests.cs` (regras de `destinationUrl`, FR-006, D8), cada caso verificando a chave `destinationUrl` e a mensagem exata do contrato: `null`/`""` → "A URL de destino é obrigatória."; `"https://"`, `"exemplo"`, `"http://"` e `" https://exemplo.com"` (espaço inicial) → "A URL de destino deve ser um endereço absoluto válido, com domínio."; `"ftp://exemplo.com"` e `"javascript:alert(1)"` → "A URL de destino deve usar o esquema http ou https."; URL http válida com 2049 caracteres → "A URL de destino deve ter no máximo 2048 caracteres."; URL com exatamente 2048 caracteres → sem erro; `http://` e `https://` com domínio → sem erro
- [X] T026 [P] [US1] Escrever `tests/Encurtador.UnitTests/Links/LinkServiceCreateTests.cs` (com `FakeLinkRepository` e `FakeTimeProvider`): pedido sem `customCode` retorna `CreateLinkResult.Created` com código de 7 caracteres do alfabeto, `CreatedAt` igual ao relógio fake (UTC), `ExpiresAt` nulo e `ManagementKey` não vazia; o `Link` salvo tem `ManagementKeyHash == ManagementKey.ComputeHash(result.ManagementKey)` e nenhuma propriedade contém a chave em texto (D3); `AccessCount` 0; quando `CodeExistsAsync` acusa colisão, gera outro código; quando `AddAsync` devolve `DuplicateCode` (fila forçada), gera outro código e tenta de novo (D1); após 5 colisões consecutivas, lança `InvalidOperationException`; pedido inválido retorna `ValidationFailed` e `AddCalls == 0`; a mesma URL criada duas vezes gera dois links com códigos diferentes
- [X] T027 [P] [US1] Escrever `tests/Encurtador.UnitTests/Links/LinkServiceResolveTests.cs`: código existente e não expirado → `ResolveLinkResult.Redirect(destinationUrl)` e `TryRegisterAccessCalls == 1`; código inexistente → `NotFound`; código mal formado (`""`, `"a b"`, 21 caracteres, `"x/y"`) → `NotFound` sem chamar o repositório
- [X] T028 [P] [US1] Escrever `tests/Encurtador.IntegrationTests/CreateLinkTests.cs` (`IClassFixture<LinksApiFactory>`): `POST /api/links` com `{"destinationUrl":"https://exemplo.com/artigos/2026/um-texto-bem-longo"}` → 201, `Location` = `/api/links/{code}/stats`, corpo com `code` de 7 caracteres do alfabeto de D4, `shortUrl` terminando em `/{code}`, `destinationUrl` igual ao enviado, `createdAt` com `Z`, `expiresAt` nulo e `managementKey` com 43 caracteres. Princípio V: `{"destinationUrl":"ftp://exemplo.com"}` → 400, `Content-Type` `application/problem+json`, `title` "Um ou mais campos são inválidos." e `errors.destinationUrl` com a mensagem de esquema; URL de 2049 caracteres → 400 com a mensagem de limite; corpo `"{ não é json"` → 400 `application/problem+json` com `title` "A requisição não pôde ser lida."
- [X] T029 [P] [US1] Escrever `tests/Encurtador.IntegrationTests/RedirectTests.cs` (`IClassFixture<LinksApiFactory>`, cliente sem auto-redirect): após criar um link, `GET /{code}` → 302, `Location` igual ao destino, `Cache-Control` contendo `no-store`; `GET /naoexiste1` → 404 `application/problem+json` com `type` `/problems/link-not-found`; `GET /api` → 404 (reservado nunca existe). Em `tests/Encurtador.IntegrationTests/PersistenceTests.cs`: criar link com uma `LinksApiFactory(path)`, descartá-la, abrir nova `LinksApiFactory(path)` no mesmo arquivo e confirmar que `GET /{code}` ainda devolve 302 para o mesmo destino (US1 cenário 6, SC-005)

### Implementation for User Story 1

- [X] T030 [P] [US1] Criar `src/Encurtador.Application/Links/ShortCodeGenerator.cs`: `public const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";`, `public const int Length = 7;` e `string Generate()` com `new string(RandomNumberGenerator.GetItems<char>(Alphabet, Length))` (D4). Registrar como singleton em `src/Encurtador.Application/DependencyInjection.cs`
- [X] T031 [P] [US1] Criar `src/Encurtador.Application/Links/ManagementKey.cs`: `readonly record struct ManagementKey(string PlainText, byte[] Hash)`; `static ManagementKey Generate()` (32 bytes de `RandomNumberGenerator.GetBytes(32)`, `PlainText = Base64Url.EncodeToString(bytes)` de `System.Buffers.Text`, `Hash = SHA256.HashData(bytes)`); `static byte[] ComputeHash(string plainText)`, que decodifica o Base64Url e aplica SHA-256 (D3). A verificação entra em T046
- [X] T032 [P] [US1] Criar `src/Encurtador.Application/Links/CreateLinkRequest.cs` (`sealed record CreateLinkRequest(string? DestinationUrl, string? CustomCode, string? ExpiresAt)`) e, em `src/Encurtador.Application/Links/Results/`, os resultados: `CreateLinkResult.cs` (hierarquia `abstract record` com `Created(string Code, string DestinationUrl, DateTime CreatedAt, DateTime? ExpiresAt, string ManagementKey)` e `ValidationFailed(IReadOnlyDictionary<string, string[]> Errors)`) e `ResolveLinkResult.cs` (`Redirect(string DestinationUrl)` e `NotFound`)
- [X] T033 [US1] Criar `src/Encurtador.Application/Links/CreateLinkValidator.cs`: classe que recebe `TimeProvider`, com `CreateLinkValidation Validate(CreateLinkRequest request)`. Criar em `src/Encurtador.Application/Links/CreateLinkValidation.cs` o `sealed record CreateLinkValidation(IReadOnlyDictionary<string, string[]> Errors, ValidatedLink? Value)`, com `bool IsValid => Errors.Count == 0`, e o `sealed record ValidatedLink(string DestinationUrl, string? CustomCode, DateTime? ExpiresAtUtc)`. `Value` só é preenchido quando não há erros. O método acumula erros por campo com chaves `destinationUrl`, `customCode` e `expiresAt` em camelCase. Nesta história, só as regras de `destinationUrl`, na ordem: vazio → obrigatória; `Length > 2048` → limite; espaço no início/fim ou `!Uri.TryCreate(value, UriKind.Absolute, out var uri)` → endereço inválido; esquema diferente de `http`/`https` (`Uri.UriSchemeHttp`/`UriSchemeHttps`) → mensagem de esquema; `string.IsNullOrEmpty(uri.Host)` → endereço inválido. Um erro por campo, com o texto exato do contrato. Registrar no DI de Application. Depende de T032
- [X] T034 [US1] Criar `src/Encurtador.Application/Links/LinkService.cs` com construtor `(ILinkRepository repository, CreateLinkValidator validator, ShortCodeGenerator generator, TimeProvider time)`. `Task<CreateLinkResult> CreateAsync(CreateLinkRequest request, CancellationToken ct)`: valida (com erro, `ValidationFailed` sem tocar no repositório); `now = time.GetUtcNow().UtcDateTime`; gera `ManagementKey`; para `attempt` de 1 a 5: `code = generator.Generate()`; se `CodeExistsAsync`, continua; cria um **novo** `Link(code, url, now, null, key.Hash)`; `AddAsync`; com `Added`, retorna `Created(..., key.PlainText)`; com `DuplicateCode`, continua; após 5 tentativas, `throw new InvalidOperationException("Não foi possível gerar um código único.")`. `Task<ResolveLinkResult> ResolveAsync(string code, CancellationToken ct)`: `!ShortCodeFormat.IsWellFormed(code)` → `NotFound`; `GetByCodeAsync` nulo → `NotFound`; `TryRegisterAccessAsync(code, now)` verdadeiro → `Redirect(link.DestinationUrl)`; caso contrário → `NotFound` (US4 substitui esse ramo por `Expired`). Registrar `LinkService` como scoped em `src/Encurtador.Application/DependencyInjection.cs`. Depende de T030–T033
- [X] T035 [P] [US1] Criar os DTOs HTTP em `src/Encurtador.Api/Contracts/`: `CreateLinkHttpRequest.cs` (`sealed record CreateLinkHttpRequest(string? DestinationUrl, string? CustomCode, string? ExpiresAt)`, todos `string?` para acumular erros, D8) e `LinkCreatedResponse.cs` (`Code`, `ShortUrl`, `DestinationUrl`, `CreatedAt`, `ExpiresAt`, `ManagementKey`)
- [X] T036 [US1] Criar `src/Encurtador.Api/Endpoints/LinkEndpoints.cs` com `static RouteGroupBuilder MapLinkEndpoints(this IEndpointRouteBuilder app)`, que cria o grupo `/api/links` e mapeia `POST /` → `CreateLinkHttpRequest` → `CreateLinkRequest` → `LinkService.CreateAsync` → `switch`: `Created` → `TypedResults.Created($"/api/links/{code}/stats", LinkCreatedResponse)`; `ValidationFailed` → `Problems.Validation(errors)`. `shortUrl` = `ShortLinks:PublicBaseUrl` sem `/` final, ou `{Request.Scheme}://{Request.Host}` quando vazio, + `/` + código (D11). Sem regra de negócio e sem acesso a dados no endpoint. Chamar `app.MapLinkEndpoints()` em `src/Encurtador.Api/Program.cs`. Depende de T034, T035, T016
- [X] T037 [US1] Criar `src/Encurtador.Api/Endpoints/RedirectEndpoints.cs` com `MapRedirectEndpoints`: `GET /{code}` → `LinkService.ResolveAsync` → `Redirect` → define `Response.Headers.CacheControl = "no-store"` e retorna `TypedResults.Redirect(url, permanent: false)` (302, nunca 301/308, D9); `NotFound` → `Problems.LinkNotFound(code)`. Chamar `app.MapRedirectEndpoints()` em `src/Encurtador.Api/Program.cs`, **depois** de `MapLinkEndpoints`. Depende de T034
- [X] T038 [US1] Rodar `dotnet test` e confirmar T021 e T023–T029 verdes

**Checkpoint**: MVP funcional. Criar e redirecionar funcionam e persistem.

---

## Phase 4: User Story 2 - Contar e consultar acessos (Priority: P2)

**Goal**: `GET /api/links/{code}/stats` com `X-Management-Key` válida devolve o total de acessos e os dados do link; sem chave válida, 401 sem nenhum dado; contagem exata sob concorrência.

**Independent Test**: criar um link, abri-lo 3 vezes e consultar com a chave → `accessCount = 3`; consultar sem chave → 401.

### Tests for User Story 2 ⚠️

- [X] T039 [P] [US2] Acrescentar a `tests/Encurtador.UnitTests/Links/ManagementKeyTests.cs` os testes de verificação: `ManagementKey.Verify(plain, hash)` é `true` para o par gerado; `false` para outra chave válida; `false` para `null`, `""`, Base64Url inválido (`"@@@"`) e Base64Url válido que não decodifica para exatamente 32 bytes; nenhum desses casos lança exceção
- [X] T040 [P] [US2] Escrever `tests/Encurtador.UnitTests/Links/LinkServiceStatsTests.cs`: código inexistente → `LinkStatsResult.NotFound`, mesmo com chave qualquer (o 404 vem antes da chave, US2 cenário 5); código existente com chave ausente, errada ou de outro link → `Unauthorized`, um caso sem propriedades de dados; chave correta → `Stats` com `Code`, `DestinationUrl`, `CreatedAt`, `ExpiresAt`, `IsExpired = false` e `AccessCount` igual ao do repositório fake; código mal formado → `NotFound`
- [X] T041 [P] [US2] Escrever `tests/Encurtador.IntegrationTests/StatsTests.cs` (`IClassFixture<LinksApiFactory>`): link recém-criado → 200 com `accessCount: 0` (cenário 1); após 5 `GET /{code}` → `accessCount: 5` (cenário 2); sem cabeçalho → 401 `application/problem+json`, `type` `/problems/invalid-management-key`, cabeçalho `WWW-Authenticate: ManagementKey` e corpo **sem** as propriedades `accessCount`, `destinationUrl`, `createdAt` e `expiresAt` (cenário 6, SC-007); chave errada → mesmo 401; chave do link A no link B → 401 (cenário 7); usar o código como chave → 401; usar a chave como código (`GET /api/links/{managementKey}/stats`) → 404 sem dados (Edge Case); código inexistente → 404 (cenário 5); chave na query string (`?managementKey=`) não é aceita → 401
- [X] T042 [P] [US2] Escrever `tests/Encurtador.IntegrationTests/ConcurrencyTests.cs`: criar um link; disparar **1.000** `GET /{code}` simultâneos (`Task.WhenAll`, cliente sem auto-redirect), todos 302; consultar stats → `accessCount == 1000` exatamente (SC-003, US2 cenário 3, D2). Segundo teste: enquanto 200 acessos concorrentes rodam, consultar stats repetidamente e afirmar que cada valor é ≥ ao anterior e o último é 200 (US2 cenário 4). Terceiro teste (SC-002): com o link já criado, fazer 200 `GET /{code}` sequenciais medindo cada um com `Stopwatch` e afirmar que o p95 fica abaixo de 1.000 ms. Comentar no teste que o `TestServer` mede só aplicação e banco, sem rede, e serve como limite de regressão. O SC-002 completo é validado no cenário manual do quickstart. Se algum teste falhar por lock do SQLite (`SQLITE_BUSY`), a saída é uma emenda no plano (D7, Princípio II), nunca um `PRAGMA` sem registro
- [X] T043 [P] [US2] Estender `tests/Encurtador.IntegrationTests/PersistenceTests.cs`: antes do reinício, abrir o link 3 vezes; depois do reinício, stats com a chave original → `accessCount == 3`, mesmo `destinationUrl` e mesmo `createdAt` (SC-005, FR-013)

### Implementation for User Story 2

- [X] T044 [P] [US2] Criar `src/Encurtador.Application/Links/Results/LinkStatsResult.cs`: `abstract record` com `Stats(string Code, string DestinationUrl, DateTime CreatedAt, DateTime? ExpiresAt, bool IsExpired, long AccessCount)`, `NotFound` e `Unauthorized`, os dois últimos sem dados
- [X] T045 [P] [US2] Criar `src/Encurtador.Api/Contracts/LinkStatsResponse.cs` com `Code`, `DestinationUrl`, `CreatedAt`, `ExpiresAt`, `IsExpired` e `AccessCount` (serializados em camelCase)
- [X] T046 [US2] Adicionar `static bool Verify(string? plainText, byte[] expectedHash)` em `src/Encurtador.Application/Links/ManagementKey.cs`: retorna `false` para nulo/vazio, para `Base64Url.TryDecodeFromChars` que falha ou para tamanho decodificado ≠ 32; caso contrário, compara `SHA256.HashData(bytes)` com `expectedHash` usando `CryptographicOperations.FixedTimeEquals` (D3). Nunca lança exceção
- [X] T047 [US2] Adicionar `Task<LinkStatsResult> GetStatsAsync(string code, string? managementKey, CancellationToken ct)` em `src/Encurtador.Application/Links/LinkService.cs`: código mal formado → `NotFound`; `GetByCodeAsync` nulo → `NotFound`; `!ManagementKey.Verify(managementKey, link.ManagementKeyHash)` → `Unauthorized`; caso contrário, `Stats(..., IsExpired: link.IsExpiredAt(now), AccessCount: link.AccessCount)`. Depende de T044, T046
- [X] T048 [US2] Mapear `GET /{code}/stats` no grupo de `src/Encurtador.Api/Endpoints/LinkEndpoints.cs`: ler a chave **somente** de `[FromHeader(Name = "X-Management-Key")] string?`; `switch` do resultado: `Stats` → `TypedResults.Ok(LinkStatsResponse)`; `NotFound` → `Problems.LinkNotFound(code)`; `Unauthorized` → `Problems.InvalidManagementKey(httpContext)`. Depende de T045, T047
- [X] T049 [US2] Rodar `dotnet test` e confirmar T039–T043 verdes, com os testes de US1 ainda verdes

**Checkpoint**: US1 e US2 funcionam de forma independente.

---

## Phase 5: User Story 3 - Escolher o próprio código (Priority: P3)

**Goal**: o criador informa `customCode` (4–20 caracteres, `A–Z a–z 0–9 - _`); códigos reservados são recusados; códigos em uso dão 409, inclusive na corrida simultânea.

**Independent Test**: criar link com `customCode: "promo-natal"`, abrir `/promo-natal` e ser redirecionado; repetir a criação → 409.

### Tests for User Story 3 ⚠️

- [X] T050 [P] [US3] Escrever `tests/Encurtador.UnitTests/Links/CreateLinkValidatorCodeTests.cs` (FR-003, FR-005, D9), verificando a chave `customCode` e a mensagem exata: `"abc"` (3) e 21 caracteres → "O código deve ter entre 4 e 20 caracteres."; `"abcd"` (4), 20 caracteres, `"promo-natal"` e `"promo_natal"` → sem erro; `"promo natal"`, `"a/bcd"` e `"promoção"` → "O código só pode conter letras sem acento (A–Z, a–z), dígitos (0–9), hífen (-) e sublinhado (_)."; `"api"` não é testado aqui porque tem 3 caracteres e cai na regra de tamanho; `"health"`, `"HEALTH"`, `"Swagger"` e `"openapi"` → "O código \"{code}\" está indisponível."; `customCode` `null` → sem erro (código gerado); `customCode` `""` → mensagem de tamanho
- [X] T051 [P] [US3] Escrever `tests/Encurtador.UnitTests/Links/LinkServiceCustomCodeTests.cs`: código livre → `Created` com exatamente esse código e uma única chamada a `AddAsync`; `CodeExistsAsync` verdadeiro → `CreateLinkResult.CodeInUse(code)`, `AddCalls == 0`; `AddAsync` devolvendo `DuplicateCode` (corrida perdida) → `CodeInUse`, **sem** nova tentativa com outro código (D1); `"Promo"` e `"promo"` são aceitos como links distintos (case-sensitive); código personalizado pode conter `0`, `O`, `1` e `l` (D4 vale só para gerados)
- [X] T052 [P] [US3] Escrever `tests/Encurtador.IntegrationTests/CustomCodeTests.cs` (`IClassFixture<LinksApiFactory>`): `customCode: "promo-natal"` → 201 com `code == "promo-natal"` e `GET /promo-natal` → 302 (cenário 1); repetir → 409 `application/problem+json`, `type` `/problems/code-in-use`, `detail` citando o código, e o link original continua redirecionando para o destino original (cenário 4); `"Promo"` e `"promo"` → ambos 201; `"health"` → 400 com `errors.customCode`; `"abc"` → 400 (cenário 2); `"a b c"` → 400 (cenário 3)
- [X] T053 [P] [US3] Acrescentar a `tests/Encurtador.IntegrationTests/ConcurrencyTests.cs` o teste da corrida de D1: 20 `POST /api/links` simultâneos com o mesmo `customCode` (`"corrida-1"`) e destinos diferentes → exatamente um 201 e dezenove 409; `GET /corrida-1` redireciona para o destino do único 201

### Implementation for User Story 3

- [X] T054 [P] [US3] Criar `src/Encurtador.Application/Links/ReservedCodes.cs`: `static readonly FrozenSet<string> Values = new[] { "api", "health", "openapi", "swagger" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)` e `static bool IsReserved(string code)`. Comentário: o roteamento do ASP.NET Core não diferencia caixa (D9)
- [X] T055 [US3] Adicionar as regras de `customCode` em `src/Encurtador.Application/Links/CreateLinkValidator.cs`, aplicadas só quando `CustomCode is not null`, na ordem: comprimento fora de 4–20 → mensagem de tamanho; não casa `ShortCodeFormat` → mensagem de caracteres; `ReservedCodes.IsReserved` → "O código \"{code}\" está indisponível.". Os erros acumulam com os de `destinationUrl` no mesmo dicionário. Depende de T054
- [X] T056 [US3] Em `src/Encurtador.Application/Links/Results/CreateLinkResult.cs`, adicionar o caso `CodeInUse(string Code)`. Em `src/Encurtador.Application/Links/LinkService.cs`, adicionar o ramo de código personalizado ao `CreateAsync`: se `CustomCode` informado e válido, `CodeExistsAsync` verdadeiro → `CodeInUse`; senão, uma única tentativa de `AddAsync`, e `DuplicateCode` → `CodeInUse` (a corrida que a checagem não pegou, D1). O ramo de código gerado não muda. Depende de T055
- [X] T057 [US3] Mapear `CodeInUse` → `Problems.CodeInUse(code)` (409) no `switch` do `POST` em `src/Encurtador.Api/Endpoints/LinkEndpoints.cs`. Depende de T056
- [X] T058 [US3] Rodar `dotnet test` e confirmar T050–T053 verdes, com US1 e US2 ainda verdes

**Checkpoint**: US1, US2 e US3 funcionam de forma independente.

---

## Phase 6: User Story 4 - Definir data de expiração (Priority: P3)

**Goal**: `expiresAt` opcional, ISO 8601 com fuso e no futuro; link expirado responde 410 e não conta acesso; stats indica `isExpired`.

**Independent Test**: criar link com expiração 1 minuto à frente; abrir antes (302, conta); avançar o relógio fake além do prazo; abrir (410, não conta); stats com `isExpired: true` e o mesmo total.

### Tests for User Story 4 ⚠️

- [X] T059 [P] [US4] Escrever `tests/Encurtador.UnitTests/Links/CreateLinkValidatorExpirationTests.cs` (FR-007, D8), com relógio fake em `2026-10-30T12:00:00Z`, verificando a chave `expiresAt` e as mensagens exatas: `"2026-10-30T12:00:01Z"` e `"2026-10-30T09:00:01-03:00"` → válidos, normalizados para UTC (`2026-10-30T12:00:01Z`, `Kind = Utc`); `"2026-10-30T12:00:00Z"` (igual a agora) e `"2020-01-01T00:00:00Z"` → "A data de expiração precisa estar no futuro."; `"2026-12-31T23:59:00"` (sem fuso), `"31/12/2026"`, `"amanhã"` e `""` → "A data de expiração deve estar no formato ISO 8601 com fuso horário (ex.: 2026-12-31T23:59:00-03:00)."; `null` → sem erro. Caso de vários campos (Edge Case): `destinationUrl` `"ftp://x.com"`, `customCode` `"a b"` e `expiresAt` `"2020-01-01T00:00:00Z"` → três chaves no dicionário, uma mensagem cada
- [X] T060 [P] [US4] Escrever `tests/Encurtador.UnitTests/Links/LinkServiceExpirationTests.cs`: `CreateAsync` com `expiresAt` futuro grava `ExpiresAt` em UTC e devolve o valor em `Created`; `ResolveAsync` de link com `ExpiresAt <= now` → `ResolveLinkResult.Expired(code, expiresAt)` **sem** chamar `TryRegisterAccessAsync` (contador intacto, FR-009); link válido na leitura, mas cujo `TryRegisterAccessAsync` retorna `false` (expirou entre leitura e incremento) → `Expired`; `GetStatsAsync` com chave correta em link expirado → `Stats` com `IsExpired = true` e o total acumulado (US4 cenário 5); link sem expiração continua redirecionando depois de avançar o relógio em 100 anos (cenário 1)
- [X] T061 [P] [US4] Escrever `tests/Encurtador.IntegrationTests/ExpirationTests.cs` (`IClassFixture<LinksApiFactory>`, usando `factory.Time`): criar com `customCode` `"expira-1"` e `expiresAt` = agora + 1 min (com fuso `-03:00`) → 201 e `expiresAt` em UTC com `Z`; `GET /{code}` → 302; `Time.Advance(1 min)` (instante exato) → `GET` 410 `application/problem+json`, `type` `/problems/link-expired`; outro `GET` → 410; stats com a chave → 200, `isExpired: true`, `accessCount: 1` (cenários 2, 3 e 5, SC-004); depois da expiração, `POST` com `customCode` `"expira-1"` → 409 `/problems/code-in-use` (FR-004, US3 cenário 4); criar com `expiresAt` no passado → 400 com `errors.expiresAt` (cenário 4). Princípio V: `POST` com `destinationUrl` `"ftp://x.com"`, `customCode` `"a b"` e `expiresAt` `"2020-01-01T00:00:00Z"` → 400 `application/problem+json` com `errors` contendo exatamente `destinationUrl`, `customCode` e `expiresAt` (FR-015, Edge Case)

### Implementation for User Story 4

- [X] T062 [US4] Adicionar as regras de `expiresAt` em `src/Encurtador.Application/Links/CreateLinkValidator.cs`, aplicadas só quando `ExpiresAt is not null`: casar `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$` e `DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto)`; se falhar, mensagem de formato; senão, `utc = dto.UtcDateTime`; se `utc <= time.GetUtcNow().UtcDateTime`, mensagem "precisa estar no futuro". Preencher `ValidatedLink.ExpiresAtUtc` com o valor normalizado. Os erros acumulam com os demais campos
- [X] T063 [US4] Em `src/Encurtador.Application/Links/LinkService.cs`: `CreateAsync` passa o `ExpiresAt` validado (UTC) ao construtor de `Link` nos dois ramos (gerado e personalizado). Em `src/Encurtador.Application/Links/Results/ResolveLinkResult.cs`, adicionar o caso `Expired(string Code, DateTime ExpiresAt)`. `ResolveAsync`: depois do `GetByCodeAsync`, se `link.IsExpiredAt(now)` → `Expired` sem chamar `TryRegisterAccessAsync`; se `TryRegisterAccessAsync` retornar `false` → `Expired` (substitui o `NotFound` provisório de T034). `GetStatsAsync` já calcula `IsExpired` (T047). Depende de T062
- [X] T064 [US4] Mapear `Expired` → `Problems.LinkExpired(code, expiresAt)` (410) no `switch` de `src/Encurtador.Api/Endpoints/RedirectEndpoints.cs`; conferir que `LinkCreatedResponse.ExpiresAt` em `src/Encurtador.Api/Endpoints/LinkEndpoints.cs` recebe o valor UTC. Depende de T063
- [X] T065 [US4] Rodar `dotnet test` e confirmar T059–T061 verdes, com todas as histórias anteriores ainda verdes

**Checkpoint**: todas as histórias funcionam de forma independente.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: conformidade com a constituição, documentação e validação final.

- [X] T066 [P] Conferir que FR-002 em `specs/001-link-shortener/spec.md` exclui `0 O o 1 I l` e cita a clarificação de D4. Se não excluir, corrigir antes de fechar a feature
- [X] T067 [P] Auditoria do Princípio I: confirmar que nenhum arquivo em `src/Encurtador.Api/Endpoints/` referencia `LinksDbContext`, `DbContext`, `ILinkRepository` ou `Microsoft.EntityFrameworkCore` (Grep) e que `src/Encurtador.Api/Program.cs` só usa `AddInfrastructure`/`ApplyMigrationsAsync`; corrigir o que encontrar
- [X] T068 [P] Auditoria dos Princípios II e VI: Grep em `src/` por `FromSqlRaw|ExecuteSqlRaw|SqlQuery|PRAGMA|SqliteException` (este último só pode aparecer em `src/Encurtador.Infrastructure/Persistence/UniqueViolation.cs`); confirmar que `src/Encurtador.Application/Encurtador.Application.csproj` não tem `PackageReference`; comparar todos os `PackageReference` dos `.csproj` com a tabela de [research.md](./research.md#dependências-nuget-princípio-vi) e remover qualquer extra
- [X] T069 [P] Auditoria do Princípio VII: conferir que toda string devolvida ao cliente em `src/Encurtador.Application/Links/CreateLinkValidator.cs`, `src/Encurtador.Api/Http/Problems.cs` e `src/Encurtador.Api/Program.cs` está em português e igual ao contrato, e que nenhum identificador de código está em português
- [X] T070 Rodar `dotnet build Encurtador.slnx` (zero warnings) e `dotnet test` (todos verdes, inclusive o teste de 1.000 acessos)
- [X] T071 Executar manualmente os cenários 3.1–3.6 de [quickstart.md](./quickstart.md) contra `dotnet run --project src/Encurtador.Api`, incluindo a inspeção do `links.db`: `ManagementKeyHash` tem 32 bytes e a chave não aparece em texto em nenhuma coluna (D3); corrigir divergências

### Interface de testes (adicionada após a implementação, a pedido; executada antes de T072)

- [X] T073 Adicionar `Microsoft.AspNetCore.OpenApi` e `Scalar.AspNetCore` a `src/Encurtador.Api/Encurtador.Api.csproj`, com justificativa na tabela de dependências e na decisão D12 de [research.md](./research.md) (Princípio VI)
- [X] T074 Em `src/Encurtador.Api/Program.cs`, `AddOpenApi()` e, só em `Development`, `MapOpenApi()` e `MapScalarApiReference()`; em `src/Encurtador.Api/Properties/launchSettings.json`, `launchUrl: "scalar"`; declarar `WithSummary`/`WithDescription`/`Produces*` em `src/Encurtador.Api/Endpoints/LinkEndpoints.cs` e `src/Encurtador.Api/Endpoints/RedirectEndpoints.cs`
- [X] T075 Reservar `scalar` em `src/Encurtador.Application/Links/ReservedCodes.cs` (a rota literal `/scalar` venceria `/{code}`), com casos em `tests/Encurtador.UnitTests/Links/CreateLinkValidatorCodeTests.cs`; atualizar a lista em research.md D9, data-model.md e contracts/http-api.md
- [X] T076 Escrever `tests/Encurtador.IntegrationTests/ApiReferenceTests.cs`: `/openapi/v1.json` descreve os 3 endpoints, `/scalar/` responde 200 `text/html` e `customCode: "scalar"` é recusado (Princípio IV)

- [X] T072 Checklist final do Fluxo de Desenvolvimento da constituição: endpoints sem banco e sem regra (I), sem SQL fora da infraestrutura (II), testes III/IV/V existem e passam, dependências justificadas no plano (VI); registrar o resultado no PR/commit final `chore(links): verifica conformidade com a constituição [sem-requisito]`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem dependências.
- **Foundational (Phase 2)**: depende do Setup e BLOQUEIA todas as histórias.
- **US1 (Phase 3)**: depende da Phase 2. É o MVP.
- **US2 (Phase 4)**: depende da Phase 2. Para testar, cria links pelo `POST` de US1, e por isso na prática roda depois de US1. Não altera nada de US1, só acrescenta.
- **US3 (Phase 5)**: depende de US1 (estende `CreateLinkValidator`, `LinkService.CreateAsync` e o `POST`). Independe de US2.
- **US4 (Phase 6)**: depende de US1 (estende validador, `CreateAsync`, `ResolveAsync` e redirect). Usa o stats de US2 em T060/T061, então roda depois de US2. Independe de US3, exceto o teste de vários campos (T059/T061), que usa a regra de `customCode` de US3; rode US4 depois de US3 ou adie esse caso.
- **Polish (Phase 7)**: depende de todas as histórias.

### User Story Dependencies

```text
Setup ─► Foundational ─► US1 (MVP) ─┬─► US2 ─┐
                                     └─► US3 ─┴─► US4 ─► Polish
```

### Within Each User Story

- Testes primeiro; precisam falhar antes da implementação.
- Resultados/DTOs → validador/gerador → `LinkService` → endpoints.
- Arquivos tocados por mais de uma história (`CreateLinkValidator.cs`, `LinkService.cs`, `LinkEndpoints.cs`, `RedirectEndpoints.cs`, `CreateLinkResult.cs`, `ResolveLinkResult.cs`) são editados em sequência, nunca em paralelo.

### Parallel Opportunities

- Setup: T004 ∥ T005.
- Foundational: T007 ∥ T008 ∥ T009 ∥ T011 ∥ T015 ∥ T016 ∥ T018; depois T019 ∥ T021 (dependem de T007/T008); T020 depois de T017.
- US1: todos os testes T023–T029 em paralelo; T030 ∥ T031 ∥ T032 ∥ T035.
- US2: T039–T043 em paralelo; T044 ∥ T045 ∥ T046.
- US3: T050–T053 em paralelo; T054 isolado.
- US4: T059–T061 em paralelo.
- Polish: T066 ∥ T067 ∥ T068 ∥ T069.

---

## Parallel Example: User Story 1

```bash
# Testes de US1 juntos (arquivos distintos):
Task: "ShortCodeGeneratorTests em tests/Encurtador.UnitTests/Links/ShortCodeGeneratorTests.cs"
Task: "ManagementKeyTests (geração) em tests/Encurtador.UnitTests/Links/ManagementKeyTests.cs"
Task: "CreateLinkValidatorUrlTests em tests/Encurtador.UnitTests/Links/CreateLinkValidatorUrlTests.cs"
Task: "LinkServiceCreateTests em tests/Encurtador.UnitTests/Links/LinkServiceCreateTests.cs"
Task: "LinkServiceResolveTests em tests/Encurtador.UnitTests/Links/LinkServiceResolveTests.cs"
Task: "CreateLinkTests em tests/Encurtador.IntegrationTests/CreateLinkTests.cs"
Task: "RedirectTests + PersistenceTests em tests/Encurtador.IntegrationTests/"

# Peças independentes de implementação:
Task: "ShortCodeGenerator em src/Encurtador.Application/Links/ShortCodeGenerator.cs"
Task: "ManagementKey em src/Encurtador.Application/Links/ManagementKey.cs"
Task: "CreateLinkRequest + Results em src/Encurtador.Application/Links/"
Task: "DTOs HTTP em src/Encurtador.Api/Contracts/"
```

## Parallel Example: User Story 2

```bash
Task: "ManagementKeyTests (verificação) em tests/Encurtador.UnitTests/Links/ManagementKeyTests.cs"
Task: "LinkServiceStatsTests em tests/Encurtador.UnitTests/Links/LinkServiceStatsTests.cs"
Task: "StatsTests em tests/Encurtador.IntegrationTests/StatsTests.cs"
Task: "ConcurrencyTests (1.000 acessos) em tests/Encurtador.IntegrationTests/ConcurrencyTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 (Setup) → Phase 2 (Foundational).
2. Phase 3 (US1).
3. **PARAR E VALIDAR**: `dotnet test` verde e quickstart 3.1 e 3.5.
4. Demonstrar: criar link, redirecionar, reiniciar.

### Incremental Delivery

1. Setup + Foundational → base pronta.
2. US1 → MVP (criar + redirecionar + persistir).
3. US2 → contagem consultável com chave (o contador já incrementava desde US1).
4. US3 → códigos personalizados com unicidade garantida pelo índice.
5. US4 → expiração.
6. Polish → auditorias da constituição e quickstart completo.

---

## Notes

- [P] = arquivos diferentes, sem dependência pendente.
- O incremento atômico (D2) já está em US1, porque o redirecionamento de FR-008 sempre conta. US2 entrega a consulta e a prova de concorrência.
- `TryRegisterAccessAsync` já filtra por expiração desde a Foundational. US4 só acrescenta a validação de entrada e o 410.
- Commit ao fim de cada tarefa ou grupo lógico, no formato da constituição, por exemplo: `test(links): cobre gerador de código sem ambíguos [FR-002]`, `feat(links): incrementa acessos de forma atômica [FR-012]`, `feat(links): armazena hash da chave de gerenciamento [FR-016]`.
- Evitar: ler e regravar `AccessCount`, gravar a chave em texto, `DateTime.UtcNow` direto, mensagens em inglês para o cliente, pacotes fora da lista.
