# Plataforma interna de ideias

Aplicação web interna onde qualquer pessoa da área de TI e produto propõe uma ideia e
vota nas dos outros. O resultado é uma lista pública ordenada por apoio real.

O propósito é promover iniciativa e colaboração. As ideias mais votadas servem para a
equipa aprender e explorar em conjunto, não para alimentar um backlog de entrega.
Ninguém se compromete a construir o que ganha, e isso é intencional.

## Estado

Em construção. O briefing está fechado, a arquitetura está decidida e documentada, e o
produto mínimo está construído e a correr localmente.

O que falta para a Fase 5, a publicação, está em
[Docs/05-Plano-de-Entrega.md](Docs/05-Plano-de-Entrega.md).

O **identificador de código está decidido e é `IdeaBoard`**. Fica em namespaces,
caminhos, nomes de projeto e no nome do site do IIS.

O **nome de produto em prosa continua por escolher**, e é uma decisão separada.
`IdeaBoard` não é o nome do produto, é o identificador.

## Resumo técnico

| | |
|---|---|
| Plataforma | .NET 10 (LTS) |
| Aplicação | ASP.NET Core Razor Pages, renderização no servidor |
| Dados | EF Core sobre SQLite |
| Autenticação | Microsoft Entra ID por OpenID Connect |
| Publicação | IIS num servidor interno, hosting in-process |
| Testes | xUnit, mais Playwright para os testes ponta a ponta |

## Documentação

Comece pelo briefing, que define o porquê e o âmbito. Os documentos seguintes assumem
que ele já foi lido.

| Documento | Para quê |
|---|---|
| [BRIEFING.md](BRIEFING.md) | Problema, objetivo, produto mínimo, o que fica de fora, riscos assumidos |
| [Docs/01-Arquitetura.md](Docs/01-Arquitetura.md) | Visão geral do sistema, estrutura da solução, fluxo de um pedido |
| [Docs/02-Especificacao-Funcional.md](Docs/02-Especificacao-Funcional.md) | As quatro funcionalidades em detalhe, ecrãs, regras e validações |
| [Docs/03-Modelo-de-Dados.md](Docs/03-Modelo-de-Dados.md) | Entidades, índices, configuração EF Core, migrações |
| [Docs/04-Requisitos-Nao-Funcionais-e-Seguranca.md](Docs/04-Requisitos-Nao-Funcionais-e-Seguranca.md) | Carga, desempenho, segurança, privacidade, retenção |
| [Docs/05-Plano-de-Entrega.md](Docs/05-Plano-de-Entrega.md) | Fases, pré requisitos e dependências externas |
| [Docs/06-Operacao-e-Publicacao.md](Docs/06-Operacao-e-Publicacao.md) | Entra ID, IIS, backup, métricas, correções manuais |
| [Docs/07-Plano-de-Verificacao.md](Docs/07-Plano-de-Verificacao.md) | Estratégia de testes e verificação manual antes do lançamento |
| [Docs/08-Prompts-dos-Agentes.md](Docs/08-Prompts-dos-Agentes.md) | Os cinco papéis de agente, as suas fronteiras e os prompts de arranque |
| [Docs/ADR/](Docs/ADR/) | Uma decisão por ficheiro, com alternativas e consequências |

## Decisões ainda em aberto

Registadas aqui para não desaparecerem. São três decisões de produto, mais o que a
mudança de autenticação deixar em aberto quando o ADR for escrito.

- **Nome de produto em prosa.** O identificador de código já está fechado em
  `IdeaBoard`, e esta é a outra metade da decisão, que continua por tomar. Pode ser
  diferente do identificador e pode ser decidido até ao lançamento.
- **Chave de idempotência da submissão.** O voto é idempotente, a submissão não é, e a
  chave que a tornaria idempotente aproxima se da deteção de duplicados, que o briefing
  exclui. As alternativas estão em
  [Docs/02](Docs/02-Especificacao-Funcional.md).
- **Valores alvo dos critérios de sucesso.** O briefing pede que sejam fixados pela
  equipa antes do lançamento.

### Decididas, com o valor por preencher

- **Endereço do site.** É o **nome curto do servidor com porta**, e não o nome
  qualificado. A forma está decidida e o valor concreto ainda não, porque o servidor
  ainda não está escolhido.

### Pendentes do ADR da autenticação

A 13 de novembro de 2026 a autenticação mudou de Microsoft Entra ID para autenticação
Windows por Negotiate no IIS. A necessidade de client secret e o registo de aplicação no
Entra ID **saíram desta lista por deixarem de existir**, e não por terem sido
respondidos.

O prazo da substituição do certificado, antes aqui registado por causa do
[ADR-013](Docs/ADR/ADR-013-certificado-no-arranque.md), depende agora da decisão de HTTP
contra HTTPS, que está em aberto e que decide se aquele ADR sobrevive.

O que substitui estes pontos fica por escrever até existir o ADR que substitui o
[ADR-003](Docs/ADR/ADR-003-autenticacao-entra-id.md), que pertence ao ARQ. Até lá esta
lista está incompleta de propósito.
