# Plataforma interna de ideias

Aplicação web interna onde qualquer pessoa da área de TI e produto propõe uma ideia e
vota nas dos outros. O resultado é uma lista pública ordenada por apoio real.

O propósito é promover iniciativa e colaboração. As ideias mais votadas servem para a
equipa aprender e explorar em conjunto, não para alimentar um backlog de entrega.
Ninguém se compromete a construir o que ganha, e isso é intencional.

## Estado

Fase de planeamento. Ainda não existe código. O briefing está fechado e a arquitetura
está decidida e documentada.

O nome de produto ainda não foi escolhido. `IdeaBoard` é um nome de trabalho usado
apenas como identificador de código, em namespaces, caminhos e no nome do site do IIS.

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
| [Docs/08-Prompts-dos-Agentes.md](Docs/08-Prompts-dos-Agentes.md) | Os quatro papéis de agente, as suas fronteiras e os prompts de arranque |
| [Docs/ADR/](Docs/ADR/) | Uma decisão por ficheiro, com alternativas e consequências |

## Decisões ainda em aberto

Registadas aqui para não desaparecerem. Nenhuma delas bloqueia o arranque da Fase 1,
com exceção da primeira.

- **Nome de produto e identificador de código.** Tem de ficar decidido antes de criar
  a solução, porque depois fica espalhado por namespaces e caminhos.
- **Valores alvo dos critérios de sucesso.** O briefing pede que sejam fixados pela
  equipa antes do lançamento.
- **Necessidade de client secret no registo do Entra ID.** A confirmar num spike
  curto, conforme [ADR-003](Docs/ADR/ADR-003-autenticacao-entra-id.md).
- **Prazo da substituição do certificado.** A aplicação arranca com um certificado
  emitido pela própria equipa, conforme
  [ADR-013](Docs/ADR/ADR-013-certificado-no-arranque.md). O prazo para o trocar por um
  certificado confiado fica fixado no lançamento, e não pode ficar em aberto.
