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
- [ ] Iniciar sessão com uma conta corporativa real, de alguém que **nunca** entrou
      antes, e confirmar que a linha de `User` é criada com o nome certo e com o
      `accountName` na forma `DOMINIO\pessoa`. **Não há coluna de email**, e este item
      pedia se para a confirmar até 17 de novembro de 2026.
- [ ] Confirmar que o nome apresentado na aplicação é o esperado, e que o `accountName`
      não aparece em lado nenhum da interface.
- [ ] Confirmar que um acesso sem sessão a `/ideias/1` pede autenticação e, depois
      dela, chega à página pedida, e não à página inicial.

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
      ficheiros: `ConnectionStrings__IdeaBoard` e `DataProtection__KeysPath`.
- [ ] Nenhum desses dois nomes definido **também** no ambiente da máquina ou do pool de
      aplicações. Definidos nos dois sítios, os valores podem ser combinados e a
      aplicação não arranca, com uma mensagem que aponta para um caminho que ninguém
      escreveu.
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
- [ ] A `__EFMigrationsHistory` tem as **três** linhas esperadas e nenhuma a mais.
- [ ] `PRAGMA integrity_check;` devolve `ok`.

### Configuração de servidor

- [ ] Pool de aplicações com **um só processo de trabalho**.
- [ ] Chaves de Data Protection a serem escritas em `C:\IdeaBoard\keys\`. Confirmar
      que o ficheiro existe, e não apenas que a pasta está configurada.
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
- [ ] Base de dados em `C:\IdeaBoard\data\`, fora da pasta da aplicação.
- [ ] Modo WAL ativo no ficheiro da base de dados.
- [ ] Provocar um erro de propósito e confirmar que aparece no log `Application` com
      origem `IdeaBoard`. Sem este passo, uma origem por criar não tem sintoma nenhum:
      as páginas servem, as pessoas votam, e o registo de erros simplesmente não existe.
- [ ] Entrar a partir de uma máquina do domínio, usando **o endereço exato** que vai
      ser dado à equipa, e confirmar que a entrada é silenciosa, sem pedido de
      credenciais. A forma do nome é o que decide: um pedido de credenciais aqui não é
      um defeito da aplicação, é configuração de zona por resolver antes do lançamento.

### Dados e operação

- [ ] Backup diário configurado e a produzir ficheiro.
- [ ] **Restauro ensaiado uma vez**, para uma cópia, com verificação de que os dados
      lá estão.
- [ ] As três consultas de métricas correm e devolvem valores plausíveis com dados
      reais.

### Produto

- [ ] Submeter uma ideia real e confirmar que aparece nos dois separadores.
- [ ] Votar a partir de duas contas diferentes e confirmar a contagem e a lista
      nominal.
- [ ] Confirmar que o aviso de voto público e o aviso de submissão definitiva estão
      visíveis **antes** da ação, e não depois.
- [ ] Percorrer a aplicação só com teclado.
- [ ] Desativar o JavaScript e confirmar que votar continua a funcionar, com
      recarregamento da página.

---

## Critério de pronto

Uma fase está pronta quando a suite dos três níveis passa, quando os cenários ponta a
ponta dessa fase existem e passam, e quando os itens manuais aplicáveis foram
verificados por uma pessoa.

Uma suite verde não é, por si só, critério de pronto. É condição necessária.
