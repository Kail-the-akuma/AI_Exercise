# ADR-006. Um projeto de aplicação, com separação por pastas

**Estado**: Aceite. **Data**: 30 de outubro de 2026.

## Contexto

É hábito dividir uma aplicação .NET em projetos por camada, com `Domain`,
`Application`, `Infrastructure` e `Web` em assemblies separados. O hábito existe por
boas razões, que aqui não se aplicam.

A aplicação tem quatro funcionalidades, três entidades e três serviços.

## Decisão

Um projeto de aplicação, `IdeaBoard.Web`, com separação por pastas: `Pages`,
`Domain`, `Data` e `Services`.

Dois projetos de teste, `IdeaBoard.Tests` e `IdeaBoard.E2E`, que são projetos
separados por serem artefactos diferentes, e não por serem camadas.

As dependências entre pastas são mantidas numa só direção. Um page model chama um
serviço e nunca toca no `DbContext`. Um serviço usa o domínio e os dados. O domínio
não conhece nada.

## Alternativas consideradas

**Quatro projetos, um por camada.** A fronteira de assembly tornaria a direção das
dependências impossível de violar por distração, que é o argumento real a favor. O
preço é quatro ficheiros de projeto, quatro conjuntos de referências, e uma navegação
mais lenta, para proteger uma regra que três serviços tornam fácil de ver a olho.

A fronteira pode ser introduzida mais tarde, se a aplicação crescer. Introduzi la no
início é pagar o custo antes de existir o problema.

**Arquitetura vertical por funcionalidade.** Faria sentido com muitas funcionalidades
independentes. Com quatro, que partilham as mesmas três entidades, produziria pastas
quase vazias e duplicação entre elas.

## Consequências

**Boas.** Menos cerimónia. Uma solução que se abre e se percebe numa sessão. Um único
artefacto para publicar.

**Más.** Nada impede, ao nível do compilador, que um page model injete o `DbContext`
diretamente e salte a camada de serviços. A regra passa a depender de revisão de
código e da convenção registada em `CLAUDE.md`.

É um risco real, e é aceite por ser proporcional à dimensão do projeto. Numa base de
código com muitos colaboradores a entrar e a sair, a conclusão seria outra.
