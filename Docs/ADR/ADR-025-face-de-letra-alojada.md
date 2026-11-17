# ADR-025. Uma face de letra alojada no repositório e servida pela aplicação

**Estado**: Aceite, e a escolha da face fechou. **Data**: 17 de novembro de 2026.

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

## Decisão

**IdeaBoard Sans**, que são os desenhos da Lato num subconjunto latino feito por outra
pessoa e renomeado por nós, em dois ficheiros `woff2` em
`src/IdeaBoard.App/wwwroot/fonts/`:

| ficheiro | peso | bytes |
|---|---|---|
| `ideaboard-sans-400-latin.woff2` | 400 | 25 080 |
| `ideaboard-sans-700-latin.woff2` | 700 | 24 488 |

**O nome não é o da face, e isso não é descuido.** A razão é da licença e está escrita
duas secções abaixo, em "Os desenhos são da Lato e o nome não é".

Com o `OFL.txt` ao lado. O corpo desse ficheiro foi descarregado da SIL e é byte a byte o
texto oficial da versão 1.1, de 26 de fevereiro de 2007, com o cabeçalho de direitos da
Lato por cima e uma frase a dizer que estes ficheiros são um subconjunto renomeado.

Declarados em duas regras `@font-face` com a mesma família e pesos 400 e 700, ambas com
`font-display: swap`, e com a pilha do sistema declarada a seguir na regra do `body`:
`"IdeaBoard Sans", system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`.

**Não há peso 600, e nove regras pediam um.** Com 400 e 700 na família, o navegador arredonda
um pedido de 600 para cima e desenha o 700, medido e confirmado. As nove regras passaram a
dizer 700, que é o que já estava a ir para o ecrã, para a folha deixar de descrever uma
coisa que nunca acontecia.

**Servida pela própria aplicação e nunca por CDN.**

Por estar na biblioteca de classes Razor, é servida sob o prefixo
`_content/IdeaBoard.App/`, como o resto dos ficheiros estáticos, com o custo que o
[ADR-015](ADR-015-estrutura-de-quatro-projetos.md) já regista.

## O motivo apareceu e não bate certo com a face que está no repositório

O design system da empresa fixa **Lato**, nos pesos 300, 400 e 700. O que está no
repositório é Public Sans, que foi escolhida por ser humanista, numa altura em que o
motivo ainda não tinha sido dito a ninguém que estivesse a escolher.

Ou seja, este registo esteve a um passo de afirmar que se alojou uma face para aproximar
do sistema da empresa, ao lado de uma face que não é a desse sistema. As duas metades
eram verdadeiras em separado e falsas juntas, que é a forma em que isto passa por uma
revisão.

**O Hugo decidiu trocar para Lato**, e a troca foi feita com a licença verificada antes,
que foi o que mordeu à primeira. A secção seguinte é o que essa verificação encontrou, e é
a razão de a folha de estilos pedir hoje um nome que não existe fora deste
repositório.

**Proveniência.** O conteúdo do design system, incluindo a Lato e os três pesos, foi
verificado pelo ORQ contra o documento da empresa. Esse documento não está neste
repositório, e a palavra Lato não aparece em ficheiro nenhum dele, confirmado hoje.

## Os desenhos são da Lato e o nome não é, e isto é porquê

**Os desenhos são Lato. O nome não é, e não pode ser.**

Os ficheiros vieram do `HOF_Reviewer`, que é o outro projeto do Hugo onde a Lato já estava
a ser servida. A tabela de nomes dentro deles traz a identidade original completa, incluindo
`Version 1.104; Western+Polish opensource` e o aviso com `Reserved Font Name "Lato"`.

**Essa tabela não prova nada**, e é a parte que vale o registo. Qualquer ferramenta de
subconjunto copia a tabela de nomes tal e qual, por isso um ficheiro cortado continua a
dizer que é o original com todas as palavras do original. Quem parar na tabela de nomes
conclui que tem o kit completo e está enganado.

**O que decide é a contagem.** Medidos por dentro, os dois ficheiros têm 228 glifos e 218
pontos de código cada um, e **não têm uma única letra polaca**: faltam `ą ć ę ł ń ś ź ż`.
Um ficheiro que se anuncia como Western+Polish e não tem polaco nenhum é um subconjunto a
usar o nome do original. Para português não falta nada, portanto o problema nunca foi de
cobertura.

**Esta contagem não foi refeita por quem escreve esta secção, e não é por descuido.** Abrir
um `woff2` obriga a descomprimir brotli, e esta máquina não tem o módulo que a ferramenta
precisa para isso, confirmado ao tentar. O que foi confirmado por comando aqui é a
assinatura `wOF2` e o comprimento dos dois ficheiros, 25 080 e 24 488, ambos múltiplos de
quatro. A contagem de glifos fica como medição do papel que fez a renomeação, atribuída e
não adoptada.

**A consequência é da licença e foi verificada na fonte autoritativa**, o FAQ da SIL em
`openfontlicense.org/ofl-faq/`, e não de memória. A entrada 2.6 diz que fazer subconjunto
de uma webfont é modificação e que isso não permite usar o Nome Reservado. A 2.8 dá o único
teste que deixaria mantê lo, e o primeiro critério dele é suportar o mesmo inventário
completo de caracteres, que um subconjunto falha por definição. A 5.3 estende a proibição a
tudo o que identifique a face a quem a usa, o que apanha a `font-family` da folha de
estilos. A saída limpa existe e está na 2.2.1, converter o original sem lhe mexer, e não é
o caso destes ficheiros.

**Decisão do Hugo, tomada com isto à frente: usar estes ficheiros com outro nome.** O nome
é `IdeaBoard Sans`.

**Renomear não é mudar o CSS.** É o nome interno que identifica a face ao sistema, por isso
a troca foi feita dentro dos ficheiros, na tabela de nomes, e não só na folha de estilos.
Os campos que apresentam a face passaram a dizer `IdeaBoard Sans`: família, nome completo,
identificador único, nome PostScript.

**O que ficou lá dentro, porque a licença manda.** O aviso de direitos do
`tyPoland Lukasz Dziedzic`, incluindo a frase do Nome Reservado, que é atribuição e não
identificação, e é precisamente o que a condição 1 obriga a conservar. A descrição da
licença, o fabricante, o desenhador e o endereço dele.

**O que saiu, e não foi por descuido.** A marca registada, que dizia
`Lato is a trademark of tyPoland Lukasz Dziedzic`. Mantida, passaria a ler se como uma marca
sobre um nome que este ficheiro já não usa, e a licença não obriga a conservá la. E a string
de versão deixou de prometer polaco, porque era falsa para estes bytes: diz agora
`Version 1.104; Latin subset`, que guarda a versão de origem sem afirmar o que lá não está.

**Um ficheiro woff2 tem de acabar num múltiplo de quatro bytes.** Isto aparece aqui porque
custou meia hora e porque falha da pior maneira: a primeira versão do ficheiro bold calhou
em múltiplo de quatro e carregou, e a regular calhou um byte ao lado e o Chromium recusou a
com `Invalid font data`, saída do mesmo código no mesmo minuto. Uma regra que só morde
conforme a aritmética calha é a que se despacha como ficheiro estragado.

## Isto não é autorização para adoptar o design system da empresa

A regra mais importante deste repositório é o âmbito, e esta decisão toca lhe de raspão,
por isso a fronteira fica escrita.

O design system da empresa **continua não adoptado em tudo o resto, paleta incluída**,
por decisão do Hugo repetida duas vezes. O que esta decisão autoriza é uma face de letra
alojada e servida pela aplicação, e mais nada.

Quem ler isto a seguir e concluir que o sistema da empresa passou a ser a referência do
produto está a ler o que aqui não está. A aproximação é de uma coisa, e a decisão de
adoptar o resto não foi tomada.

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

A da Public Sans era `Copyright 2015 The Public Sans Project Authors` e **não indicava nome
reservado nenhum**, o que pela definição da própria licença significa que não havia Nome
Reservado a respeitar. Foi por isso que ela entrou, e é por isso que esteve certa enquanto
esteve cá.

A da Lato traz `with Reserved Font Name "Lato"`, que é o caso que a Source Sans 3 tinha
feito evitar. A diferença entre as duas é que desta vez a face não foi rejeitada: foi
renomeada, por decisão do Hugo, porque o motivo que manda nela é aproximar do design system
da empresa e esse motivo pede estes desenhos e não outros.

**Proveniência das três verificações, porque não são iguais, e porque este ficheiro passou
a ter mais do que um autor.** A palavra "eu" deixou de servir aqui, por isso cada uma leva
o papel que a fez.

A da Public Sans foi feita pelo papel de arquitetura, no ficheiro que acompanhava a fonte.
A da Source Sans 3 foi feita pelo ORQ no `OFL.txt` dessa fonte, e corroborada por fontes
secundárias, incluindo a declaração de empacotamento da Debian. Esse ficheiro de licença
nunca foi lido aqui, porque a fonte foi rejeitada e nunca esteve no repositório. A da Lato
foi feita pelo papel de interface, dentro dos próprios ficheiros e contra o FAQ da SIL, e
está corroborada acima apenas naquilo que esta máquina consegue medir.

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

Quando isto foi escrito, nenhum teste pedia a face ao host: a face podia deixar de ser
servida, por renomeação do projeto, por um erro de caminho ou por não chegar à publicação,
e a suite ficava verde e a página ficava bonita.

É o mesmo problema de compilar, arrancar e servir que o ADR-015 descreve, agravado por o
sintoma ter sido desenhado para não existir. **Já há dois testes a cobri lo**, e estão
descritos nas consequências, no fim deste registo.

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

**Public Sans.** Esteve no repositório de 16 a 17 de novembro de 2026, variável, subconjunto
latino, 26 832 bytes. Saiu por não ser a face do design system da empresa, que é o motivo
registado acima, e não por defeito nenhum dela. Fica aqui porque foi a escolha certa pelo
critério que existia na altura, e porque quem comparar os dois commits merece saber que não
houve erro a corrigir.

**Instâncias estáticas em vez da face variável.** Era uma alternativa quando havia um
ficheiro variável para escolher, e deixou de ser: os ficheiros que a empresa usa são
estáticos, portanto dois pedidos de cerca de 25 KB em vez de um de 26,8 KB. É o custo da
decisão e não uma opção dentro dela.

**Pedir à empresa o kit licenciado da Lato.** Seria o caminho que dispensava renomear, e
está por fazer e não por rejeitar. Se esse kit aparecer, a troca é substituir os ficheiros,
repor os nomes originais e apagar metade deste registo.

**Converter o original completo para woff2 sem lhe mexer.** A entrada 2.2.1 do FAQ permite
manter o nome nesse caso. Posta de lado por duas razões práticas: o original está atrás de
uma barreira de consentimento no site do autor, e esta máquina não tem codificador de woff2
nenhum. O custo em bytes **não foi medido**, e por isso não aparece aqui número nenhum.

## Consequências

**Boas.** A aparência deixa de depender do que cada máquina tem instalado. Não há pedido
para fora, portanto não há dependência de rede externa nem terceiro a observar. A face é a
do design system da empresa, que era o motivo de tudo isto.

**A letra é mais estreita, e isso ganha se.** Medido no navegador sobre um título típico:
476,9 px contra 520,5 px da Public Sans a 700 e 20px, e 373,1 contra 408,3 no corpo a 400 e
16px. São cerca de 8,5 por cento. Cabe mais título antes de a linha partir, não menos.

**Más.** Dois pedidos de cerca de 25 KB em vez de um de 26,8 KB, porque são instâncias
estáticas e não uma face variável. Mais dois ficheiros binários no repositório, com a regra
de `.gitattributes` que eles obrigam a manter.

**E uma que não é de desenho: o nome não existe fora deste repositório.** Quem procurar
`IdeaBoard Sans` não encontra nada, e quem quiser substituir os ficheiros não tem onde ir
buscar os mesmos. É o preço de a condição 3 da licença proibir o nome verdadeiro, e é por
isso que este registo diz quais são os desenhos.

O subconjunto é latino. Um glifo fora dele cai para a pilha do sistema no meio do texto,
o que é o comportamento correto e nota se.

**O que ficava por fazer está feito, e por duas vias e não uma.** O
`tests/IdeaBoard.Tests/Integracao/PublishedHostTests.cs` pede ao host publicado todas as
faces que a folha declara, e já não só a primeira, porque com dois ficheiros a primeira
podia estar certa e a segunda ausente.

E o `tests/IdeaBoard.E2E/Scenarios/TipografiaTests.cs` manda um navegador verdadeiro
carregá las. Isto não é zelo a dobrar: um woff2 traz a assinatura nos primeiros quatro bytes
e tudo o que pode estar mal nele está no resto, por isso o teste de host dá verde a um
ficheiro corrompido. Deu, durante esta troca, ao ficheiro que não acabava em múltiplo de
quatro. O navegador é a única coisa aqui que sabe mesmo responder à pergunta.

Nenhum dos dois nomeia a face. Um lê os endereços da folha de estilos, o outro lê o
conjunto de faces do próprio documento, e os dois continuam verdadeiros depois da próxima
renomeação.
