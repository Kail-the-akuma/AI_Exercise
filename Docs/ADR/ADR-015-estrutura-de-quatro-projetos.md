# ADR-015. Quatro projetos, com o produto numa biblioteca e dois hosts irmãos

**Estado**: Aceite. **Data**: 13 de novembro de 2026.
**Substitui parcialmente**: [ADR-006](ADR-006-projeto-unico.md).

## Contexto

O [ADR-006](ADR-006-projeto-unico.md) decidiu um só projeto de aplicação,
`IdeaBoard.Web`, com separação por pastas, e aceitou de forma explícita a consequência
que isso tinha: nada impede, ao nível do compilador, que um page model injete o
`DbContext` e salte a camada de serviços, ficando a regra dependente de revisão de
código e da convenção registada no `CLAUDE.md`.

Durante a construção a solução passou a ter quatro projetos em `src`. A decisão foi
tomada a construir, nunca foi registada, e este ficheiro ratifica a em vez de a
reverter. O `Docs/01` continua a descrever a estrutura antiga e tem de ser alinhado
depois disto, pelo DOC.

## Decisão

Quatro projetos em `src`, com fronteiras que não são camadas.

**`IdeaBoard.App`**, com o SDK `Microsoft.NET.Sdk.Razor`, ou seja uma biblioteca de
classes Razor. Contém o produto inteiro: `Pages`, `Domain`, `Data` e `Services`. **Não
referencia projeto nenhum.** É a peça burra, e é deliberado que o seja.

**`IdeaBoard.Web`**, host web, que referencia apenas o `IdeaBoard.App`. É o que vai
publicado para o servidor, e não tem dentro dele nada além do arranque.

**`IdeaBoard.Local`**, host web, que referencia o `IdeaBoard.App` e o
`IdeaBoard.LocalIdentity`. Serve para correr e exercitar a aplicação fora do domínio,
com a identidade escolhida à mão. **Nunca é publicado.**

**`IdeaBoard.LocalIdentity`**, biblioteca simples, que referencia o `IdeaBoard.App`.
Contém o handler de autenticação que o host local usa.

A separação por pastas que o ADR-006 decidiu não desapareceu. Mudou de sítio, e vive
agora dentro do `IdeaBoard.App`, com as mesmas quatro pastas e a mesma direção única de
dependências.

## O que esta estrutura compra, e que o projeto único não conseguia dar

A regra mais dura do `CLAUDE.md` diz que nunca se coloca código de autenticação de
teste dentro do `IdeaBoard.Web`, nem sequer protegido por uma condição de ambiente, e a
razão escrita é que uma condição de ambiente é uma linha que alguém pode inverter.

Com um só projeto, essa regra era uma convenção, e a sua força era a de quem a lesse.
Com esta estrutura passa a ser um facto estrutural: o handler vive num projeto que o
host publicado não alcança, e pô lo lá dentro deixa de ser uma transgressão de estilo
para passar a ser uma alteração de referências que a guarda do MSBuild recusa.

É exatamente a consequência má que o ADR-006 tinha aceitado, resolvida para o caso em
que ela mais custava. Não é resolvida para todos: um page model continua a poder
injetar o `DbContext`, porque páginas e dados continuam no mesmo assembly. O que mudou
foi o caso da autenticação, e mudou porque era o único em que o erro chegava a produção
em vez de ficar feio no código.

## A proteção que se perdeu na divisão, e que teve de ser reposta à mão

Esta é a parte que não é óbvia e que custaria caro a descobrir sozinho.

Antes da divisão, uma referência do host publicado para o host local teria formado um
ciclo de projetos, e o MSBuild recusa se a construir um ciclo. A proteção existia e era
de graça. Com os dois hosts a serem irmãos, o ciclo desapareceu, e uma referência
errada passaria a compilar, a publicar, e a levar o handler de identidade local para
produção.

O `Directory.Build.targets` repõe essa proteção à mão, e a invariante que ele defende
não é a que parece. **Não é "o `IdeaBoard.Web` só referencia o `IdeaBoard.App`". É
"nada alcançável a partir do `IdeaBoard.Web` referencia um host".**

A distinção é o ponto todo. Acrescentar uma referência a um host parece uma
transgressão e faz hesitar quem a faz. Acrescentar uma referência à biblioteca
partilhada parece exatamente aquilo para que uma biblioteca partilhada serve, e é esse
o movimento que alguém faz sem reparar. Por isso a guarda vigia os dois projetos, e o
conjunto permitido do `IdeaBoard.App` está vazio.

As referências que apontam para dentro do `IdeaBoard.App`, vindas de fora do fecho
publicado, como a do `IdeaBoard.LocalIdentity`, são a direção segura e não estão
restringidas.

## Compila, arranca, serve, que são três coisas e não uma

Uma divisão de projetos pode partir a aplicação de três maneiras, e só a primeira dá
erro de compilação.

A biblioteca de classes Razor serve os ficheiros estáticos sob o prefixo
`_content/IdeaBoard.App/`, e isso aparece nas referências do layout, em
`src/IdeaBoard.App/Pages/Shared/_Layout.cshtml`, nas linhas 35 e 57. Uma página pode
compilar na perfeição e a folha de estilos devolver 404, porque o prefixo é
configuração de servidor e não código.

É por isso que `tests/IdeaBoard.Tests/Integracao/PublishedHostTests.cs` existe e faz
duas coisas que nenhum teste de unidade faz: arranca o `IdeaBoard.Web` a sério e
confirma que ele chegou a arrancar, e pede
`/_content/IdeaBoard.App/css/site.css` e exige 200. E exige mais do que 200, porque
antes disso recusa explicitamente um 307 e um 302, de forma a que um redirecionamento
não possa passar por um ficheiro servido.

O custo desta escolha é esse prefixo. Toda a referência a um ficheiro estático carrega
o nome do pacote, e mudar o nome do projeto parte todas elas de uma vez.

## Alternativas consideradas

**Manter um só projeto, como o ADR-006 decidiu.** Menos cerimónia, e era a decisão
certa enquanto não houve dois hosts. Deixou de servir no momento em que passou a ser
preciso correr a aplicação com uma identidade falsa sem que esse código pudesse chegar
ao servidor. Com um só projeto, as duas coisas vivem no mesmo artefacto e a separação
volta a ser uma promessa.

**Pôr o handler local dentro do projeto de testes.** Era o que o
[ADR-011](ADR-011-autenticacao-nos-testes-e2e.md) decidiu e continua a ser a resposta
certa para os testes. Não serve aqui, porque o host local é uma aplicação que uma
pessoa arranca e usa com o browser, e um projeto de testes não é executável dessa
forma. Seriam duas cópias do mesmo handler.

**Dividir por camadas, com `Domain` e `Infrastructure` separados.** É a divisão que o
ADR-006 recusou pelos motivos que lá estão, e nada nos motivos mudou. Esta divisão não
é por camadas, é por artefacto publicável, e é por isso que não contradiz aquele
raciocínio.

## Consequências

**Boas.** O atalho de autenticação deixa de depender de disciplina para não chegar a
produção. O que se publica é um host sem lógica dentro, e a aplicação inteira é uma
biblioteca que dois hosts consomem da mesma maneira.

**Más.** Quatro ficheiros de projeto em vez de um, e o prefixo `_content` em todas as
referências a ficheiros estáticos. E uma proteção que o MSBuild dava de graça passou a
ser um alvo escrito à mão, que é código que alguém tem de manter e que falha em
silêncio se for apagado.

**Os projetos de teste ficam de fora deste registo, e ficam por um motivo.** O ADR-006
fala de dois, `IdeaBoard.Tests` e `IdeaBoard.E2E`, e descreve os como separados por
serem artefactos diferentes e não por serem camadas. Esse raciocínio não é tocado aqui.

Em `main` existe apenas o primeiro. O segundo existe com esse nome exato no ramo
`fase1/e2e`, por confirmar quando esse trabalho chegar a `main`. A contagem de quatro
projetos deste registo é de `src`, e não da solução inteira.
