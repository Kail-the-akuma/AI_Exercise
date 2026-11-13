# ADR-017. xUnit v3 como único framework de teste da solução

**Estado**: Aceite. **Data**: 13 de novembro de 2026.

## Contexto

O [07](../07-Plano-de-Verificacao.md) fixou três níveis de teste e disse que todos usam
xUnit, para que a solução tenha um único framework. Não disse qual linha do xUnit, e na
altura não era uma pergunta.

Passou a ser quando os projetos foram criados, porque o xUnit tem duas linhas
mantidas em paralelo, a v2 e a v3, com pacotes de nome diferente e com pacotes de
integração de terceiros que existem numa variante por linha.

## Decisão

**xUnit v3 em toda a solução.** Verificado nos ficheiros de projeto: `xunit.v3` versão
4.0.3 nos dois projetos de teste, `xunit.runner.visualstudio` 4.0.1 e
`Microsoft.NET.Test.Sdk` 18.12.0.

Em consequência, o pacote de integração do Playwright é o
**`Microsoft.Playwright.Xunit.v3`**, versão 1.64.1, e não o `Microsoft.Playwright.Xunit`,
que é a variante da linha v2.

## Porque é que isto merece um registo

Pela razão de sempre nestes casos, que é o erro ser silencioso de um lado e barulhento
do outro.

Escolher a linha errada não dá erro de compilação interessante, dá uma suite que não
descobre testes, ou um pacote de integração que não encontra o tipo base que espera. E o
sintoma aparece longe da causa.

Este registo existe sobretudo porque o nome do pacote **já foi escrito errado** em dois
sítios, no `Docs/07` e na secção da stack do `CLAUDE.md`, ambos a dizer
`Microsoft.Playwright.Xunit`. Não foi descuido de quem escreveu, foi o nome certo na
altura em que a linha ainda não tinha sido escolhida. Ter o nome exato num sítio só
evita que o próximo o vá buscar ao sítio errado.

## Alternativas consideradas

**xUnit v2.** É a linha com mais material escrito à volta e mais exemplos. Posta de
lado por ser a linha anterior, e por não haver nenhuma restrição neste projeto que
obrigue a ficar nela. Começar numa linha em fim de vida é escolher uma migração para
daqui a pouco tempo.

**Um framework por nível, por exemplo NUnit nos testes de ponta a ponta.** Não teve
defensor nenhum e está registada só para ficar fechada. Dois frameworks significam dois
corredores, duas formas de declarar um teste, e duas maneiras de montar uma fixture,
para uma solução com dois projetos de teste.

## Consequências

**Boas.** Um só framework, um só corredor, e um só modelo de fixture em toda a solução.
Quem souber escrever um teste num projeto sabe escrever no outro.

**Más.** Qualquer pacote de integração de terceiros tem de ser a variante da linha v3, e
nem todos a têm. No dia em que for preciso um pacote que só exista para v2, a escolha é
entre passar sem ele ou reabrir esta decisão, e não há meio termo.

**Correção devida noutros ficheiros.** O `Docs/07` e o `CLAUDE.md` nomeiam o pacote da
linha errada. Nenhum dos dois é alterado a partir daqui: o primeiro tem dono na tabela
do `Docs/08` e o segundo é instrução do repositório e pertence a quem o escreveu.
