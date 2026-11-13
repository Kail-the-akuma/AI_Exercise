# 06. Operação e publicação

**Versão 2, 13 de novembro de 2026.**

Destina se a quem publica e mantém a aplicação no servidor interno. Os caminhos usam
o nome de trabalho `IdeaBoard` e têm de ser ajustados ao nome final.

## Provisionamento no Entra ID

> **Esta secção inteira está pendente de substituição, e não deve ser executada.**
> A 13 de novembro de 2026 a autenticação mudou de Microsoft Entra ID para autenticação
> Windows por Negotiate no IIS. Deixa de haver registo de aplicação, client secret e URI
> de redirecionamento de qualquer espécie.
>
> **Ninguém deve pedir este registo à equipa de identidade.** O pedido já não tem objeto,
> e fazê lo gasta o tempo de outra equipa a provisionar o que não vai ser usado.
>
> O que substitui esta secção depende do ADR que substitui o
> [ADR-003](ADR/ADR-003-autenticacao-entra-id.md), que ainda não está escrito e pertence
> ao ARQ. O texto abaixo fica como está, e não alinhado com o desenho novo, porque
> documentar uma autenticação que ainda não existe em código nem em ADR seria registar
> uma intenção e não o que foi construído.

Registo de aplicação de um só tenant, no tenant corporativo.

| Definição | Valor |
|---|---|
| Tipo de conta suportado | Apenas contas desta organização |
| URI de redirecionamento | `https://{nome-do-site}/signin-oidc`, do tipo Web |
| URL de saída pós sessão | `https://{nome-do-site}/signout-oidc` |
| Permissões | Apenas as de início de sessão e leitura do perfil básico |

Nenhuma permissão do Microsoft Graph é necessária. A aplicação não consulta o Graph em
momento nenhum, e pedir permissões a mais torna a aprovação mais lenta sem benefício.

**Client secret**: a confirmar antes de pedir um. Uma aplicação que apenas autentica
pode não precisar de segredo, e nesse caso basta ter a emissão de token de
identificação ativada no registo. Ver
[ADR-003](ADR/ADR-003-autenticacao-entra-id.md). Se for necessário, o valor não entra
no repositório, fica na configuração do servidor, e a data de expiração tem de ficar
registada em sítio onde alguém a veja antes de expirar.

Os valores de `TenantId` e `ClientId` vão para a configuração do servidor, não para o
`appsettings.json` versionado.

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

## Configuração do IIS

Pré requisito na máquina: **ASP.NET Core Hosting Bundle** correspondente ao .NET 10,
que instala o ASP.NET Core Module v2. Sem ele o site responde com erro 500.19 ou 502.

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

HTTPS com redirecionamento de HTTP, e **HSTS desligado até a Fase 6 estar concluída**.
O `web.config` é gerado pela publicação e não deve ser editado à mão no servidor,
porque a publicação seguinte sobrepõe o.

### Certificado

A aplicação arranca com um certificado emitido pela própria equipa, conforme
[ADR-013](ADR/ADR-013-certificado-no-arranque.md), e é substituído por um certificado
confiado na Fase 6.

**Por ordem de preferência:**

1. **Autoridade certificadora interna da organização**, se existir. É o melhor caso,
   porque as máquinas do domínio já confiam nela e não há aviso nenhum. Vale sempre a
   pena perguntar antes de assumir que não existe.
2. **Auto assinado**, criado no próprio servidor.

```powershell
New-SelfSignedCertificate `
    -DnsName "{nome-dns-do-site}" `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -FriendlyName "IdeaBoard" `
    -NotAfter (Get-Date).AddYears(2)
```

Depois, associar o certificado ao site no IIS, na porta 443, com o mesmo nome.

**O nome tem de ser o nome DNS definitivo.** Não o nome da máquina, não um endereço
IP. É o mesmo nome que vai no URI de redirecionamento do registo do Entra ID, e o
mesmo que vai no certificado de substituição mais tarde.

**O Entra ID não é afetado por o certificado não ser confiado.** O Entra ID nunca liga
à aplicação. Quem liga é o browser, que devolve o token por POST depois de a pessoa
aceitar a exceção de segurança. O início de sessão funciona na mesma.

### HSTS, e porque fica desligado

Com HSTS ativo, um browser que encontre um erro de certificado **termina a ligação sem
dar opção de prosseguir**. O botão de continuar deixa de existir, e não há forma de o
utilizador contornar.

Ativar o HSTS enquanto o certificado for auto assinado deixa portanto o site
inacessível a toda a gente, incluindo a quem o publicou. Pior, o browser guarda a
instrução durante o tempo declarado, portanto desligar o HSTS no servidor não
desbloqueia de imediato quem já a recebeu.

O HSTS é ativado na Fase 6, depois do certificado confiado estar no lugar, e nunca
antes.

## Ligação à base de dados

Cadeia de ligação na configuração do servidor, a apontar para o caminho absoluto em
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
| Erro 500.19 ou 502 no arranque | Hosting Bundle em falta ou desatualizado |
| Ciclo infinito de redirecionamentos no início de sessão | URI de redirecionamento do registo diferente do URL real do site, ou site a ser servido em HTTP |
| Site inacessível, sem opção de prosseguir no aviso de certificado | HSTS ativado antes de haver certificado confiado. Ver a secção do HSTS acima |
| Aviso de segurança do browser a cada acesso | Esperado até à Fase 6. Deixa de ser esperado depois dela |
| Dados desaparecidos depois de uma publicação | Base de dados dentro da pasta da aplicação |
| Backup que restaura sem os últimos dias | Cópia do ficheiro em vez de `VACUUM INTO` |

Os detalhes das exceções estão no Event Log do Windows, com identificador de
correlação, conforme [04](04-Requisitos-Nao-Funcionais-e-Seguranca.md).
