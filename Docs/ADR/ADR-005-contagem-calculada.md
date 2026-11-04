# ADR-005. Contagem de votos calculada, e não denormalizada

**Estado**: Aceite. **Data**: 30 de outubro de 2026.

## Contexto

O ranking ordena por número de votos, e a contagem aparece em todas as listas. Há a
tentação habitual de guardar o número numa coluna `voteCount` na tabela `Idea`, para
evitar a agregação.

## Decisão

A contagem de votos é obtida por agregação na consulta. Não existe coluna de contagem.

## Alternativas consideradas

**Coluna `voteCount` atualizada a cada voto.** Evitaria a agregação, ao preço de ter o
mesmo facto guardado em dois sítios. A coluna e as linhas de `Vote` teriam de ser
atualizadas na mesma transação, e qualquer intervenção manual na base de dados que
mexesse numa sem mexer na outra produziria um número errado visível a toda a gente.

**Vista materializada ou cache em memória.** O mesmo problema de duplicação, com
invalidação a resolver por cima.

## Consequências

**Boas.** Existe um único facto e uma única fonte dele. A contagem não pode divergir
dos votos que a compõem, porque é derivada deles no momento em que é lida. O modelo
tem menos uma coluna para manter coerente, e as correções manuais descritas em
[06](../06-Operacao-e-Publicacao.md) não podem desalinhar contagens.

**Más.** Cada leitura do ranking faz uma agregação. Com algumas centenas ou alguns
milhares de linhas de `Vote`, o custo é irrelevante, e os índices existentes tornam no
ainda menor.

**Onde isto deixaria de ser verdade.** Com dezenas de milhares de ideias e centenas de
milhares de votos, a agregação em cada leitura passaria a notar se, e valeria a pena
reconsiderar. Esse volume está três ordens de grandeza acima do esperado, e se alguma
vez chegar lá, a plataforma terá problemas mais interessantes do que este.

A decisão é portanto reversível e barata de reverter, o que é outra razão para não a
antecipar.
