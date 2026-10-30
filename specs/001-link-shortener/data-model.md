# Data Model: Encurtador de Links

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md) | **Date**: 2026-10-30

## Entidade `Link`

Classe em `src/Encurtador.Application/Links/Link.cs`, mapeada para a tabela `Links`.

| Propriedade | Tipo C# | Coluna (SQLite) | Nulo | Regras |
| --- | --- | --- | --- | --- |
| `Id` | `long` | `INTEGER PK AUTOINCREMENT` | não | Chave técnica; nunca exposta na API |
| `Code` | `string` | `TEXT(20)` | não | **Índice único** `IX_Links_Code`, case-sensitive (D1). 7 caracteres do alfabeto de D4 se gerado; `^[A-Za-z0-9_-]{4,20}$` e não reservado se personalizado (FR-002, FR-003). Imutável |
| `DestinationUrl` | `string` | `TEXT(2048)` | não | Absoluta, `http`/`https`, com host, ≤ 2048 caracteres (FR-006). Imutável (FR-014) |
| `CreatedAt` | `DateTime` (UTC) | `TEXT` | não | `TimeProvider.GetUtcNow()` na criação. Imutável |
| `ExpiresAt` | `DateTime?` (UTC) | `TEXT` | sim | Opcional; > `CreatedAt` na criação (FR-007). Imutável nesta versão |
| `AccessCount` | `long` | `INTEGER` | não | Começa em 0. Só muda por `UPDATE … SET AccessCount = AccessCount + 1` (D2). Nunca diminui (FR-012) |
| `ManagementKeyHash` | `byte[]` (32) | `BLOB` | não | SHA-256 da chave de gerenciamento (D3). A chave em texto puro nunca é persistida |

Observações de mapeamento (em `Infrastructure`, `LinkConfiguration : IEntityTypeConfiguration<Link>`):
- Value converter em `CreatedAt`/`ExpiresAt` para garantir `DateTimeKind.Utc` na leitura (D6).
- `AccessCount` não é marcado como token de concorrência. Ele nunca é regravado pela aplicação.
- `Link.Restore(...)` (`internal`) existe só para montar uma `Link` com contador em teste
  unitário. Ele não faz parte do contrato público.
- Propriedades com setter `private`/`init`. A entidade não expõe método para mudar destino,
  código, chave ou contador.

### Regras derivadas (métodos puros, testados por unidade)

- `IsExpiredAt(DateTime nowUtc)` → `ExpiresAt is not null && nowUtc >= ExpiresAt`.
  O instante exato da expiração já conta como expirado (Edge Case).
- Validade para redirecionar = existe **e** `!IsExpiredAt(now)`. A mesma condição é repetida
  no `WHERE` do incremento atômico, para fechar a corrida com a expiração.

### Transições de estado

```text
           criação (POST /api/links)
                    │
                    ▼
             ┌─────────────┐   acesso válido: AccessCount += 1 (atômico)
             │    Ativo    │◄───────────────┐
             └─────────────┘────────────────┘
                    │
                    │ now >= ExpiresAt  (só se ExpiresAt definido)
                    ▼
             ┌─────────────┐   acesso: 410, contador inalterado
             │  Expirado   │   consulta com chave: 200, isExpired = true
             └─────────────┘
```

"Expirado" não é gravado no banco: é calculado a partir de `ExpiresAt` e do relógio. Não há
transição de volta, exclusão nem alteração (fora de escopo). O código continua ocupado para
sempre (FR-004).

## Valores não persistidos

### Chave de gerenciamento (`ManagementKey`)

- 32 bytes de `RandomNumberGenerator`, codificados em Base64Url sem padding (43 caracteres).
- Existe em texto puro só na memória, durante a criação, e só na resposta 201.
- Verificação: Base64Url-decode → se falhar ou não tiver 32 bytes, recusa → `SHA256` →
  `CryptographicOperations.FixedTimeEquals` com `ManagementKeyHash`.

### Código gerado

- Alfabeto (56 símbolos, D4): `23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz`.
- `RandomNumberGenerator.GetItems<char>(alphabet, 7)`.
- Colisão (pela checagem ou pelo índice): gera de novo, até **5 tentativas**. Se todas
  colidirem, o serviço devolve uma falha interna (500 `ProblemDetails`). Com 56⁷ códigos,
  esse caso só aconteceria com o espaço praticamente esgotado.

### Códigos reservados

`api`, `health`, `openapi`, `scalar`, `swagger`, comparados com `StringComparer.OrdinalIgnoreCase` (D9).
Ficam em uma lista estática em `Application`.

## Contratos internos (Application)

```text
ILinkRepository
  Task<bool>            CodeExistsAsync(string code, CancellationToken ct)
  Task<AddLinkResult>   AddAsync(Link link, CancellationToken ct)          // Added | DuplicateCode
  Task<Link?>           GetByCodeAsync(string code, CancellationToken ct)  // AsNoTracking
  Task<bool>            TryRegisterAccessAsync(string code, DateTime nowUtc, CancellationToken ct)
                        // UPDATE atômico condicionado a não expirado; true se 1 linha afetada

CreateLinkValidator
  CreateLinkValidation  Validate(CreateLinkRequest)
    Errors : IReadOnlyDictionary<string, string[]>   // chaves: destinationUrl, customCode, expiresAt
    Value  : ValidatedLink(DestinationUrl, CustomCode?, ExpiresAtUtc?)  // só se válido

LinkService
  CreateAsync(CreateLinkRequest)        → Created(code, destinationUrl, createdAt, expiresAt, managementKey)
                                        | ValidationFailed(errors por campo)
                                        | CodeInUse
  ResolveAsync(string code)             → Redirect(destinationUrl) | NotFound | Expired
  GetStatsAsync(string code, string? managementKey)
                                        → Stats(code, destinationUrl, createdAt, expiresAt, isExpired, accessCount)
                                        | NotFound | Unauthorized
```

O serviço devolve resultados tipados em vez de lançar exceções para os casos esperados. O
endpoint apenas traduz cada caso para o status HTTP de [contracts/http-api.md](./contracts/http-api.md).

## Rastreabilidade

| Requisito | Onde é garantido |
| --- | --- |
| FR-002, FR-003, FR-005 | `ShortCodeGenerator`, `CreateLinkValidator`, lista de reservados |
| FR-004 | Índice único `IX_Links_Code` + checagem no serviço (D1) |
| FR-006, FR-007, FR-015 | `CreateLinkValidator` (D8) |
| FR-008, FR-009, FR-012 | `TryRegisterAccessAsync` (D2), `Link.IsExpiredAt` |
| FR-011, FR-016–FR-018 | `ManagementKey` + `ManagementKeyHash` (D3), `GetStatsAsync` |
| FR-013 | Arquivo SQLite + migrations (D7) |
| FR-014 | Entidade sem setters públicos; nenhum endpoint de alteração |
