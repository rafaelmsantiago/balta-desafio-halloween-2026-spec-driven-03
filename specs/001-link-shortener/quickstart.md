# Quickstart: validar o Encurtador de Links

**Feature**: [spec.md](./spec.md) | **Contrato**: [contracts/http-api.md](./contracts/http-api.md) | **Modelo**: [data-model.md](./data-model.md)

Guia de validação ponta a ponta. Não descreve a implementação; para isso veja `plan.md` e,
depois, `tasks.md`.

## Pré-requisitos

- .NET SDK 10 (`dotnet --version` → `10.x`)
- `curl` (ou outro cliente HTTP)
- Ferramentas locais restauradas: `dotnet tool restore` (traz o `dotnet-ef`)

## 1. Rodar os testes

```bash
dotnet test --solution Encurtador.slnx
```

O `global.json` da raiz ativa o Microsoft.Testing.Platform, exigido pelo `xunit.v3` no SDK 10.

Esperado: tudo verde em `Encurtador.UnitTests` e `Encurtador.IntegrationTests`, incluindo:

| Teste | Prova |
| --- | --- |
| Unitários do validador / gerador / hasher / `LinkService` | Princípio III; D4 (alfabeto sem `0 O o 1 I l`) |
| Caminho feliz de cada endpoint | Princípio IV |
| Formato do 400 com vários campos inválidos | Princípio V, FR-015 |
| 1.000 acessos concorrentes → `accessCount = 1000` | SC-003, D2 |
| Dois `POST` simultâneos com o mesmo `customCode` → um 201 e um 409 | D1 |
| Reinício do host com o mesmo arquivo SQLite | SC-005, FR-013 |

## 2. Subir a API

```bash
dotnet run --project src/Encurtador.Api
```

O banco `links.db` é criado e migrado na inicialização. Nos exemplos abaixo, a API está em
`http://localhost:5000` (ajuste a porta conforme a saída do `dotnet run`).

### Interface de testes (Scalar)

Em `Development` (o padrão do `dotnet run`), o navegador abre em `/scalar`, com um formulário
para cada endpoint. O documento OpenAPI fica em `/openapi/v1.json` (research.md D12). Para
consultar os acessos pela interface, preencha o cabeçalho `X-Management-Key` com a
`managementKey` devolvida na criação. Em outros ambientes essas rotas não existem.

## 3. Cenários manuais

### 3.1 Criar e redirecionar (US1)

```bash
curl -i -X POST http://localhost:5000/api/links \
  -H "Content-Type: application/json" \
  -d '{"destinationUrl":"https://exemplo.com/artigos/2026/um-texto-bem-longo"}'
```

Esperado: `201`, `code` com 7 caracteres e nenhum de `0 O o 1 I l`, `managementKey`
preenchida. Guarde os dois valores como `CODE` e `KEY`.

```bash
curl -i http://localhost:5000/$CODE
```

Esperado: `302`, `Location` igual ao destino, `Cache-Control: no-store`.

### 3.2 Contar e consultar (US2)

```bash
for i in 1 2 3; do curl -s -o /dev/null http://localhost:5000/$CODE; done
curl -s http://localhost:5000/api/links/$CODE/stats -H "X-Management-Key: $KEY"
```

Esperado: `accessCount` = 4 (1 do passo 3.1 + 3).

```bash
curl -i http://localhost:5000/api/links/$CODE/stats
curl -i http://localhost:5000/api/links/$CODE/stats -H "X-Management-Key: chave-errada"
```

Esperado: `401` nos dois, sem `accessCount` nem `destinationUrl` no corpo.

### 3.3 Código personalizado (US3)

```bash
curl -i -X POST http://localhost:5000/api/links -H "Content-Type: application/json" \
  -d '{"destinationUrl":"https://exemplo.com","customCode":"promo-natal"}'
# repetir o mesmo comando
```

Esperado: `201` na primeira vez e `409` com "já está em uso" na segunda.

```bash
curl -i -X POST http://localhost:5000/api/links -H "Content-Type: application/json" \
  -d '{"destinationUrl":"ftp://exemplo.com","customCode":"a b","expiresAt":"2020-01-01T00:00:00Z"}'
```

Esperado: `400`, `application/problem+json`, `errors` com as três chaves
(`destinationUrl`, `customCode`, `expiresAt`) e mensagens em português.

```bash
curl -i -X POST http://localhost:5000/api/links -H "Content-Type: application/json" \
  -d '{"destinationUrl":"https://exemplo.com","customCode":"HEALTH"}'
```

Esperado: `400`, com `errors.customCode` = `O código "HEALTH" está indisponível.` (reservado; a
comparação ignora maiúsculas e minúsculas). `api` não serve de exemplo: com 3 caracteres, cai antes
na regra de tamanho.

### 3.4 Expiração (US4)

Crie um link com `expiresAt` cerca de 1 minuto no futuro (com fuso, por exemplo
`2026-10-30T15:01:00-03:00`). Abra antes do prazo (`302`). Depois do prazo:

- `GET /$CODE` → `410`;
- `GET /api/links/$CODE/stats` com a chave → `200`, `isExpired: true`, `accessCount`
  igual ao de antes da expiração.

### 3.5 Persistência (SC-005)

Pare a API (Ctrl+C), suba de novo e repita `GET /$CODE` e a consulta de stats. Esperado:
mesmo destino, contador com o mesmo valor.

### 3.6 Chave nunca em texto puro (D3)

Abra `links.db` em um visualizador SQLite e confira que `Links.ManagementKeyHash` é um BLOB
de 32 bytes, diferente da `managementKey` recebida, e que nenhuma coluna contém a chave.

## Critério de pronto

Todos os testes da seção 1 passam e os cenários da seção 3 se comportam como descrito.
