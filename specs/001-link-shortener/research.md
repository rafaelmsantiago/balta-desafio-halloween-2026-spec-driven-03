# Research: Encurtador de Links

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-10-30

Cada decisão segue o formato Decisão / Justificativa / Alternativas consideradas. As decisões
D1 a D4 foram pedidas explicitamente para registro; as demais resolvem pontos técnicos que a
spec e a constituição deixam em aberto.

---

## D1. Unicidade do código: índice único no banco + checagem no serviço

**Decisão**: a coluna `Code` da tabela `Links` tem um **índice único** (case-sensitive). O
`LinkService` também verifica, antes de inserir, se um código personalizado já existe, para
devolver a mensagem amigável de "código em uso". Se a inserção mesmo assim violar o índice
(corrida entre dois pedidos), a infraestrutura traduz a violação em
`AddLinkResult.DuplicateCode`, e o serviço responde exatamente como na checagem prévia
(código personalizado) ou gera outro código e tenta de novo (código automático).

**Justificativa**:
- A checagem no serviço sozinha é do tipo *check-then-act*: dois pedidos simultâneos com o
  mesmo código podem ambos ver "não existe" e ambos inserir. A spec exige que exatamente um
  seja aceito (Edge Case "Dois pedidos simultâneos…", FR-004). Só o banco consegue serializar
  essa decisão; o índice único é a garantia real.
- A checagem no serviço continua útil: resolve o caso comum sem depender de exceção, e deixa
  a regra de negócio legível e testável por teste unitário (Princípio III).
- A tradução da exceção do provider (`SqliteException` com código estendido 2067,
  `SQLITE_CONSTRAINT_UNIQUE`; no PostgreSQL, `PostgresException` com `SqlState` 23505) fica
  dentro de `Infrastructure`, então trocar de provider não toca no serviço (Princípio II).
- Case-sensitive é o comportamento padrão nos dois bancos (SQLite com collation `BINARY`,
  PostgreSQL com collation determinística), o que atende à premissa "`Promo` ≠ `promo`".

**Alternativas consideradas**:
- *Só checagem no serviço*: rejeitada por perder a corrida descrita acima.
- *Só índice único, sem checagem*: funciona, mas transforma o caminho comum (código em uso)
  em fluxo por exceção e esconde a regra de negócio na infraestrutura.
- *Lock na aplicação (`SemaphoreSlim`, lock distribuído)*: não protege várias instâncias,
  acrescenta complexidade e continua dependendo do banco como fonte da verdade.

---

## D2. Contador incrementado por update atômico

**Decisão**: o acesso é contado com um único comando
`UPDATE Links SET AccessCount = AccessCount + 1 WHERE Code = @code AND (ExpiresAt IS NULL OR ExpiresAt > @now)`,
emitido via `ExecuteUpdateAsync` do EF Core
(`SetProperty(l => l.AccessCount, l => l.AccessCount + 1)`). O valor do contador nunca é lido
para a aplicação, somado e regravado. O método retorna o número de linhas afetadas: `1` conta
o acesso e libera o redirecionamento; `0` significa que o link expirou entre a leitura e o
incremento, e o acesso é tratado como expirado (não redireciona, não conta).

**Justificativa**:
- Ler, somar e gravar (`link.AccessCount++; SaveChanges()`) perde acessos sob concorrência:
  dois pedidos leem 10 e ambos gravam 11. A spec exige zero perda (FR-012, US2 cenário 3,
  SC-003 com 1.000 acessos simultâneos).
- O incremento dentro do próprio `UPDATE` é atômico no banco, sem transação explícita, lock
  ou retry de concorrência otimista.
- Pôr a condição de expiração no mesmo `WHERE` fecha a corrida "o link expira entre a leitura
  e o incremento" (FR-009, SC-004), porque a decisão "ainda vale?" e o incremento acontecem no
  mesmo comando.
- `ExecuteUpdateAsync` é LINQ traduzido pelo provider, não SQL bruto (Princípio II), e
  funciona igual no SQLite e no PostgreSQL.
- O incremento é sempre `+1`, então o contador nunca diminui (FR-012, US2 cenário 4).

**Alternativas consideradas**:
- *Ler e regravar com token de concorrência otimista (`RowVersion`) e retry*: correto, mas sob
  carga gera muitos conflitos e retries, e o código fica maior que um `UPDATE`.
- *Tabela de acessos (uma linha por acesso) com `COUNT(*)` na consulta*: evita contenção na
  linha do link, mas a spec pede só o total agregado (Key Entities → Acesso). Cresce sem
  limite e não se justifica nesta versão.
- *Contador em memória com descarga periódica*: perde acessos se a aplicação cair, violando
  FR-013.

---

## D3. Chave de gerenciamento armazenada como hash

**Decisão**: na criação, o serviço gera 32 bytes aleatórios com `RandomNumberGenerator`, os
codifica em Base64Url (43 caracteres) e os devolve ao criador **uma única vez**. No banco é
gravado apenas o `SHA-256` desses bytes (coluna `ManagementKeyHash`, 32 bytes). Na consulta, a
chave recebida no cabeçalho `X-Management-Key` é decodificada, passa pelo mesmo hash e é
comparada com `CryptographicOperations.FixedTimeEquals`. Chave ausente, malformada ou
diferente resulta na mesma resposta 401, sem dados do link.

**Justificativa**:
- Se o banco vazar (backup, cópia do arquivo SQLite, log de consulta), as chaves em texto puro
  dariam acesso a todas as estatísticas. Com hash, quem tem o banco não consegue apresentar
  uma chave válida (FR-016, FR-017, SC-007).
- A spec já impede recuperação da chave (FR-018), então a aplicação nunca precisa do valor
  original depois da criação. Guardar só o hash não custa nada funcionalmente.
- SHA-256 simples, sem salt e sem KDF lenta, é suficiente porque a chave tem 256 bits de
  entropia gerados por CSPRNG. KDFs lentas (PBKDF2, bcrypt, Argon2) e salt existem para
  segredos de baixa entropia escolhidos por pessoas, como senhas, que podem ser atacados por
  dicionário. Aqui o espaço de busca já é inviável. Hash rápido também mantém a consulta
  barata.
- A comparação em tempo constante evita vazar, pelo tempo de resposta, quantos bytes do hash
  coincidem.
- Tudo isso vem da BCL (`System.Security.Cryptography`), sem dependência nova (Princípio VI).
- Cabeçalho em vez de query string: a URL costuma aparecer em logs de proxy e no histórico, e
  a chave não pode aparecer ali.

**Alternativas consideradas**:
- *Texto puro*: rejeitada pelo motivo acima.
- *PBKDF2/Argon2 com salt*: proteção desnecessária para um segredo de 256 bits, com custo de
  CPU em cada consulta. O Argon2 ainda exigiria um pacote NuGet.
- *Criptografia reversível (Data Protection)*: permitiria recuperar a chave, o que a spec
  proíbe, e passa a exigir gestão de chaves.
- *Derivar a chave do código com HMAC e segredo do servidor*: não precisaria de armazenamento,
  mas um vazamento do segredo do servidor comprometeria todos os links de uma vez e
  impediria rotação.

---

## D4. Alfabeto do código gerado sem caracteres ambíguos

**Decisão**: códigos gerados automaticamente usam 7 caracteres sorteados de um alfabeto de
**56 símbolos**: as letras de A a Z maiúsculas e minúsculas e os dígitos de 0 a 9, **sem**
`0` (zero), `O`, `o`, `1` (um), `I` (i maiúsculo) e `l` (L minúsculo):

```text
23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz
```

O sorteio usa `RandomNumberGenerator.GetItems<char>(alphabet, 7)`, que é uniforme (sem viés
de módulo) e criptograficamente seguro.

**Justificativa**:
- Links curtos são lidos em voz alta, digitados a partir de material impresso e copiados à
  mão. `0/O/o` e `1/I/l` se confundem na maioria das fontes, e um erro de leitura leva a
  "link não encontrado" ou, pior, ao link de outra pessoa.
- O alfabeto é um subconjunto do definido em FR-002 (letras A–Z/a–z e dígitos 0–9). Todo
  código gerado continua tendo exatamente 7 caracteres alfanuméricos, então a regra é
  atendida.
- Espaço de códigos: 56⁷ ≈ 1,7 × 10¹² (contra 62⁷ ≈ 3,5 × 10¹²). Continua grande o bastante
  para colisões serem raras; quando ocorrem, o fluxo de D1 gera outro código.
- Um gerador aleatório criptográfico torna os códigos imprevisíveis, então não dá para
  enumerar links vizinhos a partir de um código conhecido.
- A restrição vale **só para códigos gerados**. Códigos personalizados seguem FR-003 e podem
  conter qualquer letra, dígito, `-` e `_`, inclusive os caracteres ambíguos, porque quem os
  escolhe é o criador.

**Alternativas consideradas**:
- *Alfabeto completo de 62 símbolos*: dobra o espaço de códigos, mas mantém o problema de
  legibilidade.
- *Base32 de Crockford (só maiúsculas e dígitos)*: ainda mais legível, mas com 32 símbolos o
  espaço em 7 caracteres cai para ≈ 3,4 × 10¹⁰. Também violaria a expectativa da spec de usar
  maiúsculas e minúsculas.
- *Sequencial codificado (ID → base56)*: elimina colisões, mas torna os códigos previsíveis e
  enumeráveis.

**Nota para a spec**: o parêntese de FR-002 ("letras de A a Z maiúsculas e minúsculas e
dígitos de 0 a 9") pode ser lido como o alfabeto exato. Vale um ajuste de redação para algo
como "composto apenas por caracteres alfanuméricos, excluídos os ambíguos
(`0 O o 1 I l`)", para que testes de aceitação não esperem os 62 símbolos.

---

## D5. Camadas e abstração de persistência

**Decisão**: três projetos de produção (`Api`, `Application`, `Infrastructure`). O
`LinkService` (em `Application`) depende de uma interface `ILinkRepository` definida em
`Application` e implementada com EF Core em `Infrastructure`. Endpoints recebem só o
`LinkService`.

**Justificativa**:
- Princípio I: o endpoint não pode tocar no `DbContext`; o `LinkService` é a única porta para
  os dados.
- Princípio III: as regras do serviço (retry de colisão, mapeamento de código duplicado,
  expiração, verificação da chave) precisam de teste unitário **sem banco real**. Um
  `ILinkRepository` pequeno pode ser trocado por um fake em memória nos testes. Usar o
  `DbContext` direto no serviço obrigaria os testes unitários a usar SQLite.
- Princípio II: detalhes do provider (detecção de violação de unicidade, migrations, opções
  do SQLite) ficam todos em `Infrastructure`.
- O repositório expõe só as operações que o caso de uso precisa (`AddAsync`,
  `GetByCodeAsync`, `CodeExistsAsync`, `TryRegisterAccessAsync`), não um CRUD genérico.

**Alternativas consideradas**:
- *Um único projeto com pastas*: menos arquivos, mas nada impede, no compilador, um endpoint
  de injetar o `DbContext`. Projetos separados tornam o Princípio I verificável pelas
  referências.
- *Projeto `Domain` separado*: o domínio aqui é uma entidade e algumas regras de formato;
  quatro projetos seria cerimônia sem ganho.
- *Repositório genérico `IRepository<T>`*: esconderia o `UPDATE` atômico de D2 atrás de uma
  API que convida a ler e regravar.

---

## D6. Provider SQLite trocável por PostgreSQL

**Decisão**: `Infrastructure` expõe `AddInfrastructure(IConfiguration)`, que lê
`Database:Provider` (valor nesta versão: `Sqlite`) e `ConnectionStrings:Links`. Só o ramo
`Sqlite` é implementado agora. O modelo evita tipos e comportamentos que só um provider
suporta:
- datas como `DateTime` em UTC (`Kind = Utc` garantido por value converter), e não
  `DateTimeOffset`, porque o provider SQLite do EF Core não traduz comparações de
  `DateTimeOffset` e a expiração precisa ser comparada no `WHERE` (D2). O formato ISO em
  texto que o EF grava no SQLite ordena corretamente, e no PostgreSQL o mesmo tipo vira
  `timestamp with time zone`, que exige `Kind = Utc`;
- hash da chave como `byte[]` (BLOB / `bytea`);
- contador como `long` (INTEGER / `bigint`).

As migrations são geradas por provider. Ao adicionar o PostgreSQL, ele ganha seu próprio
conjunto de migrations, a detecção do `SqlState` 23505 em D1 e o pacote do provider
(justificado no plano daquela mudança).

**Justificativa**: Princípio II, além de deixar a troca concentrada em um arquivo de
registro e em um detector de violação de unicidade.

**Alternativas consideradas**:
- *`EnsureCreated` em vez de migrations*: não evolui o schema sem apagar dados (FR-013).
- *Unix epoch em `long` para datas*: portável, mas ilegível no banco e sem ganho real sobre
  `DateTime` UTC.

---

## D7. Concorrência no SQLite e persistência entre reinícios

**Decisão**: o banco é um arquivo SQLite (`links.db`, caminho configurável) com migrations
aplicadas na inicialização (`Database.MigrateAsync()`). Escritas concorrentes dependem do
retry automático do `Microsoft.Data.Sqlite` para `SQLITE_BUSY`, limitado pelo timeout do
comando (padrão de 30 s). Não usamos `PRAGMA` nem SQL bruto.

**Justificativa**:
- Arquivo em disco atende FR-013 / SC-005 (os links sobrevivem ao reinício).
- O SQLite serializa as escritas; com 1.000 `UPDATE`s curtos (SC-003), a fila cabe com folga
  no timeout, e o incremento atômico de D2 garante a contagem exata.
- Sem SQL bruto, o Constitution Check (Princípio II) fica limpo.

**Alternativas consideradas**:
- *`PRAGMA journal_mode=WAL` na inicialização*: melhora a concorrência entre leitura e
  escrita, mas exige SQL bruto (aceitável na infraestrutura, mas exigiria justificativa) e não
  é necessário para as metas da spec. Fica registrado como otimização futura, se o teste
  de carga mostrar contenção.
- *SQLite em memória*: perde os dados no reinício (viola FR-013).

---

## D8. Validação, mensagens e formato de erro

**Decisão**:
- O corpo de `POST /api/links` é recebido como DTO com **todos os campos `string?`**
  (`destinationUrl`, `customCode`, `expiresAt`). Quem converte e valida é o
  `CreateLinkValidator` em `Application`, que acumula **todos** os erros por campo
  (Edge Case "vários campos inválidos") com mensagens em português (Princípio VII).
- `expiresAt` precisa ser ISO 8601 com fuso explícito (`Z` ou `±hh:mm`); sem fuso é
  recusado (premissa "instante absoluto com fuso"). Precisa ser estritamente maior que
  "agora" (FR-007, US4 cenário 4).
- URL: `Uri.TryCreate(..., UriKind.Absolute)`, esquema `http`/`https`, `Host` não vazio,
  comprimento ≤ 2048 medido na string recebida. Valores com espaços no início ou no fim são
  recusados, sem trim silencioso.
- Código personalizado: regex `^[A-Za-z0-9_-]{4,20}$` e lista de reservados (D9).
- Os erros de validação viram `Results.ValidationProblem(...)`: HTTP 400,
  `application/problem+json`, com `errors` por campo (Princípio V).
- `AddProblemDetails()` + `UseExceptionHandler()` + `UseStatusCodePages()`, com
  `CustomizeProblemDetails` para pôr `title`/`detail` em português. Assim um 400 de JSON
  malformado (falha de binding, antes do validador) também sai como `ProblemDetails`, e não
  existe 400 em outro formato.
- Não usamos a validação nativa de Minimal APIs do .NET 10 (`AddValidation`/DataAnnotations).
  As regras são de negócio e, pelo Princípio I, ficam no serviço; além disso, as mensagens
  padrão saem em inglês.

**Justificativa**: campos `string?` evitam que um único valor com tipo errado (uma data
inválida, por exemplo) derrube o binding inteiro e esconda os outros erros. Assim a resposta
consegue listar todos os problemas.

**Alternativas consideradas**:
- *FluentValidation*: dependência nova sem necessidade real (Princípio VI). Poucas regras,
  fáceis de escrever à mão.
- *DTO tipado (`Uri`, `DateTimeOffset?`)*: mensagens de binding em inglês e só o primeiro
  erro.

---

## D9. Rotas, status HTTP e códigos reservados

**Decisão**:

| Situação | Status | Corpo |
| --- | --- | --- |
| Link criado | 201 + `Location` | JSON com link curto e chave |
| Validação falhou | 400 | `ValidationProblem` |
| Código personalizado em uso | 409 | `ProblemDetails` |
| Redirecionamento válido | 302 + `Location` | vazio |
| Link não encontrado | 404 | `ProblemDetails` |
| Link expirado (redirecionamento) | 410 | `ProblemDetails` |
| Consulta sem chave / chave errada | 401 | `ProblemDetails` |

- O redirecionamento usa **302**, não 301 nem 308: navegadores e proxies guardam
  redirecionamentos permanentes em cache e deixariam de chamar o serviço, então os acessos
  não seriam contados (FR-008) e a expiração seria ignorada (FR-009). A resposta também leva
  `Cache-Control: no-store`.
- O redirecionamento fica na raiz (`GET /{code}`) para o link ser curto. A API de
  gerenciamento fica em `/api/links`.
- **Códigos reservados** (FR-005, Edge Case): `api`, `health`, `openapi`, `scalar`, `swagger`,
  comparados **sem** diferenciar maiúsculas de minúsculas, porque o roteamento do ASP.NET
  Core não diferencia (`/API/links` cai na mesma rota que `/api/links`). Ser reservado é
  regra estática, então entra como erro de validação 400 no campo `customCode`, junto dos
  demais erros. "Em uso" depende do banco e vira 409.
- Na consulta, código inexistente → 404 (US2 cenário 5). Código existente com chave
  inválida → 401 sem dados (FR-017). Revelar que um código existe não é problema, porque o
  próprio redirecionamento já revela isso.
- O `{code}` das rotas `GET /{code}` e `GET /api/links/{code}/stats` é **chave de busca, não
  entrada validada**. Um código de formato impossível (vazio, mais de 20 caracteres,
  caracteres inválidos) simplesmente não existe, e a FR-010 define a resposta como "não
  encontrado" (404), sem ir ao banco. O Princípio V (400 com `ProblemDetails`) vale para os
  dados de entrada que o cliente pede para o sistema gravar, ou seja, o corpo de
  `POST /api/links`. Responder 400 aqui também deixaria `/abc` e `/abcd` com respostas
  diferentes para a mesma situação: o link não existe.

**Alternativas consideradas**:
- *307*: semântica equivalente para `GET`; 302 é o padrão mais compatível com encurtadores.
- *403 para chave inválida*: 401 deixa claro que falta uma credencial válida. Os dois casos
  (ausente e errada) recebem a mesma resposta.

---

## D10. Testes

**Decisão**:
- `tests/Encurtador.UnitTests` (xUnit): validador, gerador de código (tamanho, alfabeto,
  ausência de caracteres ambíguos), hasher da chave, e `LinkService` com `FakeLinkRepository`
  e um `TimeProvider` fake escrito no próprio projeto de teste. Sem banco, rede ou HTTP
  (Princípio III).
- `tests/Encurtador.IntegrationTests` (xUnit + `WebApplicationFactory<Program>`): cada
  endpoint com caminho feliz até o SQLite (Princípio IV), formato do 400 (Princípio V),
  1.000 acessos concorrentes (SC-003), reinício com o mesmo arquivo de banco (SC-005) e
  corrida de código personalizado duplicado. Cada classe de teste usa um arquivo SQLite
  temporário próprio, apagado no fim.
- Tempo: `TimeProvider` (BCL, .NET 8+) injetado no serviço, para testar expiração sem
  `Thread.Sleep`.

**Alternativas consideradas**:
- *`Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`)*: pronto, mas é uma
  dependência para substituir uma classe de ~10 linhas (Princípio VI).
- *Provider EF Core InMemory nos testes de integração*: não aplica índice único nem traduz
  `ExecuteUpdate` como o SQLite, então não provaria D1 e D2.
- *FluentAssertions*: dependência evitável; as asserções do xUnit bastam.

---

## D11. URL pública do link curto

**Decisão**: `shortUrl` = `ShortLinks:PublicBaseUrl` (configuração, opcional) + `/` + código.
Se a configuração estiver vazia, usa esquema e host da requisição atual. A montagem da URL
fica no endpoint, porque depende do HTTP. O serviço devolve só o código.

**Justificativa**: atrás de um proxy reverso, o host da requisição nem sempre é o domínio
público. A configuração resolve isso sem `ForwardedHeaders` nesta versão.

---

## D12. Interface de testes com Scalar

**Decisão**: em `Development`, a API publica o documento OpenAPI em `/openapi/v1.json`
(`Microsoft.AspNetCore.OpenApi`) e uma interface de testes em `/scalar`
(`Scalar.AspNetCore`). O F5 do VS Code (`.vscode/launch.json`, via `serverReadyAction`) e o
`dotnet watch` (`launchUrl` do `launchSettings.json`) abrem o navegador direto nela; o
`dotnet run` ignora `launchBrowser`, e aí basta abrir `/scalar` à mão. Os
endpoints declaram resumo, descrição e respostas possíveis (`Produces`, `ProducesProblem`,
`ProducesValidationProblem`), porque devolvem `IResult` e, sem isso, o documento não
teria tipos de resposta. `scalar` entra na lista de códigos reservados (D9).

**Justificativa**:
- Pedido explícito: uma forma de exercitar a API sem montar `curl` à mão. O Scalar lê o
  documento OpenAPI e gera formulários para cada endpoint, inclusive o cabeçalho
  `X-Management-Key`.
- Só em `Development`: a interface é ferramenta de desenvolvimento, não parte do produto.
  Em produção, as rotas não existem.
- Reservar `scalar`: o roteamento prefere a rota literal `/scalar` ao parâmetro `/{code}`,
  então um link com esse código existiria, mas nunca redirecionaria.
- O documento OpenAPI não muda o contrato: é gerado a partir dos mesmos endpoints, e o
  contrato de referência continua sendo [contracts/http-api.md](./contracts/http-api.md).

**Alternativas consideradas**:
- *Swashbuckle (Swagger UI)*: o template do .NET 9+ deixou de incluí-lo, e o
  `Microsoft.AspNetCore.OpenApi` já gera o documento. Seriam dois geradores para o
  mesmo fim.
- *Arquivo `.http` do VS Code/Visual Studio*: não precisa de pacote, mas não oferece uma
  interface navegável nem documenta as respostas.

---

## Dependências NuGet (Princípio VI)

| Pacote | Projeto | Problema que resolve | Por que o existente não basta |
| --- | --- | --- | --- |
| `Microsoft.EntityFrameworkCore.Sqlite` | Infrastructure | Provider EF Core para SQLite | EF Core não traz providers relacionais embutidos; SQLite é o banco escolhido nesta versão |
| `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`) | Infrastructure e Api | Geração de migrations em tempo de desenvolvimento. Na Api, é exigido porque ela é o startup project do `dotnet ef` | Exigido pelo `dotnet ef`; com `PrivateAssets=all` não vai para o runtime nem se propaga para outros projetos |
| `dotnet-ef` (ferramenta local, `.config/dotnet-tools.json`) | — | CLI para criar migrations | O SDK não inclui a ferramenta do EF |
| `xunit.v3` | ambos os projetos de teste | Framework de teste | Exigido pela constituição |
| `xunit.runner.visualstudio` | ambos os projetos de teste | Execução via `dotnet test` / IDE | Sem ele o `dotnet test` não descobre os testes |
| `Microsoft.NET.Test.Sdk` | ambos os projetos de teste | Host de teste do `dotnet test` | Exigido pelo runner |
| `Microsoft.AspNetCore.Mvc.Testing` | IntegrationTests | `WebApplicationFactory` (servidor em memória com a pilha real) | Princípio IV pede a pilha HTTP real; montar o `TestServer` à mão duplicaria o que o pacote faz |
| `Microsoft.AspNetCore.OpenApi` | Api | Gera o documento OpenAPI a partir dos endpoints (D12) | O ASP.NET Core não gera OpenAPI sem esse pacote; é o gerador oficial da Microsoft |
| `Scalar.AspNetCore` | Api | Interface de testes navegável sobre o documento OpenAPI, só em `Development` (D12) | O ASP.NET Core não traz interface; o pacote só serve a página e usa o documento já gerado |

Nenhum outro pacote. Criptografia, aleatoriedade, `TimeProvider`, `ProblemDetails` e
validação vêm da BCL e do ASP.NET Core.

Itens que não são pacotes NuGet, registrados durante a implementação:

| Item | Onde | Problema que resolve | Por que o existente não basta |
| --- | --- | --- | --- |
| `FrameworkReference Microsoft.AspNetCore.App` | Application | Dá a `AddApplication()` as abstrações de DI (`IServiceCollection`, `TryAddSingleton`) | Elas não fazem parte de `Microsoft.NETCore.App`. A alternativa seria o NuGet `Microsoft.Extensions.DependencyInjection.Abstractions`; o framework compartilhado já está instalado com o runtime e não adiciona pacote. Regra de convivência: Application não usa tipos HTTP (verificado na auditoria T067) |
| `global.json` com `"test": { "runner": "Microsoft.Testing.Platform" }` | raiz | O `xunit.v3` 4.x roda sobre o Microsoft.Testing.Platform, e o SDK 10 recusa o modo VSTest no `dotnet test` | Configuração do SDK, não dependência. Sem ela o `dotnet test` falha. Os testes rodam com `dotnet test --solution Encurtador.slnx` |
