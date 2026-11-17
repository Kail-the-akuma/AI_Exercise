# 07. Plano de verificação

**Versão 2, 13 de novembro de 2026.**

## Três níveis, sem sobreposição

Cada nível responde a uma pergunta diferente. Um teste que responda à pergunta de
outro nível está no sítio errado, e o custo disso é uma suite lenta que falha em
vários sítios pelo mesmo motivo.

| Nível | Projeto | Pergunta a que responde |
|---|---|---|
| Unidade | `IdeaBoard.Tests` | As regras estão certas? |
| Integração | `IdeaBoard.Tests` | A base de dados faz o que assumimos? |
| Ponta a ponta | `IdeaBoard.E2E` | Uma pessoa consegue usar isto? |

xUnit em todos, para que a solução tenha um único framework de teste.

---

## Unidade

Sem base de dados, sem servidor, sem browser. Regras de domínio em isolamento.

- Validação dos limites de título e de descrição, incluindo o caso do texto feito só
  de espaços.
- Remoção de espaços no início e no fim antes de validar.
- Critério de ordenação do ranking, incluindo o desempate pela data mais recente.
- Decisão de votar ou de retirar o voto, consoante o estado atual.

É o nível onde o desempate se verifica de forma exaustiva, com casos construídos à
mão, porque é mais barato do que montar dados reais para a mesma coisa.

---

## Integração

Serviços contra **SQLite real**, numa base de dados temporária criada e destruída pelo
teste.

Não se usa um substituto em memória de outro fornecedor. O que interessa verificar
aqui é precisamente o comportamento do SQLite, e um substituto com semântica diferente
daria confiança em algo que não é o que corre em produção.

- **O índice único bloqueia mesmo o segundo voto.** Uma inserção direta de um voto
  repetido tem de falhar ao nível da base de dados.
- **A violação do índice único é tratada como sucesso idempotente**, e não propagada
  como erro. Este é o teste que protege o comportamento descrito em
  [02](02-Especificacao-Funcional.md).
- Retirar um voto que não existe não falha e não altera nada.
- As consultas do ranking e do detalhe devolvem a ordem correta com dados que forçam
  empates.
- O upsert do utilizador local cria na primeira vez e não duplica nas seguintes, e
  continua a dar uma linha só quando o nome muda entre inícios de sessão. **Não há
  coluna de email**, e esta linha falava dela até 17 de novembro de 2026.
- Um nome vindo do diretório segue uma alteração posterior do diretório, e um nome
  escrito pela própria pessoa nunca é substituído por ele.
- Duas pessoas com o mesmo nome apresentado continuam a ser duas linhas.
- A consulta do estado do botão de voto resolve se num só acesso, e não num por linha.

### Os scripts de `deploy` são confrontados com as migrações

Acrescentado a 17 de novembro de 2026. **Nada na aplicação chama `Migrate`**, portanto o
esquema que corre no servidor é o que os scripts de `deploy` produzem, e não o que as
migrações produzem. Eram duas coisas que se presumiam iguais e que ninguém comparava.

- O `migracao.sql` construído de raiz dá a mesma estrutura que as migrações, e regista as
  mesmas migrações aplicadas. As duas asserções são separadas de propósito: um script que
  cria todas as tabelas e carimba uma migração a menos deixa uma base que parece certa e
  faz o script gerado a seguir sair contra a base errada.
- Cada script incremental leva o esquema ao sítio onde a sua migração o leva. A base é
  migrada até à anterior, o script é aplicado por cima, e o resultado é comparado com uma
  base que as migrações levaram um passo mais longe.
- Existe um incremental por cada migração depois da primeira, sem saltos na numeração. É
  o que impede que um script em falta se traduza apenas em menos um caso a correr, que é
  a forma de uma teoria passar a dizer menos sem ninguém reparar.

#### O que esta comparação não cobre, e o dia em que deixa de chegar

> **A comparação é de estrutura e corre sobre uma base vazia.** Hoje chega, porque os
> dois scripts incrementais que existem são aditivos: só acrescentam tabelas e colunas, e
> numa base vazia não há linhas para se perderem.
>
> **Deixa de chegar no dia em que uma migração largar ou alterar uma coluna.** No SQLite
> isso faz se por reconstrução da tabela, que é criar outra, copiar as linhas, largar a
> antiga e renomear. Uma reconstrução escrita à mão que copie as colunas por ordem
> trocada deixa a estrutura **idêntica** e os dados nas colunas erradas. Numa base vazia
> não há nada para trocar, portanto o teste fica verde e no servidor desaparecem votos ou
> comentários.
>
> **O gatilho observável, para quem lá chegar saber que chegou:** o dia em que um script
> incremental tiver um `CREATE TABLE` seguido de `INSERT ... SELECT` e de um `DROP TABLE`
> ou de um `RENAME TO`. Nesse dia a comparação de estrutura deixa de ser suficiente e é
> preciso comparar também linhas, com dados postos de propósito antes de aplicar.

### A invariante do artefacto publicado, e o desvio que fica em aberto

Três testes afirmam que nada alcançável a partir do `IdeaBoard.Web` carrega identidade
local: a pasta de saída não tem ficheiro nenhum com esse prefixo, o manifesto de
dependências não o nomeia, e a caminhada pelas referências da assembly do host não lá
chega.

São a invariante e não a guarda que a defende. O `Directory.Build.targets` recusa a
construção de quem declare uma referência de projeto proibida, e vê menos do que a
invariante diz: uma DLL referenciada directamente, uma que chegue por pacote, uma copiada
para a pasta de saída por um target, ou uma referência num projeto que ninguém se lembrou
de pôr na lista vigiada, passam todas sem dar nas vistas.

> **Há uma forma de a quebrar que passa aos três, e fica escrita por não ser fechável com
> mais um teste do mesmo tipo.** Um `Compile Include` apontado ao ficheiro do handler de
> identidade local traz esse código para dentro do `IdeaBoard.App`, sem criar assembly
> nova e sem pôr ficheiro novo na pasta. As três perguntas são sobre nomes de ficheiros e
> de assemblies, e nenhuma delas muda: o código passa a viver numa assembly que **tem** de
> lá estar.
>
> Fechar isto pediria olhar para dentro dos tipos e não para os nomes, e nessa altura a
> pergunta deixa de ser a mesma. Fica aqui em vez de ficar por dizer, porque uma
> invariante que se julga fechada é pior do que uma que se sabe aberta.

---

## Ponta a ponta

`Microsoft.Playwright.Xunit.v3`, com browser real. O sufixo não é detalhe: a variante
sem ele serve o xunit v2, e esta solução está em `xunit.v3`.

### Como a aplicação é arrancada

Um fixture de coleção xUnit arranca a aplicação **como processo próprio**, numa porta
livre, com um ficheiro SQLite novo numa pasta temporária por execução, migrado no
arranque e apagado no fim.

A propriedade que interessa é que **as portas não colidem entre execuções**. A mecânica
de a obter é escolhida pelo fixture, e não fixada aqui.

Nada de base de dados partilhada entre execuções, e nada de IIS. A suite corre em
qualquer máquina que tenha o .NET e os browsers instalados, sem preparação prévia de
dados.

#### A pergunta que estava em aberto, e a resposta medida

Este documento perguntava se o `WebApplicationFactory` do .NET 10 já expunha um servidor
Kestrel real de forma nativa, para o fixture usar isso em vez de arrancar o host.

**A resposta é não, e fica registada em vez de apagada**, porque é ela que poupa a
próxima pessoa que tentar o caminho óbvio. Medido na construção da suite:

- Um `WebApplicationFactory` simples instala o `TestServer` do
  `Microsoft.AspNetCore.TestHost`, que não publica endereço nenhum.
- Substituí lo por Kestrel não falha por omissão, **é ativamente recusado**: a
  infraestrutura faz cast do servidor para `TestServer` e lança `InvalidCastException`.

É por isso que a aplicação é arrancada como processo próprio. E é também por isso que a
porta não pode ser obtida pedindo a porta zero e lendo o endereço atribuído, como este
documento dizia: fora do processo, esse endereço só se obteria a analisar uma linha de
log. O fixture pede uma porta livre ao sistema operativo, devolve a, e passa a ao host.

Isto é **correção a este documento e não exceção a ele**. A diferença importa, porque uma
exceção deixaria o documento a descrever uma mecânica que não funciona.

### Como a identidade é resolvida

**A suite arranca o `IdeaBoard.Local` e não o `IdeaBoard.Web`**, e a identidade vem da
fonte própria desse host, escolhida por quem corre o teste. **Não há handler de
autenticação de teste em lado nenhum.** Ver
[ADR-016](ADR/ADR-016-identidade-nos-testes-por-host-proprio.md), que substitui o
ADR-011.

O que o produto precisa de exercitar não mudou: **dois utilizadores diferentes na mesma
execução**. Sem isso não é possível provar que o voto é por pessoa, que a lista nominal
mostra os nomes certos, e que a contagem soma os dois.

O que mudou foi a forma de lá chegar. O desenho anterior mandava sobrepor o esquema de
autenticação do host publicado, e isso significava remover dele exatamente aquilo que o
torna o host publicado. O que sobrava era uma aplicação com identidade escolhida à mão a
servir Razor Pages, que é precisamente o que o `IdeaBoard.Local` é.

#### A garantia ficou mais forte, e deixou de custar um mecanismo

O raciocínio do ADR-011 era que um atalho de autenticação não pode chegar a produção, e
que código que não existe no artefacto não pode ser ativado por engano. Esse raciocínio
sobrevive inteiro, e **muda de função**: deixa de justificar onde o atalho vive e passa
a justificar porque não existe nenhum.

Antes, a garantia custava uma convenção sobre onde um ficheiro fica, defendida por
revisão de código. Agora não custa mecanismo nenhum na suite, porque o host que ela
arranca nunca teve um esquema de produção para ser sobreposto, e porque a guarda do
`Directory.Build.targets` recusa a construção de quem torne a identidade local
alcançável a partir do host publicado.

#### O custo que foi aceite, e fica escrito

A suite passa a assentar na escolha de pessoa do `IdeaBoard.Local`, que é código de
conveniência desse host e **não é ele próprio verificado**.

É tolerável por duas razões, e não porque seja pouco importante. Uma avaria aí parte
todos os cenários de forma imediata e ruidosa, em vez de falsear um resultado em
silêncio. E nada do que [02](02-Especificacao-Funcional.md) especifica depende dela.

### O que esta suite não cobre

**O fluxo de autenticação real nunca é exercitado**, e isso não mudou com a troca de
fornecedor, só mudou de nome. Era o redirecionamento e o token do tenant, passa a ser o
desafio de Negotiate, a consulta ao diretório pelo identificador de segurança e a
criação do utilizador a partir de uma identidade verdadeira do Windows.

Seria fácil ler "já não há handler de autenticação de teste" como se o problema tivesse
desaparecido. Não desapareceu nem um pouco.

Isto está escrito aqui de propósito. É a diferença entre uma suite verde e uma
aplicação que funciona, e a compensação é a verificação manual mais abaixo, que é
obrigatória antes de cada lançamento e não opcional. O que mudou foi a lista: a
verificação numa máquina em domínio substitui a verificação contra uma conta real do
Entra ID.

### Cenários

Um por funcionalidade do produto mínimo, mais os dois casos de limite que mais custam
se falharem.

| Cenário | O que prova |
|---|---|
| Submeter uma ideia e vê la em Recentes, com o nome do autor | F1 e parte de F4 |
| Votar, confirmar o incremento, retirar o voto, confirmar o decremento | F2, com a reversibilidade a ser o ponto |
| Dois utilizadores votam na mesma ideia, e ambos aparecem na lista nominal | F4, e a contagem por pessoa |
| Ranking ordenado por votos, com o desempate pela data mais recente | F3, incluindo [ADR-007](ADR/ADR-007-ordenacao-e-desempate.md) |
| Lista vazia mostra o convite a submeter a primeira ideia | O primeiro ecrã do dia do lançamento |
| Formulário rejeita título vazio e título acima do limite | Validação no servidor |

**A suite tem mais cenários do que esta tabela**, e a tabela é assim de propósito: promete
um por funcionalidade do produto mínimo, e é isso que cumpre. O que foi acrescentado
depois vive ao lado, em `tests/IdeaBoard.E2E/Scenarios/`, num ficheiro por assunto:
`Arnes`, `Autorizacao`, `Comentarios`, `Pesquisa`, `Ranking`, `Submissao`, `Tipografia` e
`Voto`.

São nomes de ficheiro e não uma contagem, por uma razão. Um número fica errado no dia em
que alguém acrescentar um cenário, e uma enumeração completa aqui seria uma segunda cópia
de uma lista que já existe na pasta, com o mesmo defeito das cópias de listas de exclusões
que este repositório já viu falhar duas vezes.

### Browsers

**Chromium apenas.** Aplicação interna, parque de máquinas conhecido, e cobrir três
motores seria custo sem risco correspondente. Ver
[ADR-012](ADR/ADR-012-chromium-apenas.md).

### Execução

Localmente, por quem desenvolve, e antes de publicar.

Pré requisito de máquina, uma vez por máquina e não um passo de build. O script é o
mesmo, muda o interpretador conforme o que estiver instalado:

```
# PowerShell 7 ou superior
pwsh bin/Debug/net10.0/playwright.ps1 install chromium

# Windows PowerShell, quando o pwsh não existe na máquina
powershell.exe -File bin/Debug/net10.0/playwright.ps1 install chromium
```

**Não assumir que o `pwsh` está instalado.** Nas máquinas desta equipa pode existir
apenas o Windows PowerShell, e foi com ele que a instalação foi feita.

**Nota de contexto, não item de trabalho**: o repositório tem remoto, em
`https://github.com/Kail-the-akuma/AI_Exercise.git`, e o trabalho chega a `main` por
pull request. Não há pipeline de integração contínua configurada, e este plano não
assume nenhuma.

Correr esta suite automaticamente é portanto possível hoje, e a única preparação
necessária seria a instalação dos browsers no agente. Se passa a haver pipeline é uma
decisão por tomar, e não um pressuposto deste plano.

---

## Verificação manual antes do lançamento

Obrigatória. Cobre o que nenhum teste automático desta solução cobre, e passa pelas
definições de servidor que são invisíveis no código.

### Autenticação real

- [ ] **A autenticação anónima está desligada no site, e a do Windows ligada.** Ler as
      duas no gestor do IIS, no site, e não confiar em terem sido postas uma vez.

      Este item existe porque o código abdicou de a verificar e apontou para aqui. Com
      a identidade a vir do Windows, essas definições vivem no `web.config` do site e na
      configuração do IIS, fora do alcance do processo no arranque: **o host arranca na
      perfeição com a anónima ligada e nada dentro dele dá por isso.** O sintoma é toda a
      gente recusada no primeiro pedido, visível e sem explicação. A decisão e as suas
      razões estão no [ADR-026](ADR/ADR-026-onde-vive-a-configuracao.md).

      O `deploy/provisionar.ps1` põe as duas e relê as no fim, e isso é melhor do que uma
      caixa assinalada, com um limite que convém não confundir: **corre uma vez, no
      provisionamento.** Este item cobre o dia em que alguém mexer nas definições do site
      pelo gestor do IIS, que é precisamente quando ninguém está a pensar nisto.
- [ ] **Uma pessoa entra pela primeira vez e a linha de `User` nasce.** Contar antes e
      depois, em vez de acreditar que a pessoa é nova:
      ```powershell
      sqlite3 "C:\IdeaBoard\data\ideaboard.db" "SELECT COUNT(*) FROM User;"
      ```
      A contagem tem de subir **exactamente um**, e é a linha nova que se inspecciona: o
      nome certo e o `accountName` na forma `DOMINIO\pessoa`. **Não há coluna de email**,
      e este item pedia se para a confirmar até 17 de novembro de 2026.

      **"Alguém que nunca entrou antes" não é verificável, e era assim que este item
      estava escrito.** Não há como saber que a pessoa é nova, e o método natural é
      alguém dizer que lhe parece que sim. Se já tiver entrado, a linha já lá está, lê se
      uma linha antiga e a caixa é assinalada.

      O que se perde nesse caso é exactamente o caminho que este documento diz que nenhum
      teste cobre: a consulta ao diretório e a criação do utilizador a partir de uma
      identidade verdadeira. Fica por exercitar e ninguém sabe. A contagem antes e depois
      troca uma suposição por uma transição observada.

      Feito logo a seguir ao provisionamento a contagem é `0`, e aí a primeira pessoa a
      entrar é necessariamente nova. Feito mais tarde, a contagem é a única coisa que
      salva o item.
- [ ] Confirmar que o nome apresentado na aplicação é o esperado.
- [ ] **O nome mostrado é um nome de pessoa e não um nome de conta.** Olhar para a
      linha de `User` criada e para o ecrã, e confirmar que o nome não tem barra
      invertida nem arroba.

      **Este item mudou de alvo a 17 de novembro de 2026, e a razão vale mais do que a
      mudança.** Ele dizia que o `accountName` não aparece em lado nenhum da interface, e
      isso guarda um caminho que não existe: o valor não é projectado para read model
      nenhum, portanto não há por onde chegar a um ecrã. Era uma caixa que não podia
      falhar.

      Medido pelo papel de verificação e confirmado aqui, em duas procuras, a larga e a
      estreita: onze ficheiros do `IdeaBoard.App` mencionam `accountName` e **nenhum é um
      `.cshtml`**, e não há nenhum nos read models. Com um controlo a confirmar que a
      procura encontra termos em `.cshtml` quando lá estão.

      **O risco a sério é o vizinho dele**: não é o `accountName` chegar ao ecrã, é o
      `displayName` **guardado** ser ele próprio um nome de conta, que é o que acontece se
      a resolução do nome devolver `DOMINIO\pessoa` e isso for escrito na coluna do nome.
      É por isso que o código tem um `AssertIsNotAnAccountName`, chamado nos dois caminhos
      de escrita, e que as regras do nome trazem a forma da conta com um aviso a dizer que
      não se simplifica.

      **A procura pela barra invertida no código fonte das páginas fica como rede e não
      como objectivo.** É barata e cobre o dia em que alguém acrescentar a coluna a uma
      vista. É também o inverso do item das duas variáveis do `web.config`, onde contar
      por procura de texto é o método errado: vale a pena dizer qual é qual, em vez de
      deixar a escolha ao hábito.
- [ ] **Um acesso sem sessão ao detalhe de uma ideia chega a essa ideia, e não à página
      inicial.** Submeter uma ideia primeiro e usar **o endereço dela**, copiado da barra
      de endereço, e não um identificador escrito à mão.

      **Duas correcções a este item, e a primeira é sobre o que ele deixou de provar.**
      Com autenticação integrada o browser autentica se sem a pessoa sair do endereço,
      portanto a metade que pedia "confirmar que pede autenticação" tornou se quase
      verdadeira por construção. A metade com dentes é o **destino**. O item fica
      estreitado a ela, e isto já tinha sido decidido numa ronda anterior e não tinha
      chegado ao documento.

      A segunda: numa base acabada de provisionar não existe ideia nenhuma, portanto
      `/ideias/1` devolve a página de não encontrada. Quem fizer o item pela ordem em que
      ele aparece nesta lista testa o contra um 404 e **nunca chega a provar o destino**.

### Pré requisitos da máquina

Antes de copiar seja o que for. Os dois primeiros falham com o **mesmo** código de erro,
por isso confirmam se os dois e não um.

- [ ] **ASP.NET Core Hosting Bundle** do .NET 10 instalado.
- [ ] **Serviço de função Autenticação do Windows do IIS** instalado. Não vem por
      omissão, e sem ele a autenticação integrada não existe como opção.
- [ ] Origem `IdeaBoard` criada no Event Log, confirmada por
      `[System.Diagnostics.EventLog]::SourceExists("IdeaBoard")` a devolver `True`. A
      aplicação não a cria, de propósito.

### Configuração que chega ao host

- [ ] As duas variáveis presentes no `web.config` **do servidor**, depois de copiados os
      ficheiros, **declaradas uma vez cada**:
      ```powershell
      [xml]$x = Get-Content "C:\inetpub\IdeaBoard\web.config" -Raw
      $vars = $x.SelectNodes("//aspNetCore/environmentVariables/environmentVariable")
      foreach ($n in @("ConnectionStrings__IdeaBoard","DataProtection__KeysPath"))
      {
          "$n declarado " + @($vars | Where-Object { $_.name -eq $n }).Count + " vez(es)"
      }
      ```
      Tem de dar `1` e `1`.

      **Não contar por procura de texto.** O `ConnectionStrings__IdeaBoard` aparece duas
      vezes no ficheiro, e a segunda está dentro de um comentário, na frase que explica o
      duplo sublinhado. Quem contar com Ctrl+F, que é como uma pessoa conta, vê duas e
      conclui que está declarada duas vezes.

      É um falso alarme, e chega no dia em que um falso alarme custa tempo e custa
      confiança no resto desta lista. O que o item distingue é **declarada** de
      **mencionada**.
- [ ] **Nenhum desses dois nomes definido noutro sítio.** São quatro passos, e os três
      primeiros verificam o que está escrito enquanto o quarto observa o resultado.

      **1. Ambiente da máquina e do utilizador**, pela presença do **nome** e não pelo
      valor:
      ```powershell
      $chaves = @{
          "Maquina"    = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
          "Utilizador" = "HKCU:\Environment"
      }
      foreach ($k in $chaves.Keys)
      {
          $props = (Get-ItemProperty -Path $chaves[$k]).PSObject.Properties.Name
          foreach ($n in @("ConnectionStrings__IdeaBoard","DataProtection__KeysPath"))
          {
              "$k / $n -> definido: " + ($props -contains $n)
          }
      }
      ```
      Os quatro resultados têm de ser `False`. Vai ao registo e não ao
      `GetEnvironmentVariable` de propósito: **uma variável definida com valor vazio
      continua a ser uma definição**, e a função devolve vazio tanto para essa como para
      uma que não existe. O registo distingue as duas.

      **2. Ambiente do pool de aplicações**, lido do `applicationHost.config`, sem precisar
      de módulo nenhum do IIS:
      ```powershell
      [xml]$ah = Get-Content "$env:windir\system32\inetsrv\config\applicationHost.config" -Raw
      $vars = $ah.SelectNodes("//applicationPools/add/environmentVariables/add")
      foreach ($n in @("ConnectionStrings__IdeaBoard","DataProtection__KeysPath"))
      {
          $hit = @($vars | Where-Object { $_.name -eq $n })
          if ($hit.Count -gt 0)
          {
              "CONFLITO: $n definido no pool " + ($hit | ForEach-Object { $_.ParentNode.ParentNode.name })
          }
          else
          {
              "limpo: $n"
          }
      }
      ```
      Os dois têm de dizer `limpo`.

      > **Como este passo foi validado, e fica escrito porque não é a mesma coisa que ter
      > corrido num servidor.** Foi testado pelo papel de verificação contra um
      > `applicationHost.config` **sintético**, escrito à mão numa pasta temporária, com um
      > pool a definir o `DataProtection__KeysPath`, e o comando acusou o conflito. **Não
      > foi testado contra um servidor**, por esta máquina não ter IIS. Quem o correr num
      > servidor a sério pode substituir esta nota pela melhor.

      **3. O `web.config` do site que recebe a aplicação**, que não é o da aplicação.
      Mesmo XPath do item anterior, apontado ao ficheiro do site. É o sítio de que ninguém
      se lembra, porque pertence ao vizinho. Ver
      [ADR-027](ADR/ADR-027-aplicacao-dentro-de-um-site-alheio.md).

      **4. A confirmação do resultado**, que é a única que não se pode fazer antes de
      publicar. Com o site a correr e alguém a usá lo, confirmar que existem
      `ideaboard.db-wal` e `ideaboard.db-shm` ao lado de
      `C:\IdeaBoard\data\ideaboard.db`.

      Os três acima verificam o que está escrito. **Este observa qual ficheiro foi mesmo
      aberto.** Se os valores tivessem sido combinados a aplicação não arrancava, e se
      tivesse sido aberto outro ficheiro estes dois apareciam noutro sítio.

      **Porque é que este item existe.** A documentação do módulo avisa que uma variável
      presente no `web.config` e também ao nível do sistema faz com que o valor do
      `web.config` seja **acrescentado** ao do sistema, com o exemplo
      `ASPNETCORE_ENVIRONMENT: Development;Development`, e que isso impede a aplicação de
      arrancar. Numa cadeia de ligação isso dá `Data Source=...;Data Source=...`, que é a
      mensagem a apontar para um caminho que ninguém escreveu.

      **E há uma honestidade que vale ser escrita**: a mesma página da documentação diz
      noutro parágrafo que os valores do `web.config` têm precedência sobre os do sistema.
      As duas frases convivem e não concordam. A leitura segura é a do aviso, e o facto de
      não concordarem é por si a razão de o passo 4 existir.
- [ ] Não existe `appsettings.Production.json` na pasta da aplicação. Se existir, alguém
      pôs a configuração no único sítio que a publicação seguinte apaga.

### Base de dados sem dados de teste

- [ ] As quatro contagens a zero numa base recém provisionada, antes de a equipa entrar:
      ```sql
      SELECT 'User' AS tabela, COUNT(*) AS linhas FROM User
      UNION ALL SELECT 'Idea', COUNT(*) FROM Idea
      UNION ALL SELECT 'Vote', COUNT(*) FROM Vote
      UNION ALL SELECT 'Comment', COUNT(*) FROM Comment;
      ```
- [ ] **O `sqlite_sequence` tem zero linhas**, numa base acabada de provisionar:
      ```powershell
      sqlite3 "C:\IdeaBoard\data\ideaboard.db" "SELECT COUNT(*) FROM sqlite_sequence;"
      ```
      Tem de devolver `0`.

      **As quatro contagens acima dizem que não há nada lá dentro. Esta diz que nunca
      houve**, e as duas coisas não são a mesma. Uma base onde alguém submeteu ideias para
      experimentar e depois as apagou passa as quatro contagens sem uma falha: as tabelas
      ficam a zero e o `sqlite_sequence` guarda a marca de água dos identificadores, pelo
      que a primeira ideia da equipa nasce com o número 4 ou 11 em vez de 1.

      Esta secção chama se "sem dados de teste", e é literalmente isto que esta linha
      verifica e as outras quatro não.
- [ ] A `__EFMigrationsHistory` tem as **três** linhas esperadas e nenhuma a mais.
- [ ] `PRAGMA integrity_check;` devolve `ok`.

### Configuração de servidor

- [ ] Pool de aplicações com **um só processo de trabalho**.
- [ ] Chaves de Data Protection a serem escritas em `C:\IdeaBoard\keys\`. Confirmar
      que o ficheiro existe, e não apenas que a pasta está configurada.

      **Feito depois de alguém ter aberto uma página com formulário**, e a de submeter
      uma ideia serve. O anel de chaves só é escrito à primeira vez que alguma coisa
      precisa dele, e com autenticação integrada o único consumidor é o antiforgery.
      Numa aplicação acabada de arrancar que ninguém usou, a pasta está vazia e este item
      falha **sem haver avaria nenhuma**.

      Dizer quando é o que devolve à pasta vazia o significado de sinal de avaria.
- [ ] Reciclar o pool de aplicações e confirmar a persistência das chaves, por esta
      ordem exata:
      1. Abrir o formulário de submissão e **deixá-lo aberto sem submeter**.
      2. Reciclar o pool.
      3. Confirmar no Gestor de Tarefas que o identificador do processo `w3wp` **mudou**.
      4. Só então submeter o formulário que ficou aberto, e confirmar que é aceite.
      O passo 3 não é zelo. A reciclagem do IIS é por omissão sobreposta, e o processo
      antigo continua a servir até o novo estar pronto, portanto uma submissão logo a
      seguir pode ser servida pelo processo antigo, que ainda tem o anel de chaves em
      memória. Passa, e não prova nada.
      Com autenticação integrada não há sessão da aplicação que possa sobreviver ou
      não, por isso o antiforgery é o único consumidor das chaves e este é o único item
      que as exercita. Se as chaves não estiverem a ser guardadas, a submissão falha
      com um erro de pedido inválido, e não com qualquer mensagem sobre chaves.
- [ ] **Parar apenas o pool de aplicações liberta mesmo os ficheiros para a cópia.**
      Com o site a correr, parar o pool, e confirmar que uma cópia por cima da pasta da
      aplicação passa sem erro de ficheiro bloqueado. É a confirmação de uma decisão
      tomada por raciocínio e não por medição, registada em
      [06](06-Operacao-e-Publicacao.md): quem segura os ficheiros é o processo de
      trabalho, que pertence ao pool. **Se falhar, a decisão inverte se** e o
      procedimento volta a mandar parar o site.
- [ ] **Durante essa cópia, um pedido ao site responde 503 e não uma ligação recusada.**
      É a propriedade que se perdeu ao parar o pool em vez do site, e perdeu se de
      propósito, conforme [06](06-Operacao-e-Publicacao.md). Confirmar aqui serve para
      que quem a encontrar daqui a meses saiba que foi decidida e não que escapou.
- [ ] Base de dados em `C:\IdeaBoard\data\`, fora da pasta da aplicação.
- [ ] **Modo WAL activo no ficheiro da base de dados**, lido no caminho final e num
      processo novo:
      ```powershell
      sqlite3 "C:\IdeaBoard\data\ideaboard.db" "PRAGMA journal_mode;"
      ```
      Tem de devolver `wal`.

      **O comando que põe o modo e o comando que o lê imprimem a mesma palavra, e são
      actos diferentes.** O primeiro responde `wal` por ter sido executado agora, naquele
      processo e naquele ficheiro. Este responde `wal` por ser verdade no ficheiro que a
      aplicação vai abrir, e é a segunda coisa que esta caixa pede.

      A condição que mais importa, além do processo novo: **corrê lo contra o caminho que
      está na cadeia de ligação do `web.config`**, e não contra a base onde o
      provisionamento a construiu. Se forem o mesmo caminho, o item sai mais barato e
      continua a valer.

      Apanha o que o comando de escrita não pode apanhar: a base ter sido provisionada
      noutro sítio e copiada, ter vindo de um restauro, o modo ter sido mudado depois, ou
      a pessoa estar a olhar para um ficheiro diferente daquele que a aplicação abre.

      **Há uma rede por baixo, e convém saber o que esta caixa acrescenta a ela.** A
      aplicação lê este pragma no arranque e recusa arrancar se não for `wal`, portanto um
      arranque bem sucedido já prova o modo. O que fica por cobrir, e que é o que esta
      caixa cobre, é o intervalo entre provisionar e arrancar pela primeira vez, e o dia
      em que alguém mexer na base sem reiniciar.
- [ ] Provocar um erro de propósito e confirmar que aparece no log `Application` com
      origem `IdeaBoard`. Sem este passo, uma origem por criar não tem sintoma nenhum:
      as páginas servem, as pessoas votam, e o registo de erros simplesmente não existe.
- [ ] **Ler o identificador de correlação que a página de erro mostra, e procurá lo na
      entrada do Event Log desse erro.**

      O item acima é satisfeito por qualquer entrada com aquela origem. Este pergunta
      outra coisa: se a entrada leva o identificador que a pessoa é convidada a citar.

      **Há razão para duvidar, e é por isso que o item existe em vez de uma afirmação.**
      O identificador é o do `Activity` em curso, que vive no scope do registo, e os
      fornecedores que a documentação do ASP.NET Core lista como suportando scopes não
      incluem o do Event Log. Se for assim, a entrada leva a mensagem e a excepção e não
      leva o identificador, e a correspondência acaba por ser feita pela hora.
      Levantado pelo papel de verificação, pelo mecanismo e sem o ter executado.

      **O que se faz com cada resultado, para que nenhum fique sem seguimento.** Se o
      identificador lá estiver, o comentário do `Erro.cshtml.cs` está certo e não se toca
      em nada. Se não estiver, a afirmação dele é falsa e volta ao DEV com esta medição
      em cima, porque o achado é do comentário e não deste documento.

      **Enquanto isto não for medido na máquina, não está errado.** As duas leituras que
      sustentam a dúvida são de documentação, feitas por duas pessoas em separado, e
      documentação lida não é comportamento observado.
- [ ] Entrar a partir de uma máquina do domínio, em **`http://servidor-app.dominio.interno/IdeaBoard`**, que é o endereço exacto que vai ser dado à
      equipa, e confirmar que a entrada é silenciosa, sem pedido de credenciais.

      **Um pedido de credenciais aqui não é um defeito da aplicação**, é a zona de
      segurança do browser. E a forma do nome é o que decide: a 17 de novembro de 2026,
      naquele servidor, o nome curto `http://SERVIDOR-APP/IdeaBoard` **pediu credenciais** e
      o nome qualificado entrou sozinho. Fazer o item com o endereço errado prova o
      contrário do que ele pergunta.
- [ ] **A outra aplicação do mesmo site continua a servir**, depois do provisionamento e
      depois de uma publicação. É o risco em torno do qual os três scripts foram
      escritos, e é o único item desta lista que não é sobre o IdeaBoard. Nenhum teste o
      cobre, porque nenhum teste conhece o vizinho.
- [ ] Nenhum 500.19 a falar de uma **secção duplicada**, que é o sintoma de a aplicação
      herdar do `web.config` do site que a recebe. A cura habitual mexe no ficheiro do
      vizinho, e nenhum script lhe toca, portanto esta é uma decisão para uma pessoa.

### Dados e operação

- [ ] Backup diário configurado e a produzir ficheiro.
- [ ] **Restauro ensaiado uma vez**, para uma cópia, com verificação de que os dados
      lá estão **e de que o ficheiro está são**:
      ```powershell
      sqlite3 "<copia>" "PRAGMA integrity_check;"
      sqlite3 "<copia>" "PRAGMA foreign_key_check;"
      ```
      O primeiro tem de devolver `ok` e o segundo não deve imprimir nada.

      "Com verificação de que os dados lá estão" era a parte frouxa: um ficheiro SQLite
      restaurado pode ter as linhas todas e estar corrompido. **É aqui que o
      `foreign_key_check` pertence**, e não na base acabada de provisionar, onde é
      trivial por não haver linhas. Numa cópia restaurada com dados reais deixa de o ser.
- [ ] As três consultas de métricas correm e devolvem valores plausíveis com dados
      reais.

### Produto

- [ ] Submeter uma ideia real e confirmar que aparece nos dois separadores.
- [ ] Votar a partir de duas contas diferentes e confirmar a contagem e a lista
      nominal.
- [ ] Confirmar que o aviso de voto público e o aviso de submissão definitiva estão
      visíveis **antes** da ação, e não depois.
- [ ] Percorrer a aplicação só com teclado.
- [ ] Desativar o JavaScript e confirmar que votar continua a funcionar, **pelo
      endereço e não pelo piscar**: sem script o voto responde com um redirecionamento
      para a mesma página com uma âncora, portanto o endereço passa a terminar em
      `#ideia-` e o número da ideia. Com script o endereço não muda.

      "Com recarregamento da página" confirmava se por se ver a página recarregar, que é
      o que se perde numa rede rápida. O endereço fica. Verificado por leitura: as quatro
      páginas com botão de voto constroem o redirecionamento pelo mesmo `BuildIdeaAnchor`.

      **Na área pessoal há um caso que não é falha e parece uma.** Retirar um voto tira a
      ideia da lista, portanto a âncora pode apontar para uma linha que deixou de existir
      e o browser fica no topo da página. O endereço continua a ter a âncora e a página
      não salta. Está escrito no código como comportamento correcto, e fica escrito aqui
      para não ser assinalado como defeito.

---

## Critério de pronto

Uma fase está pronta quando a suite dos três níveis passa, quando os cenários ponta a
ponta dessa fase existem e passam, e quando os itens manuais aplicáveis foram
verificados por uma pessoa.

Uma suite verde não é, por si só, critério de pronto. É condição necessária.
