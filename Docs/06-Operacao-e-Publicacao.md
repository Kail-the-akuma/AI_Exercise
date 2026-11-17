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
autenticação lá dentro, e o IIS tranca essa secção ao nível do servidor, por isso um site
cujo `web.config` a traga pode responder 500.19 em vez de arrancar. O
`deploy/provisionar.ps1` destranca a com `overrideMode` a `Allow` e **com `-Location` do
site**, portanto só para este site e sem mexer no que vale para os outros.

**Nenhum de nós verificou isto numa máquina com IIS**, nem quem escreveu o script nem
quem escreve esta linha, e a referência do IIS consultada não diz qual é o valor por
omissão. Fica como está em vez de ser afirmado: o gatilho observável é um 500.19 cuja
mensagem fale de uma secção que não pode ser usada neste caminho, em vez de falar do
módulo. É aí que esta causa é a certa, e é no script que se mexe.

### Pool de aplicações

| Definição | Valor | Porquê |
|---|---|---|
| Versão do .NET CLR | Sem Código Gerido | A aplicação não usa o CLR do IIS |
| Máximo de processos de trabalho | **1** | O SQLite aceita um escritor. Mais do que um processo produz bloqueios e corrupção de expectativas. Ver [ADR-002](ADR/ADR-002-sqlite.md) |
| Modo de arranque | AlwaysRunning | Evita o primeiro pedido lento do dia |
| Tempo limite de inatividade | 0 | O mesmo motivo |
| Reciclagem por intervalo regular | 0 | Desliga a reciclagem a cada 29 horas, que cai a meio do dia de trabalho |
| Reciclagem a hora fixa | Fora do horário laboral | Se for mesmo necessária |

A limitação a um processo de trabalho é a definição mais fácil de esquecer e a mais
difícil de diagnosticar depois. Fica registada aqui e é item da lista de verificação
em [07](07-Plano-de-Verificacao.md).

### Site

HTTP, no nome curto do servidor com porta. O `web.config` é gerado pela publicação e não
deve ser editado à mão no servidor, porque a publicação seguinte sobrepõe o.

| Definição de autenticação | Valor |
|---|---|
| Autenticação Windows | **Ativada** |
| Autenticação anónima | **Desligada** |

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

## Publicação

1. **Backup da base de dados**, pelo procedimento descrito mais abaixo. Antes de
   qualquer outra coisa, incluindo publicações que não mudam o esquema.
2. **Parar o site** no IIS, não apenas o pool, para que não entrem pedidos a meio.
3. **Copiar os ficheiros publicados** para `C:\inetpub\IdeaBoard\`.
4. **Aplicar as migrações** com o script SQL gerado a partir das migrações do EF Core,
   sempre com `-bail`, pela razão da secção seguinte.
5. **Arrancar o site.**
6. **Verificar** com a lista de verificação de [07](07-Plano-de-Verificacao.md).

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
| `sqlite3 base.db < migracao.sql` | A coluna fica criada e a linha fica inserida. Trabalho parcial commitado |
| `sqlite3 -bail base.db < migracao.sql` | Nada fica. A base de dados mantém se como estava |

Sem `-bail`, uma migração que falha a meio deixa o esquema num estado que não é nem o
anterior nem o seguinte, e que nenhum script sabe reparar. O comando é portanto:

```
sqlite3 -bail "C:\IdeaBoard\data\ideaboard.db" < deploy\migracao.sql
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

Os sintomas que esta configuração produz quando está errada, e o que procurar primeiro.

| Sintoma | Causa provável |
|---|---|
| Erro de base de dados só de leitura | Falta permissão de escrita **na pasta**, e não no ficheiro |
| Pessoas atiradas para fora da sessão ao acaso, ou erros em submissões | Chaves de Data Protection não persistidas. Ver [ADR-009](ADR/ADR-009-publicacao-iis.md) |
| Bloqueios ou tempos limite em escritas | Mais de um processo de trabalho no pool de aplicações |
| Erro 500.19 ou 502 no arranque | Hosting Bundle em falta ou desatualizado, **ou** o serviço de função Autenticação do Windows do IIS por instalar. As duas causas dão o mesmo código |
| 500.19 cuja mensagem fala de uma **secção que não pode ser usada neste caminho** | A secção de autenticação continua trancada ao nível do servidor. O `deploy/provisionar.ps1` destranca a para este site, e é lá que se mexe. Esta é a causa que se distingue das outras duas pela mensagem, e não pelo código |
| O site não arranca e a mensagem nomeia `ConnectionStrings:IdeaBoard` ou `DataProtection:KeysPath` | As variáveis do `web.config` não chegaram, ou alguém publicou por cima de um `appsettings.Production.json` que as trazia |
| Caminho de base de dados na mensagem de erro que ninguém escreveu | O mesmo nome de variável definido no `web.config` **e** no ambiente do sistema ou do pool |
| Nada da aplicação aparece no Event Log, com tudo o resto a funcionar | A origem `IdeaBoard` não foi criada no servidor |
| Toda a gente recusada no primeiro acesso | Autenticação anónima ligada no site, ou autenticação Windows desligada |
| O browser pede credenciais em vez de entrar sozinho | O endereço não está na zona de intranet local do browser. Ver [ADR-024](ADR/ADR-024-http-sem-certificado.md) |
| Ciclo infinito de redirecionamentos para `/nome` | A linha de utilizador existe sem nome, o que não devia acontecer, porque a linha só nasce com nome |
| Nome de utilizador a aparecer como autor, na forma `DOMINIO\pessoa` | Nome registado à mão a contornar a validação, que recusa barra invertida |
| Dados desaparecidos depois de uma publicação | Base de dados dentro da pasta da aplicação |
| Backup que restaura sem os últimos dias | Cópia do ficheiro em vez de `VACUUM INTO` |

Os detalhes das exceções estão no Event Log do Windows, com identificador de
correlação, conforme [04](04-Requisitos-Nao-Funcionais-e-Seguranca.md).
