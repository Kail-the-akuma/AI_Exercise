# Post mortem: o IdeaBoard, e o modo de trabalho que o construiu

Escrito a 17 de novembro de 2026, no dia em que a aplicação entrou em produção.

Este documento não é sobre o IdeaBoard. É sobre **como** o IdeaBoard foi feito, porque a
aplicação era o pretexto e o modo de trabalho era o exercício. Quem quiser saber o que a
aplicação faz lê o `BRIEFING.md` e o `Docs/02`.

## O que se construiu, em números

| | |
|---|---|
| Período | 4 a 17 de novembro de 2026, catorze dias |
| Commits | mais de 140, dos quais cerca de metade no último dia |
| Código | 122 ficheiros C# e 19 de Razor, cerca de 24 300 linhas em `src` e `tests` |
| Testes | 282 de unidade e integração, 37 ponta a ponta |
| Registos de decisão | 27 ADR |
| Pessoas | uma |
| Agentes | cinco papéis em sessões separadas |

Os números de ficheiros, de linhas e de registos foram contados neste repositório. O de
commits está arredondado de propósito, porque a história foi reescrita no último dia para
substituir nomes internos por marcadores e um número exacto fica errado à primeira
contagem seguinte. **O de testes é relatado e não medido aqui**, e é o único da tabela que
não foi confirmado por quem a escreve.

A aplicação é uma plataforma interna de ideias com quatro funcionalidades: submeter,
votar, ver um ranking e ver quem votou. Corre em ASP.NET Core sobre SQLite, com
autenticação integrada do Windows, servida por IIS numa máquina da empresa.

## O modo de trabalho

Cinco papéis, cada um numa sessão própria, cada um dono de um conjunto de ficheiros e de
mais nenhum:

| Papel | É dono de |
|---|---|
| **ORQ**, orquestrador | Nada. Abre e fecha o trabalho, e verifica |
| **ARQ**, arquitectura | `Docs/ADR/` |
| **DEV** | `src/`, `tests/IdeaBoard.Tests/` |
| **QA** | `tests/IdeaBoard.E2E/` e a lista de verificação manual |
| **DOC** | `Docs/` excepto os ADR, e o `README.md` |

Três regras governam isto, e as três foram postas à prova.

**Todo o trabalho passa pelo orquestrador.** A pessoa fala com o ORQ e com mais ninguém.
Os papéis não falam entre si sem ele saber. Custa um salto em cada passagem e paga-se em
não haver duas versões de uma decisão a correr em paralelo.

**O orquestrador não é dono de nenhum ficheiro.** Não escreve código, não escreve
documentação, não escreve registos. Se o fizesse, deixava de poder verificar com
independência o que os outros escreveram.

> **Este documento é a excepção, e convém que isso se leia aqui e não só no histórico.**
> Foi escrito pelo orquestrador, a pedido da pessoa, porque é sobre o modo de trabalho e
> não sobre o produto. Passou para o papel de documentação no momento em que entrou em
> `Docs/`, e foi esse papel que verificou os números acima e escreveu esta nota.
>
> Uma regra enunciada num documento que a quebra, sem o dizer, é precisamente o género de
> coisa que este post mortem diz ter custado caro noutros sítios.

**O orquestrador não acredita em relatórios.** Verifica por comando. Esta é a regra que
mais vezes salvou o trabalho e a que mais custou a cumprir, porque um relatório bem escrito
parece-se muito com um facto.

## O que funcionou

**A fronteira de ficheiros.** Nenhuma colisão de escrita em catorze dias. Quando um papel
encontrou um defeito fora do que lhe pertence, relatou em vez de corrigir, e isso aconteceu
dezenas de vezes. O custo é um salto a mais; o benefício é que quem verifica nunca está a
verificar trabalho seu.

**A verificação por comando.** Num único dia, três afirmações de agentes que pareciam
verdadeiras foram desmentidas por medição: um bloqueador que não se reproduzia, uma causa
atribuída ao mecanismo errado, e uma explicação de um erro que o próprio código tornava
impossível. Nenhuma delas se via na leitura. Todas se viram ao correr alguma coisa.

**Escrever a condição que falsifica.** Um dos registos previu, com confiança alta e
documentação a sustentar, exactamente o contrário do que viria a acontecer no servidor:
dizia que o nome curto do servidor entraria sem pedir credenciais, e no dia da publicação é
o nome qualificado que entra. Esse registo tinha escrito, no primeiro dia, a circunstância
em que a sua afirmação deixaria de valer.

> **E aqui é preciso cuidado, porque esta é a lição mais fácil de contar bem de mais.** Que
> a afirmação era condicional está escrito e é verdade. Que foi **aquela** condição que se
> realizou **não está medido**. O próprio registo diz, duas linhas abaixo, que o porquê não
> está verificado e que a explicação mais económica é apenas a mais económica. Há pelo
> menos um segundo mecanismo capaz de produzir o mesmo resultado, e distinguir os dois pede
> uma leitura numa máquina do parque que ainda ninguém fez.
>
> O que se pode afirmar hoje é mais modesto e continua a ser a lição: **ter escrito a
> condição foi o que permitiu saber, no dia em que a previsão falhou, que havia uma
> condição por verificar em vez de um registo simplesmente errado.** O que não se pode
> afirmar é que a condição escrita foi a que disparou.
>
> E vale a pena dizer porque é que esta passou, sendo que este documento é inteiro sobre
> apanhar afirmações largas: **uma história que fecha bem demais não se verifica com o
> mesmo rigor de uma que incomoda.** Esta fechava bem para toda a gente que a leu,
> incluindo para quem a escreveu e para quem a elogiou.

**Registar a razão e não só a decisão.** Quando o endereço da aplicação teve de mudar no
dia da publicação, a razão que sustentava o anterior caiu com ele. Por estar escrita,
soube-se que tinha caído. Se só estivesse escrita a decisão, ter-se-ia trocado um endereço
por outro sem ninguém perceber que o argumento tinha morrido.

## O que correu mal, e é a parte útil

**A mesma falha repetiu-se o mês inteiro, com caras diferentes.** Afirmar mais largo do que
se mediu. Pesquisar restrito e concluir em geral. Um exemplo de cada direcção, todos reais:

- Uma procura que **não devolve nada** lida como prova de ausência.
- Uma procura que **devolve** lida como prova de presença, quando os dois resultados eram
  prosa dentro de comentários a dizer que a coisa não existe.
- Uma medição feita numa forma de invocação, afirmada para todas.
- Um comando que **parece** ecoar o que lhe pediram, e não ecoa.

Nenhuma destas se apanha a reler. Apanham-se a exercitar.

**O destinatário de uma afirmação também viaja, e ninguém o verifica.** Numa passagem entre
papéis reverifica-se a citação, o número de linha, o ficheiro. Não se reverifica quem pediu
nem a quem se respondeu, porque um nome de papel não tem aspecto de afirmação. Quando essa
mensagem traz uma instrução colada ao lado, executa-se sobre o endereço errado.

**Uma decisão tomada em conversa e não despachada no mesmo dia está perdida.** Aconteceu
duas vezes. O que a torna invisível é o texto antigo continuar a ler-se bem.

**Verificar o artefacto errado com precisão.** Houve um dia inteiro de medições correctas
sobre a coisa errada. A pergunta que apanha isto não é "a medição está certa", é "qual foi
a âncora".

## O que o servidor ensinou em duas horas

A publicação foi o primeiro contacto com a realidade, e desmentiu coisas que catorze dias
de trabalho cuidadoso não tinham desmentido.

Um módulo do servidor que o guião mandava procurar num caminho, e que vivia noutro. Um
runtime que se presumia instalado por a máquina já servir aplicações do mesmo tipo, e que
não estava, porque as aplicações vizinhas o levam dentro delas. Um endereço que a
documentação dizia ser o certo e que pede credenciais, enquanto a forma qualificada do mesmo
nome entra sozinha.

Nada disto era descoberto por mais revisão. **Uma suite verde não é prova de que a
aplicação funciona**, e foi preciso ir à máquina para saber.

## O que se levaria para o próximo exercício

**Três mecanismos valem mais do que todas as outras práticas juntas.** Verificar por comando
em vez de acreditar. Escrever ao lado de cada afirmação a condição em que ela deixa de valer.
E não deixar nenhum papel ser dono do que também verifica.

**E uma precisão sobre o primeiro, que é o que o torna cumprível.** "Verificar tudo" não se
cumpre, e toda a gente sabe que não se cumpre, portanto não é regra nenhuma. O que se
cumpre é verificar **duas categorias nomeadas**, e as duas custam segundos:

| Categoria | Porquê | O que custa confirmar |
|---|---|---|
| Uma afirmação sobre como uma **ferramenta** se comporta | Um comportamento lembrado é uma hipótese, e as ferramentas mudam entre versões sem avisar | Correr a ferramenta |
| **Apagar ou alterar um sintoma documentado** por relato de outra pessoa | É a mesma classe de erro que escrever um sem ir ao código, que foi como o sintoma errado nasceu | Abrir o ficheiro |

**Nenhuma das duas pede juízo sobre quem mandou a afirmação**, e é isso que as torna
repetíveis por quem vier a seguir e não conhecer ninguém. Uma regra que obrigue a avaliar a
fonte não sobrevive a um dia com pressa, nem a uma equipa onde as pessoas mudam.

Nos três casos deste exercício em que a versão medida acabou por ser diferente da que tinha
chegado pelo caminho, foi uma destas duas categorias que fez alguém ir medir, e não
desconfiança de quem a mandou.

**O canal único pelo orquestrador escala mal e vale a pena na mesma.** Com cinco papéis
funciona. Com dez seria um estrangulamento. A alternativa, que é deixá-los falar entre si,
troca o estrangulamento por versões divergentes da mesma decisão, e isso é mais caro de
descobrir do que de evitar.

**O trabalho em bloco em vez de um pedido de integração por correcção.** Durante a maior
parte do exercício abriu-se um pedido de integração por cada peça, e o custo de revisão
passou a ser maior do que o do trabalho. A mudança para um ramo de desenvolvimento dominante,
com passagens ao ramo principal só quando um bloco está validado e testado, foi a decisão
que mais tempo libertou.

**A parte que um agente não faz.** Decidir o âmbito. Decidir o endereço. Aceitar um risco.
Instalar o que falta numa máquina da empresa. Olhar para um ecrã e dizer que está feio.
Nenhuma destas apareceu por iniciativa de ninguém, e todas as vezes que uma delas foi
assumida em vez de perguntada, teve de ser desfeita.
