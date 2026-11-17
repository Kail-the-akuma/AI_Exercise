# ADR-024. HTTP sem certificado

**Estado**: Aceite, com o endereço emendado a 17 de novembro de 2026, depois de medido na
máquina. **Data**: 13 de novembro de 2026.
**Substitui**: [ADR-013](ADR-013-certificado-no-arranque.md).

## Contexto

O pedido original era arrancar sem HTTPS e rever o assunto mais tarde, para tirar do
caminho crítico a dependência da equipa de infraestrutura.

O [ADR-013](ADR-013-certificado-no-arranque.md) mostrou que isso não era possível com o
Entra ID, e deu três razões. Duas eram sobre o protocolo de autenticação, que o Entra ID
recusa URIs de redirecionamento em HTTP e que os cookies de ligação precisam de `Secure`.
A terceira era sobre o produto, que o cookie de sessão viajaria em texto limpo e
permitiria votar em nome de um colega.

O [ADR-014](ADR-014-autenticacao-integrada-windows.md) tirou o Entra ID do caminho. As
duas primeiras razões caem inteiras, porque deixa de haver registo de aplicação, URI de
redirecionamento e cookie de ligação. A terceira não cai, muda de ator, e está tratada
mais abaixo.

O Hugo decidiu HTTP.

## O ADR-013 valida a sua própria substituição

Vale a pena registar, porque não é ironia e é evidência.

O ADR-013, nas alternativas que pôs de lado, escreve à letra que a troca para Windows
Authentication seria a única forma de ter mesmo HTTP com as pessoas autenticadas, e
rejeita a por ser uma mudança de arquitetura e não um adiamento.

Ou seja, este caminho foi identificado no momento em que foi fechado, e foi fechado pelo
custo da mudança e não pelo mérito dela. Esse custo foi pago. A decisão de hoje não
contradiz o ADR-013, executa a alternativa que ele próprio nomeou.

## Decisão

**HTTP, sem certificado e sem HSTS.**

O endereço é **`http://servidor-app.dominio.interno/IdeaBoard`**, no nome qualificado do
servidor, servido como aplicação dentro do `Default Web Site` que já existe naquela máquina
e já serve outra aplicação na porta 80, conforme o
[ADR-027](ADR-027-aplicacao-dentro-de-um-site-alheio.md).

**Este título já mudou três vezes em dois dias, e por isso deixou de dizer o endereço.**
Começou em "com o nome curto do servidor e porta", passou a "no nome curto do servidor", e
hoje nenhuma das duas é verdade. Um título viaja para o índice e para dentro de cada ligação
que aponta para cá, e é lido por quem nunca abre o ficheiro, portanto **a parte volátil não
pertence lá**. O que não muda é HTTP sem certificado, e é isso que o título passa a dizer.

## A forma do endereço mudou a 17 de novembro, e mudou a favor

**Quando este registo foi escrito, o endereço era o nome curto com porta**, porque a
aplicação ia ter site próprio. A frase antiga fica contada e não apagada, porque o título
deste ficheiro a carregava, e porque as secções seguintes foram todas escritas a pensar
numa porta.

**O que não muda.** A exigência de um nome estável, e a razão dela, que é o início de sessão
automático. As zonas de segurança são indexadas por anfitrião e esquema, e um caminho não
entra nessa chave tal como uma porta não entrava.

**O que muda, e foi medido no mesmo dia, mais tarde.** A forma do nome deixou de ser a curta.
A secção seguinte trata disso, e é a emenda mais séria deste registo.

## O nome curto pede credenciais e o nome qualificado entra sozinho, medido na máquina

**Medido pelo Hugo a 17 de novembro de 2026, no browser dele, contra a instalação a sério.**
O `http://SERVIDOR-APP/IdeaBoard` **pede credenciais**. O
`http://servidor-app.dominio.interno/IdeaBoard` **entra sozinho**. É a primeira vez que esta
aplicação corre com identidade verdadeira, e a primeira vez que esta questão sai do papel.

**É o contrário do que este registo previa.** A subsecção abaixo afirmava, com confiança alta
e apoiada em documentação de desenho do Chromium, que um nome sem pontos entra na zona de
intranet local e um nome com pontos fica fora dela, e concluía que o nome curto era a forma
favorável. **A conclusão estava errada neste parque.**

**O que este registo ganhou por ter escrito uma condição, e é menos do que parece.** Logo a
seguir àquela afirmação está escrito que a deteção automática de intranet só acontece
**quando a lista de servidores de autenticação não está definida por política**, que havendo
lista é ela que governa, e que **num parque gerido a lista estar definida é o caso provável e
não o improvável**.

**O que se pode afirmar é que havia uma condição por verificar, e não que foi aquela que
disparou.** A medição diz o quê e não diz o porquê. Ter escrito a condição evitou ficarmos
com um registo simplesmente errado, e deixou uma pergunta em vez de uma surpresa. Não previu
nada.

**E há três mecanismos candidatos, não um.** A lista de servidores de autenticação definida
por política, que é a condição escrita aqui no primeiro dia. A deteção automática de intranet
a comportar se neste parque de outra maneira. E a **lista de excepções do proxy**, levantada
pelo ORQ a 17 de novembro de 2026, que não está escrita em parte nenhuma deste registo.

**Se for a terceira, a condição que disparou não é a que estava escrita**, e o mérito de a
ter escrito encolhe para o que está no parágrafo anterior: saber que havia uma condição, sem
saber qual.

**Nenhum dos três está medido.** Resolve se com uma leitura numa máquina do parque,
`edge://policy`, que já estava mandada fazer mais abaixo neste registo e que ninguém fez. Até
lá a confiança de qualquer explicação aqui é baixa, e é menor do que a da afirmação que elas
substituem, como tem de ser.

## A decisão não mudou, mudou o endereço que a satisfaz

Isto é diferente de uma reversão, e quem ler daqui a um ano precisa de saber a diferença.

O critério do Hugo, escrito no [ADR-027](ADR-027-aplicacao-dentro-de-um-site-alheio.md), é
**um endereço sem porta escrita e sem depender de ninguém**. O nome qualificado cumpre o
melhor do que o curto cumpria: não leva porta, e **já existe**, portanto não precisa de
pedido nenhum à infraestrutura. É o mesmo critério a escolher outra vez, com um facto novo à
frente.

**E há a metade que não é lisonjeira, e fica ao lado.** A razão técnica que foi dada para
preferir o nome curto, a zona de intranet, **não se verificou aqui**. Não é só que apareceu um
endereço melhor, é que o argumento que sustentava o anterior caiu. As duas coisas são verdade
ao mesmo tempo, e contar só a primeira seria escrever a versão simpática.

**O que muda, e muda para melhor. A preocupação com o `EnableAuthNegotiatePort`
desaparece.** Essa política só decide alguma coisa quando a porta não é 80 nem 443, o que
está escrito mais abaixo desde o primeiro dia, e o endereço novo é porta 80. Dos dois
mecanismos que a verificação em `edge://policy` ia investigar, um deixa de poder falhar, e
fica a lista de servidores de autenticação sozinha.

Vale a pena reparar que esta conclusão **já estava contida no que este registo tinha
escrito**, e não precisou de informação nenhuma que não estivesse cá. É o inverso do que
costuma acontecer neste repositório: uma decisão a jusante costuma matar uma razão
escrita, e desta vez confirmou uma.

**E isto não se estende à secção seguinte, que é sobre a forma do nome.** Aqui o registo
continha mesmo a conclusão, porque dizia que a política da porta só decide quando a porta não
é 80 nem 443, e o endereço novo é porta 80. Ali continha apenas uma condição, e qual delas se
realizou ainda não está medido. **Duas coisas parecidas com destinos diferentes, e juntá las
num elogio só seria dar a esta casa um crédito que só metade merece.**

**O que esta emenda não trata, de propósito.** Entrar dentro de um site que outra equipa
já usa é uma decisão sobre a superfície de alojamento, com consequências próprias que não
são sobre transporte nem sobre endereço: a configuração do vizinho passa a estar no nosso
caminho de herança, o modelo in-process exige pool próprio, e passa a haver quem nos possa
partir sem tocar neste repositório. Isso não cabe num registo intitulado sobre
certificados, e está no [ADR-027](ADR-027-aplicacao-dentro-de-um-site-alheio.md).

## O que morre com esta decisão, e uma morte que é boa notícia

Morre o certificado emitido pela própria equipa, da autoridade interna ou auto assinado.
Morre a Fase 6 do [05](../05-Plano-de-Entrega.md), que existia só para o substituir por
um certificado confiado.

E morre **a armadilha do HSTS**, que o próprio ADR-013 descreve como a consequência menos
óbvia desta área e a que tem pior resultado: com HSTS ativo, um browser que encontre um
erro de certificado termina a ligação e não oferece forma de prosseguir, o que torna o
site inacessível a toda a gente incluindo a quem o publicou, e não se corrige depressa
porque o browser guarda a instrução pelo tempo declarado.

Essa armadilha desaparece por completo. É o melhor resultado desta decisão, e é maior do
que o incómodo que ela evita, porque era o único modo de falha do projeto que não tinha
recuperação rápida.

## A regra do HSTS sobrevive à morte do certificado, e tem de sobreviver

Aqui está o cuidado que esta substituição obriga a ter.

É tentador apagar a armadilha do HSTS junto com o certificado, por o problema ter deixado
de existir. Seria errado, porque o problema não deixou de existir, deixou de estar
agendado.

**Se o HTTPS voltar algum dia, a ordem continua fixa: certificado confiado primeiro,
HSTS depois, nunca ao contrário.** A regra não dependia do certificado ser emitido pela
equipa. Dependia de haver um certificado não confiado e HSTS ao mesmo tempo, que é uma
combinação que qualquer caminho para HTTPS pode voltar a produzir.

Fica escrita aqui e não no ADR-013, porque um registo substituído não é sítio onde
alguém vá procurar uma regra em vigor.

## O nome DNS definitivo continua obrigatório, e a razão é outra

O ADR-013 exigia o nome definitivo desde o primeiro dia e dava três razões: o
certificado emitido para esse nome, o registo da aplicação no Entra ID, e o certificado
de substituição. **As três morreram.**

Isso cria um risco preciso, e não é o de alguém distraído: quem rever este requisito
daqui a um mês vai verificar os três destinos, encontrar zero, e removê lo por obsoleto.
Um leitor cuidadoso chega lá mais depressa do que um distraído.

**A razão que sobrevive é o início de sessão automático**, e é mais exigente do que as
três que morreram, porque depende da forma exata do nome e não apenas de ele ser estável.
Trocar o nome depois obriga ainda a refazer a comunicação à equipa, os atalhos e os
marcadores de toda a gente, que é custo humano e não técnico.

### O que está verificado sobre a forma do nome, e com que confiança

**Esta subsecção está desmentida pela medição de 17 de novembro de 2026, e fica inteira.** O
que ela diz continua a ser o que a documentação diz, e o que se passou a saber é que não é
isso que governa este parque. Encolhê la deixaria a emenda acima sem o que ela emenda.

**Um nome sem pontos entra na zona de intranet local por omissão, e é nessa zona que o
início de sessão automático está ligado por omissão. Um nome com pontos fica fora dela.**
Confiança alta, com a documentação de desenho do Chromium a dizer o inverso de forma
direta, que um host com ponto fica fora da intranet local.

Portanto o nome curto que o Hugo escolheu é a forma favorável a esta propriedade, e o
nome qualificado completo é que precisaria de configuração no parque. **Esta última frase foi
medida a ser falsa neste parque**, e a condição do parágrafo seguinte é a razão.

**A condição que limita esta afirmação, e sem a qual ela não é verdadeira.** A deteção
automática de intranet só acontece **quando a lista de servidores de autenticação não
está definida por política**. Se estiver, é a lista que governa, e o que decide passa a
ser o nosso nome estar lá dentro. Num parque gerido, a lista estar definida é o caso
provável e não o improvável.

**Sobre a porta.** As zonas de segurança não são indexadas por porta, e a estrutura do
registo não tem onde guardar uma. Confiança média, porque assenta na estrutura e não numa
fonte que o afirme. Existe um mecanismo real que torna o comportamento sensível à porta,
a política `EnableAuthNegotiatePort` do Chrome e do Edge, que decide se a porta entra no
nome do serviço pedido ao Kerberos quando não é 80 nem 443. Confiança alta, é documento
de desenho do Chromium.

**Desde 17 de novembro de 2026 isto não governa o nosso endereço**, que passou a ser porta
80. Fica escrito por inteiro em vez de encolhido, porque é a análise que torna a pergunta
respondível, e quem um dia mudar esta aplicação para uma porta própria precisa dela outra
vez e não a vai saber procurar.

### O que isto manda fazer, e por que ordem

Primeiro, **confirmar numa máquina da equipa** que a nossa aplicação, no nosso servidor,
com o nome curto e com Negotiate ligado, faz início de sessão automático. Só se não
fizer é que se pergunta qual dos mecanismos falhou.

**Este passo foi feito a 17 de novembro de 2026 e a resposta foi não.** Com o nome curto pede
credenciais, com o nome qualificado entra sozinho. O passo cumpriu se e **morre com resultado
em vez de morrer por omissão**. O que ele mandava perguntar a seguir, qual dos mecanismos
falhou, **está por responder**, e é a única coisa que fica em aberto nesta área.

A ordem importa porque dois dos três mecanismos possíveis não têm pedido nenhum
associado à infraestrutura, e uma única página, `edge://policy` numa máquina do parque,
responde a duas perguntas de uma vez, a lista de servidores de autenticação e a política
da porta.

**Houve uma medição noutro servidor** que mostrou início de sessão automático apenas em
três portas, as de duas aplicações vizinhas. Não é propriedade desta aplicação e não está
confirmada no nosso ambiente. E a leitura mais económica dela é que essas três portas são
as únicas onde existe aplicação a pedir autenticação integrada, porque uma porta onde
nenhuma aplicação a pede não pode dar início de sessão automático, com zona ou sem zona.
Nessa leitura, a medição não diz nada sobre a nossa porta.

**E desde 17 de novembro de 2026 não diz nada sobre nós de todo**, porque deixámos de ter
porta própria. Fica pela mesma razão do parágrafo anterior, e por uma segunda: é um exemplo
medido de uma observação verdadeira a ser lida como propriedade de uma aplicação quando era
propriedade do que estava instalado, e esse erro não precisa de portas para se repetir.

## O que se perde com HTTP, nomeado e não escondido

**A confidencialidade do conteúdo.** Ideias, nomes de autores e listas nominais de
votantes viajam em claro no segmento de rede.

**A confidencialidade dos tokens antiforgery.** E isto importa mais do que importava com
o Entra ID, não menos, porque com Negotiate as credenciais passam a ser ambientes: o
browser autentica sozinho qualquer pedido àquele host, venha ele de onde vier, que é a
propriedade que tornava o antiforgery necessário quando havia cookies.

**A ligação ao canal.** A proteção forte contra relay de autenticação integrada deriva do
certificado do servidor, e sem TLS não há canal a que ligar. Confiança média, com a
cláusula que a limita: sem TLS a proteção **degrada e não desaparece**, passando a
assentar em ligação ao serviço pelo nome do serviço. Não encontrei uma fonte única da
Microsoft a tratar precisamente esta combinação, e a parte do nome do serviço veio
sobretudo de manuais de teste de intrusão.

### O terceiro fundamento do ADR-013 não cai, muda de ator

O ADR-013 dizia que o cookie de sessão em claro permitiria votar e submeter em nome de um
colega, e que isso destrói o produto e não apenas a segurança, por o valor da plataforma
assentar em os votos serem nominais e verdadeiros.

Com Negotiate não há cookie de sessão para roubar, e essa via fecha.

**Raciocínio meu, não confirmado, e registado como tal.** Quem observa o tráfego em claro
vê o par de antiforgery, o cookie e o campo do formulário. Com esse par, e levando a
pessoa a uma página que submeta um voto, o browser dela junta o Negotiate sozinho e o
voto sai em nome dela. Se isto se confirmar, o fundamento diz exatamente o que dizia, por
outro caminho, e o ator passa de observador passivo com um cookie roubado para observador
passivo mais uma isca.

**A condição em que a minha afirmação é falsa**: se o antiforgery do ASP.NET Core não for
utilizável desta forma por quem apenas o observa, ou se houver ligação ao canal que o
invalide fora dele. Não verifiquei nenhuma das duas. Isto não é fundamento desta decisão,
é um ponto por verificar que pertence a quem decidir reabrir o assunto.

## Alternativas consideradas

**Manter o ADR-013, com certificado emitido pela equipa.** Funcionava, e a razão pela
qual foi posta de lado está no próprio ADR-013: toda a gente veria um aviso de segurança
no primeiro acesso e teria de o aceitar, o que ensina a equipa a clicar em avisos de
certificado, hábito que se leva para sítios onde o aviso é verdadeiro. O ADR-013 chama
lhe formação em más práticas, e tinha razão.

**Pedir um certificado confiado à infraestrutura antes de arrancar.** É o caminho correto
em estado estacionário. Posta de lado pelo mesmo motivo de sempre, que coloca no caminho
crítico uma dependência externa com duração que a equipa não controla, e é precisamente
o que o pedido original queria evitar.

**HTTPS com certificado confiado mais tarde, sem adiar o arranque.** Não é alternativa, é
o que esta decisão deixa em aberto. Ver abaixo.

## Consequências

**Boas.** Ninguém vê aviso nenhum no primeiro acesso, e a equipa não é treinada a
clicar em avisos de certificado. A Fase 6 desaparece do plano. Não há certificado para
expirar nem para renovar. A armadilha do HSTS deixa de existir.

**Más.** O tráfego é legível por quem tiver acesso ao segmento de rede, numa aplicação
cujo valor assenta em os votos serem verdadeiros.

Isto é aceite por ser uma aplicação interna, numa rede corporativa, com trinta pessoas, e
sem dados além de ideias e votos que já são públicos dentro da equipa por desenho do
produto. É uma decisão proporcional a este contexto e não é uma posição sobre HTTP em
geral.

**O que ficava em aberto, e já não fica.** Era o valor do endereço, e fechou a 17 de
novembro de 2026 em **`http://servidor-app.dominio.interno/IdeaBoard`**, depois de a forma curta
ter sido medida a pedir credenciais nesse mesmo dia. A frase fica com o destino escrito em vez
de desaparecer, para que quem a procure encontre a resposta em vez de um vazio.

**E uma consequência que só existe por o endereço passar a ter caminho.** Servida debaixo
de `/IdeaBoard`, a aplicação recebe o pedido com o caminho partido em base e resto, e tudo
o que escreva um caminho literal para o browser passa a mandar a pessoa para fora dela.
Havia exactamente um sítio assim, o redireccionamento do portão do nome, corrigido no `dev`
em `4f1a3ad`.

Fica registado porque a classe não se fecha com a correcção. Qualquer caminho literal
escrito a partir de hoje tem o mesmo defeito, e o que o apanha são **dois testes a correr
debaixo de um caminho base**, que foi como a correcção foi medida: tirando a base da
frente, os dois ficam vermelhos, o que prova que o `Response.Redirect` não acrescenta nada
de seu. Medição de quem a fez, e não minha, que tinha levantado o ponto com a confiança
marcada e sem o conseguir confirmar aqui.

**E o que fica para rever.** HTTPS continua a ser possível mais tarde, e passar a ter o
não obriga a mudar arquitetura nenhuma, ao contrário do que aconteceu na direção
inversa. Se for revisto, a ordem do HSTS acima é a primeira coisa a ler.
