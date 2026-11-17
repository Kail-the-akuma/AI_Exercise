# 06. Operação e publicação

**Versão 3, 17 de novembro de 2026.**

Destina se a quem publica e mantém a aplicação no servidor interno. Os caminhos usam
o nome de trabalho `IdeaBoard` e têm de ser ajustados ao nome final.

## Identidade

**Não há nada a provisionar junto da equipa de identidade.** Nem registo de aplicação,
nem client secret, nem URI de redirecionamento. A autenticação é integrada do Windows,
servida pelo IIS, conforme
[ADR-014](ADR/ADR-014-autenticacao-integrada-windows.md).

O que é preciso é configuração do site, e está na secção do IIS mais abaixo:
autenticação Windows ativada e acesso anónimo desligado.

### A consulta ao diretório

A aplicação consulta o diretório corporativo para obter o nome apresentável, uma vez por
pessoa, no primeiro acesso. Pede **apenas o atributo do nome**, e não o registo da
pessoa.

A identidade do pool de aplicações tem de conseguir ler o diretório. Com identidade de
pool por omissão a consulta sai como a conta da máquina, o que basta num domínio para
ler atributos públicos, e convém confirmar antes de assumir.

**Se o diretório não responder, a aplicação não fica bloqueada.** Quem chega sem nome
regista o seu, e a linha só nasce quando há nome para escrever. Uma indisponibilidade do
diretório degrada a experiência, não impede o acesso.

## Pastas no servidor

Fora da pasta de publicação, sem exceção.

| Caminho | Conteúdo |
|---|---|
| `C:\IdeaBoard\data\` | `ideaboard.db`, mais os ficheiros `-wal` e `-shm` |
| `C:\IdeaBoard\keys\` | Anel de chaves de Data Protection |
| `C:\IdeaBoard\backup\` | Cópias diárias |
| `C:\inetpub\IdeaBoard\` | Aplicação publicada |

A base de dados dentro da pasta da aplicação é apagada na primeira republicação. Isto
não é uma hipótese remota, é o resultado normal de um `dotnet publish` com limpeza do
destino.

### Permissões

A identidade do pool de aplicações precisa de **Modificar sobre a pasta**
`C:\IdeaBoard\data\`, não apenas sobre o ficheiro da base de dados. O SQLite cria e
apaga os ficheiros `-wal` e `-shm` ao lado do ficheiro principal, e sem permissão de
escrita na pasta o resultado é um erro de base de dados só de leitura, apesar de o
ficheiro ter as permissões certas.

A mesma permissão é necessária em `C:\IdeaBoard\keys\`.

Com identidade de pool por omissão, o principal a usar é
`IIS AppPool\{nome-do-pool}`.

### A origem do Event Log, criada uma vez por um administrador

Fora de Development, a aplicação escreve para o Event Log do Windows com a origem
**`IdeaBoard`**, e **não a cria**. Criar uma origem escreve no registo em `HKLM`, e isso
é permissão que a identidade do pool de aplicações não deve ter. O `Program.cs` diz
isto, e até agora não havia documento nenhum que mandasse fazer a outra metade.

Numa consola de administrador, uma vez por servidor, antes do primeiro arranque:

```powershell
New-EventLog -LogName Application -Source "IdeaBoard"
```

Confirmar que ficou:

```powershell
[System.Diagnostics.EventLog]::SourceExists("IdeaBoard")
```

**O sintoma de isto estar por fazer é não haver sintoma nenhum na aplicação.** As páginas
servem, as pessoas votam, e o registo de erros que o [04](04-Requisitos-Nao-Funcionais-e-Seguranca.md)
descreve não existe. O gatilho observável é provocar um erro de propósito e **não**
encontrar nada no log `Application` com origem `IdeaBoard`, que é um item da lista de
[07](07-Plano-de-Verificacao.md).

## Configuração do IIS

São **dois** pré requisitos na máquina, e falhar qualquer um deles dá o mesmo erro, o que
é a razão de estarem os dois escritos aqui.

**ASP.NET Core Hosting Bundle** correspondente ao .NET 10, que instala o ASP.NET Core
Module v2.

#### Não se presume o runtime por a máquina já servir ASP.NET Core

**Medido a 17 de novembro de 2026**, e custou uma paragem. Aquele servidor servia ASP.NET
Core havia meses e tinha apenas o runtime **8.0.34**, porque as duas aplicações vizinhas
são publicadas **auto contidas**: levam o runtime dentro do artefacto e não precisam de
nenhum instalado na máquina.

**O IdeaBoard é publicado dependente do framework**, portanto o runtime da versão maior
com que foi construído é requisito **nosso** e não uma coisa que se herde do estado da
máquina. Foi preciso instalar o Hosting Bundle do 10.

A forma disto é a que interessa guardar. "A máquina já serve ASP.NET Core" é verdade e
**não responde à pergunta**, que é se serve a versão de que precisamos e no modelo de
publicação que usamos. Sem a verificação, o provisionamento teria terminado bem, o site
teria sido criado, e a aplicação falharia depois com um `500.30` ou um `502.5`, **nenhum
dos quais diz uma palavra sobre versões de runtime** e ambos mandam quem os lê procurar
noutro sítio.

O `deploy/provisionar.ps1` passou a exigi lo, e lê as pastas do framework partilhado em
vez de correr o `dotnet`, porque num servidor o `dotnet` muitas vezes não está no
caminho.

#### Não se pergunta ao disco se o módulo está instalado, pergunta se ao IIS

**O ficheiro do módulo vive em sítios diferentes consoante a instalação**, e os dois
existem na natureza:

| Caminho | |
|---|---|
| `%ProgramFiles%\IIS\Asp.Net Core Module\V2\` | É onde está no `SERVIDOR-APP`, medido a 17 de novembro de 2026 |
| `%SystemRoot%\System32\inetsrv\` | O outro sítio habitual, e o primeiro onde quase toda a gente vai procurar |

**A lição não é "é neste".** É que testar um caminho é adivinhar onde outra pessoa pôs
alguma coisa, e o IIS sabe onde os módulos dele estão:

```powershell
Get-WebGlobalModule | Where-Object { $_.Name -eq "AspNetCoreModuleV2" }
```

Se devolver alguma coisa, o módulo está registado, que é a pergunta que interessa. A
propriedade `Image` diz onde o ficheiro está, e serve para imprimir e não para decidir.

**Isto custou uma paragem a 17 de novembro de 2026.** O `verificar-servidor.ps1` testava o
caminho em `System32\inetsrv`, não o encontrou, e disse que o módulo faltava num servidor
que o tinha. Quem seguisse essa resposta ia instalar o que já lá estava. O
`provisionar.ps1` passou a perguntar ao IIS, e a medição foi feita de uma consola de 64
bits, portanto não era redirecionamento do sistema de ficheiros.

**Convém reparar em como isto se parece com o resto deste documento.** Não era uma
afirmação falsa, era uma pergunta feita ao sítio errado, e a resposta vinha com o aspecto
exacto de uma resposta verdadeira.

**O serviço de função Autenticação do Windows do IIS**, em Servidor Web, Segurança,
Autenticação do Windows. Não vem instalado por omissão. Sem ele, o elemento
`windowsAuthentication` que o `web.config` da aplicação traz não é reconhecido pelo
servidor, e a autenticação integrada que todo o acesso a esta aplicação depende não
existe sequer como opção. Está nomeado como requisito pela documentação da Microsoft
para alojar aplicações ASP.NET Core com autenticação integrada.

Sem o primeiro ou sem o segundo, o site responde com erro 500.19 ou 502. **O código não
distingue as duas causas**, por isso verificar ambos antes de procurar noutro lado.

E há uma **terceira** causa com o mesmo código, que não é um pré requisito da máquina mas
aparece no mesmo sítio. O `web.config` que a publicação produz traz a secção de
autenticação lá dentro, e o IIS tranca essa secção ao nível do servidor, por isso uma
aplicação cujo `web.config` a traga pode responder 500.19 em vez de arrancar. O
`deploy/provisionar.ps1` destranca a com `overrideMode` a `Allow` e **com `-Location` da
própria aplicação**, `Site/IdeaBoard`, e não do site.

A diferença importa para quem for procurar a entrada no `applicationHost.config`: está no
caminho da aplicação e não no do site. E importa pela propriedade que dá, que é mais
forte do que destrancar ao nível do site: **nem o site que recebe a aplicação nem o
vizinho que o partilha mudam de comportamento.**

O gatilho observável é um 500.19 cuja mensagem fale de uma secção que não pode ser usada
neste caminho, em vez de falar do módulo. É aí que esta causa é a certa, e é no script que
se mexe.

O tratamento completo, incluindo o que o script relê e o que faz com cada resposta, está
mais abaixo, em **O destrancamento da autenticação, que agora se relê**. Este parágrafo
afirmava que ninguém tinha verificado isto numa máquina com IIS, o que deixou de ser a
descrição certa quando a releitura entrou, e ficava a contradizer a secção de baixo.

### Pool de aplicações

| Definição | Valor | Porquê |
|---|---|---|
| Versão do .NET CLR | Sem Código Gerido | A aplicação não usa o CLR do IIS |
| Máximo de processos de trabalho | **1** | O SQLite aceita um escritor. Mais do que um processo produz bloqueios e corrupção de expectativas. Ver [ADR-002](ADR/ADR-002-sqlite.md) |
| Modo de arranque | AlwaysRunning | Evita o primeiro pedido lento do dia |
| Tempo limite de inatividade | 0 | O mesmo motivo |
| Reciclagem por intervalo regular | 0 | Desliga a reciclagem a cada 29 horas, que cai a meio do dia de trabalho |
| Reciclagem a hora fixa | Fora do horário laboral | Se for mesmo necessária |
| Aplicações de 32 bits | **Desligadas** | Posto de forma explícita, pela razão a seguir à tabela |

A limitação a um processo de trabalho é a definição mais fácil de esquecer e a mais
difícil de diagnosticar depois. Fica registada aqui e é item da lista de verificação
em [07](07-Plano-de-Verificacao.md).

#### As aplicações de 32 bits, e porque é que a razão tem fonte e âmbito

**Isto é uma observação sobre este servidor e não um facto geral sobre o IIS.** Está
registada no guia de instalação do AplicacaoVizinhaB, em `RepositorioVizinhoB`, na primeira
instalação a sério, de 7 de setembro de 2026, com as palavras "confirmed the hard way":
nesta máquina o `New-WebAppPool` cria o pool com as aplicações de 32 bits **ligadas**.

Fica com a fonte e com a data à frente de propósito. Lido como regra do IIS, isto seria
aplicado noutro servidor onde pode não valer, e um valor por omissão depende da versão e
da configuração da máquina.

**O que nos faria não é o que lhes fez a eles.** Eles publicam auto contido `win-x64`,
que um pool de 32 bits não consegue sequer carregar, por isso tiveram um `HTTP Error
500.32` e souberam logo. Esta aplicação é publicada dependente do framework e em
processo, e o Hosting Bundle instala também o runtime x86, portanto num pool de 32 bits
isto arrancaria e serviria, em silêncio, como processo de 32 bits.

A afirmação honesta não é que parte. É que **diverge em silêncio** do que julgamos ter
publicado, e uma avaria assim não tem dia em que apareça. Por isso o `provisionar.ps1`
põe a definição e, no fim, relê a e pára se ela não ficou.

### Site e aplicação

**A aplicação não tem site próprio.** É servida como aplicação dentro de um site que já
existe, no caminho virtual `/IdeaBoard`, e herda as ligações desse site.

O endereço desta instalação é **`http://servidor-app.dominio.interno/IdeaBoard`**. HTTP,
**nome qualificado**, sem porta, porque o site que a recebe já serve na porta 80.

**É o nome qualificado e não o nome curto, e isso foi medido e não escolhido.** A 18 de
novembro de 2026, no dia da publicação, `http://SERVIDOR-APP/IdeaBoard` pede credenciais no
browser e `http://servidor-app.dominio.interno/IdeaBoard` entra sozinho.

A causa não está no IIS nem na aplicação: **quem decide o início de sessão silencioso é o
browser**, pela zona de intranet, e uma das regras por omissão dessa zona é incluir os
sítios que não passam pelo proxy. É a lista de excepções do proxy que decide, e o nome
curto fica de fora dela. O raciocínio completo está no
[ADR-024](ADR/ADR-024-http-sem-certificado.md).

**É o endereço a dar à equipa**, e dar o nome curto produz um pedido de credenciais que
se lê como avaria da aplicação e não é.

| | |
|---|---|
| Site | `Default Web Site`, que já existe e não é tocado |
| Caminho virtual | `/IdeaBoard` |
| Pool de aplicações | Próprio, `IdeaBoardPool`, e não o do site |
| Pasta física | `C:\inetpub\IdeaBoard` |

#### O mapa daquele servidor, medido e já não suposto

Durante dois dias este documento e o [ADR-027](ADR/ADR-027-aplicacao-dentro-de-um-site-alheio.md)
descreveram o site que nos recebe como um site de que se sabia a porta e não se sabia o
conteúdo. **Ficou medido a 17 de novembro de 2026.**

O `Default Web Site` tem `physicalPath` igual a `C:\AplicacaoVizinhaA`. Ou seja, **o site
da porta 80 é a aplicação vizinha**, e não um site vazio por omissão. O IdeaBoard é uma
aplicação filha dentro dela.

| Camada | O que é |
|---|---|
| Servidor | `SERVIDOR-APP`, partilhado com duas aplicações de outra equipa |
| Site da porta 80 | `Default Web Site`, servido a partir de `C:\AplicacaoVizinhaA` |
| Aplicação vizinha | ASP.NET Core, alojada em processo, com pool próprio |
| **O IdeaBoard** | Aplicação em `/IdeaBoard`, pasta `C:\inetpub\IdeaBoard`, pool `IdeaBoardPool` |

**Isto muda a gravidade de uma coisa e não muda a decisão de nenhuma.** A colisão de
handlers descrita abaixo era o cenário remoto enquanto o pai podia ser um site vazio.
Sendo o pai uma aplicação ASP.NET Core alojada em processo, somos exactamente o caso que a
documentação da Microsoft trata por nome, e o cenário passa a ser o provável.

O que salva hoje é uma propriedade do ficheiro deles e não nossa: **o `web.config` da
aplicação vizinha traz `inheritInChildApplications="false"`**, portanto o que eles
declaram não desce até nós. É uma propriedade que ninguém se comprometeu a manter, e que
desaparece no dia em que eles republicarem com um ficheiro diferente.

**Fica escrito aqui e não só no registo de decisão** porque é o mapa que falta a quem
chega ao servidor para diagnosticar e encontra duas aplicações que não conhece.

**O pool é próprio e isso não é detalhe.** Um pool por aplicação é o que mantém o limite
de um processo de trabalho, que o SQLite exige, sem o impor ao vizinho que partilha o
site. Partilhar o pool obrigaria a mudar as definições de outra aplicação, que é
precisamente o que nada aqui faz.

#### O que a aplicação herda do site que a recebe

Uma aplicação dentro de um site lê a configuração do site **antes** da sua. Se o site pai
tiver `web.config` próprio, o que lá estiver vale primeiro, e o `provisionar.ps1` imprime
o conteúdo desse ficheiro para que quem provisiona o veja em vez de o descobrir depois.

O sintoma de uma colisão é um 500.19 a falar de uma secção duplicada, ou uma aplicação
que arranca e se comporta de maneira que o `web.config` dela não explica. **A cura
habitual mexe no ficheiro do vizinho**, e nenhum destes scripts lhe toca, de propósito.

> **O nosso `web.config` parece proteger disto e não protege.** Abre com
> `<location path="." inheritInChildApplications="false">`, que é o que o SDK põe em
> qualquer aplicação publicada. Esse atributo impede que o que nós declaramos desça para
> aplicações debaixo de nós, e **nós não temos nenhuma**. A herança vem de cima e ele não
> lhe toca.
>
> Fica escrito porque quem abrir o ficheiro à procura desta resposta encontra uma frase
> que parece dá la. O raciocínio completo está no
> [ADR-027](ADR/ADR-027-aplicacao-dentro-de-um-site-alheio.md).

#### A colisão de handlers, e o sintoma que faz perder a tarde

Uma colecção de configuração **funde se** com a do pai em vez de a substituir. Se o site
que nos recebe for também ASP.NET Core e o ficheiro dele não trouxer aquele atributo, o
`aspNetCore` dele chega até nós e choca com o nosso por chave duplicada.

O sintoma é o que o torna caro, e não a causa:

| O que se vê | |
|---|---|
| No browser | 500.19 |
| No registo de aplicação | Nada |
| No log do site pai | Criado, e com funcionamento normal |
| No log da nossa aplicação | **Nunca chega a existir** |

Lê se como a nossa aplicação a não arrancar, e ela **nem foi chamada**. É a diferença
entre procurar durante uma tarde no sítio errado e olhar para o ficheiro do lado.

Os dois remédios documentados são um `<remove name="aspNetCore" />` antes do nosso
`<add>`, ou o ficheiro do pai trazer o atributo da herança, que não está nas nossas mãos.
**Hoje o nosso ficheiro tem `<add>` e não tem `<remove>`**, e se isso é ou não um
problema depende de um ficheiro que ainda não foi lido. Fica em aberto no
[ADR-027](ADR/ADR-027-aplicacao-dentro-de-um-site-alheio.md) e não aqui, porque é decisão
e não operação.

#### O caminho base, e o que ele obriga a escrever

Servida em `/IdeaBoard`, a aplicação recebe esse prefixo do IIS como caminho base. Não há
`UsePathBase` no arranque, porque o módulo o entrega.

Isso é transparente para quase tudo, e há um sítio onde não é: um redirecionamento é
literal. Um `Location` escrito como `/nome` manda a pessoa para a raiz do servidor, que é
outra aplicação, e não para `/IdeaBoard/nome`. O portão do nome leva o caminho base à
frente por isso, e é o único sítio do produto que teve de saber disto.

**O `web.config` não é gerado pela publicação.** Está versionado em
`src/IdeaBoard.Web/web.config`, escrito à mão, e a publicação transforma o e leva o no
artefacto. É código fonte e muda como tudo o resto, por alteração ao ficheiro versionado.

Dizer que era gerado lia se como "isto não é código fonte", que é o contrário da decisão
tomada. A outra metade da frase continua verdadeira e fica mais forte: **não editar à mão
no servidor**, porque a publicação seguinte sobrepõe o e a alteração desaparece sem deixar
rasto no repositório.

É nele que vivem as duas variáveis sem as quais a aplicação recusa arrancar, e é por isso
que o `publicar.ps1` confere que lá estão e que não estão vazias antes de copiar.

Estas duas são postas **na aplicação** e não no site, pelo mesmo `-Location` da secção
acima, portanto o site que a recebe fica como estava.

| Definição de autenticação | Valor | Onde |
|---|---|---|
| Autenticação Windows | **Ativada** | Na aplicação, `Site/IdeaBoard` |
| Autenticação anónima | **Desligada** | Na aplicação, `Site/IdeaBoard` |

As duas andam juntas. Com a anónima ligada, o IIS serve o pedido sem desafiar ninguém, e
a aplicação recebe pedidos sem identidade, o que faz a política de autorização recusar
toda a gente em vez de autenticar.

### Certificado e HSTS, que não existem

**Não há certificado e o HSTS fica desligado.** O site serve HTTP, conforme
[ADR-024](ADR/ADR-024-http-sem-certificado.md), que substitui o ADR-013.

Fica aqui com título próprio em vez de ausente, porque esta secção já descreveu um
certificado durante algum tempo, e quem a procure tem de encontrar a resposta em vez de
um vazio que se lê como esquecimento.

**Se o HTTPS voltar algum dia, a ordem é fixa: certificado confiado primeiro, HSTS
depois, nunca ao contrário.** O raciocínio está em
[04](04-Requisitos-Nao-Funcionais-e-Seguranca.md) e no
[ADR-024](ADR/ADR-024-http-sem-certificado.md). Com HSTS ativo e um certificado não
confiado, o browser termina a ligação sem oferecer forma de prosseguir, e guarda a
instrução pelo tempo declarado, portanto desligar o HSTS no servidor não desbloqueia
quem já a recebeu.

## Onde vive a configuração, que é a pergunta que bloqueia o primeiro arranque

A aplicação **recusa arrancar** sem duas chaves, e di lo com os nomes à frente:

```
IdeaBoard refused to start because its configuration is incomplete.

  - 'ConnectionStrings:IdeaBoard' is missing or empty.
  - 'DataProtection:KeysPath' is missing or empty.
```

O `appsettings.json` publicado traz as duas **vazias de propósito**, para que não haja
nada em que cair: o host pára e nomeia o que falta, em vez de arrancar errado. Essa
guarda só serve de alguma coisa se quem publica souber onde pôr os valores, e é por isso
que esta secção existe.

**Vivem no `web.config`, como variáveis de ambiente do módulo**, e vão versionadas:

```xml
<aspNetCore processPath="dotnet" arguments=".\IdeaBoard.Web.dll" hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ConnectionStrings__IdeaBoard"
                         value="Data Source=C:\IdeaBoard\data\ideaboard.db;Foreign Keys=True;Default Timeout=30" />
    <environmentVariable name="DataProtection__KeysPath"
                         value="C:\IdeaBoard\keys" />
  </environmentVariables>
</aspNetCore>
```

O duplo sublinhado é como se escreve uma secção de configuração numa variável de
ambiente: `ConnectionStrings__IdeaBoard` é a chave `ConnectionStrings:IdeaBoard` que a
guarda de arranque pede pelo nome.

Não há aqui credencial nenhuma, e não é preciso haver. A base de dados é um ficheiro e a
identidade vem do servidor web, portanto o que está escrito são caminhos de um servidor
interno, que já estão neste documento à vista.

### Porque não um `appsettings.Production.json` ao lado da aplicação

Porque a pasta da aplicação é esvaziada pela publicação seguinte. É a mesma falha que
este documento já descreve para uma base de dados guardada lá, e o sintoma é pior: o site
deixa de arrancar, e a mensagem fala de configuração em falta, que não se parece com
"alguém publicou por cima".

Postas no `web.config`, as duas viajam com o artefacto, sobrevivem a cada publicação e
mudam por pull request como o resto.

### Cada nome num sítio só

A documentação do módulo avisa que uma variável que **também** exista no ambiente do
sistema pode ser combinada com a que está aqui, e que o resultado pode impedir a
aplicação de arrancar.

Portanto não definir estes dois nomes na máquina nem no pool de aplicações. Se forem
definidos nos dois sítios, a avaria chega com o aspecto de uma cadeia de ligação errada,
e o caminho que aparece na mensagem é um que ninguém escreveu.

**São quatro sítios e não três, desde que a aplicação passou a viver dentro de um site
alheio.** O quarto é o `web.config` do site que a recebe, que esta equipa não escreve e
não controla. Ver [ADR-027](ADR/ADR-027-aplicacao-dentro-de-um-site-alheio.md).

A avaria é a mesma e chega com o mesmo disfarce, e é por isso que o quarto é o pior: os
outros três estão todos ao alcance de quem procura, e este está num ficheiro que quem
procura não tem razão nenhuma para abrir.

## Ligação à base de dados

A cadeia está na secção acima, a apontar para o caminho absoluto em
`C:\IdeaBoard\data\ideaboard.db`, com as chaves estrangeiras ativadas e um tempo
limite por omissão folgado, para que uma escrita concorrente espere em vez de falhar.

### Modo WAL

Executado **uma vez**, no aprovisionamento da base de dados:

```sql
PRAGMA journal_mode = WAL;
```

O modo WAL é persistente. Fica gravado no próprio ficheiro e mantém se através de
ligações, reinícios e republicações, portanto não precisa de ser aplicado a cada
arranque da aplicação.

Dá leituras concorrentes com uma escrita em curso, que é exatamente o padrão desta
aplicação: muita gente a ler o ranking, uma pessoa de vez em quando a votar.

#### Nenhum script o liga, e a aplicação recusa arrancar sem ele

Este passo é à mão e **não está dentro de nenhum dos scripts de `deploy`**, nem dos de SQL
nem dos de PowerShell. Quem provisiona uma base e salta esta linha vê o site não arrancar.

A razão não é esquecimento. Os scripts de SQL são gerados pelo EF Core a partir das
migrações, e uma base criada pelas ferramentas do EF Core já vem em WAL, portanto um
script gerado nunca traz este comando. Não é que falte nos três que existem hoje, é que
não pode aparecer em nenhum que seja gerado da mesma maneira.

Medido, e por dois caminhos que concordam. Não há instrução `PRAGMA` nenhuma nos três
ficheiros de `deploy`, de nenhum tipo, e uma base construída de raiz a partir do
`migracao.sql` responde `delete` a `PRAGMA journal_mode`.

Do outro lado, o `SqliteJournalModeVerifier` lê o modo no arranque e **recusa arrancar**
se não for WAL, nomeando o modo que encontrou. A mensagem dele diz qual é o comando que
falta e manda tratar o caso como rastilho e não como a avaria em si, porque uma base que
chegou ali sem WAL é uma base cujo provisionamento foi saltado, e vale a pena ver o que
mais foi saltado com ele. **Esse texto e esta secção têm de continuar a dizer o mesmo.**
O texto da mensagem vive no código e não aqui, portanto uma mudança de um lado pede a
mudança do outro.

## Publicação

### Os três scripts de `deploy`, e a ordem por que correm

A instalação é feita à mão, por uma pessoa, numa consola do PowerShell como
administrador. Os três scripts não a automatizam, dão lhe guardas.

| Script | O que faz | Escreve? |
|---|---|---|
| `verificar-servidor.ps1` | Lê o que a máquina já tem e imprime um relatório | Não, nada |
| `provisionar.ps1` | Cria as pastas, o pool, a aplicação e a origem do Event Log | Sim, só o que não existia |
| `publicar.ps1` | Produz o artefacto, confere o que lá está, e copia para o servidor | Sim, na pasta da aplicação |

#### Os valores desta instalação são as omissões, nos dois que escrevem

**O `provisionar.ps1` corre sem argumento nenhum** e provisiona o `SERVIDOR-APP`: a
aplicação `/IdeaBoard` dentro do `Default Web Site`, no pool próprio `IdeaBoardPool`,
sobre `C:\inetpub\IdeaBoard`. O `publicar.ps1` tem as omissões equivalentes para o
destino, o pool e a raiz de dados, e só o `-ArtefactPath` é obrigatório.

Isto muda como se usam, e muda em duas direcções. Numa, deixa de haver transcrição de
valores à mão, que é por onde entra um erro de uma letra num caminho que depois é apagado
por uma cópia espelhada. Noutra, **deixou de haver a rede que um parâmetro obrigatório
dava contra um arranque acidental**.

O que fica entre um arranque acidental e uma máquina alterada são as guardas, e não a
falta de argumentos: elevação, o site existir, o pool não existir, e não haver já uma
aplicação naquele caminho virtual. Todas recusam antes de criar seja o que for.

Cada script imprime todos os valores antes de mexer em nada, **dizendo de cada um se veio
da omissão ou da linha de comandos**, para que quem corra sem argumentos possa parar na
primeira linha errada em vez de descobrir no fim.

#### Os três esperam por uma tecla no fim

Começados por duplo clique ou por "Executar com o PowerShell", a consola pertence ao
script e fecha se com ele, levando o relatório do ecrã antes de alguém o ler. A espera
existe por isso, e é a última coisa que acontece, depois de o código de saída já estar
decidido.

Passar `-NoPause` salta a espera, para correr um destes a partir de outro script. Um
script começado com a entrada redirecionada também não espera, porque aí não há ninguém
para carregar na tecla e a espera ficaria pendurada para sempre.

A ordem completa de uma instalação de raiz:

1. **`verificar-servidor.ps1`.** Resolver o que ele apontar antes de continuar.
2. **`provisionar.ps1`.** Pool, aplicação dentro do site existente, pastas, permissões
   e autenticação. Corrido sem argumentos, usa os valores desta instalação, e imprime
   cada um a dizer se veio dele ou de quem o chamou, para se poder parar na primeira
   linha errada.
3. **Criar a base de dados**, com o script da secção das migrações.
4. **Ligar o modo WAL**, uma vez, conforme a secção acima. Este passo não está em script
   nenhum e é o mais fácil de saltar.
5. **`publicar.ps1`.** Produz o artefacto e copia o, parando e arrancando o pool.
6. **Verificação manual de [07](07-Plano-de-Verificacao.md)**, que é a parte que nenhum
   teste automático cobre.

O `publicar.ps1` arranca o pool no fim, se tiver sido ele a pará lo, portanto não há um
passo separado para arrancar. Entre o passo 2 e o passo 5 o endereço responde com erro,
por a pasta ainda não ter aplicação nenhuma. **O resto do site continua a servir**, que é
a diferença entre esta instalação e um site próprio.

### Quando um deles pára

**Param a dizer porquê, e não continuam.** É a forma deles e não acidente: o
`provisionar.ps1` recusa em vez de reparar, e nada do que ele já fez é desfeito.

| Pára a dizer | O que aconteceu, e o que fazer |
|---|---|
| Que a consola não é de administrador | Faltou elevação. Abrir outra consola, nada foi alterado |
| Que falta o ASP.NET Core Module v2, ou a Autenticação do Windows | Pré requisito por instalar. As duas causas dão o mesmo 500.19, por isso confirmar as duas |
| Que já existe um pool com esse nome, ou uma aplicação naquele caminho virtual | Escolher outro nome ou outro caminho, ou apagar o existente à mão depois de confirmar de quem é. O script não reconfigura o que encontra |
| Que o site que devia receber a aplicação não existe | O nome do site está errado, ou a máquina não é a que se pensava. O script não cria o site neste modo. A mensagem lista os sites que existem |
| Que o caminho virtual parece um caminho de disco | Ver o aviso sobre o Git Bash, a seguir a esta tabela |
| Que o pool ficou com as aplicações de 32 bits ligadas | A definição foi posta e não ficou. Nesse estado a aplicação arrancaria em silêncio como processo de 32 bits |
| Que a aplicação não aparece no site depois de criada, ou ficou no pool errado | O script releu o que criou e não bateu certo. Sem pool próprio não há o limite de um processo de trabalho |
| Que a autenticação não ficou como devia | O script releu o que escreveu e não bateu certo. Verificar à mão antes de continuar |
| Que uma secção de autenticação continua trancada | O `OverrideModeEffective` diz `Deny`. A mensagem nomeia as duas formas confirmadas de destrancar. Correr uma e repetir |
| Que o artefacto leva identidade local | É a invariante do `CLAUDE.md`. Não se publica assim, e o problema está na construção e não na publicação |
| Que o artefacto leva uma base de dados | A base vive em `C:\IdeaBoard\data` e nunca na pasta da aplicação, senão a publicação seguinte apaga a |
| Que falta uma variável no `web.config`, ou que está vazia | A aplicação recusa arrancar sem elas, e é melhor descobrir isso aqui do que no servidor |
| Que o destino não parece a pasta desta aplicação | Ver o parágrafo seguinte, antes de forçar seja o que for |
| Que o `robocopy` saiu com código 8 ou mais | Cópia falhada. O pool fica parado de propósito, para não arrancar sobre uma cópia a meio |
| Que o destino ficou com ficheiros que não estão no artefacto | A cópia espelhada não espelhou, e o código de saída não o disse. Quase sempre um ficheiro ainda aberto. Parar o pool e repetir |

Uma verificação que não conseguiu responder não é uma resposta negativa, e o
`verificar-servidor.ps1` conta as que ficaram por responder em vez de as dar por
ausentes. Resolver isso antes de provisionar, em vez de assumir o pior ou o melhor.

**Correr estes scripts a partir do PowerShell, e não de uma janela Git Bash.** O Git Bash
reescreve um argumento que comece por barra num caminho do Windows antes de o script o
ver, portanto `-VirtualPath /IdeaBoard` chega lá dentro como um caminho de disco. O
`provisionar.ps1` reconhece essa forma e pára a explicar o que aconteceu, em vez de criar
uma aplicação num caminho que ninguém escreveu.

#### O que o `verificar-servidor.ps1` responde e que o provisionamento já não precisa de descobrir

Ele lista, por cada site, as **aplicações lá dentro**, com o caminho virtual, o pool e a
pasta de cada uma. Saber quem é dono da porta 80 responde a uma pergunta; saber que
caminhos virtuais estão ocupados dentro desse site responde à que decide se a aplicação
pode nascer em `/IdeaBoard`.

Sem isso, quem descobre é o `provisionar.ps1`, a recusar, já no servidor, numa ida que
esta leitura teria poupado.

### Dois cuidados que este servidor exige e um servidor vazio não exigiria

Esta máquina já serve outra aplicação a pessoas reais, com autenticação integrada. As
duas coisas a seguir existem por causa disso e não devem ser afrouxadas.

**Nenhum comando global.** Não há `iisreset` em script nenhum, o serviço de publicação
nunca é parado, e nenhum pool ou site que não tenha sido criado por estes scripts é
arrancado, parado ou reconfigurado. Um comando global levaria a outra aplicação abaixo, e
quem corresse o script não teria motivo para ligar as duas coisas.

O `publicar.ps1` pára **um pool só, pelo nome**, e o nome vem da omissão
`IdeaBoardPool` quando não é dado. Este documento dizia que ele só parava um pool quando
lho davam, e isso deixou de ser verdade no dia em que as omissões entraram. A propriedade
que interessa sobrevive inteira, e é outra: o pool parado é sempre um só e é sempre
nomeado, nunca o do vizinho e nunca todos.

**A cópia é espelhada, e por isso o destino é confirmado antes.** O `robocopy` corre com
`/MIR`, que apaga no destino tudo o que não venha no artefacto. É o que uma pasta de
publicação precisa, e é um desastre se o caminho estiver errado por um carácter. Antes de
apagar seja o que for, o `publicar.ps1` recusa um destino dentro de `C:\IdeaBoard`, recusa
a raiz de uma unidade, e recusa uma pasta que tenha ficheiros mas não tenha o
`IdeaBoard.Web.dll`. Uma pasta vazia ou inexistente passa, por ser a primeira cópia.

#### O espelho pode não espelhar, e o código de saída não o diz

**Isto é medido e não receado**, e desmente a palavra "apaga" do parágrafo acima na forma
em que ela se lê.

Com um ficheiro do destino ainda aberto por outra coisa, o `robocopy` **não o conseguiu
apagar**, escreveu `ERROR 32` no registo dele, contou `FAILED` como **zero**, e saiu com
código **3**, que é "ficheiros copiados" mais "extras detectados" e fica abaixo do limiar
de falha. O script dava a cópia por feita e saía com zero. **Um resto de uma versão
anterior sobreviveu a uma publicação que se reportou bem sucedida, e ninguém foi avisado.**

A forma disto é própria do alojamento em processo: o processo de trabalho pode segurar um
ficheiro da versão anterior por um momento depois de o pool parar, e **um apagamento que
falha não é contado como falha pelo `robocopy` da mesma maneira que uma cópia que falha**.

Por isso o `publicar.ps1` passou a **comparar o destino com o artefacto** depois de
copiar, em vez de confiar no código de saída. A pergunta que isso responde é a que
interessa, que é se o destino é agora o que foi publicado, e não como é que o `robocopy`
escolheu codificar o que aconteceu.

Se ficar algum ficheiro a mais, o script pára e nomeia os. A causa habitual é um ficheiro
ainda aberto pela aplicação a correr, e a saída é parar o pool desta aplicação e repetir.
**O pool fica parado de propósito**, para não arrancar sobre uma mistura de versões.

### O destrancamento da autenticação, que agora se relê

O `provisionar.ps1` destranca as secções de autenticação ao nível da **aplicação**, com
`-Location Site/IdeaBoard`, porque o IIS as tranca ao nível do servidor por omissão e uma
aplicação cujo `web.config` as traga responde 500.19 em vez de arrancar.

**Escrever e nunca reler era a lacuna, e fechou a 17 de novembro de 2026.** As duas
aplicações vizinhas desta máquina fecham a nos guias delas, que exigem o
`OverrideModeEffective` a dizer `Allow` antes de se fazer o que quer que seja.

#### A armadilha é que este servidor não nos pode dizer se a nossa chamada fez alguma coisa

O desbloqueio global já foi corrido aqui uma vez, pela equipa do RepositorioVizinhoA, com
`appcmd unlock config`. **Portanto a aplicação arranca quer a nossa escrita tenha pegado
quer não**, e um passo que não faz nada passa despercebido precisamente na única máquina
onde não é preciso.

O dia em que isso se nota é o dia em que alguém instala isto num servidor limpo, e nessa
altura já ninguém se lembra deste passo. É essa a razão de serem lidos **dois** valores e
não um:

| Valor | O que diz |
|---|---|
| `OverrideModeEffective` | O que de facto se aplica a este alvo |
| `OverrideMode` | O que a **nossa** entrada diz, na nossa própria localização |

`Allow` e `Allow` quer dizer que a nossa entrada está a fazer o trabalho. `Allow` com
outra coisa qualquer quer dizer que vem herdado do desbloqueio global e que **a nossa
chamada não mudou nada**, o que aqui é aviso e não falha, e num servidor limpo seria
falha.

#### Os três desfechos, e nenhum é silencioso

**`OverrideModeEffective` a dizer `Deny`: o script pára.** Continuar seria provisionar
algo que não arranca. O guia do RepositorioVizinhoA traz duas formas confirmadas de destrancar, e
a mensagem de paragem nomeia as duas: o `appcmd unlock config` sobre a secção, ou o
`Set-WebConfiguration` com `-PSPath MACHINE/WEBROOT/APPHOST`. Correr uma delas e repetir.

**Efectivo a `Allow` com a nossa entrada a dizer outra coisa: aviso a amarelo.** É o caso
que esta máquina provavelmente dá. Aqui não faz mal e o site arranca, e num servidor limpo
faria, porque este passo não estaria a destrancar nada.

**A releitura a falhar: nem uma coisa nem outra, e dito a cheio.** Uma leitura que não
responde não é prova de que a escrita falhou, e parar aí deixaria uma máquina com o pool,
as pastas e a aplicação já criados. O script diz alto que não conseguiu responder e
continua, com o sintoma à frente para quem tiver de voltar ao assunto.

#### Uma medição dos vizinhos que é sobre outra chamada, e não sobre a nossa

O guia do RepositorioVizinhoA regista, a 7 de setembro de 2026, que o `Set-WebConfiguration`
contra `IIS:\` **sem `-Location`** falha em silêncio a destrancar, e que o alvo de que
precisa é `MACHINE/WEBROOT/APPHOST`.

**Essa medição não é sobre a chamada que fazemos.** A deles não leva `-Location` e a nossa
leva, e escrever numa entrada de localização não é a mesma coisa que destrancar na raiz. A
nossa pode muito bem estar certa como está.

Fica escrito por uma razão prática: é a releitura que vai dizer, e é por isso que **não se
muda a chamada por palpite**. Uma correcção aplicada a partir de uma medição sobre outra
coisa é indistinguível de uma correcção certa, até ao dia em que não é.

#### O sintoma, se tudo isto falhar

Um **500.19 cuja mensagem fala de uma secção que não pode ser usada neste caminho**.
Distingue se dos outros 500.19 pela mensagem e não pelo código, e é nesse passo do
`provisionar.ps1` que se mexe.

### A republicação de uma aplicação já instalada

1. **Backup da base de dados**, pelo procedimento descrito mais abaixo. Antes de
   qualquer outra coisa, incluindo publicações que não mudam o esquema.
2. **Parar o pool de aplicações**, e só esse, pela razão a seguir à lista.
3. **Copiar os ficheiros publicados** para `C:\inetpub\IdeaBoard\`.
4. **Aplicar as migrações** com o script SQL gerado a partir das migrações do EF Core,
   sempre com `-bail`, pela razão da secção seguinte.
5. **Arrancar o pool**, que é o que foi parado no passo 2.
6. **Verificar** com a lista de verificação de [07](07-Plano-de-Verificacao.md).

#### Porque é o pool e não o site, e porque isto é uma decisão e não uma medição

Esta lista mandava parar o site. Foi alinhada com o `publicar.ps1`, que pára o pool, e a
decisão é do orquestrador, tomada a 17 de novembro de 2026.

**O raciocínio é sobre quem segura os ficheiros.** Quem os tem abertos é o processo de
trabalho, e esse pertence ao pool e não ao site. Parar o site pára o atendimento de
pedidos e **não termina o processo de trabalho**, sobretudo se o pool servir mais do que
um site, portanto os ficheiros continuam bloqueados na mesma e a cópia falha pela razão
que parar servia para evitar.

Que o 503 durante a cópia se leia melhor do que uma ligação recusada é verdade e não é o
que decide. É um argumento sobre o sintoma, e o outro é sobre a causa.

##### Uma propriedade perdeu se nesta troca, e a perda foi deliberada

A frase antiga não protegia só os ficheiros. Dizia **parar o site para não entrarem
pedidos a meio**, que é uma preocupação diferente, e essa deixou de estar coberta. Com o
pool parado e o site de pé, os pedidos continuam a chegar ao IIS e levam 503 em vez de não
encontrarem nada.

Numa aplicação interna, durante uma publicação, a diferença entre um 503 e uma ligação
recusada é indiferente, e é por isso que a decisão se mantém apesar disto.

**Fica escrito porque uma garantia que se fecha em silêncio volta como discussão.** Daqui
a seis meses, alguém que veja o 503 a aparecer durante as publicações tem duas leituras
disponíveis: que foi decidido, ou que foi descuido. Sem esta linha, as duas são igualmente
defensáveis e a segunda é a mais fácil de acreditar.

> **ISTO NÃO FOI MEDIDO, E A DISTINÇÃO É O PONTO.** Nem quem decidiu nem quem escreve isto
> tem IIS para o confirmar. É um raciocínio sobre como o IIS funciona, e um raciocínio
> coerente não é a mesma coisa que uma observação.
>
> A confirmação está na lista de verificação manual de
> [07](07-Plano-de-Verificacao.md), para ser fechada numa máquina a sério. **Se ela disser
> o contrário, a decisão inverte se e era o documento que estava certo.** Fica escrito
> assim para que, nesse dia, ninguém tenha de descobrir que havia uma decisão por trás
> disto.

### Migrações

O script é gerado a partir das migrações e versionado junto com elas:

```
dotnet ef migrations script --project src/IdeaBoard.App --output deploy/migracao.sql
```

Aplicado de forma explícita, com supervisão, e nunca automaticamente no arranque da
aplicação. Uma aplicação que migra sozinha altera o esquema sem backup prévio, sem
ninguém a olhar, e no pior momento possível, que é durante um reinício não planeado.

#### O script não é idempotente, e não pode ser

Não existe a opção de o tornar idempotente. O EF Core recusa a para SQLite, com esta
mensagem:

```
Generating idempotent scripts for migrations is not currently supported for SQLite.
```

A consequência prática é que o script tem de ser aplicado uma vez e só uma vez, contra
uma base de dados cujo estado de migração é conhecido. Aplicá lo duas vezes falha, e
falha a meio.

#### `-bail` faz parte do procedimento, e não é uma opção

O script abre uma transação, mas a transação **não chega por si**. A linha de comandos
do `sqlite3` continua a executar depois de uma instrução falhar, e o `COMMIT` no fim
confirma o que correu bem até ali.

Isto foi medido. Um script com `BEGIN TRANSACTION`, um `ALTER TABLE` válido, um
`CREATE TABLE` que falha, um `INSERT` e um `COMMIT`:

| Execução | Resultado |
|---|---|
| Sem `-bail` | A coluna fica criada e a linha fica inserida. Trabalho parcial commitado |
| Com `-bail` | Nada fica. A base de dados mantém se como estava |

Sem `-bail`, uma migração que falha a meio deixa o esquema num estado que não é nem o
anterior nem o seguinte, e que nenhum script sabe reparar.

##### O comando, na forma que a consola do servidor aceita

```
Get-Content deploy\migracao.sql | sqlite3 -bail "C:\IdeaBoard\data\ideaboard.db"
```

**A forma com `<` não funciona numa consola do PowerShell.** O `<` é um operador reservado
e o comando nem chega a correr: a consola responde `The '<' operator is reserved for
future use.` e nada acontece. Medido no Windows PowerShell 5.1, que é o que está nestas
máquinas, e [07](07-Plano-de-Verificacao.md) já regista que o `pwsh` pode não existir
nelas.

Isto é fácil de ler como um pormenor e não é. O erro acontece antes de a base ser tocada,
portanto não estraga nada, mas quem o vê tem o script à frente a dizer `<` e tende a
procurar o problema no `sqlite3` ou no ficheiro, que estão os dois bem.

A forma acima foi medida: aplica as três migrações a uma base nova, sai com código zero, e
com `-bail` continua a parar na primeira instrução que falha e a sair com código 1. O BOM
que os ficheiros gerados trazem no início passa pelo tubo sem estorvar.

Quem preferir a forma com `<` corre a pelo `cmd`, que a aceita, e que também foi medido:

```
cmd /c "sqlite3 -bail C:\IdeaBoard\data\ideaboard.db < deploy\migracao.sql"
```

#### Qual dos três scripts aplicar, e é preciso escolher

A pasta `deploy` tem **três** ficheiros, e aplicar o errado falha.

| Ficheiro | Para que base de dados |
|---|---|
| `migracao.sql` | Uma base de dados **nova**. Contém tudo, incluindo o aviso de votos novos e os comentários |
| `migracao-002-aviso-de-votos-novos.sql` | Uma base **já em uso** que tem apenas a migração inicial |
| `migracao-003-comentarios.sql` | Uma base **já em uso** que tem a inicial e a do aviso de votos novos |

Numa base já em uso o script completo falha logo na primeira instrução, por as tabelas já
existirem, e é para esse caso que os outros dois ficheiros existem. Eles empilham: uma
base parada na migração inicial leva o 002 e depois o 003, por esta ordem.

O enquadramento é o mesmo das outras migrações, por verificação e não por idempotência:

1. Ler a `__EFMigrationsHistory` antes de aplicar. Se a migração do script já lá estiver,
   `20261116185858_AvisoDeVotosNovos` ou `20261117102626_Comentarios`, não aplicar nada.
2. Aplicar com `-bail`.
3. Confirmar o código de saída igual a zero.

**O primeiro passo não é formalidade, e isso está medido.** Aplicar o 003 a uma base que
já o tem pára na criação da tabela, com `table "Comment" already exists`, e sai com código
1. Medido aqui, sobre uma base construída a partir do `migracao.sql` anterior aos
comentários: a primeira aplicação sai com zero e a `__EFMigrationsHistory` fica com as
três linhas, e a segunda sai com 1. É o comportamento esperado de um script que não é
idempotente, e é exactamente por isso que a verificação está antes e não depois.

**O 003 já foi aplicado uma vez a uma base de dados em uso**, com este procedimento, e o
código de saída foi zero. Reportado pelo orquestrador, que o fez, e é a primeira vez que
um destes scripts correu contra uma base real em vez de ter sido apenas gerado. Fica com
a proveniência à vista porque não é uma medição minha.

A coluna `lastNotificationsSeenAt` entra a `NULL` em todas as linhas existentes, e isso é
deliberado. `NULL` quer dizer que a pessoa nunca abriu a página que lhe mostra os votos
recebidos, o que é verdade para toda a gente no momento em que a coluna passa a existir.
Um valor por omissão com a data desse dia declararia em silêncio que toda a gente tinha
acabado de ler tudo.

## Backup e restauro

### Backup

Diário, fora do horário laboral, para `C:\IdeaBoard\backup\`, com retenção de trinta
dias.

```sql
VACUUM INTO 'C:\IdeaBoard\backup\ideaboard-AAAAMMDD.db';
```

**Não copiar o ficheiro `.db` com o explorador ou com `copy`.** Com o modo WAL ativo,
parte do estado recente vive no ficheiro `-wal`, e uma cópia simples do ficheiro
principal produz um backup silenciosamente incompleto, que parece válido até ao dia em
que é preciso.

### Restauro

1. Parar o site.
2. Mover o ficheiro atual e os seus `-wal` e `-shm` para um lado, sem os apagar.
3. Copiar o backup para `C:\IdeaBoard\data\ideaboard.db`.
4. Confirmar as permissões da pasta.
5. Arrancar o site e verificar.

**O restauro tem de ser ensaiado pelo menos uma vez antes do lançamento.** Um backup
que nunca foi restaurado é uma suposição, não uma salvaguarda.

## Métricas dos critérios de sucesso

O briefing pede três métricas na avaliação aos três meses e exclui administração do
âmbito. As consultas são corridas à mão contra uma **cópia** da base de dados, nunca
contra o ficheiro em utilização.

**Número de ideias submetidas:**

```sql
SELECT COUNT(*) AS totalIdeias FROM Idea;
```

**Número de pessoas distintas que submeteu pelo menos uma ideia**, que é o que
distingue uma comunidade de um monólogo:

```sql
SELECT COUNT(DISTINCT authorUserId) AS autoresDistintos FROM Idea;
```

**Número de pessoas distintas que votou pelo menos uma vez:**

```sql
SELECT COUNT(DISTINCT userId) AS votantesDistintos FROM Vote;
```

### Cuidado com o denominador da percentagem

O briefing pede a **percentagem** de pessoas que votou pelo menos uma vez. A tabela
`User` só contém quem iniciou sessão pelo menos uma vez, e não a equipa toda.

Dividir os votantes pelas linhas de `User` responde a uma pergunta diferente e mais
benévola: de quem entrou, quantos votaram. Quem nunca entrou desaparece da conta, e a
participação parece maior do que é.

O denominador correto é a dimensão real da área de TI e produto, que vem de fora da
aplicação. A consulta seguinte dá o numerador e a base interna, e o denominador
verdadeiro é escrito à mão:

```sql
SELECT
    (SELECT COUNT(DISTINCT userId) FROM Vote) AS votantesDistintos,
    (SELECT COUNT(*) FROM User)               AS pessoasQueEntraram;
```

A diferença entre as duas colunas é informação útil por si só. Muita gente a entrar e
pouca a votar é um problema diferente de pouca gente a entrar.

## Correção manual de dados

Não há administração na aplicação, por decisão do briefing. Ver
[ADR-008](ADR/ADR-008-sem-administracao.md). A correção de um erro, como um título
mal escrito ou uma ideia submetida por engano, é um procedimento manual e deliberado.

O briefing antecipa que esta é a decisão com maior probabilidade de se tornar
incómoda primeiro, e este procedimento existe para que a resposta seja controlada em
vez de improvisada.

### Regras

1. **Só a pedido de quem escreveu a ideia.** Ninguém corrige o texto de outra pessoa.
2. **Backup antes**, sempre, mesmo para alterar uma letra.
3. **Uma instrução de cada vez, com `WHERE` pelo identificador.** Nunca uma atualização
   sem filtro, nunca por correspondência de título.
4. **Dentro de uma transação explícita**, com verificação antes de confirmar, e com
   `ROLLBACK` como saída por omissão. Só se escreve `COMMIT` depois de a verificação
   mostrar exatamente o que se esperava.
5. **Registar o que foi feito**, com data, quem pediu, quem executou e a instrução
   exata. Um ficheiro de registo na pasta de operação chega.
6. **Votos nunca são alterados.** Acrescentar, remover ou transferir votos à mão
   falsifica o único resultado que a plataforma produz. Se uma ideia tiver de
   desaparecer, desaparece com os seus votos.

### Sessão manual

Abrir sempre com `-bail`, pela mesma razão da secção das migrações. Sem ele, uma
instrução que falha não interrompe a sessão, e o `COMMIT` seguinte confirma o que correu
bem até ali:

```
sqlite3 -bail "C:\IdeaBoard\data\ideaboard.db"
```

A ligação pela linha de comandos do `sqlite3` **não ativa chaves estrangeiras por
omissão**, ao contrário das ligações abertas pela aplicação. Numa sessão manual, o
primeiro comando é sempre:

```sql
PRAGMA foreign_keys = ON;
```

Sem isto, um apagamento deixa votos órfãos a apontar para uma ideia que já não existe,
e o erro só aparece mais tarde, na forma de contagens que não batem certo.

### A forma de uma correção

A verificação acontece **dentro** da transação, antes de confirmar, e a saída por
omissão é o `ROLLBACK`:

```sql
PRAGMA foreign_keys = ON;
BEGIN TRANSACTION;

UPDATE Idea SET title = 'título corrigido' WHERE id = 42;

SELECT id, title FROM Idea WHERE id = 42;
SELECT changes() AS linhasAfetadas;

-- Confere? COMMIT. Não confere, ou mexeu em mais linhas do que as esperadas? ROLLBACK.
ROLLBACK;
```

`changes()` é a salvaguarda que apanha o erro mais caro desta operação, que é um `WHERE`
que apanha mais linhas do que se pensava. Uma correção a uma ideia afeta uma linha. Se
o número for outro, o `ROLLBACK` já está escrito.

## Saída de colaboradores

Quando alguém sai da organização, a conta deixa de autenticar e o acesso termina de
imediato, sem necessidade de qualquer ação na aplicação.

As ideias e os votos dessa pessoa continuam visíveis com o nome, conforme
[04](04-Requisitos-Nao-Funcionais-e-Seguranca.md). Um pedido individual de remoção é
tratado como correção manual, pelo procedimento acima, depois de confirmado com quem
trata de privacidade na organização.

## Diagnóstico

### Antes da tabela: uma avaria que aparece sem nada nosso ter mudado

**Passou a existir quem nos parta sem tocar neste repositório.** Uma publicação do
vizinho, uma alteração ao `web.config` dele, uma mudança de autenticação ou de ligações ao
nível do site: qualquer uma muda o comportamento desta aplicação sem que uma linha daqui
mude.

E há uma assimetria que decide quanto isso custa. **O sintoma aparece no nosso endereço,
com o nosso nome em cima**, e o primeiro sítio onde alguém vai procurar é este
repositório, onde não mudou nada.

Portanto, perante uma avaria sem alteração nossa, e **antes** de procurar uma regressão no
código:

1. Ver a **data do `web.config` do site que nos recebe**, e a configuração do site.
2. Confirmar que as duas secções de autenticação continuam destrancadas e que o papel
   `Web-Windows-Auth` continua instalado.

O segundo ponto tem uma razão que não é óbvia e que está no
[ADR-027](ADR/ADR-027-aplicacao-dentro-de-um-site-alheio.md): **nenhuma das duas coisas
foi feita por nós nem para nós.** Foram feitas por outra equipa, por outra razão, meses
antes deste projeto existir, e quem as fez não tem razão nenhuma para saber que
dependemos delas. Voltar a trancar as secções, ou remover o papel, parte nos sem tocar em
nada que seja nosso.

São trinta segundos de verificação que só ocorrem a quem souber que existe um vizinho, e
daqui a um ano isso não vai estar na cabeça de ninguém. É por isso que está escrito como
primeiro passo e não como nota.

### A tabela

Os sintomas que esta configuração produz quando está errada, e o que procurar primeiro.

| Sintoma | Causa provável |
|---|---|
| Erro de base de dados só de leitura | Falta permissão de escrita **na pasta**, e não no ficheiro |
| O site não arranca e a mensagem fala de `journal mode` | O modo WAL não foi ligado no provisionamento. Nenhum script o liga, e é o passo 4 da ordem de instalação |
| Pessoas atiradas para fora da sessão ao acaso, ou erros em submissões | Chaves de Data Protection não persistidas. Ver [ADR-009](ADR/ADR-009-publicacao-iis.md) |
| Bloqueios ou tempos limite em escritas | Mais de um processo de trabalho no pool de aplicações |
| Erro 500.19 ou 502 no arranque | Hosting Bundle em falta ou desatualizado, **ou** o serviço de função Autenticação do Windows do IIS por instalar. As duas causas dão o mesmo código |
| 500.19 cuja mensagem fala de uma **secção que não pode ser usada neste caminho** | A secção de autenticação continua trancada ao nível do servidor. O `deploy/provisionar.ps1` destranca a para este site, e é lá que se mexe. Esta é a causa que se distingue das outras duas pela mensagem, e não pelo código |
| O site não arranca e a mensagem nomeia `ConnectionStrings:IdeaBoard` ou `DataProtection:KeysPath` | As variáveis do `web.config` não chegaram, ou alguém publicou por cima de um `appsettings.Production.json` que as trazia |
| Caminho de base de dados na mensagem de erro que ninguém escreveu | O mesmo nome de variável definido no `web.config` **e** no ambiente do sistema ou do pool |
| Nada da aplicação aparece no Event Log, com tudo o resto a funcionar | A origem `IdeaBoard` não foi criada no servidor |
| Toda a gente recusada no primeiro acesso | Autenticação anónima ligada no site, ou autenticação Windows desligada |
| O browser pede credenciais em vez de entrar sozinho | O endereço não está na zona de intranet local do browser. Ver [ADR-024](ADR/ADR-024-http-sem-certificado.md) |
| A pessoa chega à página do nome | **Não é avaria.** É o comportamento correcto de quem ainda não tem linha, e ver esta página prova que a identidade do Windows chegou bem |
| Regista o nome, é aceite, e no pedido seguinte é lhe pedido outra vez | A escrita e a leitura não estão no mesmo ficheiro. Ver a cadeia de ligação do `web.config` contra o ficheiro que está mesmo a ser escrito |
| Ao ser mandada para o nome, a pessoa aterra fora da aplicação | O caminho base não foi à frente do redirecionamento. Corrigido em `4f1a3ad`, e é a única regressão possível neste ponto |
| Nome de utilizador a aparecer como autor, na forma `DOMINIO\pessoa` | Nome registado à mão a contornar a validação, que recusa barra invertida |
| Dados desaparecidos depois de uma publicação | Base de dados dentro da pasta da aplicação |
| Backup que restaura sem os últimos dias | Cópia do ficheiro em vez de `VACUUM INTO` |

Os detalhes das exceções estão no Event Log do Windows, com identificador de
correlação, conforme [04](04-Requisitos-Nao-Funcionais-e-Seguranca.md).

### A ordem de diagnóstico que outra equipa pagou um dia para aprender

Está no `RepositorioVizinhoA/Docs/Instalation/INSTALLATION-IIS.md`, na secção sobre uma pessoa
reconhecida e sem conta na aplicação. **É afirmação deles, datada de 13 de outubro de
2026**, lida pelo ARQ a 17 de novembro, e não medição nossa. Fica aqui resumida, com o
apontador, e não copiada.

**O valor dela não está nos passos, está no passo zero.**

> **Enquanto a pessoa chega à aplicação, o Kerberos fez o trabalho dele.**

Uma mensagem a dizer que a pessoa não tem conta só é alcançável **depois** de o servidor
ter negociado e entregue uma identidade verdadeira. Quem lê isso e vai investigar o
Kerberos está a investigar a camada que acabou de funcionar. Foi aí que o dia se perdeu:
a excluir, uma a uma, as hipóteses da camada errada.

Só se investiga SPN, replicação do Active Directory, desvio de relógio ou restrições de
NTLM **depois** de a base de dados estar excluída. São todas causas reais de autenticação
integrada partida, e nenhuma era a causa naquela vez.

#### O que transfere para esta aplicação e o que não transfere

**Não transfere** o passo de confirmar que a linha da pessoa existe. A aplicação deles
exige a linha criada à mão antes de alguém entrar, e esta cria a linha sozinha. Mandar
procurar uma linha que este produto não exige manda a pessoa procurar o que não há.

**Transfere inteiro** o passo de confirmar que a aplicação está a ler a base de dados que
julgamos. A cadeia de ligação está no `web.config`, versionada, e **um caminho válido mas
errado passa as duas guardas de arranque**: elas confirmam que a cadeia existe, que o
caminho é absoluto, que está fora da pasta da aplicação e que o ficheiro está em WAL.
Nenhuma delas confirma que é o ficheiro **certo**.

O sintoma nesta aplicação não é um erro, é uma repetição: a pessoa regista o nome, é
aceite, e no pedido seguinte volta a ser lhe pedido. A escrita foi para um ficheiro e a
leitura veio de outro.

#### A linha que estava aqui e era falsa

Este documento dizia que um ciclo infinito de redirecionamentos para `/nome` vinha de uma
linha de utilizador existir sem nome. **As duas metades eram falsas**, e a verificação foi
feita no código e não por raciocínio.

O ciclo não é possível. O portão redireciona quando a resolução do nome devolve nulo, e no
`UserProvisioningService` há **um único** `return null`, alcançável apenas quando não há
linha **e** nenhum nome foi obtido. Com linha, o método devolve sempre a linha. Do outro
lado, a página do nome manda para o início exactamente quando a linha existe. As duas
condições são complementares sobre o mesmo predicado, portanto não há estado em que as
duas redirecionem.

A causa também não existia. Uma linha sem nome não se cria, porque o caminho automático só
chama a criação depois de confirmar que o nome não está vazio. E se uma aparecesse por
escrita directa na base, o portão **deixava a pessoa passar na mesma**, porque o que ele
pergunta é se existe linha e não se ela tem nome.

Fica registado em vez de apagado porque o erro tem forma reconhecível: era uma causa
plausível, escrita sem ir ao código, que teria mandado quem a lesse procurar na tabela de
utilizadores por uma linha que não podia estar lá.
