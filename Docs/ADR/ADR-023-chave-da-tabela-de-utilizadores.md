# ADR-023. A chave da tabela de utilizadores passa a ser o identificador de segurança, e o email sai

**Estado**: Aceite. **Data**: 13 de novembro de 2026.
**Substitui parcialmente**: [ADR-004](ADR-004-tabela-local-de-utilizadores.md).

## Contexto

O [ADR-004](ADR-004-tabela-local-de-utilizadores.md) decidiu uma tabela local de
utilizadores com o identificador de objeto do Entra ID como chave de correspondência, e
nunca o email. O raciocínio que o sustenta está certo e não é tocado aqui: o email de uma
pessoa muda, e uma chave que muda deixa de encontrar a linha de quem já existe.

O que mudou foi o fornecedor, pelo
[ADR-014](ADR-014-autenticacao-integrada-windows.md). Não há identificador de objeto
porque não há Entra ID.

Por isso este registo mexe em duas coisas concretas do ADR-004, e deixa a terceira,
que é o argumento, exatamente onde está.

## Decisão

**A chave de correspondência é o identificador de segurança da conta**, lido da claim
correspondente, e a coluna chama se `subjectId`.

O nome da coluna é o ponto, e não um detalhe. Não se chama `windowsSid` nem
`entraObjectId`. Chama se pelo que é, que é o identificador por que a pessoa é
encontrada, e não pelo sítio de onde veio. **O fornecedor por trás dela já mudou uma
vez**, e uma coluna com o nome do fornecedor do dia garante um segundo renomear na
mudança seguinte.

**A coluna `email` sai da tabela**, e no lugar dela entra `accountName`, que guarda a
conta na forma `DOMINIO\pessoa`, serve para distinguir à mão duas pessoas com o mesmo
nome visível, e **nunca é mostrada na interface**.

## As três afirmações do ADR-004 que deixam de valer, e a que ganha força

**A chave deixa de ser o identificador de objeto do Entra ID.** Passa a ser o
identificador de segurança, com a mesma propriedade que interessava, que é ser estável
quando o nome muda.

**O upsert deixa de atualizar o nome e o email a cada início de sessão.** O email já não
existe, e o nome passa a ser governado pela regra de proveniência do
[ADR-022](ADR-022-resolucao-e-substituicao-do-nome.md), que não é uma atualização cega.

**A aplicação deixa de continuar a funcionar com o fornecedor indisponível da forma que
o ADR-004 descreve.** Com o Entra ID, quem já tivesse sessão continuava a trabalhar
porque a sessão vivia num cookie. Com Negotiate cada pedido volta a autenticar se, e a
dependência passa a ser o controlador de domínio responder a cada pedido e não apenas no
primeiro.

**E o argumento central sai reforçado.** O ADR-004 dizia que usar o email como chave
estava errado. Agora o email nem sequer está na tabela, portanto o erro que ele receava
deixou de ser possível por construção, e não apenas desaconselhado.

## Sobre a posição que eu defendia

Eu queria retirar a coluna `email` e mais nada. A decisão foi retirá la **e** acrescentar
`accountName`, e a diferença importa.

Sem `accountName`, duas pessoas com o mesmo nome de apresentação ficam
indistinguíveis para quem abrir a tabela a investigar uma correção manual, que é
exatamente o procedimento que o [ADR-008](ADR-008-sem-administracao.md) deixou como
única forma de corrigir seja o que for. Eu estava a remover uma coluna sem olhar para o
que ela sustentava, e o que ela sustentava não era o email, era a capacidade de
identificar uma linha à mão.

## Alternativas consideradas

**Manter o nome `entraObjectId` e guardar lá o identificador de segurança.** Zero
trabalho de migração, porque não há base de dados publicada, e seria mentira escrita no
esquema. Um nome de coluna que descreve outra coisa é pior do que um nome genérico,
porque engana com autoridade.

**Chamar lhe `windowsSid`.** Honesto hoje e com o mesmo defeito do anterior amanhã. A
escolha de `subjectId` é precisamente a recusa de nomear a coluna pelo fornecedor, agora
que já se viu o fornecedor mudar.

**Guardar o email também, vindo do diretório.** Posta de lado por ser dado pessoal sem
utilizador: nada no produto o usa, o briefing não o pede, e a lista nominal de votantes
mostra nomes e não endereços. Guardar o seria recolher por hábito.

## Consequências

**Boas.** O esquema deixa de nomear um fornecedor, e a próxima troca não obriga a
renomear colunas. O produto deixa de guardar um dado pessoal que não usa. Uma correção
manual continua a poder identificar a pessoa certa.

**Más.** `subjectId` é menos imediato de ler do que um nome que diga de onde vem o
valor, e quem abrir a tabela pela primeira vez tem de ir ao código ou aqui para saber o
que lá está. É o preço de não voltar a prometer no nome da coluna uma coisa que o tempo
desfaz, e está pago com o comentário na própria entidade.
