# Trabalhar com uma equipa de agentes

Escrito a 17 de novembro de 2026, no dia em que a aplicação entrou em produção.

Este documento não é sobre o IdeaBoard. A aplicação foi o pretexto, e o exercício era
outro: **perceber se um conjunto de agentes com papéis separados produz melhor trabalho
do que um agente a fazer tudo, e o que é preciso montar à volta deles para que isso
aconteça.**

Quem quiser saber o que a aplicação faz lê o [briefing](../../BRIEFING.md). Quem quiser
saber como se organiza uma equipa destas, continue.

## O que se montou

Uma pessoa, cinco papéis de agente em sessões separadas, catorze dias. No fim, uma
aplicação em produção num servidor da empresa, com autenticação integrada do Windows,
usada por uma pessoa real.

| | |
|---|---|
| Período | 4 a 17 de novembro de 2026 |
| Papéis | cinco, em sessões independentes |
| Pessoas | uma |
| Registos de decisão | 27 |
| Testes | 282 de unidade e integração, 37 ponta a ponta |

Os registos e os testes aparecem aqui não como medida de produtividade, mas porque são o
que a equipa produziu **além do código**, e é aí que a diferença entre um agente e cinco
se nota.

## O briefing veio primeiro, e é a peça que torna o resto possível

Antes de existir um único papel, existia um briefing. Define o problema, os utilizadores,
quatro funcionalidades, os critérios de sucesso e os riscos aceites. São 158 linhas.

A secção que faz o trabalho não é nenhuma dessas. É a lista do que fica **fora** do
âmbito, e a frase que a acompanha: as exclusões não são omissões nem oportunidades de
melhoria.

**Isto não é zelo documental, é uma peça de engenharia dirigida a um comportamento
conhecido.** Um agente competente, posto a construir uma lista de ideias com votos,
propõe categorias, filtros, pesquisa, estados, aprovação e notificações. Cada proposta é
razoável, bem argumentada, e desloca o produto. Multiplicado por cinco papéis, o âmbito
desfaz-se numa semana, e desfaz-se por acumulação de boas sugestões, que é a forma mais
difícil de travar.

A lista de exclusões transforma uma discussão de gosto numa verificação. Perante uma
proposta, a pergunta deixa de ser "isto é boa ideia" e passa a ser "isto está na lista".

E a lista encolheu **duas vezes**, nos dois casos por decisão explícita de quem é dono do
produto, cada uma registada com data e com a razão. Isso importa tanto como a lista
existir: um âmbito que nunca cede é ignorado, e um âmbito que cede por insistência deixa
de ser âmbito. Ceder por decisão datada mantém as duas coisas de pé.

**Se houvesse uma só coisa a levar deste exercício, era esta.** O briefing não foi
documentação do trabalho, foi a condição para o trabalho poder ser distribuído.

## Os cinco papéis

| Papel | É dono de |
|---|---|
| **ORQ**, orquestrador | Nada. Abre e fecha o trabalho, e verifica |
| **ARQ**, arquitectura | Os registos de decisão |
| **DEV** | O código e os testes de unidade e integração |
| **QA** | Os testes ponta a ponta e a lista de verificação manual |
| **DOC** | A documentação |

A divisão óbvia é por especialidade. Não é essa que interessa.

**O que a divisão faz, e que nenhuma outra prática conseguiu, é produzir para cada um o
gatilho que nenhum produz para si.** Quem escreve uma afirmação não tem como duvidar
dela, porque a escreveu precisamente por acreditar nela. Quem a recebe para fazer outra
coisa com ela, para a testar, para a documentar, para a registar, tropeça nela a partir
de fora.

Isto apanhou coisas que nenhuma releitura apanharia, porque não davam erro nenhum. Um
teste que passava por não haver nada a validar. Um item de verificação manual que
confirmava a sobrevivência de uma sessão que não existe. Um requisito sustentado por três
razões que tinham morrido todas no mesmo dia. Todos verdes, todos com óptimo aspecto.

Uma segunda propriedade, mais simples: **quem verifica nunca está a verificar trabalho
seu.** O orquestrador não é dono de ficheiro nenhum precisamente para não ter de confiar
em si próprio no momento em que verifica.

## Como a equipa trabalha

**Tudo passa pelo orquestrador.** A pessoa fala com um só interlocutor. Os papéis não
decidem entre si. O custo é um salto a mais em cada passagem. O benefício é não haver
duas versões da mesma decisão a correr em paralelo, que é o modo de falhar mais caro de
descobrir.

**Um de cada vez.** Enquanto um trabalha, os outros estão parados. É mais lento do que
trabalhar em paralelo e elimina por completo a classe de problemas em que um agente revê
código que entretanto já mudou.

**O orquestrador não acredita em relatórios.** Verifica por comando. Foi a regra que mais
vezes mudou o resultado, e a mais difícil de cumprir, porque um relatório bem escrito
parece-se muito com um facto.

## O que a conversa entre agentes produziu

Esta é a parte que não se antecipa lendo sobre o assunto.

**Correcções em todas as direcções, e não de cima para baixo.** O orquestrador despachou
trabalho para corrigir um defeito que não existia, e quem o recebeu mediu-o e devolveu-o
em vez de o fazer. Um papel relatou um bloqueador, o orquestrador não o reproduziu, e ao
medirem os dois em conjunto descobriram que estavam ambos certos e a medir formas
diferentes de invocar a mesma coisa. Uma afirmação passou de um papel ao orquestrador e
deste a um terceiro, e foi o terceiro que a mediu e a desmentiu.

**Nenhuma dessas correcções foi pedida.** Vieram de cada papel ter uma razão própria para
ir ver, que não era desconfiança de quem mandou.

**A melhor delas é um exemplo pequeno.** Um papel estava prestes a apagar um comentário
no código por o achar errado. Antes de o apagar, foi medir. O comentário estava certo, e
a frase que ele ia escrever no lugar é que estava errada. Ninguém lhe pediu aquela
verificação, e ela nasceu de uma regra que ele próprio tinha formulado nesse dia: apagar
um sintoma documentado é a mesma classe de erro que escrever um sem ir ao código.

**É isto que distingue uma equipa de um agente com uma lista de tarefas.** As regras que
os papéis formularam uns para os outros foram mais úteis do que as que lhes foram dadas
de início, porque nasceram de um caso concreto em que alguém se enganou à frente de outra
pessoa.

## O que custou, e onde o método pode ser melhor

**Houve conversa a mais.** Nem todas as passagens entre papéis precisaram de existir.
Algumas mensagens confirmaram o que já estava confirmado, outras levaram contexto que o
destinatário não ia usar, e o orquestrador respondeu a relatórios que não pediam
resposta. O canal único garante consistência e paga-se em volume, e nem todo esse volume
rendeu alguma coisa.

**E o orquestrador devia ter travado mais cedo.** Houve trabalho em curso que não era
necessário e que ele viu passar sem o cortar, limitando-se a limitá-lo. Quem o travou foi
a pessoa, com uma instrução directa a dizer que mediar inclui parar o que não é preciso.
A partir desse momento o comportamento mudou e não voltou atrás, mas **a correcção veio
de fora e devia ter vindo de dentro**.

As duas coisas têm a mesma raiz. Um orquestrador que verifica tudo trata qualquer
trabalho em curso como legítimo por omissão, porque a pergunta que lhe é natural é "isto
está certo" e não "isto é preciso". **A segunda pergunta tem de estar escrita, senão não
é feita.**

## O que se leva para o próximo exercício

**O briefing antes dos agentes.** Sem uma lista explícita do que fica de fora, uma equipa
de agentes desloca o produto por acumulação de boas sugestões, e ninguém consegue apontar
o momento em que isso aconteceu.

**Papéis separados por propriedade de ficheiros, e não por especialidade.** O valor não
está em cada um saber mais da sua área. Está em o trabalho de um chegar às mãos de outro,
que o lê de fora e tropeça no que o autor não tinha como ver.

**Um interlocutor só para a pessoa.** Escala mal, e com cinco papéis compensa. A
alternativa troca o estrangulamento por versões divergentes da mesma decisão, e isso é
muito mais caro de descobrir do que de evitar.

**Verificação por categoria, e não verificação de tudo.** Verificar tudo não se cumpre num
dia com pressa, e toda a gente sabe que não se cumpre. Duas categorias nomeadas
cumprem-se: uma afirmação sobre o comportamento de uma ferramenta confirma-se em segundos,
e apagar algo documentado é a mesma classe de erro que escrevê-lo sem ir à fonte. Nenhuma
das duas pede juízo sobre quem mandou a afirmação, e é isso que as torna repetíveis por
quem vier a seguir e não conhecer ninguém.

**E a parte que a equipa não faz.** Decidir o âmbito. Aceitar um risco. Escolher um
endereço. Parar trabalho desnecessário antes de alguém o apontar. Olhar para um ecrã e
dizer que está feio. Todas as vezes que uma destas foi assumida em vez de perguntada,
teve de ser desfeita.
