# 04. Requisitos não funcionais e segurança

**Versão 2, 13 de novembro de 2026.**

## Carga esperada

| Grandeza | Valor |
|---|---|
| Utilizadores registados | Entre 15 e 50 |
| Utilizadores em simultâneo, pico realista | Menos de 10 |
| Escritas por dia, em período normal | Dezenas |
| Escritas por minuto, no pico do dia do lançamento | Poucas dezenas |
| Leituras por dia | Centenas |

Esta é uma aplicação de carga desprezável. A consequência prática é que nenhuma
decisão de arquitetura deve ser justificada por desempenho, e qualquer uma que o seja
precisa de ser explicada.

## Desempenho

| Objetivo | Valor |
|---|---|
| Resposta do ranking, percentil 95 | Abaixo de 300 ms no servidor |
| Resposta de um voto, percentil 95 | Abaixo de 200 ms no servidor |
| Peso da página inicial | Abaixo de 150 KB, sem contar com fontes do sistema |

Estes valores são folgados de propósito. Servem como alarme, não como meta. Uma
medição acima destes números indica quase de certeza um erro, como um problema de N
mais 1 nas consultas, e não falta de capacidade do servidor.

Sem framework de interface no cliente. O único JavaScript é o script de melhoria
progressiva do botão de voto, descrito em [01](01-Arquitetura.md).

## Disponibilidade

Horário laboral, melhor esforço. Sem compromisso formal de nível de serviço, sem
redundância e sem failover.

Uma máquina e um ficheiro de base de dados. Uma falha do servidor é uma
indisponibilidade até alguém a resolver, e isso é aceitável para uma ferramenta
interna de participação voluntária. O que não é aceitável é perder dados, e por isso o
backup tem requisitos próprios em [06](06-Operacao-e-Publicacao.md).

**Janela de perda de dados tolerada**: um dia. É o intervalo do backup diário.

## Autenticação

**Autenticação integrada do Windows, por Negotiate no IIS.** Ver
[ADR-014](ADR/ADR-014-autenticacao-integrada-windows.md), que substitui o ADR-003.

A aplicação regista o esquema do IIS e mais nada. Não há registo de aplicação, não há
client secret, não há URI de redirecionamento e não há secção `AzureAd` na
configuração. **Não há segredo nenhum para guardar**, e portanto não há data de
expiração para alguém vigiar.

**Não há cookie de sessão.** Cada pedido volta a autenticar se contra a identidade que
o IIS fornece, o que tem uma consequência que não é óbvia e está registada mais abaixo,
na secção das chaves de Data Protection.

A identidade da pessoa é o identificador de segurança da conta, e é por ele que a linha
local é encontrada. O nome apresentável não vem na identidade e é obtido por consulta ao
diretório, com registo manual pela própria pessoa quando a consulta não devolve nada.
Ver [ADR-022](ADR/ADR-022-resolucao-e-substituicao-do-nome.md) e
[ADR-023](ADR/ADR-023-chave-da-tabela-de-utilizadores.md).

**Só funciona dentro da rede corporativa.** Quem trabalhe de fora deixa de conseguir
entrar, e não há contrapartida. É um custo aceite com conhecimento de causa, e não uma
limitação por resolver. O ADR-014 explica porque é que esta objeção foi aceite e a
outra que o ADR-003 levantava foi resolvida.

## Autorização

Uma regra, aplicada uma vez: **todas as páginas exigem utilizador autenticado**.

A política é registada como política de autorização de fallback, não como atributo
página a página. A diferença importa: com fallback, uma página nova nasce protegida
por omissão, e só fica aberta se alguém o declarar de forma explícita. Com atributos,
uma página nova nasce aberta, e só fica protegida se alguém se lembrar.

Não há exceções. As rotas de início e de fim de sessão desapareceram com a saída do
`Microsoft.Identity.Web.UI`, e com autenticação integrada não há sessão aplicacional
para terminar, portanto **não existe botão de terminar sessão em página nenhuma**. Isso
é o comportamento normal deste desenho e não uma funcionalidade em falta.

A página onde quem chega sem nome o regista **não é exceção a esta política**, e
convém não a descrever como tal. Continua a exigir utilizador autenticado. O que ela
é, é exceção a um segundo portão, o do nome: um middleware redireciona para lá quem
esteja autenticado e ainda não tenha nome, e deixa passar os pedidos a essa página, às
de erro e às de diagnóstico, porque de outro modo o redirecionamento entrava em ciclo.

Não há papéis, não há permissões e não há verificação de propriedade, porque não há
nenhuma operação restrita a um subconjunto de pessoas. Quem está autenticado pode
submeter e votar, e nada mais existe para fazer.

## Proteção contra ataques correntes

| Risco | Resposta |
|---|---|
| Cross-site request forgery | Token antiforgery em todos os POST, incluindo o voto enviado por `fetch`. É o comportamento por omissão das Razor Pages, e não deve ser desativado em lado nenhum |
| Cross-site scripting | O Razor codifica a saída por omissão. O título, a descrição e o nome registado pela própria pessoa são apresentados como texto simples, e `Html.Raw` não é usado em parte nenhuma sobre conteúdo de utilizador |
| Injeção de SQL | Acesso exclusivamente por EF Core com consultas parametrizadas. SQL em texto com interpolação de valores de utilizador não é usado |
| Enumeração de identificadores | Os identificadores são sequenciais e isso não é problema, porque todo o conteúdo é visível para toda a gente autenticada por decisão do produto |
| Escalada de privilégios | Não aplicável. Não existem privilégios diferenciados |
| Abuso de volume | Ver abaixo |

### Transporte

**HTTP, sem certificado e sem HSTS.** Decisão do Hugo, registada em
[ADR-024](ADR/ADR-024-http-sem-certificado.md), que substitui o ADR-013.

O endereço é o nome curto do servidor com porta. Com autenticação integrada do Windows
não há redirecionamento para um fornecedor externo nem cookie de sessão a proteger, e
por isso o HTTPS deixou de ser condição de funcionamento, que é o que era com o Entra
ID.

Nem o redirecionamento nem o HSTS são compostos pela aplicação. Não há
`UseHttpsRedirection` nem `UseHsts` no arranque, e isso é deliberado, não esquecimento.
Tudo o que diga respeito a transporte pertence ao site do IIS, e quem for procurar um
middleware no código não o vai encontrar.

#### A regra da ordem do HSTS sobrevive, e tem de sobreviver

O problema do HSTS não deixou de existir, deixou de estar agendado. Se o HTTPS voltar
algum dia, **a ordem continua fixa: certificado confiado primeiro, HSTS depois, nunca
ao contrário.**

Com HSTS ativo, um browser que encontre um erro de certificado termina a ligação e não
oferece forma de prosseguir, o que torna o site inacessível a toda a gente incluindo a
quem o publicou, e não se corrige depressa porque o browser guarda a instrução pelo
tempo declarado.

A regra nunca dependeu de o certificado ser emitido pela equipa. Dependia de haver um
certificado não confiado e HSTS ao mesmo tempo, que é uma combinação que qualquer
caminho para HTTPS pode voltar a produzir. O raciocínio completo está no
[ADR-024](ADR/ADR-024-http-sem-certificado.md).

### Chaves de Data Protection

Têm de ser persistidas numa pasta no disco, com nome de aplicação definido de forma
explícita.

Faltando, o anel de chaves vive em memória e é perdido a cada reciclagem do pool de
aplicações, e tudo o que foi cifrado com as chaves anteriores deixa de ser legível. Ver
[ADR-009](ADR/ADR-009-publicacao-iis.md).

**A justificação encolheu, e o que resta continua a bastar.** As chaves protegiam duas
coisas, o cookie de autenticação e os tokens antiforgery. Com autenticação integrada não
há cookie de autenticação, portanto protegem uma. Continuam a ser necessárias, porque um
token antiforgery que deixa de ser desencriptável faz falhar submissões.

**A consequência perigosa é no teste, e não na configuração.** O teste que provava esta
configuração era reciclar o pool e confirmar que uma sessão iniciada continuava válida.
Sem cookie de sessão, cada pedido volta a autenticar se, e por isso esse teste **passa
mesmo que as chaves não estejam a ser persistidas de todo**. Ficaria verde sem provar
nada.

O teste que prova esta configuração passa a ser outro: carregar uma página com
formulário, reciclar o pool, e só então submeter. Se as chaves não persistirem, é aí que
falha.

A pasta das chaves tem o mesmo requisito de permissões e de backup que a pasta da base
de dados.

### Abuso de volume

Não há limitação de ritmo, e é uma decisão.

A população é fechada, autenticada e nominal, e cada voto está publicamente associado
a uma pessoa. Alguém que submeta cinquenta ideias de propósito ou vote em tudo de
forma automática cria um problema social, não técnico, e resolve se falando com essa
pessoa. Acrescentar limitação de ritmo seria construir defesa contra um atacante que,
por definição, está identificado.

A idempotência do voto, descrita em [02](02-Especificacao-Funcional.md), protege
contra duplicação acidental, que é o caso real e frequente.

## Dados pessoais e privacidade

### O que é guardado

| Dado | Coluna | Origem | Porquê |
|---|---|---|---|
| Identificador de segurança da conta | `subjectId` | Identidade fornecida pelo IIS | Chave estável da pessoa |
| Nome apresentado | `displayName` | Diretório corporativo **ou a própria pessoa** | Mostrado como autor e na lista de votantes |
| Proveniência do nome | `displayNameSource` | A aplicação, ao registar o nome | Decide se uma consulta posterior o pode substituir |
| Conta de domínio | `accountName` | Identidade fornecida pelo IIS | Identificação inequívoca em caso de nomes iguais |
| Data do primeiro acesso | `firstSeenAt` | Relógio do servidor | Diagnóstico |
| Ideias submetidas e votos | | Utilização da aplicação | É o produto |

Nada mais. Sem fotografia, sem departamento, sem hierarquia, sem registo de navegação
e sem analítica de terceiros. **Não há coluna de email**, e o `accountName` ocupa o
lugar que ela ocupava.

**Obter o nome implica a aplicação consultar o diretório corporativo**, no primeiro
acesso de cada pessoa. Isto é tratamento que acontece, e não apenas proveniência de um
dado guardado. Dizer que o nome vem do diretório descreve de onde ele veio, não que a
aplicação vai lá buscá lo, e para quem avalia tratamento de dados são coisas diferentes.

A consulta pede ao diretório **apenas o atributo do nome**, e não o registo da pessoa.
A diferença não é cosmética: a via alternativa materializava um objeto com dezenas de
propriedades, incluindo telefone, número de empregado e histórico de autenticações
falhadas, e nada disso seria guardado mas tudo isso seria lido.

**O nome pode ser escrito pela própria pessoa.** Quando a consulta não devolve nada,
quem chega regista o seu nome, e a linha só nasce quando há nome para escrever. É por
isso que a origem do `displayName` tem duas entradas e não uma, e é por isso que a
proveniência é guardada. Ver
[ADR-022](ADR/ADR-022-resolucao-e-substituicao-do-nome.md).

### Transparência deliberada

O autor de cada ideia e a lista nominal de quem votou são públicos para toda a equipa.
É uma escolha explícita do briefing, não uma falha de proteção.

Daí decorrem duas obrigações de produto, ambas especificadas em
[02](02-Especificacao-Funcional.md): a interface avisa que o voto é público **antes**
de a pessoa votar, e avisa que a submissão é definitiva **antes** de a pessoa submeter.
Ninguém deve descobrir estas regras depois de as ter usado.

### A confirmar com quem trata de privacidade na organização

Registado aqui para não ser esquecido, e não como parecer. A aplicação trata dados
pessoais de colaboradores, e convém confirmar antes do lançamento se precisa de
constar do registo de atividades de tratamento, e se a comunicação interna de
lançamento é suficiente como informação às pessoas abrangidas.

**Há tratamento novo que não existia antes, e pode mudar essa resposta.** A aplicação
passou a consultar o diretório corporativo, o que é interrogar um sistema sobre uma
pessoa e não apenas guardar o que ela escreveu. A consulta e o seu âmbito estão
descritos acima, na secção do que é guardado.

Que a consulta acontece é facto decidido e não pergunta. O que fica por confirmar é se
muda a resposta sobre o registo de atividades de tratamento.

### Retenção

Os dados são mantidos enquanto a plataforma existir. Não há apagamento automático nem
arquivo.

Quando alguém sai da organização, a conta deixa de autenticar e o acesso termina de
imediato. As ideias e os votos dessa pessoa continuam visíveis com o nome, porque
removê los apagaria parte do histórico coletivo e distorceria as contagens. Se for
necessário tratar um pedido individual, é um procedimento manual, descrito em
[06](06-Operacao-e-Publicacao.md).

## Registo de eventos

`ILogger` com o provider de Event Log do Windows, que já faz parte da plataforma. Sem
biblioteca de logging adicional.

**A registar**: arranque e paragem da aplicação, falhas de autenticação, exceções não
tratadas com identificador de correlação, aprovisionamento de um novo utilizador
local, e falhas de escrita na base de dados.

**A não registar**: o conteúdo das ideias, tokens, cookies ou qualquer valor de
configuração sensível. Quem vota em quê já está na base de dados e não precisa de ser
duplicado no registo de eventos.

Sem métricas, sem tracing distribuído e sem painel de observabilidade. Um processo,
uma máquina, e o Event Log chega.

## Compatibilidade

Browsers correntes em versão atual, no parque de máquinas da organização. Sem suporte
a Internet Explorer.

A aplicação funciona com JavaScript desativado, com a única diferença de o voto
recarregar a página. Isto não é compatibilidade com browsers antigos, é uma
consequência de a lógica viver no servidor, e é o que mantém a aplicação simples.

Interface utilizável em ecrã de telemóvel. Não é prioridade, mas uma lista e um botão
não justificam uma versão separada.

## Manutenção

Alvo de atualização ao .NET seguinte de suporte alargado dentro do período de suporte
do .NET 10. Um projeto interno sem manutenção planeada torna se um risco de segurança
por si só, e registar a intenção é a forma mínima de a tornar visível.
