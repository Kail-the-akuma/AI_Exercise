# ADR-025. Uma face de letra alojada no repositório e servida pela aplicação

**Estado**: Aceite quanto a alojar a face e a servi la pela aplicação. A escolha da face
está em revisão desde 17 de novembro de 2026. **Data**: 17 de novembro de 2026.

## Contexto

O `CLAUDE.md` manda minimizar dependências externas e verificar primeiro o que a
plataforma já oferece. A plataforma oferece aqui alguma coisa real: a pilha do sistema,
que não custa pedido nenhum, não tem licença para ler e está instalada em todas as
máquinas da equipa.

Trazer uma face de letra para dentro do repositório é portanto uma excepção deliberada a
uma regra do repositório, autorizada pelo Hugo a 16 de novembro de 2026 depois de a
colisão lhe ter sido posta à frente.

O motivo é **aproximar o máximo possível do design system da empresa**, nas palavras do
Hugo. Não estava escrito em parte nenhuma do repositório quando este registo foi
começado, e foi preciso ir perguntar.

## O motivo apareceu e não bate certo com a face que está no repositório

O design system da empresa fixa **Lato**, nos pesos 300, 400 e 700. O que está no
repositório é Public Sans, que foi escolhida por ser humanista, numa altura em que o
motivo ainda não tinha sido dito a ninguém que estivesse a escolher.

Ou seja, este registo esteve a um passo de afirmar que se alojou uma face para aproximar
do sistema da empresa, ao lado de uma face que não é a desse sistema. As duas metades
eram verdadeiras em separado e falsas juntas, que é a forma em que isto passa por uma
revisão.

**O Hugo decidiu trocar para Lato**, e a troca está a ser feita com a licença verificada
antes, que foi o que mordeu à primeira. Até ela entrar, este registo **não diz qual é a
face final**, e descreve o que está no repositório hoje.

**Proveniência.** O conteúdo do design system, incluindo a Lato e os três pesos, foi
verificado pelo ORQ contra o documento da empresa. Esse documento não está neste
repositório, e a palavra Lato não aparece em ficheiro nenhum dele, confirmado hoje.

## Isto não é autorização para adoptar o design system da empresa

A regra mais importante deste repositório é o âmbito, e esta decisão toca lhe de raspão,
por isso a fronteira fica escrita.

O design system da empresa **continua não adoptado em tudo o resto, paleta incluída**,
por decisão do Hugo repetida duas vezes. O que esta decisão autoriza é uma face de letra
alojada e servida pela aplicação, e mais nada.

Quem ler isto a seguir e concluir que o sistema da empresa passou a ser a referência do
produto está a ler o que aqui não está. A aproximação é de uma coisa, e a decisão de
adoptar o resto não foi tomada.

## Decisão

**Public Sans**, variável, subconjunto latino, num ficheiro `woff2` de 26 832 bytes, com
o `OFL.txt` dela ao lado, em `src/IdeaBoard.App/wwwroot/fonts/`.

Declarada em `@font-face` com o intervalo de peso 300 a 700 num único ficheiro, com
`font-display: swap`, e com a pilha do sistema declarada a seguir na regra do `body`:
`"Public Sans", system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`.

**Servida pela própria aplicação e nunca por CDN.**

Por estar na biblioteca de classes Razor, é servida sob o prefixo
`_content/IdeaBoard.App/`, como o resto dos ficheiros estáticos, com o custo que o
[ADR-015](ADR-015-estrutura-de-quatro-projetos.md) já regista.

## Porque é que o CDN não é opção, e não é por princípio

São duas razões e falham de maneiras diferentes, o que importa porque uma delas é
invisível.

**A aplicação vive numa rede interna fechada.** Um pedido para fora ou não sai, e então a
face nunca chega, ou sai, e então um terceiro fica a saber que alguém daquela
organização está a ver aquela página e quando.

A segunda é um problema de privacidade numa aplicação cujo conteúdo é interno. A primeira
é pior do que parece, e a razão está mais abaixo: com `font-display: swap`, uma face que
nunca chega não parece uma avaria, parece uma decisão de desenho.

## A licença escolheu a face, e não o desenho

A primeira escolha foi a **Source Sans 3**, e foi rejeitada por licença.

A OFL define Nome Reservado como os nomes indicados como tal **a seguir à declaração de
copyright**, e define Versão Modificada como um derivado obtido por acrescentar, apagar
ou substituir componentes, ou por mudar de formato. Um subconjunto latino em `woff2` é as
duas coisas ao mesmo tempo: componentes apagados e formato mudado. A condição 3 proíbe
que uma Versão Modificada use o Nome Reservado no nome principal apresentado ao
utilizador.

A declaração de copyright da Source Sans 3 traz `with Reserved Font Name 'Source'`.

A da Public Sans, verificada no `OFL.txt` que está no repositório, é
`Copyright 2015 The Public Sans Project Authors` e **não indica nome reservado nenhum**,
o que pela definição da própria licença significa que não há Nome Reservado a respeitar.

**Proveniência das duas verificações, porque não são iguais.** A da Public Sans fiz a eu,
no ficheiro que acompanha a fonte. A da Source Sans 3 foi feita pelo ORQ no `OFL.txt`
dessa fonte, e corroborei a por fontes secundárias, incluindo a declaração de
empacotamento da Debian. Não li esse ficheiro de licença, porque a fonte foi rejeitada e
não está no repositório.

## O `.gitattributes`, que não é zelo

O ficheiro começa com `* text=auto`, e isso é uma armadilha para ficheiros binários que
nenhuma regra nomeie.

Um ficheiro de fonte reescrito por conversão de fins de linha **não é um ficheiro que
falhe a carregar. É um ficheiro que carrega como nada enquanto todas as ferramentas
dizem que correu bem.** Foram acrescentadas as regras `*.woff2 binary` e `*.woff binary`.

O comentário deixado no próprio ficheiro diz que o `text=auto` as deteta corretamente
hoje. É esse o ponto: as regras existem para não depender disso, e não para corrigir um
defeito presente.

## A propriedade que torna a página legível é a mesma que esconde a falha

Esta é a consequência que vale o registo, e é a classe que este projeto já apanhou várias
vezes.

O `font-display: swap` existe para que a página seja legível antes de a face chegar e
continue legível se ela nunca chegar. Sem ele, o primeiro desenho seria texto invisível
durante o tempo que o ficheiro demorasse. É a escolha certa.

E tem esta consequência: **um 404 na face é indistinguível de um recurso a funcionar.** A
página fica com a pilha do sistema, que é exatamente o que se desenhou para acontecer, e
ninguém é avisado de nada.

**Verificado hoje: nenhum teste pede a face ao host.** O
`tests/IdeaBoard.Tests/Integracao/PublishedHostTests.cs` prova que a folha de estilos da
biblioteca é servida sob o prefixo `_content`, e não há asserção equivalente para o
`woff2`. A face pode deixar de ser servida, por renomeação do projeto, por um erro de
caminho ou por o ficheiro não chegar à publicação, e a suite fica verde e a página fica
bonita.

É o mesmo problema de compilar, arrancar e servir que o ADR-015 descreve, agravado por o
sintoma ter sido desenhado para não existir.

## Alternativas consideradas

**Só a pilha do sistema.** Sem pedido, sem ficheiro, sem licença para ler e sem nada do
que está escrito acima. É a alternativa que esta decisão rejeita, e o que se ganha ao
rejeitá la é a aproximação ao design system da empresa, que a pilha do sistema não
consegue dar por depender do que cada máquina tenha instalado.

**A mesma face servida por CDN.** Posta de lado pelas duas razões acima, a rede fechada e
o terceiro a saber o que a pessoa está a ver.

**Source Sans 3.** Posta de lado por licença e não por desenho, como acima.

Esta rejeição fica escrita mesmo quando já não restar nenhum vestígio dela no código, e
a razão é que ela é a origem de um procedimento. **Ler a primeira linha do `OFL.txt`
antes de trazer uma face** parece zelo a quem chegue e veja só o resultado, e é a lição
de uma face que já tinha sido escolhida e teve de ser deitada fora. Um procedimento sem
a história que o produziu é o primeiro candidato a ser saltado por quem tem pressa.

**Instâncias estáticas em vez da face variável.** Um ficheiro por peso em vez de um
ficheiro para o intervalo de 300 a 700. Posta de lado porque seriam vários pedidos para
cobrir o mesmo, ou menos pesos disponíveis para poupar pedidos, e o ficheiro variável
custa 26,8 KB uma vez.

## Consequências

**Boas.** A aparência deixa de depender do que cada máquina tem instalado. Não há pedido
para fora, portanto não há dependência de rede externa nem terceiro a observar. A licença
permite subconjunto e mudança de formato sem condição de nome por cumprir.

**Más.** Um pedido de 26,8 KB, que já vem comprimido e não encolhe mais no transporte.
Mais um ficheiro binário no repositório, com a regra de `.gitattributes` que ele obriga a
manter.

O subconjunto é latino. Um glifo fora dele cai para a pilha do sistema no meio do texto,
o que é o comportamento correto e nota se.

**O que fica por fazer, e não é meu.** Uma asserção que peça a face ao host publicado,
como a que já existe para a folha de estilos. Enquanto não existir, esta decisão não tem
nada que a verifique, e o modo de falha dela foi desenhado para não se ver.

**O que fica por fechar.** A face. O motivo está registado e a troca para Lato está a ser
feita. Quando entrar, este registo passa a descrever a face que o motivo implica, guarda
as duas escolhas anteriores como história, e o estado deixa de ter a revisão em cima.

Até lá, tudo o que está escrito sobre alojar, servir, comprimir, versionar e verificar
vale tal e qual, porque nada disso depende de qual é a face.
