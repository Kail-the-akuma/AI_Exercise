# 02. Especificação funcional

**Versão 2, 13 de novembro de 2026.**

Descreve o comportamento das quatro funcionalidades do produto mínimo definido em
[BRIEFING.md](../BRIEFING.md). O que aqui não estiver, não existe.

## Identidade e acesso

Toda a aplicação exige utilizador autenticado, e quem autentica é o IIS, por
autenticação integrada do Windows. Não há área pública, não há papéis e não há
permissões diferenciadas. Quem entra, propõe e vota nas mesmas condições de toda a
gente.

Não há botão de terminar sessão, porque não há sessão aplicacional para terminar. Ver
[ADR-014](ADR/ADR-014-autenticacao-integrada-windows.md).

O nome apresentado em toda a aplicação é o que está guardado na linha dessa pessoa. Ver
[ADR-004](ADR/ADR-004-tabela-local-de-utilizadores.md) para a consequência disto quando
alguém muda de nome.

### Quando o nome não é conhecido

A identidade que o IIS fornece não traz nome apresentável. A aplicação obtém no por
consulta ao diretório corporativo, no primeiro acesso de cada pessoa.

**Quando a consulta não devolve nada, a pessoa escreve o seu nome.** O pedido é
redirecionado para `/nome`, e a linha de utilizador só é criada quando há nome para
escrever. Nenhuma página tem de defender a ausência de um nome, porque a ausência nunca
chega a ser gravada. Ver
[ADR-022](ADR/ADR-022-resolucao-e-substituicao-do-nome.md).

| Campo | Obrigatório | Limite | Notas |
|---|---|---|---|
| Nome | Sim | 80 caracteres | Linha única, espaços aparados antes de validar e de guardar |

| Situação | Comportamento |
|---|---|
| Nome vazio | "Indique o seu nome." |
| Nome acima de 80 caracteres | "O nome não pode ultrapassar 80 caracteres." |
| Nome com barra invertida | "O nome não pode conter uma barra invertida. Escreva o seu nome, não o nome de utilizador." |

**A regra da barra invertida não é cosmética, e convém que a razão fique escrita**, senão
alguém a remove por parecer arbitrária. Uma conta de domínio tem sempre a forma
`DOMINIO\pessoa` e um nome de pessoa não tem barra invertida nenhuma. Sem esta regra,
alguém poderia registar se com o aspeto de um identificador de conta e aparecer assim na
lista nominal de votantes, que é pública para toda a equipa. Aparentar autoridade de
sistema é um problema diferente de usar o nome de um colega.

O ecrã avisa, antes de o nome ser escrito, que ele fica visível para os colegas como
autor das ideias e na lista de quem votou.

## Mapa de ecrãs

| Rota | Ecrã | Conteúdo |
|---|---|---|
| `/` | Ranking | Ideias ordenadas por votos, separador principal |
| `/recentes` | Recentes | As mesmas ideias, ordenadas por data de submissão |
| `/nova` | Nova ideia | Formulário de submissão |
| `/ideias/{id}` | Detalhe | Uma ideia, com a descrição completa e a lista de votantes |
| `/nome` | Registo do nome | Só para quem chega sem nome conhecido |

Quatro ecrãs do produto, mais o registo do nome e as páginas de erro. A navegação
principal tem dois separadores, Mais votadas e Recentes, e um botão para submeter.

O `/nome` não está na navegação. Chega se lá por redirecionamento e uma só vez, e quem
tem nome conhecido nunca o vê.

---

## F1. Submeter uma ideia

Qualquer pessoa autenticada submete uma ideia a partir de `/nova`.

### Campos

| Campo | Obrigatório | Limite | Notas |
|---|---|---|---|
| Título | Sim | 120 caracteres | Linha única |
| Descrição | Sim | 2000 caracteres | Várias linhas, texto simples |

O autor e a data de submissão são atribuídos pelo sistema, a partir da sessão e do
relógio do servidor. Não são campos do formulário.

### Regras

- Os espaços no início e no fim são removidos antes de validar e de guardar. Um
  título feito só de espaços é um título vazio.
- Qualquer tema é aceite. Não há categorias, etiquetas nem seleção de tipo.
- **Não há verificação de duplicados.** Duas ideias iguais são duas ideias, e é assim
  por decisão do briefing.
- A descrição é guardada e apresentada como texto simples. As quebras de linha são
  preservadas na apresentação. Markdown e HTML não são interpretados.

### Validação

A validação do servidor é a que conta. A validação no browser é conveniência, e a
aplicação tem de rejeitar na mesma uma submissão que a contorne.

| Situação | Comportamento |
|---|---|
| Título vazio | A página volta com a mensagem "Indique um título." e o conteúdo preenchido mantém se |
| Título acima de 120 caracteres | Mensagem "O título não pode ultrapassar 120 caracteres." |
| Descrição vazia | Mensagem "Descreva a ideia." |
| Descrição acima de 2000 caracteres | Mensagem "A descrição não pode ultrapassar 2000 caracteres." |

### Depois de submeter

Redirect para o detalhe da ideia criada, não de volta para a lista. Quem acabou de
escrever quer ver o que escreveu, e a ideia nova não aparece no topo do ranking por
não ter votos ainda.

**A submissão é definitiva.** Não há edição nem remoção, e o ecrã diz isso antes de
submeter, com uma nota discreta junto ao botão: "Depois de submeter, a ideia não pode
ser editada nem removida."

Avisar antes é a única mitigação disponível dentro do âmbito para o risco de erros
permanentes identificado no briefing. A correção de um erro é um procedimento manual
descrito em [06](06-Operacao-e-Publicacao.md).

### Idempotência da submissão, DECISÃO EM ABERTO

O voto é idempotente por decisão fechada, descrita em F2 e em
[ADR-010](ADR/ADR-010-idempotencia-do-voto.md). **A submissão não é**, e isso ainda não
foi decidido.

Hoje, dois POST da mesma submissão criam duas ideias. O padrão POST/Redirect/GET evita
o caso do recarregamento da página, e não evita um duplo clique nem um pedido repetido
por uma ligação lenta. Como não há deteção de duplicados, por exclusão do briefing, o
resultado fica visível na lista.

Tornar a submissão idempotente obriga a escolher uma chave, e essa escolha tem
consequências que não são óbvias:

| Chave possível | O que trava | O que trava a mais |
|---|---|---|
| Autor mais título | Repetição acidental do mesmo texto | Duas pessoas com a mesma ideia genuína, e a mesma pessoa a propor duas variantes com o mesmo título |
| Token por formulário | Só a repetição daquele envio | Nada, mas obriga a guardar estado do formulário |
| Janela de tempo por autor | Repetição acidental em segundos | Submissões rápidas legítimas |

**Fica em aberto, e a decisão é de quem é dono do produto.** Não é uma decisão para um
agente tomar por iniciativa, porque qualquer uma destas chaves aproxima se da deteção
de duplicados, que o briefing exclui de forma explícita. A decisão é tomada depois de o
produto mínimo estar à vista e de se perceber se o caso acontece mesmo.

---

## F2. Votar

### Regras

- **Um voto por pessoa por ideia.** Garantido por índice único na base de dados, não
  apenas por verificação no código. Ver [ADR-010](ADR/ADR-010-idempotencia-do-voto.md).
- **Reversível a qualquer momento.** O mesmo botão vota e retira o voto.
- **Sem voto negativo** e **sem orçamento de votos.** Não há limite ao número de
  ideias em que cada pessoa pode votar.
- **Votar na própria ideia é permitido.** O briefing não o exclui, e proibi lo seria
  acrescentar uma regra que ninguém pediu.

### Comportamento

O botão tem dois estados, e o estado atual é sempre visível sem passar o rato por
cima.

| Estado | Texto do botão | Ação |
|---|---|---|
| Ainda não votou | "Votar" | Regista o voto |
| Já votou | "Votado" | Retira o voto |

A contagem ao lado atualiza em conjunto com o botão.

### Idempotência

Um duplo clique, uma submissão repetida ou um pedido duplicado não podem produzir dois
votos nem um erro visível ao utilizador.

- Votar quando já existe voto deixa a base de dados como está, e responde como
  sucesso.
- Retirar o voto quando não existe voto deixa a base de dados como está, e responde
  como sucesso.

A violação do índice único é apanhada e tratada como sucesso, não propagada como erro.
O resultado observável é sempre o estado pretendido, independentemente de quantas
vezes o pedido chegue.

### Sem JavaScript

O botão é um formulário com POST e token antiforgery, e a página recarrega na mesma
posição. Com JavaScript, o mesmo POST é enviado em segundo plano e só o botão e a
contagem mudam. A funcionalidade é idêntica nos dois casos.

---

## F3. Ver o ranking

Dois separadores sobre a mesma lista de ideias. Lista sempre aberta, sem ciclos, sem
datas de fecho e sem arquivo.

### Separador "Mais votadas", em `/`

Ordenação, por esta ordem:

1. Número de votos, descendente.
2. Data de submissão, descendente.
3. Identificador, descendente.

O desempate pela data mais recente é deliberado. Entre duas ideias com o mesmo apoio,
a mais recente aparece primeiro, o que dá uma vantagem mínima a quem chega depois.
É a resposta possível, dentro do âmbito, ao risco de cristalização da lista descrito
no briefing. Ver [ADR-007](ADR/ADR-007-ordenacao-e-desempate.md).

O terceiro critério existe só para tornar a ordem determinística, e nunca é visível
na prática.

### Separador "Recentes", em `/recentes`

Ordenação por data de submissão descendente, com o identificador descendente como
desempate.

### Conteúdo de cada linha

Título, nome do autor, data de submissão, contagem de votos e botão de voto. O título
liga ao detalhe.

A descrição não aparece na lista. Com dezenas de ideias, a lista tem de ser percorrida
de relance.

### Paginação

Não há. Todas as ideias numa página.

Com o volume esperado, entre vinte e cem ideias, a página continua pequena e a
pesquisa do browser continua a funcionar sobre a lista toda, o que compensa em parte a
ausência de pesquisa própria. Se a lista crescer muito além disso, é um sinal a
observar depois do lançamento, conforme o briefing, e não um problema a resolver por
antecipação.

---

## F4. Ver quem está por trás

A transparência é uma escolha deliberada do briefing, não uma omissão de privacidade.

- **O autor de cada ideia é público** para toda a equipa, e aparece na lista e no
  detalhe.
- **A lista nominal de quem votou é pública** e aparece no detalhe de cada ideia,
  ordenada pela data do voto, do mais antigo para o mais recente.
- Não há voto anónimo, e não há forma de o pedir.

O ecrã de submissão e o botão de voto deixam isto explícito, com a nota "O seu voto é
público" junto ao botão. Ninguém deve descobrir esta regra depois de a usar.

O briefing identifica o risco de a lista pública de votantes inibir a participação, e
propõe perguntar à equipa passado um mês. Esse é um ponto de observação, não uma
funcionalidade por construir.

---

## Estados vazios e de erro

### Lista vazia

É o primeiro ecrã que a equipa vai ver no dia do lançamento, e por isso não é um
detalhe. Em vez de uma lista em branco, mostra um convite direto a submeter a primeira
ideia, com o botão em destaque.

O briefing identifica o arranque vazio como risco, e a resposta combinada é esta mais
a submissão de ideias pela equipa antes do lançamento, descrita em
[05](05-Plano-de-Entrega.md).

### Ideia inexistente

Um pedido a `/ideias/{id}` com identificador que não existe devolve 404 com uma página
própria, que liga de volta ao ranking.

### Erro inesperado

Página de erro genérica, sem detalhe técnico, com um identificador de correlação que a
pessoa possa citar. O detalhe fica no Event Log do servidor.

### Sessão expirada

O pedido seguinte é redirecionado para o Entra ID e, após autenticação, volta à página
onde a pessoa estava. Numa submissão em curso, o conteúdo escrito perde se, o que é
uma limitação aceite e não compensada por guardar rascunhos.

---

## Textos de interface

Português europeu em toda a aplicação. Sem emojis.

| Elemento | Texto |
|---|---|
| Separador principal | Mais votadas |
| Segundo separador | Recentes |
| Botão de submissão | Submeter ideia |
| Botão de voto, por votar | Votar |
| Botão de voto, votado | Votado |
| Contagem, singular | 1 voto |
| Contagem, plural | {n} votos |
| Contagem, zero | Sem votos |
| Estado vazio | Ainda não há ideias. Seja a primeira pessoa a propor uma. |
| Aviso de permanência | Depois de submeter, a ideia não pode ser editada nem removida. |
| Aviso de transparência | O seu voto é público. |
| Registo do nome, título | Escreva o seu nome |
| Registo do nome, explicação | Não foi possível obter o seu nome automaticamente. Escreva-o para continuar. |
| Registo do nome, etiqueta | Nome |
| Registo do nome, botão | Guardar |
| Registo do nome, aviso | Este nome fica visível para os seus colegas, como autor das suas ideias e na lista de quem votou. |

---

## Acessibilidade

Não é um requisito formal de conformidade, mas o custo de fazer bem desde o início é
próximo de zero.

- HTML semântico, com a lista de ideias como lista e os separadores como navegação.
- O estado do botão de voto é comunicado por texto, não apenas por cor.
- A alteração da contagem após um voto é anunciada a leitores de ecrã, através de uma
  região com `aria-live`.
- Todos os campos de formulário têm etiqueta associada, e as mensagens de erro estão
  ligadas ao campo correspondente.
- Navegação completa por teclado, com foco visível.

---

## Fora do âmbito

Repetido aqui de forma explícita, porque uma especificação funcional é o sítio onde
estas ausências são mais facilmente confundidas com esquecimento. Todas estas decisões
vêm do briefing.

| Não existe | Nota |
|---|---|
| Comentários | |
| Categorias, etiquetas e filtros | |
| Pesquisa | A pesquisa do browser sobre a página serve de paliativo |
| Administração | Sem ecrã de gestão e sem utilizadores privilegiados |
| Edição de ideias | A submissão é definitiva |
| Remoção de ideias | Correção apenas por procedimento manual documentado |
| Deteção de duplicados | |
| Estados, triagem ou aprovação | Uma ideia submetida está publicada |
| Mecanismo de exploração | Nada acontece a uma ideia vencedora dentro da plataforma |
| Inscrição em ideias | |
| Registo do que se aprendeu | |
| Notificações | Nem por email nem na aplicação |
| Perfis de utilizador | Não há página por pessoa |
| Decaimento de votos ou ciclos | A lista é contínua |

O briefing lista consequências conhecidas destas ausências e afirma que nenhuma delas
é motivo para mudar o âmbito. Qualquer proposta de as resolver é uma mudança de
âmbito, e precisa de decisão humana.
