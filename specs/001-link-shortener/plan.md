# Implementation Plan: Encurtador de Links

**Branch**: `001-link-shortener` | **Date**: 2026-10-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-link-shortener/spec.md`

## Summary

Serviço HTTP, sem páginas web, que cria links curtos para URLs http/https, redireciona quem
abre o link e conta cada acesso. O criador consulta o total com uma chave secreta entregue
uma única vez na criação.

Abordagem: ASP.NET Core Minimal APIs no .NET 10, com um arquivo de endpoints por recurso. As
regras de negócio ficam no `LinkService`, e a persistência usa EF Core com SQLite em arquivo,
atrás de um `ILinkRepository` que permite trocar o provider para PostgreSQL. Quatro decisões
sustentam as garantias da spec, todas detalhadas em [research.md](./research.md):

| # | Decisão | Garante |
| --- | --- | --- |
| D1 | Unicidade do código por **índice único no banco**, além da checagem no serviço | Exatamente um vencedor em corridas pelo mesmo código (FR-004) |
| D2 | Contador por **`UPDATE … SET AccessCount = AccessCount + 1` atômico**, condicionado a não expirado | Zero acessos perdidos e nenhum acesso contado após expirar (FR-009, FR-012, SC-003) |
| D3 | Chave de gerenciamento persistida só como **hash SHA-256** | Vazamento do banco não expõe chaves (FR-016, FR-017) |
| D4 | Código gerado com **alfabeto sem caracteres ambíguos** (`0 O o 1 I l` excluídos) | Códigos legíveis e digitáveis sem erro |

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.403 instalado)

**Primary Dependencies**: ASP.NET Core Minimal APIs; EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`). Lista completa e justificada em [research.md → Dependências NuGet](./research.md#dependências-nuget-princípio-vi)

**Storage**: SQLite em arquivo (`links.db`) com migrations do EF Core; provider selecionado por configuração (`Database:Provider`), com PostgreSQL previsto para depois (D6)

**Testing**: xUnit v3. `Encurtador.UnitTests` (sem banco/HTTP, com repositório fake) e `Encurtador.IntegrationTests` (`WebApplicationFactory` + SQLite temporário em arquivo)

**Target Platform**: servidor Linux ou Windows rodando o host Kestrel do ASP.NET Core

**Project Type**: web-service (API HTTP, sem frontend)

**Performance Goals**: redirecionamento < 1 s no p95 (SC-002). Na prática, uma leitura e um `UPDATE` por acesso. 1.000 acessos simultâneos ao mesmo link contados sem perda (SC-003). Verificado em `ConcurrencyTests` (p95 de 200 redirecionamentos em memória < 1 s) como proteção contra regressão

**Constraints**: nenhuma perda de contagem sob concorrência; persistência entre reinícios; nenhum SQL bruto; chave nunca em texto puro; mensagens em português e código em inglês

**Scale/Scope**: 3 endpoints, 1 entidade, instância única nesta versão. Espaço de códigos gerados 56⁷ ≈ 1,7 × 10¹²

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Verificação no design | Status |
| --- | --- | --- |
| **I. Endpoints finos** | `LinkEndpoints.cs` e `RedirectEndpoints.cs` só injetam `LinkService`, leem a requisição e mapeiam o resultado tipado para HTTP. `Api` referencia apenas `Application` e a extensão de registro de `Infrastructure`; o `DbContext` nunca é injetado em endpoint. Regras (validação, colisão, expiração, chave) ficam em `Application` | ✅ |
| **II. Persistência isolada via EF Core** | EF Core em `Infrastructure`. Incremento via `ExecuteUpdateAsync` (LINQ, não SQL bruto). Zero `FromSqlRaw`/`ExecuteSqlRaw`/`PRAGMA` (D7). Detecção de violação de unicidade (`SqliteException` 2067) fica dentro de `Infrastructure` e chega ao serviço como `AddLinkResult.DuplicateCode`. Provider escolhido em `AddInfrastructure` (D6) | ✅ |
| **III. Regra de negócio com teste unitário** | Validador, gerador, hasher, `Link.IsExpiredAt` e cada ramo do `LinkService` (retry de colisão, duplicado na corrida, expirado, chave inválida) testados com `FakeLinkRepository` e `TimeProvider` fake, sem banco (D10) | ✅ |
| **IV. Endpoint com teste de integração** | Os 3 endpoints têm caminho feliz com `WebApplicationFactory` até o SQLite, além de concorrência, corrida de código e reinício (D10) | ✅ |
| **V. 400 com ProblemDetails** | Validação → `Results.ValidationProblem`. Falha de binding/JSON malformado → `ProblemDetails` via `AddProblemDetails` + `UseStatusCodePages`. Formato testado em integração (D8) | ✅ |
| **VI. Dependências justificadas** | 9 itens (7 do plano original + OpenAPI e Scalar, D12), cada um com problema e motivo, em [research.md](./research.md#dependências-nuget-princípio-vi). FluentValidation, FluentAssertions, TimeProvider.Testing e Argon2 recusados | ✅ |
| **VII. Idioma** | Tipos, arquivos, pastas, rotas e campos JSON em inglês. `title`/`detail`/erros de campo em português ([contracts/http-api.md](./contracts/http-api.md)) | ✅ |
| **Stack** | .NET 10, Minimal APIs (sem controllers), EF Core, xUnit | ✅ |
| **Fluxo** | Commits no formato `tipo(escopo): descrição [FR-xxx]`; checklist final do fluxo vira a última tarefa em `tasks.md` | ✅ |

**Resultado pré-pesquisa**: aprovado, sem violações.
**Resultado pós-design (Fase 1)**: aprovado. O design não introduziu SQL bruto, dependência
sem justificativa nem acesso a dados fora do serviço. *Complexity Tracking* fica vazio.

## Project Structure

### Documentation (this feature)

```text
specs/001-link-shortener/
├── plan.md              # Este arquivo
├── research.md          # Fase 0: decisões D1–D11 e dependências
├── data-model.md        # Fase 1: entidade Link, estados, contratos internos
├── quickstart.md        # Fase 1: guia de validação
├── contracts/
│   └── http-api.md      # Fase 1: contrato HTTP dos 3 endpoints
└── tasks.md             # Fase 2 (/speckit-tasks; não criado aqui)
```

### Source Code (repository root)

```text
Encurtador.slnx
.config/dotnet-tools.json                  # dotnet-ef
src/
├── Encurtador.Api/                        # Web SDK; referencia Application + Infrastructure
│   ├── Program.cs                         # DI, ProblemDetails, migração na inicialização, Map*Endpoints
│   ├── Endpoints/
│   │   ├── LinkEndpoints.cs               # POST /api/links, GET /api/links/{code}/stats
│   │   └── RedirectEndpoints.cs           # GET /{code}
│   ├── Contracts/                         # DTOs HTTP (CreateLinkHttpRequest, LinkCreatedResponse, LinkStatsResponse)
│   └── appsettings.json                   # Database:Provider, ConnectionStrings:Links, ShortLinks:PublicBaseUrl
├── Encurtador.Application/                # Class library; sem dependência de EF Core
│   ├── Links/
│   │   ├── Link.cs                        # Entidade + IsExpiredAt
│   │   ├── LinkService.cs                 # Dono das regras de negócio
│   │   ├── ILinkRepository.cs             # + AddLinkResult
│   │   ├── CreateLinkRequest.cs
│   │   ├── CreateLinkValidator.cs         # D8, mensagens em português
│   │   ├── ShortCodeGenerator.cs          # D4
│   │   ├── ReservedCodes.cs               # D9
│   │   ├── ManagementKey.cs               # geração, hash e verificação (D3)
│   │   └── Results/                       # CreateLinkResult, ResolveLinkResult, LinkStatsResult
│   └── DependencyInjection.cs             # AddApplication()
└── Encurtador.Infrastructure/             # EF Core + provider
    ├── Persistence/
    │   ├── LinksDbContext.cs
    │   ├── LinkConfiguration.cs           # índice único, tamanhos, conversores UTC
    │   ├── EfLinkRepository.cs            # ExecuteUpdateAsync (D2), tradução da violação única (D1)
    │   ├── UniqueViolation.cs             # detecção por provider
    │   └── Migrations/                    # migrations SQLite
    └── DependencyInjection.cs             # AddInfrastructure(config): escolha do provider (D6)

tests/
├── Encurtador.UnitTests/                  # referencia Application
│   ├── Fakes/ (FakeLinkRepository, FakeTimeProvider)
│   └── Links/ (validador, gerador, chave, LinkService, Link)
└── Encurtador.IntegrationTests/           # referencia Api; WebApplicationFactory<Program>
    ├── LinksApiFactory.cs                 # SQLite temporário por classe
    ├── CreateLinkTests.cs
    ├── RedirectTests.cs
    ├── StatsTests.cs
    └── ConcurrencyAndPersistenceTests.cs  # SC-003, D1 corrida, SC-005
```

**Structure Decision**: três projetos de produção. A separação física faz o compilador
impedir que endpoints alcancem o `DbContext` (Princípio I) e que `Application` dependa do
EF Core (Princípio II). Um projeto `Domain` à parte seria cerimônia para uma única entidade
(D5). O redirecionamento ganha seu próprio arquivo de endpoints porque é um recurso
distinto: o link curto público na raiz, consumido por navegadores, enquanto `/api/links` é a
API de gerenciamento.

## Complexity Tracking

Nenhuma violação a justificar.
