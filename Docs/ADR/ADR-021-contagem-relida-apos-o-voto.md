# ADR-021. A contagem mostrada depois de votar é relida, e não calculada

**Estado**: Aceite. **Data**: 13 de novembro de 2026.

## Contexto

O [ADR-005](ADR-005-contagem-calculada.md) decidiu que a contagem de votos é calculada
por agregação na consulta e não guardada numa coluna. Isso resolve de onde vem o número
nas listagens.

Fica por resolver uma coisa mais pequena e mais fácil de errar: **que número é que se
mostra imediatamente depois de alguém votar.**

A saída barata é aritmética. Sabe se o número que estava no ecrã, sabe se que a pessoa
acabou de votar, soma se um. É o que praticamente toda a gente faz, e funciona na maior
parte das vezes.

## Decisão

Depois de registar ou retirar um voto, o serviço **volta a contar na base de dados** e é
esse número que vai para o ecrã. Verificado em
`src/IdeaBoard.App/Services/VoteService.cs`, onde a contagem é obtida por uma contagem
sobre a tabela de votos, e não por soma sobre o valor anterior.

## Porque é que a aritmética estaria errada aqui em concreto

Pelo [ADR-010](ADR-010-idempotencia-do-voto.md). Um segundo voto da mesma pessoa na
mesma ideia é **absorvido como sucesso idempotente**, e não propagado como erro.

Isso significa que existe um caminho, e um caminho normal e não excecional, em que a
operação devolve sucesso e **o número não mudou**. Um duplo clique é o caso óbvio. A
aritmética, nesse caminho, mostra mais um voto do que existe, e mostra o numa página
cujo propósito inteiro é a contagem ser verdadeira.

O erro depois desaparece sozinho na recarga seguinte, o que é a pior propriedade
possível: é irreprodutível para quem o reportar, e perfeitamente reprodutível para quem
clicar duas vezes.

Há um segundo motivo, menor mas na mesma direção. Com trinta pessoas a votar nas mesmas
ideias, o número que estava no ecrã já pode estar desatualizado no momento em que alguém
clica, e somar um a um número velho produz um número que nunca foi verdade.

## Alternativas consideradas

**Somar ou subtrair um ao valor conhecido.** Um acesso a menos à base de dados por voto.
Posta de lado pela razão acima, e vale a pena dizer que o custo que ela poupa é uma
contagem sobre uma tabela com um índice e algumas centenas de linhas.

**Devolver a contagem a partir do próprio resultado da escrita.** Daria o número sem um
segundo acesso em alguns casos. Posta de lado por depender de o caminho idempotente e o
caminho normal devolverem a mesma coisa, que é precisamente o que eles não fazem.

## Consequências

**Boas.** O número mostrado depois de votar é o número que está na base de dados, em
todos os caminhos, incluindo aquele em que a operação teve sucesso sem alterar nada.

**Más.** Um acesso adicional por voto. É o preço, é pequeno nesta escala, e deixaria de
o ser numa aplicação com ordens de grandeza mais tráfego, altura em que esta decisão
merecia ser reaberta em vez de herdada.
