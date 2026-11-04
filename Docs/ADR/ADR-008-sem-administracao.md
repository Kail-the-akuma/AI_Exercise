# ADR-008. Sem administração, com correção por procedimento manual

**Estado**: Aceite. **Data**: 30 de outubro de 2026.

## Contexto

O briefing exclui administração, edição e remoção de ideias, e identifica a
consequência com clareza:

> Sem edição nem administração, os erros são permanentes. Um título mal escrito ou uma
> ideia submetida por engano ficam para sempre. É a decisão com maior probabilidade de
> se tornar incómoda primeiro.

O briefing afirma também que nenhum dos riscos listados é motivo para mudar o âmbito.

A arquitetura não pode, portanto, acrescentar um ecrã de gestão. Mas também não pode
fingir que o problema não vai acontecer, porque vai, e provavelmente na primeira
semana.

## Decisão

Nenhuma funcionalidade de administração, de edição ou de remoção na aplicação. Não há
utilizadores privilegiados e não há ecrã escondido.

A correção de dados é um **procedimento manual documentado**, executado contra a base
de dados, com backup prévio e registo do que foi feito. O procedimento está em
[06](../06-Operacao-e-Publicacao.md).

A interface avisa, antes da submissão, que a ideia não poderá ser editada nem
removida.

## Alternativas consideradas

**Ecrã de administração, mesmo mínimo.** É uma mudança de âmbito. Traz consigo o
conceito de utilizador privilegiado, que o briefing recusa de forma explícita ao
estabelecer que não há distinção de papéis.

**Janela de edição curta, só para o autor, logo após a submissão.** É a alternativa
mais defensável de todas, e resolveria a maior parte dos casos reais, que são erros de
escrita notados nos primeiros minutos. Continua a ser uma mudança de âmbito, porque
introduz edição onde o briefing diz que não há.

Fica registada aqui como a primeira coisa a propor se o incómodo se confirmar depois
do lançamento. Não é trabalho planeado, é material para uma conversa.

**Apagamento lógico, com as ideias escondidas em vez de removidas.** Seria
administração com outro nome.

## Consequências

**Boas.** O âmbito do briefing é respeitado à letra. Não há conceito de privilégio na
aplicação, o que elimina de uma vez toda uma classe de problemas de autorização. O
modelo de dados fica mais simples, sem estados nem colunas de visibilidade.

**Más.** Cada correção exige acesso ao servidor, uma pessoa com conhecimento do
procedimento, e tempo. Se os pedidos forem frequentes, o custo operacional torna se
visível depressa, e isso é precisamente o sinal que o briefing manda observar.

**Risco do próprio procedimento manual.** Alterar dados à mão numa base de dados em
produção é a operação com maior potencial de estrago de toda esta aplicação. O
procedimento em [06](../06-Operacao-e-Publicacao.md) existe para o conter, e as suas
regras não são formalidades: backup antes, uma instrução de cada vez com filtro por
identificador, e **votos nunca alterados à mão**, porque mexer em votos falsifica o
único resultado que a plataforma produz.
