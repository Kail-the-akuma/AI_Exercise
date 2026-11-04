# Convenções do repositório

Plataforma interna de ideias. Leia `BRIEFING.md` antes de qualquer trabalho, e
`Docs/01-Arquitetura.md` antes de escrever código.

## Âmbito, que é a regra mais importante deste repositório

O briefing fixa quatro funcionalidades e exclui tudo o resto de forma deliberada.
As exclusões não são omissões nem oportunidades de melhoria.

**Não acrescentar**, mesmo que pareça óbvio, barato ou útil: comentários, categorias,
etiquetas, pesquisa, filtros, administração, edição ou remoção de ideias, deteção de
duplicados, estados, triagem, aprovação, notificações, perfis, ou qualquer mecanismo
de inscrição e acompanhamento de ideias.

O briefing lista riscos conhecidos destas decisões e diz de forma explícita que nenhum
deles é motivo para mudar o âmbito. Uma proposta de funcionalidade que resolva um
desses riscos é uma mudança de âmbito e precisa de decisão humana, não de iniciativa.

## Stack

- .NET 10 (LTS), ASP.NET Core Razor Pages, renderização no servidor.
- EF Core sobre SQLite.
- `Microsoft.Identity.Web` para autenticação por Entra ID.
- xUnit em toda a solução, com `Microsoft.Playwright.Xunit` nos testes ponta a ponta.

Dependências externas são para minimizar. Antes de acrescentar um pacote, verificar o
que a plataforma já oferece. O registo de eventos usa o `ILogger` com o provider de
Event Log do Windows, precisamente por isso.

## Idioma

- **Prosa em português europeu**, incluindo documentação, mensagens de commit, textos
  de interface e descrições de issues.
- **Código em inglês**, incluindo nomes de classes, métodos, variáveis, ficheiros,
  comentários e mensagens de log.

Não usar travessões nem hífenes como substituto de pontuação na prosa. Usar vírgulas,
conjunções e frases separadas.

## Estilo C#

- PascalCase para classes, structs, enums, métodos e propriedades. camelCase para
  todas as variáveis e campos, sem exceção.
- Chavetas no estilo Allman, sempre em linha própria.
- Sem `var`. Todas as variáveis têm tipo explícito.
- Tipos nativos em minúsculas: `string`, `bool`, `int`.
- `if`/`else` em vez de operador ternário.
- Sem membros com corpo de expressão, e sem cadeias LINQ onde um `foreach` explícito
  serve.
- `switch` em vez de uma cadeia de `if`/`else if` acima de três ramos, com cada `case`
  dentro de chavetas.
- Guard clauses por omissão. Máximo de três níveis de aninhamento por método.
- `#region` organizadas por conceito. Duas linhas em branco entre métodos, uma entre
  propriedades.
- `/// <summary>` em todas as classes e métodos centrais. Comentários explicam o
  porquê, não o quê.

## Testes

Três níveis com responsabilidades separadas, descritos em `Docs/07`.

**Nunca colocar código de autenticação de teste dentro de `IdeaBoard.Web`.** O handler
de teste vive apenas no projeto de testes ponta a ponta, nem sequer protegido por uma
condição de ambiente. É a salvaguarda que garante que um atalho de autenticação não
chega a produção.

Uma suite verde não é prova de que a aplicação funciona. A entrada em sessão real
contra o Entra ID não é coberta por testes automáticos e tem de ser verificada à mão,
conforme a lista de `Docs/07`.

## Git

Remoto em `https://github.com/Kail-the-akuma/AI_Exercise.git`, com `main` como ramo
principal. Mensagens de commit em português europeu.
