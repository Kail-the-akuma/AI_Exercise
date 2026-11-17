# ADR-027. O IdeaBoard entra como aplicação dentro de um site que outra equipa já usa

**Estado**: Aceite. **Data**: 17 de novembro de 2026.
**Relaciona se com**: [ADR-009](ADR-009-publicacao-iis.md),
[ADR-024](ADR-024-http-sem-certificado.md), [ADR-026](ADR-026-onde-vive-a-configuracao.md).

## Contexto

O servidor é o `SERVIDOR-APP`, e **já serve outra aplicação** no `Default Web Site`, na porta
80, com duas ligações, uma delas sem nome de anfitrião. O IdeaBoard entra ali dentro, como
aplicação em `/IdeaBoard`.

**Proveniência desta frase, porque é ela que sustenta a decisão toda.** Vem de um relatório
corrido na máquina e relatado pelo ORQ, e não de suposição. O que está medido é que o
`Default Web Site` é dono da porta 80 e com que ligações. **O que não está medido é o que
esse site serve**, e isso é tratado na secção da hipótese, mais abaixo.

A porta 80 estar ocupada é o que tira do caminho a hipótese que o repositório assumia. Um
site próprio deixa de poder ter o endereço mais curto da máquina, e passa a precisar de
outra porta ou de outro nome.

Até hoje este repositório assumia site próprio em toda a parte. O ADR-024 decidia um
endereço com porta, e o script de provisionamento criava um site novo e recusava continuar
se já existisse um com aquele nome.

Decisão do Hugo a 17 de novembro de 2026, tomada com três caminhos à frente: porta própria
em `http://SERVIDOR-APP:8080/`, nome próprio na porta 80 com um registo de DNS, ou outra
porta qualquer.

**A 8080 daquele exemplo já estava ocupada, e isso só se soube depois.** O documento de
instalação do AplicacaoVizinhaB, lido a 18 de novembro de 2026, regista que o RepositorioVizinhoA
ocupa naquele servidor a porta 80 em produção **e a 8080 em QA**. A escolha não foi
afectada, porque o que a decidiu foi não querer porta escrita de todo, e o terceiro caminho
oferecido era precisamente outra porta qualquer. Fica corrigido para que ninguém leia o
exemplo como um endereço que esteve disponível.

**Não escolheu nenhum dos três. Perguntou se dava para ser o servidor mais o nome da
aplicação**, que é a forma que acabou por ser decidida.

A pergunta dele diz o critério melhor do que uma resposta diria. **Um endereço sem porta
escrita e sem depender de ninguém.** A porta própria obrigava trinta pessoas a escrever
`:8080` para sempre. O nome próprio obrigava a um registo de DNS que alguém da
infraestrutura teria de criar, e até lá não havia nada para mostrar a funcionar.

A aplicação dentro do site dá `http://SERVIDOR-APP/IdeaBoard` no minuto a seguir ao
provisionamento, sem pedir nada a ninguém. **E isso não é comodidade.** Num produto interno
que ainda não serviu uma única pessoa, não depender da infraestrutura para o mostrar a
funcionar é a diferença entre haver uma coisa para ver e haver um pedido em fila.

É também o mesmo critério que o [ADR-024](ADR-024-http-sem-certificado.md) e o
[ADR-013](ADR-013-certificado-no-arranque.md) já tinham encontrado duas vezes, por tirar do
caminho crítico uma dependência externa com duração que a equipa não controla. A terceira
vez que o mesmo critério decide deixa de ser preferência e passa a ser padrão desta equipa.

## Decisão

O IdeaBoard é servido como **aplicação dentro do `Default Web Site`**, no caminho
`/IdeaBoard`, **com pool de aplicações próprio**.

O pool próprio não é uma preferência de isolamento. É requisito, e a secção seguinte é o
porquê.

## O que se sabia quando isto foi decidido, e o que se soube a seguir

Vale a pena fixar a ordem, porque muda o que significa reabrir o assunto.

**Nada do que está escrito abaixo era conhecido quando a forma foi escolhida.** A herança
do `web.config` do vizinho, a colisão de handlers, o requisito do pool e o quarto sítio das
variáveis apareceram depois, ao ler a configuração para perceber o que mudava com a
mudança de forma.

Isto não torna a decisão pior nem a põe em causa. Os três caminhos continuam a existir, e a
troca que o Hugo fez continua a ser a mesma troca. **O que muda é o que um leitor daqui a um
ano deve concluir.** Sem esta secção, ele lê a lista de consequências e assume que foram
pesadas e aceites. Foram descobertas.

A diferença tem efeito prático num sítio só, e é o caminho de saída. Se a convivência vier a
custar mais do que o que está escrito aqui, **pedir um site próprio não é reverter uma
decisão ponderada, é acabar de a tomar** com a informação que ela não teve.

## O pool próprio é requisito, e é o ADR-009 que o cobra

Confirmado na documentação da Microsoft e não de memória. **Partilhar pool entre aplicações
não é suportado no modelo in-process**, e a página das sub-aplicações diz à letra que
atribuir um pool separado à sub-aplicação é requisito desse modelo, com o CLR em Sem Código
Gerido.

O que falha é a segunda aplicação a arrancar, o que significa que a avaria pode aparecer no
vizinho e não em nós, consoante a ordem.

**Isto é uma fatura do [ADR-009](ADR-009-publicacao-iis.md) que só hoje ficou pagável.** O
in-process foi escolhido lá, e é também o que faz a identidade negociada pelo servidor
chegar à aplicação no mesmo pedido em vez de um segundo salto. Enquanto tivemos site
próprio, o pool próprio vinha de graça e ninguém reparou que era obrigatório. Dentro do
site de outra pessoa, passa a ser a primeira coisa a garantir.

**E traz uma boa notícia que não é óbvia.** O pool é a fronteira, não o site. O processo de
trabalho único que o SQLite exige, pelo [ADR-002](ADR-002-sqlite.md), aplica se ao nosso
pool e **não é imposto ao vizinho**, e as definições de reciclagem dele também não nos
alcançam. A co-habitação no site não é co-habitação no processo.

## A herança desce do pai para nós, e o nosso ficheiro não a trava

Esta é a que muda a leitura de tudo o resto, e é fácil de ler ao contrário.

O nosso `web.config` abre com `<location path="." inheritInChildApplications="false">`.
**Esse atributo protege os filhos que não temos, e não nos protege do pai.** O que ele diz
é que o conteúdo daquela localização não desce para aplicações debaixo de nós. A herança
vem de cima e ele não lhe toca.

É o que o SDK põe por omissão em qualquer aplicação publicada, e existe precisamente para o
papel de pai. Nesta posição, **a forma do nosso ficheiro sugere uma proteção que ele não
dá**, o que é pior do que não ter nenhuma, porque quem o abra conclui que o assunto está
tratado.

## A colisão de handlers, com o sintoma à frente

Uma colecção de configuração funde se com a do pai em vez de a substituir. Se o vizinho for
ASP.NET Core e o ficheiro dele não trouxer aquele mesmo atributo, o `aspNetCore` dele chega
nos e choca com o nosso por chave duplicada.

**O sintoma está documentado e é o que faz perder a tarde**: 500.19 no browser, nada no
registo de aplicação, o log do pai criado e com funcionamento normal, e **o log da
sub-aplicação a nunca chegar a existir**. Parece a nossa aplicação a não arrancar, e ela nem
foi chamada.

Os dois remédios documentados são o `<remove name="aspNetCore" />` antes do nosso `<add>`,
ou o ficheiro do pai trazer o atributo da herança. O segundo não está nas nossas mãos.

**O estado de hoje, verificado por leitura.** O nosso `web.config` tem secção `<handlers>`
com `<add>` e **sem `<remove>`**. Se isso é ou não um problema depende de um ficheiro que
ainda não temos. O `deploy/provisionar.ps1` lê o `web.config` do site pai, imprime as
secções que encontra, nomeia este sintoma, e **não lhe toca de propósito**, porque a cura
habitual mexe no ficheiro de outra equipa.

**É o único ponto desta decisão que fica em aberto**, e fecha se de uma de duas maneiras: o
ficheiro do vizinho tem o atributo, e então não há nada a fazer, ou não tem, e então o
`<remove>` entra no nosso.

## A dependência não é só no vizinho, é no servidor, e foi outra equipa que a criou

Esta secção foi escrita a 18 de novembro de 2026, depois de ler os documentos de instalação
das duas aplicações que já correm naquela máquina. **Tudo o que ela afirma sobre o servidor
vem desses documentos, com as datas que eles próprios trazem, e não de uma medição nossa.**
Estamos a ler o que outra equipa escreveu sobre um servidor, e não o servidor.

O resto deste registo trata a herança que desce do site pai. **Há uma camada acima dessa, e
é mais perigosa por ser global.**

**O papel `Web-Windows-Auth` tem de estar instalado**, e está, posto lá para a primeira
daquelas aplicações. Sem ele o elemento de autenticação do nosso ficheiro não é sequer
reconhecido.

**E as duas secções de autenticação tiveram de ser destrancadas ao nível do servidor.** O
documento do RepositorioVizinhoA regista que isso foi feito uma vez, com `appcmd unlock config`, e
que sem isso um site cujo `web.config` traga aquelas secções responde 500.19 no instante em
que o IIS o lê.

**Nenhuma das duas foi feita por nós nem para nós.** Foram feitas por outra equipa, por
outra razão, meses antes de este projeto existir, e quem as fez não tem razão nenhuma para
saber que dependemos delas. Voltar a trancar as secções, ou remover o papel, parte nos sem
tocar em nada que seja nosso.

**O que isto obriga a escrever ao lado, e é a parte acionável.** Perante um 500.19 que fale
de uma secção que não pode ser usada naquele caminho, a pergunta não é o que mudou no nosso
`web.config`. É se as duas secções continuam destrancadas e se o papel continua instalado.
Os documentos dos vizinhos verificam isso lendo `OverrideModeEffective` e exigindo `Allow`,
que é a leitura de volta que o nosso provisionamento não faz, e esse é assunto do
`deploy/` e não deste registo.

## O quarto sítio onde as mesmas variáveis podem existir

O [ADR-026](ADR-026-onde-vive-a-configuracao.md) manda definir cada nome num sítio só, e
nomeia três: o nosso `web.config`, o ambiente da máquina e o do pool. **O site pai é um
quarto**, e não está lá escrito porque não existia quando aquele registo foi feito.

A avaria é a mesma e chega com o mesmo disfarce: os valores combinam se, o arranque falha a
queixar se de uma cadeia de ligação, e o caminho que aparece na mensagem é um que ninguém
escreveu.

## O que o `Default Web Site` serve é hipótese, e a pergunta que a resolve

Isto fica escrito como hipótese de propósito, porque é hipótese.

Os documentos de instalação das duas aplicações vizinhas, lidos a 18 de novembro de 2026,
dizem duas coisas que encaixam. O do RepositorioVizinhoA oferece, como uma das três saídas para o
caso de a porta 80 estar ocupada, **reaproveitar o `Default Web Site`** apontando lhe o
caminho físico e o pool. O do AplicacaoVizinhaB diz que o RepositorioVizinhoA ocupa a porta 80 naquele
servidor.

**Juntas, sugerem que o `Default Web Site` pode ser o RepositorioVizinhoA** e não um site vazio por
omissão. Se for, o nosso pai não é neutro: é uma aplicação ASP.NET Core alojada em processo,
e nós somos o caso de sub-aplicação debaixo de aplicação ASP.NET Core que a documentação da
Microsoft trata por nome, com a colisão de handlers descrita acima a ser o cenário provável
e não o remoto.

**E há uma boa notícia já verificada por leitura**, que é o `web.config` deles trazer
`<location path="." inheritInChildApplications="false">`. Com ele lá, o que eles declaram
não desce para nós, incluindo as remoções de WebDAV que o ficheiro deles traz. **Isso é uma
propriedade do ficheiro de outra equipa**, que ninguém se comprometeu a manter.

**A pergunta que fecha isto é uma só**: qual é o caminho físico e o pool do `Default Web
Site`. Se apontar para a pasta de uma daquelas aplicações, está respondido. O
`deploy/verificar-servidor.ps1` passou a listar as aplicações dentro de cada site
precisamente para a responder, e isto fecha se com a máquina a dizer e não com dois
documentos a sugerir.

**Até lá, o que este registo afirma e o que supõe.** Afirma que o `Default Web Site` é dono
da porta 80, porque isso saiu de um relatório da máquina. Supõe tudo o resto sobre o que ele
serve. A decisão de entrar lá dentro não depende da diferença, mas a gravidade da colisão de
handlers depende, e por isso a distinção fica escrita em vez de ser arrumada.

## A consequência que não é técnica, e é a que mais custa daqui a um ano

**Passa a existir quem nos parta sem tocar neste repositório.** Uma publicação do vizinho,
uma alteração ao `web.config` dele, uma mudança de autenticação ou de ligações ao nível do
site, qualquer uma pode mudar o comportamento da nossa aplicação sem que uma linha nossa
mude.

E há uma assimetria que decide quanto isso custa: **somos nós que vamos parecer os
culpados.** O sintoma aparece no nosso endereço, com o nosso nome em cima, e o primeiro
sítio onde alguém vai procurar é este repositório, onde não mudou nada. O tempo perde se
todo antes de a pergunta certa ser feita.

**Portanto a pergunta certa fica escrita aqui, como gatilho e não como risco.** Perante uma
avaria que apareça sem alteração nossa, antes de procurar uma regressão no nosso código,
**ver a data do `web.config` do vizinho e a configuração do site**. É uma verificação de
trinta segundos que só ocorre a quem souber que existe um vizinho, e daqui a um ano isso
não vai estar na cabeça de ninguém.

## Alternativas consideradas

**Site próprio noutra porta, em `http://SERVIDOR-APP:8080/`, que era o que estava decidido.**
Funcionava, estava escrito, e não dependia de ninguém de fora. Posta de lado porque obriga
toda a gente a escrever a porta, todos os dias, num endereço que vai para atalhos e
marcadores e para uma comunicação à equipa. Trazia ainda a questão de a porta entrar no
nome do serviço pedido ao Kerberos, tratada no
[ADR-024](ADR-024-http-sem-certificado.md), que nesta forma desaparece.

**Nome próprio na porta 80, com um registo de DNS.** É a melhor das quatro em estado
estacionário: endereço curto, sem porta, sem caminho, sem vizinho, e sem nada do que está
escrito neste registo. Posta de lado porque põe no caminho crítico um pedido a outra equipa,
e porque até esse pedido ser satisfeito **não há nada para mostrar**. É a mesma troca que o
ADR-013 e o ADR-024 já tinham feito duas vezes, e pela mesma razão.

Fica como a saída natural se esta convivência se tornar cara. O custo de lá chegar mais
tarde é um registo de DNS e uma comunicação à equipa, e não é mudança de arquitetura
nenhuma.

**Alojamento out-of-process, que permitiria partilhar pool.** Resolveria o requisito do
pool por remoção. Posta de lado porque o in-process é decisão do ADR-009 e porque é também o
que faz a identidade negociada chegar no mesmo pedido. Trocar o modelo de alojamento para
poupar um pool é pagar arquitetura por uma definição.

**Um site só nosso naquela máquina, sem mais nada.** Parece uma quarta via e não é. O
`Default Web Site` já tem uma ligação na porta 80 **sem nome de anfitrião**, que é a que
responde a tudo o que nenhuma ligação mais específica apanhe. Um site nosso ao lado dele
só se distingue por porta ou por nome de anfitrião, portanto desemboca numa das duas
primeiras e não acrescenta uma terceira. Raciocínio sobre como o IIS resolve ligações, e
não medição.

Fica escrito porque é a alternativa que alguém vai propor primeiro, por soar à solução
óbvia, e porque descobrir sozinho que ela não existe custa meia hora.

## Consequências

**Boas, e a primeira é a que motivou tudo.** O endereço existe no minuto a seguir ao
provisionamento, sem pedido a equipa nenhuma, o que num produto que ainda não serviu uma
pessoa é a diferença entre haver uma coisa para mostrar e haver um pedido em fila.

O endereço não leva porta escrita, portanto cabe numa comunicação à equipa e num atalho sem
explicação ao lado. E, por ser porta 80, a política `EnableAuthNegotiatePort` deixa de poder
decidir seja o que for, conforme o [ADR-024](ADR-024-http-sem-certificado.md).

**Más.** Passa a haver configuração alheia no nosso caminho de herança, com a colisão de
handlers por fechar, um quarto sítio para as variáveis de ambiente colidirem, e uma equipa
que nos pode partir sem tocar aqui.

**O que fica por verificar, e só se verifica com o ficheiro do vizinho à frente.** Se o
`web.config` dele traz `inheritInChildApplications="false"`, se declara
`<environmentVariables>` e com que nomes, se declara `security/authentication`, e qual é o
pool dele. A primeira pergunta é a que decide, e as outras três são verificação.

**E uma consequência do endereço e não do alojamento**, que vive no ADR-024 e não se repete
aqui: debaixo de um caminho, tudo o que escreva um caminho literal para o browser manda a
pessoa para fora da aplicação.
