<!--
Sync Impact Report
==================
Version change: (template, sem versão) → 1.0.0
Modified principles: nenhum (primeira ratificação; todos os placeholders substituídos)
Added principles:
  - I. Endpoints Finos, Serviços de Aplicação
  - II. Persistência Isolada via EF Core
  - III. Regra de Negócio Coberta por Teste Unitário
  - IV. Endpoint Coberto por Teste de Integração
  - V. Validação Responde 400 com ProblemDetails
  - VI. Dependências Justificadas
  - VII. Idioma: Código em Inglês, Mensagens em Português
Added sections:
  - Stack e Restrições Técnicas
  - Fluxo de Desenvolvimento
Removed sections: nenhuma
Templates: não alterados (leem a constituição em tempo de execução)
Follow-up TODOs: nenhum
-->

# Encurtador Constitution

## Core Principles

### I. Endpoints Finos, Serviços de Aplicação

- Endpoints de Minimal API (e qualquer handler HTTP) MUST NOT acessar o banco de dados
  diretamente: nada de `DbContext`, repositório ou conexão injetados no endpoint.
- Todo acesso a dados MUST passar por um serviço de aplicação.
- Regra de negócio MUST NOT viver no endpoint nem no handler HTTP. O endpoint só faz:
  ler a requisição, chamar o serviço de aplicação e traduzir o resultado em resposta HTTP.
- Decisões de negócio (o que é válido, o que acontece, em que ordem) MUST ficar em serviços
  de aplicação ou no domínio.

**Racional**: manter HTTP como detalhe de transporte deixa a regra de negócio testável sem
servidor e permite trocar ou acrescentar formas de entrada sem duplicar lógica.

### II. Persistência Isolada via EF Core

- A persistência MUST usar EF Core.
- O provider de banco MUST ser trocável por configuração/registro de DI, sem alterar código
  de domínio ou de serviços de aplicação.
- SQL bruto (`FromSqlRaw`, `ExecuteSqlRaw`, strings SQL, Dapper etc.) MUST NOT aparecer fora
  da camada de infraestrutura de dados. Se for indispensável, MUST ficar concentrado nessa
  camada e justificado no plano.
- Recursos específicos de um provider MUST NOT vazar para fora da camada de infraestrutura.

**Racional**: SQL espalhado e dependência de um provider amarram a aplicação a um banco e
tornam os testes de integração mais difíceis de montar.

### III. Regra de Negócio Coberta por Teste Unitário

- Toda regra de negócio MUST ter ao menos um teste unitário com xUnit.
- Testes unitários MUST NOT depender de banco real, rede ou servidor HTTP.
- Uma tarefa que introduz ou altera regra de negócio MUST NOT ser considerada concluída
  sem o teste unitário correspondente passando.

**Racional**: a regra de negócio é o que dá valor ao sistema; teste unitário é a forma mais
barata e rápida de garantir que ela continua certa.

### IV. Endpoint Coberto por Teste de Integração

- Todo endpoint MUST ter ao menos um teste de integração do caminho feliz, exercitando a
  pilha HTTP real (por exemplo, `WebApplicationFactory`) até a persistência.
- Testes de integração MUST usar xUnit.
- Um endpoint novo MUST NOT ser considerado concluído sem esse teste passando.

**Racional**: testes unitários não provam que roteamento, DI, serialização e persistência
estão ligados corretamente; o teste de integração prova.

### V. Validação Responde 400 com ProblemDetails

- Todo erro de validação de entrada MUST retornar HTTP 400 com corpo `ProblemDetails`
  (`application/problem+json`), incluindo os detalhes de cada campo inválido quando houver.
- Não há exceção a esta regra: respostas 400 em outro formato MUST NOT existir.
- O formato da resposta de validação MUST ser coberto por ao menos um teste de integração.

**Racional**: um formato único e padronizado (RFC 9457) permite que clientes tratem erros de
forma genérica e previsível.

### VI. Dependências Justificadas

- Um pacote NuGet, serviço externo ou ferramenta nova MUST NOT entrar no projeto sem
  justificativa escrita no plano da feature (`plan.md`).
- A justificativa MUST dizer qual problema a dependência resolve e por que o que já existe
  (BCL, ASP.NET Core, EF Core ou dependências atuais) não basta.

**Racional**: cada dependência é custo permanente de manutenção, segurança e atualização.

### VII. Idioma: Código em Inglês, Mensagens em Português

- Nomes de classes, interfaces, métodos, propriedades, arquivos e pastas MUST estar em inglês.
- Mensagens de erro voltadas ao usuário ou ao cliente da API (por exemplo, `title`, `detail`
  e erros de campo em `ProblemDetails`) MUST estar em português.

**Racional**: código em inglês segue o ecossistema .NET; mensagens em português atendem o
público da API.

## Stack e Restrições Técnicas

- Plataforma: .NET 10 com C#.
- API HTTP: ASP.NET Core Minimal APIs. Controllers MVC MUST NOT ser usados.
- Persistência: EF Core, conforme o Princípio II.
- Testes: xUnit para testes unitários e de integração, conforme os Princípios III e IV.

## Fluxo de Desenvolvimento

- Toda mensagem de commit MUST seguir o formato `tipo(escopo): descrição [ID-DO-REQUISITO]`,
  por exemplo `feat(links): cria link encurtado [FR-001]`.
  - `tipo` é um tipo de Conventional Commits (`feat`, `fix`, `test`, `refactor`, `docs`,
    `chore`, `build`, `ci`, `perf`).
  - O identificador do requisito MUST ser o da spec da feature (por exemplo, `FR-001`).
    Commits sem requisito associado (por exemplo, manutenção) MUST declarar isso
    explicitamente com `[sem-requisito]`.
- Antes de concluir uma feature, MUST ser verificado que: endpoints não acessam banco nem
  contêm regra de negócio (I), não há SQL fora da infraestrutura (II), os testes exigidos
  (III, IV, V) existem e passam, e toda dependência nova está justificada no plano (VI).

## Governance

- Esta constituição prevalece sobre specs, planos, tarefas e qualquer outra prática do
  projeto. Se um plano contradisser algum princípio, quem muda é o plano, nunca o princípio.
- O "Constitution Check" do plano MUST verificar cada princípio. Uma violação MUST ser
  resolvida alterando o plano; ela não pode ser aceita como exceção.
- A constituição só muda por emenda explícita (via `/speckit-constitution`), registrando o
  motivo e o impacto nas features em andamento.
- Versionamento semântico:
  - MAJOR: remoção ou redefinição incompatível de princípio ou regra de governança.
  - MINOR: novo princípio ou seção, ou expansão material de orientação.
  - PATCH: esclarecimentos e ajustes de redação sem mudança de significado.
- Revisões de código e de plano MUST checar conformidade com esta constituição.

**Version**: 1.0.0 | **Ratified**: 2026-10-30 | **Last Amended**: 2026-10-30
