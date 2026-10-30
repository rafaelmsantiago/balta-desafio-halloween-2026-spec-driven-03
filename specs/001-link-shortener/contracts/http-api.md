# HTTP API Contract: Encurtador de Links

**Feature**: [spec.md](../spec.md) | **Data model**: [data-model.md](../data-model.md) | **Date**: 2026-10-30

Convenções:
- Corpos JSON em `camelCase`, datas em ISO 8601 UTC com `Z` (por exemplo `2026-10-30T14:00:00Z`).
- Todo erro é `application/problem+json` (RFC 9457) com `title` e `detail` em português.
  Cada tipo de erro tem `type` próprio, o que permite distinguir as respostas (FR-019).
- Não há páginas HTML (FR-019).

| Recurso | Arquivo de endpoints | Rotas |
| --- | --- | --- |
| Links (gerenciamento) | `src/Encurtador.Api/Endpoints/LinkEndpoints.cs` | `POST /api/links`, `GET /api/links/{code}/stats` |
| Redirecionamento (link curto público) | `src/Encurtador.Api/Endpoints/RedirectEndpoints.cs` | `GET /{code}` |

---

## 1. Criar link — `POST /api/links`

### Requisição

```http
POST /api/links
Content-Type: application/json

{
  "destinationUrl": "https://exemplo.com/artigos/2026/um-texto-bem-longo",
  "customCode": "promo-natal",
  "expiresAt": "2026-12-25T23:59:59-03:00"
}
```

| Campo | Obrigatório | Regra |
| --- | --- | --- |
| `destinationUrl` | sim | Absoluta, `http`/`https`, com host, ≤ 2048 caracteres |
| `customCode` | não | `^[A-Za-z0-9_-]{4,20}$`, não reservado (`api`, `health`, `openapi`, `scalar`, `swagger`, sem diferenciar caixa). Ausente ou `null` → código gerado de 7 caracteres |
| `expiresAt` | não | ISO 8601 **com fuso**; estritamente no futuro |

Todos os campos são lidos como texto e validados juntos. A resposta 400 lista todos os
problemas encontrados.

### 201 Created

```http
HTTP/1.1 201 Created
Location: /api/links/promo-natal/stats
Content-Type: application/json

{
  "code": "promo-natal",
  "shortUrl": "https://sho.rt/promo-natal",
  "destinationUrl": "https://exemplo.com/artigos/2026/um-texto-bem-longo",
  "createdAt": "2026-10-30T14:00:00Z",
  "expiresAt": "2026-12-26T02:59:59Z",
  "managementKey": "q3VbX0m6c7w4R2a9yLkPz1dEoF8sTgHuJiNbMvCx5Ws"
}
```

- `managementKey` aparece **somente** nesta resposta (FR-016). Não há como obter de novo
  (FR-018).
- `expiresAt` é `null` quando não informado.

### 400 Bad Request — validação

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Um ou mais campos são inválidos.",
  "status": 400,
  "errors": {
    "destinationUrl": ["A URL de destino deve usar o esquema http ou https."],
    "customCode": ["O código deve ter entre 4 e 20 caracteres."],
    "expiresAt": ["A data de expiração precisa estar no futuro."]
  }
}
```

Mensagens por regra (texto final pode ser ajustado; o idioma é fixo):

| Campo | Situação | Mensagem |
| --- | --- | --- |
| `destinationUrl` | ausente/vazia | A URL de destino é obrigatória. |
| `destinationUrl` | malformada / sem domínio | A URL de destino deve ser um endereço absoluto válido, com domínio. |
| `destinationUrl` | esquema diferente | A URL de destino deve usar o esquema http ou https. |
| `destinationUrl` | > 2048 | A URL de destino deve ter no máximo 2048 caracteres. |
| `customCode` | tamanho | O código deve ter entre 4 e 20 caracteres. |
| `customCode` | caracteres | O código só pode conter letras sem acento (A–Z, a–z), dígitos (0–9), hífen (-) e sublinhado (_). |
| `customCode` | reservado | O código "{code}" está indisponível. |
| `expiresAt` | formato/sem fuso | A data de expiração deve estar no formato ISO 8601 com fuso horário (ex.: 2026-12-31T23:59:00-03:00). |
| `expiresAt` | não futura | A data de expiração precisa estar no futuro. |

JSON malformado ou corpo ausente também devolve 400 em `ProblemDetails`, com `title`
"A requisição não pôde ser lida." (Princípio V).

### 409 Conflict — código em uso

```json
{
  "type": "/problems/code-in-use",
  "title": "Código já está em uso.",
  "status": 409,
  "detail": "O código \"promo-natal\" já está em uso. Escolha outro código."
}
```

Vale também para links expirados (FR-004) e para o perdedor de uma corrida simultânea.

---

## 2. Redirecionar — `GET /{code}`

### 302 Found — link válido

```http
HTTP/1.1 302 Found
Location: https://exemplo.com/artigos/2026/um-texto-bem-longo
Cache-Control: no-store
```

O acesso é contado de forma atômica antes da resposta (FR-008, FR-012).

### 404 Not Found

```json
{
  "type": "/problems/link-not-found",
  "title": "Link não encontrado.",
  "status": 404,
  "detail": "Não existe link com o código \"abc1234\"."
}
```

### 410 Gone — link expirado

```json
{
  "type": "/problems/link-expired",
  "title": "Link expirado.",
  "status": 410,
  "detail": "O link \"promo-natal\" expirou em 2026-12-26T02:59:59Z."
}
```

O contador não é alterado (FR-009).

---

## 3. Consultar acessos — `GET /api/links/{code}/stats`

### Requisição

```http
GET /api/links/promo-natal/stats
X-Management-Key: q3VbX0m6c7w4R2a9yLkPz1dEoF8sTgHuJiNbMvCx5Ws
```

A chave vai **só** no cabeçalho `X-Management-Key`. Ela não é aceita na query string.

### 200 OK

```json
{
  "code": "promo-natal",
  "destinationUrl": "https://exemplo.com/artigos/2026/um-texto-bem-longo",
  "createdAt": "2026-10-30T14:00:00Z",
  "expiresAt": "2026-12-26T02:59:59Z",
  "isExpired": false,
  "accessCount": 5
}
```

Um link expirado também responde 200, com `isExpired: true` e o total acumulado (US4
cenário 5).

### 401 Unauthorized — chave ausente, malformada ou de outro link

```http
HTTP/1.1 401 Unauthorized
WWW-Authenticate: ManagementKey
Content-Type: application/problem+json

{
  "type": "/problems/invalid-management-key",
  "title": "Chave de gerenciamento inválida.",
  "status": 401,
  "detail": "Informe no cabeçalho X-Management-Key a chave entregue na criação do link."
}
```

O corpo é o mesmo para os três casos e não contém nenhum dado do link (FR-017, SC-007).

### 404 Not Found

Igual ao da seção 2, avaliado **antes** da chave (US2 cenário 5).

Isso inclui o caso em que a chave foi posta no lugar do código
(`GET /api/links/{chave}/stats`). A chave tem 43 caracteres e não forma um código válido,
então a resposta é 404 sem consultar o banco. A consulta é recusada sem revelar dado algum,
como pede a spec (Edge Case "Chave secreta usada no lugar do código").

---

## Resumo de status

| Endpoint | 201 | 200 | 302 | 400 | 401 | 404 | 409 | 410 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `POST /api/links` | ✔ | | | ✔ | | | ✔ | |
| `GET /{code}` | | | ✔ | | | ✔ | | ✔ |
| `GET /api/links/{code}/stats` | | ✔ | | | ✔ | ✔ | | |
