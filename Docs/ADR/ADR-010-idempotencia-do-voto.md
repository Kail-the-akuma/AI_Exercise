# ADR-010. Um voto por pessoa garantido por índice único, com tratamento idempotente

**Estado**: Aceite. **Data**: 30 de outubro de 2026.

## Contexto

A regra central do produto é um voto por pessoa por ideia, reversível a qualquer
momento. Se essa regra falhar, a lista ordenada por apoio real deixa de o ser, e a
plataforma perde a única coisa que produz.

Os modos de falha realistas não são ataques. São um duplo clique, uma ligação lenta
que leva a pessoa a carregar outra vez, e dois separadores abertos na mesma página.

## Decisão

**Índice único em `Vote(ideaId, userId)`**, ao nível da base de dados.

A aplicação verifica na mesma o estado antes de escrever, por ser o caminho normal,
mas a correção não depende dessa verificação. A violação do índice único é apanhada e
**tratada como sucesso**, não propagada como erro.

Simetricamente, retirar um voto que não existe não falha e não altera nada.

## Alternativas consideradas

**Verificação apenas no código, com leitura seguida de escrita.** É a implementação
mais comum e tem uma janela entre ler e escrever. Dois pedidos quase simultâneos da
mesma pessoa leem ambos o estado de não votado, e ambos inserem. Com um duplo clique,
isto não é teórico.

**Bloqueio ou transação serializável.** Fecharia a janela ao preço de contenção
desnecessária, para resolver um problema que um índice já resolve sem custo nenhum.

**Mostrar um erro quando o voto já existe.** Seria tecnicamente honesto e mau produto.
A pessoa carregou duas vezes no botão de votar e o estado final é o que ela queria.
Mostrar lhe um erro é informar a de um detalhe de implementação que não lhe diz
respeito.

## Consequências

**Boas.** A regra é imposta pela base de dados, no único sítio onde não pode ser
contornada por uma condição de corrida, por uma alteração futura ao código, ou por
uma correção manual distraída.

O comportamento observável é sempre o estado pretendido, independentemente de quantas
vezes o pedido chegue. Isto também torna o botão seguro de usar com JavaScript, onde
um pedido repetido é ainda mais fácil de provocar.

**Más.** A deteção da violação depende de reconhecer o erro específico devolvido pelo
SQLite, o que é código a mais do que um simples `try`. Por isso o índice tem nome
explícito na configuração do EF Core, e por isso este comportamento tem teste de
integração próprio, descrito em [07](../07-Plano-de-Verificacao.md).

Se a base de dados mudar de motor, esse tratamento tem de ser revisto, porque o erro
devolvido é diferente.

**Nota de modelo.** Retirar o voto apaga a linha, e não há histórico de votos
retirados. Nada no produto precisa desse histórico, e guardá lo seria recolher dados
sobre o comportamento de colegas sem finalidade declarada, numa plataforma cuja
transparência já está definida com precisão no briefing.
