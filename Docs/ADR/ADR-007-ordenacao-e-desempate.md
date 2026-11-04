# ADR-007. Ordenação por votos, com desempate pela data mais recente

**Estado**: Aceite. **Data**: 30 de outubro de 2026.

## Contexto

O separador principal mostra as ideias mais votadas. Com poucas dezenas de ideias e
poucas dezenas de pessoas, os empates não são uma raridade matemática, são o caso
comum: muitas ideias ficarão com dois ou três votos.

O briefing identifica um risco próprio desta lista:

> Sem ciclos nem decaimento, as primeiras ideias acumulam votos e ficam no topo
> permanentemente, o que desincentiva quem chega depois.

E afirma, de forma explícita, que nenhum dos riscos listados é motivo para mudar o
âmbito.

## Decisão

Ordenação por três critérios, nesta ordem:

1. Número de votos, descendente.
2. Data de submissão, **descendente**.
3. Identificador, descendente.

Entre duas ideias com o mesmo apoio, a mais recente aparece primeiro.

O terceiro critério existe apenas para tornar a ordem determinística, e nunca é
visível na prática.

## Alternativas consideradas

**Desempate pela data mais antiga.** É a escolha por defeito na maioria das
implementações, e premeia a antiguidade. Numa lista contínua, isso agrava exatamente o
risco que o briefing descreve: a ideia mais antiga fica à frente em todos os empates,
para sempre.

**Decaimento temporal dos votos, ou um algoritmo de popularidade com idade.**
Resolveria o problema de raiz e está **fora do âmbito**. O briefing exclui ciclos e
decaimento de forma explícita, e esta decisão não os introduz por via indireta.

**Ordem aleatória entre empatados.** Daria rotatividade, ao preço de a lista mudar de
ordem a cada recarregamento, o que torna a navegação desorientadora e impossível de
testar.

## Consequências

**Boas.** O custo é zero, é uma cláusula de ordenação. Dá uma vantagem pequena e
permanente a quem chega depois, sem introduzir nenhum mecanismo novo e sem contrariar
nenhuma exclusão do briefing.

**Más.** O efeito é modesto, e não resolve o risco de cristalização. Uma ideia com
quinze votos continua acima de uma ideia nova com três, por muito recente que seja, e
é assim que tem de ser, porque a lista ordena por apoio real.

Esta decisão mitiga, não resolve. O sinal fica para observação depois do lançamento,
conforme [05](../05-Plano-de-Entrega.md), e qualquer resposta mais forte é uma
mudança de âmbito que precisa de decisão humana.

**Verificação.** O desempate é verificado de forma explícita nos testes de unidade e
num cenário ponta a ponta próprio, porque é uma regra invisível que se perde com
facilidade numa alteração futura às consultas.
