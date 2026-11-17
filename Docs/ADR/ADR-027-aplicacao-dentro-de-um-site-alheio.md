# ADR-027. O IdeaBoard entra como aplicação dentro de um site que outra equipa já usa

**Estado**: Aceite. **Data**: 17 de novembro de 2026.
**Relaciona se com**: [ADR-009](ADR-009-publicacao-iis.md),
[ADR-024](ADR-024-http-sem-certificado.md), [ADR-026](ADR-026-onde-vive-a-configuracao.md).

## Contexto

O servidor é o `SERVIDOR-APP`, e **já serve outra aplicação** no `Default Web Site`, na porta
80. O IdeaBoard entra ali dentro, como aplicação em `/IdeaBoard`.

Até hoje este repositório assumia site próprio em toda a parte. O ADR-024 decidia um
endereço com porta, e o script de provisionamento criava um site novo e recusava continuar
se já existisse um com aquele nome.

Decisão do Hugo a 17 de novembro de 2026, tomada com as alternativas à frente. **As razões
dele não me foram relatadas e não são inventadas aqui**, que é diferente de não existirem.

## Decisão

O IdeaBoard é servido como **aplicação dentro do `Default Web Site`**, no caminho
`/IdeaBoard`, **com pool de aplicações próprio**.

O pool próprio não é uma preferência de isolamento. É requisito, e a secção seguinte é o
porquê.

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

## O quarto sítio onde as mesmas variáveis podem existir

O [ADR-026](ADR-026-onde-vive-a-configuracao.md) manda definir cada nome num sítio só, e
nomeia três: o nosso `web.config`, o ambiente da máquina e o do pool. **O site pai é um
quarto**, e não está lá escrito porque não existia quando aquele registo foi feito.

A avaria é a mesma e chega com o mesmo disfarce: os valores combinam se, o arranque falha a
queixar se de uma cadeia de ligação, e o caminho que aparece na mensagem é um que ninguém
escreveu.

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

**Site próprio, com porta, que era o que estava decidido.** Funcionava e estava escrito.
Exigia uma porta livre e uma ligação novas na máquina, e trazia consigo a questão de a
porta entrar no nome do serviço pedido ao Kerberos, tratada no
[ADR-024](ADR-024-http-sem-certificado.md). Posta de lado por decisão do Hugo.

**Alojamento out-of-process, que permitiria partilhar pool.** Resolveria o requisito do
pool por remoção. Posta de lado porque o in-process é decisão do ADR-009 e porque é também o
que faz a identidade negociada chegar no mesmo pedido. Trocar o modelo de alojamento para
poupar um pool é pagar arquitetura por uma definição.

**Pedir à outra equipa um site só para nós.** Não foi rejeitada, não foi pedida. Se a
convivência vier a custar mais do que o que está escrito acima, é o caminho de saída, e não
obriga a mudar nada no artefacto, apenas o provisionamento.

## Consequências

**Boas.** Não é preciso pedir porta nem ligação nova. Por ser porta 80, a política
`EnableAuthNegotiatePort` deixa de poder decidir seja o que for, conforme o
[ADR-024](ADR-024-http-sem-certificado.md). O endereço fica mais curto e mais fácil de
comunicar à equipa.

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
