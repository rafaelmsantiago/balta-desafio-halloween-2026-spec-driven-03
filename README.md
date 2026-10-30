<img width="100%" alt="Halloween 2026" src="https://baltaio.blob.core.windows.net/static/images/v4/challenges/halloween-2026/banner.jpg" />

## 🎃 Halloween - Desafio 3

Oi, eu sou o Rafael Santiago e este é o espaço onde compartilho minha jornada de aprendizado durante o desafio **Halloween 2026**, realizado pelo [balta.io](https://balta.io). 👻

Aqui você vai encontrar projetos, exercícios e códigos que estou desenvolvendo durante o desafio.

### Sobre este desafio

Neste desafio o objetivo é criar um encurtador de URLs completo utilizando os conceitos de Spec-Driven Development via SpecKit.

#### O que foi implementado

Uma API de encurtamento de links em **.NET 10**, construída de ponta a ponta com o fluxo do SpecKit (`specify` → `clarify` → `plan` → `tasks` → `analyze` → `implement`). Toda a documentação da feature está em [specs/001-link-shortener](specs/001-link-shortener).

##### Funcionalidades

- `POST /api/links`: cria um link curto com código gerado de 7 caracteres ou com código personalizado (4 a 20 caracteres), e expiração opcional. A resposta traz uma chave de gerenciamento, entregue uma única vez.
- `GET /{code}`: redireciona com `302` e conta o acesso. Link expirado responde `410`, e código inexistente, `404`.
- `GET /api/links/{code}/stats`: mostra o total de acessos, mas só para quem envia a chave no cabeçalho `X-Management-Key`. Sem a chave certa, responde `401` sem revelar nenhum dado do link.
- Erros de validação respondem `400` em `ProblemDetails`, listando todos os campos inválidos de uma vez, com mensagens em português.
- Interface de testes com **Scalar** em `/scalar`, disponível só em desenvolvimento.

##### Decisões técnicas registradas no plano, com justificativa

- A unicidade do código é garantida por um **índice único no banco**, além da checagem no serviço, para que dois pedidos simultâneos com o mesmo código não sejam aceitos juntos.
- O contador é incrementado por um **`UPDATE` atômico**, nunca lido e regravado pela aplicação. Assim nenhum acesso se perde e nenhum link expirado conta acesso.
- A chave de gerenciamento é guardada **só como hash SHA-256**, nunca em texto puro.
- O código gerado usa um **alfabeto sem caracteres ambíguos** (sem `0 O o 1 I l`).

##### Arquitetura e stack

- C#, ASP.NET Core Minimal APIs (um arquivo de endpoints por recurso) e EF Core com SQLite. O provider pode ser trocado para PostgreSQL por configuração.
- Três camadas: `Api` (HTTP), `Application` (o `LinkService`, dono das regras de negócio) e `Infrastructure` (persistência).
- **130 testes com xUnit v3**: unitários para as regras de negócio, sem banco, e de integração para cada endpoint, com `WebApplicationFactory`. Os de integração cobrem também 1.000 acessos simultâneos contados sem perda, a corrida pelo mesmo código e o reinício da aplicação sem perda de dados.

##### Como executar

```bash
dotnet test --solution Encurtador.slnx       # roda os testes
dotnet run --project src/Encurtador.Api      # sobe a API em http://localhost:5289
```

A interface de testes fica em `http://localhost:5289/scalar`. No VS Code, o F5 na configuração "Encurtador.Api (Scalar)" sobe a API e já abre essa página.

Neste processo eu aprendi:

* ✅ A escrever uma **constituição** para o projeto, com regras inegociáveis como endpoint sem acesso a dados, teste obrigatório e dependência justificada. Ela serve de portão em cada etapa, e o plano é que se ajusta a ela, não o contrário.
* ✅ Que o `/speckit-clarify` acha lacunas que eu não tinha visto. Sem login, quem pode ver as estatísticas de um link? Essa pergunta virou a chave de gerenciamento.
* ✅ A registrar cada decisão técnica com **justificativa e alternativas descartadas**, para que o porquê não se perca depois.
* ✅ Que o `/speckit-analyze` encontra problemas **antes do código**. Ele apontou uma tarefa impossível de concluir como estava escrita e um requisito em conflito com o alfabeto sem ambíguos.
* ✅ Que "checar e depois inserir" perde em concorrência: só o banco, com um índice único, garante que um código seja único.
* ✅ Que ler, somar e gravar um contador perde acessos sob carga, e que um `UPDATE ... SET x = x + 1` resolve sem lock.
* ✅ Que uma chave aleatória de 256 bits não precisa de bcrypt nem de salt, ao contrário de uma senha: SHA-256 com comparação em tempo constante basta.
* ✅ A escrever os testes antes do código, confirmando que falham antes de implementar.
* ✅ Detalhes do .NET 10: o xUnit v3 exige o Microsoft.Testing.Platform (configurado no `global.json`), o provider SQLite não compara `DateTimeOffset` em consultas e o `dotnet run` ignora o `launchBrowser`.

## Badge

<img src="https://baltaio.blob.core.windows.net/static/images/v4/challenges/halloween-2026/03.png" width="200" />

## Problema

Criar um encurtador de links. O criador informa uma URL longa e recebe um link curto para compartilhar. Quem abre o link curto é redirecionado ao destino, e cada acesso é contado. O criador pode consultar quantos acessos o link recebeu, escolher o próprio código e definir uma data de expiração. O código é único em todo o sistema, a URL de destino precisa ser http ou https com no máximo 2048 caracteres, o contador nunca diminui e os links continuam válidos depois que a aplicação reinicia.

Ficaram fora de escopo: cadastro e login de usuários, domínio personalizado, relatório geográfico e edição do destino de um link já criado.

## Sobre o Halloween 2026

O desafio **Halloween 2026** consiste em implementar o modelo Spec-Driven Development de ponta a ponta, criando apps completas com IA.

### Veja meu progresso no desafio

[github.com/balta-io/balta-desafio-halloween-2026-spec-driven-03](https://github.com/balta-io/balta-desafio-halloween-2026-spec-driven-03)
