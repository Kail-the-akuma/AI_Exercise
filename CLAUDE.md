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

## Método

Antes de afirmar o que um ficheiro contém, ler o ficheiro. Vale para citar, resumir ou
passar a afirmação a outra pessoa, e vale sobretudo quando a afirmação vem de alguém que
já o leu, porque é aí que ela viaja sem ser verificada e chega ao fim com a autoridade de
quem a repetiu e não de quem a confirmou.

Antes de afirmar como se comporta uma ferramenta, confirmar com ela. Um comportamento
lembrado é uma hipótese, e as ferramentas mudam entre versões sem avisar quem as usa. O
custo de confirmar mede se em segundos, o de não confirmar mede se no trabalho feito em
cima da suposição errada.

## Stack

- .NET 10 (LTS), ASP.NET Core Razor Pages, renderização no servidor.
- EF Core sobre SQLite.
- Autenticação integrada do Windows, servida pelo IIS, sobre HTTP.
- xUnit em toda a solução, com `Microsoft.Playwright.Xunit.v3` nos testes ponta a ponta.

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

**Nada alcançável a partir do `IdeaBoard.Web` referencia um host.** É a invariante que
mantém a identidade local fora do artefacto publicado, e a guarda do
`Directory.Build.targets` recusa a construção de quem a quebre. Não desactivar essa
guarda nem lhe acrescentar excepções.

Reparar que a invariante é sobre o fecho de referências e não sobre uma pasta. Uma
proibição sobre um caminho deixa de proteger no dia em que o produto muda de sítio, e
fica a dizer que protege.

Uma suite verde não é prova de que a aplicação funciona. O desafio de autenticação do
Windows, a consulta ao diretório e a criação do utilizador a partir de uma identidade
verdadeira não são cobertos por testes automáticos, e só se verificam numa máquina em
domínio, à mão, conforme a lista de `Docs/07`.

## Git

Remoto em `https://github.com/Kail-the-akuma/AI_Exercise.git`, com `main` como ramo
principal. Mensagens de commit em português europeu.
