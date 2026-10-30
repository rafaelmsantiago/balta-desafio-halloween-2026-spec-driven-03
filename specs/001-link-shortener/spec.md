# Feature Specification: Encurtador de Links

**Feature Branch**: `001-link-shortener`

**Created**: 2026-10-30

**Status**: Draft

**Input**: User description: "Um encurtador de links. O criador informa uma URL longa e recebe um link curto para compartilhar. Quem clica no link curto é redirecionado ao destino, e cada acesso é contado. O criador consegue consultar quantos acessos o link dele recebeu. Comportamentos esperados: o código do link é gerado automaticamente com 7 caracteres alfanuméricos; o criador pode escolher o próprio código, entre 4 e 20 caracteres; o código é único em todo o sistema; a URL de destino precisa ser http ou https e ter no máximo 2048 caracteres; o criador pode definir uma data de expiração; link expirado não redireciona e não conta acesso; o contador nunca diminui; os links continuam válidos depois que a aplicação reinicia. Fora de escopo: cadastro e login de usuários, domínio personalizado, relatório geográfico, edição do destino de um link já criado."

## Clarifications

### Session 2026-10-30

- Q: Sem login, quem pode consultar quantos acessos um link recebeu? → A: Apenas quem tem a chave secreta entregue na criação do link; sem chave válida, a consulta é recusada.
- Q: O criador usa o encurtador por páginas web ou apenas por chamadas diretas ao serviço? → A: Apenas chamadas diretas, sem páginas web; todas as respostas são estruturadas e o acesso a um link válido redireciona.
- Q: O código gerado pode usar qualquer letra ou dígito? → A: Não. Ficam de fora os caracteres que se confundem visualmente (`0 O o 1 I l`), para que o link possa ser lido e digitado sem erro. A restrição não vale para códigos personalizados (ver `research.md` D4).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Criar um link curto e ser redirecionado (Priority: P1)

O criador informa uma URL longa e recebe de volta um link curto com um código gerado automaticamente. Qualquer pessoa que abrir esse link curto é levada ao endereço de destino.

**Why this priority**: É a razão de existir do produto. Sem criar e redirecionar, nenhuma outra funcionalidade tem valor.

**Independent Test**: Criar um link a partir de uma URL válida, abrir o link curto retornado e confirmar que o navegador chega à URL de destino.

**Acceptance Scenarios**:

1. **Given** uma URL de destino `https://exemplo.com/artigos/2026/um-texto-bem-longo`, **When** o criador pede um link curto sem informar código, **Then** o sistema devolve um link curto cujo código tem exatamente 7 caracteres alfanuméricos, junto com uma chave secreta de consulta exibida somente nesse momento.
2. **Given** um link curto existente e não expirado, **When** alguém abre o link curto, **Then** é redirecionado à URL de destino original.
3. **Given** uma URL com esquema diferente de http/https (por exemplo `ftp://exemplo.com` ou `javascript:alert(1)`), **When** o criador tenta encurtá-la, **Then** o pedido é recusado com uma mensagem em português explicando que só são aceitas URLs http ou https.
4. **Given** uma URL http válida com 2049 caracteres, **When** o criador tenta encurtá-la, **Then** o pedido é recusado com uma mensagem explicando o limite de 2048 caracteres.
5. **Given** um código que não existe, **When** alguém abre o link curto correspondente, **Then** o sistema informa que o link não foi encontrado e não redireciona.
6. **Given** links criados antes de a aplicação reiniciar, **When** a aplicação volta a funcionar, **Then** esses links continuam redirecionando e seus contadores mantêm os valores anteriores.

---

### User Story 2 - Contar e consultar acessos (Priority: P2)

Cada abertura do link curto que resulta em redirecionamento é contada. O criador consulta quantos acessos o link recebeu, apresentando o código e a chave secreta que recebeu ao criar o link.

**Why this priority**: Medir o alcance do link é o segundo valor mais citado pelo criador, mas o link já é útil sem a contagem.

**Independent Test**: Criar um link, abri-lo 3 vezes e consultar seus acessos; o resultado deve ser 3.

**Acceptance Scenarios**:

1. **Given** um link recém-criado, **When** o criador consulta seus acessos, **Then** o total é 0.
2. **Given** um link não expirado que foi aberto 5 vezes, **When** o criador consulta seus acessos, **Then** o total é 5.
3. **Given** um link aberto por várias pessoas ao mesmo tempo, **When** todos os acessos terminam, **Then** o total consultado é igual ao número de redirecionamentos feitos, sem acessos perdidos.
4. **Given** qualquer link, **When** o total de acessos é consultado várias vezes ao longo do tempo, **Then** cada valor é maior ou igual ao valor consultado antes.
5. **Given** um código que não existe, **When** alguém consulta seus acessos, **Then** o sistema informa que o link não foi encontrado.
6. **Given** um link existente, **When** alguém consulta seus acessos sem chave ou com uma chave diferente da entregue na criação, **Then** a consulta é recusada e nenhum dado do link (total, destino ou datas) é revelado.
7. **Given** dois links criados, **When** o criador usa a chave do primeiro para consultar o segundo, **Then** a consulta é recusada.

---

### User Story 3 - Escolher o próprio código (Priority: P3)

O criador pode informar o código que quer usar no link curto, em vez de receber um gerado, para ter um link fácil de lembrar.

**Why this priority**: Melhora a experiência, mas o produto funciona plenamente com códigos gerados.

**Independent Test**: Criar um link informando o código `promo-natal`, abrir o link curto com esse código e confirmar o redirecionamento.

**Acceptance Scenarios**:

1. **Given** o código `promo-natal` ainda não usado, **When** o criador cria um link com esse código, **Then** o link curto usa exatamente esse código.
2. **Given** um código com 3 caracteres ou com 21 caracteres, **When** o criador tenta usá-lo, **Then** o pedido é recusado com uma mensagem explicando o limite de 4 a 20 caracteres.
3. **Given** um código com caracteres não permitidos (por exemplo espaço, `/` ou acentos), **When** o criador tenta usá-lo, **Then** o pedido é recusado com uma mensagem indicando os caracteres aceitos.
4. **Given** um código já usado por outro link (expirado ou não), **When** o criador tenta usá-lo, **Then** o pedido é recusado com uma mensagem informando que o código já está em uso, e o link existente não é alterado.

---

### User Story 4 - Definir data de expiração (Priority: P3)

O criador pode definir até quando o link funciona. Depois disso, o link não redireciona mais e os acessos deixam de ser contados.

**Why this priority**: Útil para campanhas temporárias, mas opcional; links sem expiração atendem o uso principal.

**Independent Test**: Criar um link com expiração próxima, confirmar que redireciona antes do prazo e que, após o prazo, não redireciona e o contador não muda.

**Acceptance Scenarios**:

1. **Given** um link sem data de expiração, **When** é aberto a qualquer momento, **Then** redireciona normalmente.
2. **Given** um link com expiração no futuro, **When** é aberto antes do prazo, **Then** redireciona e o acesso é contado.
3. **Given** um link cuja data de expiração já passou, **When** alguém o abre, **Then** o sistema informa que o link expirou, não redireciona e o contador não aumenta.
4. **Given** uma data de expiração no passado ou igual ao momento atual, **When** o criador tenta criar o link, **Then** o pedido é recusado com uma mensagem explicando que a expiração precisa estar no futuro.
5. **Given** um link expirado, **When** o criador consulta seus acessos, **Then** recebe, com a chave correta, o total acumulado até a expiração e a informação de que o link está expirado.

---

### Edge Cases

- Dois pedidos simultâneos com o mesmo código personalizado: exatamente um é aceito; o outro recebe a mensagem de código em uso.
- O código gerado automaticamente coincide com um já existente: o sistema gera outro, de forma transparente para o criador.
- A mesma URL de destino encurtada duas vezes: cada pedido cria um link novo, com código e contador próprios.
- URL de destino vazia, ausente, malformada ou sem domínio (por exemplo `https://`): o pedido é recusado.
- URL de destino com exatamente 2048 caracteres: é aceita.
- Código personalizado com exatamente 4 ou exatamente 20 caracteres: é aceito.
- Código personalizado que coincide com um endereço reservado para funções do próprio sistema: é recusado como indisponível.
- Acesso no instante exato da expiração: o link já é tratado como expirado.
- Códigos que diferem apenas em maiúsculas/minúsculas (`Promo` e `promo`): são considerados códigos diferentes.
- Pedido com vários campos inválidos ao mesmo tempo: a resposta lista todos os problemas, não apenas o primeiro.
- Criador perde a chave secreta: não há recuperação; o link continua redirecionando, mas seus acessos não podem mais ser consultados.
- Chave secreta usada no lugar do código, ou código usado no lugar da chave: a consulta é recusada.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir que o criador informe uma URL de destino e receba um link curto contendo um código que identifica o link.
- **FR-002**: Quando o criador não informar código, o sistema MUST gerar automaticamente um código com exatamente 7 caracteres alfanuméricos (letras de A a Z maiúsculas e minúsculas e dígitos de 0 a 9), excluídos os caracteres ambíguos `0`, `O`, `o`, `1`, `I` e `l`.
- **FR-003**: O sistema MUST permitir que o criador informe o próprio código, com 4 a 20 caracteres, composto apenas por letras sem acento (A–Z, a–z), dígitos (0–9), hífen (`-`) e sublinhado (`_`).
- **FR-004**: O sistema MUST garantir que cada código seja único em todo o sistema, inclusive em relação a links expirados; um código nunca é reutilizado.
- **FR-005**: O sistema MUST recusar código personalizado já usado ou reservado, sem alterar o link existente.
- **FR-006**: O sistema MUST aceitar apenas URLs de destino absolutas com esquema `http` ou `https`, com domínio, e com no máximo 2048 caracteres.
- **FR-007**: O sistema MUST permitir que o criador defina, opcionalmente, uma data e hora de expiração, que precisa estar no futuro no momento da criação.
- **FR-008**: Ao receber o acesso a um código existente e não expirado, o sistema MUST redirecionar para a URL de destino e contar esse acesso.
- **FR-009**: Ao receber o acesso a um código expirado, o sistema MUST NOT redirecionar e MUST NOT contar o acesso; MUST informar que o link expirou.
- **FR-010**: Ao receber o acesso a um código inexistente, o sistema MUST informar que o link não foi encontrado, sem redirecionar.
- **FR-011**: O sistema MUST permitir consultar o total de acessos de um link a partir do seu código e da sua chave secreta, informando também a URL de destino, a data de criação, a data de expiração (se houver) e se o link está expirado.
- **FR-012**: O contador de acessos MUST NOT diminuir em nenhuma circunstância, e MUST NOT perder acessos quando houver acessos simultâneos.
- **FR-013**: Links, destinos, datas e contadores MUST permanecer válidos e inalterados depois que a aplicação reinicia.
- **FR-014**: O sistema MUST NOT permitir alterar a URL de destino de um link já criado.
- **FR-015**: Pedidos de criação inválidos MUST ser recusados com mensagens em português que indiquem cada campo inválido e o motivo.
- **FR-016**: Ao criar um link, o sistema MUST gerar uma chave secreta de consulta exclusiva daquele link, difícil de adivinhar e diferente do código, e entregá-la ao criador somente na resposta da criação.
- **FR-017**: O sistema MUST recusar a consulta de acessos quando a chave estiver ausente ou não corresponder ao link, sem revelar nenhum dado do link.
- **FR-018**: O sistema MUST NOT oferecer recuperação nem troca da chave secreta.
- **FR-019**: Todas as interações (criar, abrir e consultar) MUST ser feitas por chamadas diretas ao serviço, sem páginas web; exceto o redirecionamento de um link válido, toda resposta MUST ser estruturada e legível por programas, inclusive as de link expirado, link não encontrado e consulta recusada, cada uma distinguível das demais.

### Key Entities

- **Link curto**: um encurtamento criado pelo criador. Atributos: código (único, imutável), URL de destino (imutável), data de criação, data de expiração opcional, total de acessos (só aumenta), chave secreta de consulta (imutável, conhecida apenas pelo criador).
- **Acesso**: uma abertura do link curto que resultou em redirecionamento. Para esta feature, basta o total agregado por link; não há relatório por origem, localização ou período.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: O criador obtém um link curto em menos de 1 minuto a partir do momento em que tem a URL longa em mãos, sem precisar se cadastrar.
- **SC-002**: 100% dos acessos a links válidos chegam ao destino correto, e o redirecionamento é percebido como imediato (menos de 1 segundo em 95% dos acessos).
- **SC-003**: Em um teste com 1.000 acessos simultâneos ao mesmo link, o total consultado é exatamente 1.000.
- **SC-004**: 0 links expirados redirecionam ou têm o contador alterado após a expiração.
- **SC-005**: Após reiniciar a aplicação, 100% dos links criados antes continuam funcionando com os mesmos destinos e contadores.
- **SC-006**: 100% dos pedidos inválidos (esquema, tamanho, código ou expiração) são recusados com uma mensagem em português que permite ao criador corrigir o pedido na primeira nova tentativa.
- **SC-007**: 100% das consultas de acessos sem a chave secreta correta são recusadas sem revelar dados do link.

## Assumptions

- Não há contas de usuário: o "criador" é quem possui a chave secreta entregue na criação; o sistema não associa links a pessoas.
- Códigos diferenciam maiúsculas de minúsculas, tanto os gerados quanto os personalizados.
- Hífen e sublinhado são aceitos em códigos personalizados para permitir códigos legíveis; códigos gerados usam só letras e dígitos.
- Todo redirecionamento bem-sucedido conta como acesso, sem distinguir pessoas, robôs ou aberturas repetidas pela mesma pessoa.
- A data de expiração é um instante absoluto (data e hora com fuso); não há limite máximo para ela.
- Links não são removidos automaticamente; um link expirado continua existindo para consulta e para manter o código ocupado.
- Encurtar a mesma URL várias vezes gera links distintos; não há deduplicação.
- O sistema não verifica se a URL de destino está no ar nem se tem conteúdo malicioso.
- O criador usa o serviço por um programa ou ferramenta de linha de comando; quem clica no link usa qualquer navegador, que segue o redirecionamento.
- Fora de escopo: páginas web (formulários, telas de erro), cadastro e login, domínio personalizado, relatório geográfico, edição do destino, exclusão de links e alteração da data de expiração após a criação.
