# 04. Requisitos não funcionais e segurança

**Versão 1, 30 de outubro de 2026.**

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

Microsoft Entra ID por OpenID Connect, com `Microsoft.Identity.Web`. Ver
[ADR-003](ADR/ADR-003-autenticacao-entra-id.md).

Configuração em `appsettings.json`, secção `AzureAd`, com `Instance`, `TenantId` do
tenant corporativo, `ClientId` e `CallbackPath` em `/signin-oidc`. O registo de
aplicação é de um só tenant, porque a aplicação é interna e não tem motivo para
aceitar identidades de outras organizações.

**Nenhum segredo fica no repositório.** O `appsettings.json` versionado contém a
estrutura e os valores não sensíveis. O `TenantId` e o `ClientId` não são segredos,
mas também não precisam de estar no controlo de versões, e ficam na configuração do
servidor. Se o registo exigir client secret ou certificado, esse valor fica fora do
repositório sem exceção, conforme o resultado do spike descrito no ADR-003.

A sessão é mantida por cookie de autenticação. Duração e renovação deslizante ficam
nos valores por omissão da plataforma, o que é adequado para uma aplicação sem dados
sensíveis.

## Autorização

Uma regra, aplicada uma vez: **todas as páginas exigem utilizador autenticado**.

A política é registada como política de autorização de fallback, não como atributo
página a página. A diferença importa: com fallback, uma página nova nasce protegida
por omissão, e só fica aberta se alguém o declarar de forma explícita. Com atributos,
uma página nova nasce aberta, e só fica protegida se alguém se lembrar.

As rotas de início e fim de sessão fornecidas pelo `Microsoft.Identity.Web.UI` são as
únicas exceções, e são lhe próprias.

Não há papéis, não há permissões e não há verificação de propriedade, porque não há
nenhuma operação restrita a um subconjunto de pessoas. Quem está autenticado pode
submeter e votar, e nada mais existe para fazer.

## Proteção contra ataques correntes

| Risco | Resposta |
|---|---|
| Cross-site request forgery | Token antiforgery em todos os POST, incluindo o voto enviado por `fetch`. É o comportamento por omissão das Razor Pages, e não deve ser desativado em lado nenhum |
| Cross-site scripting | O Razor codifica a saída por omissão. O título e a descrição são apresentados como texto simples, e `Html.Raw` não é usado em parte nenhuma sobre conteúdo de utilizador |
| Injeção de SQL | Acesso exclusivamente por EF Core com consultas parametrizadas. SQL em texto com interpolação de valores de utilizador não é usado |
| Enumeração de identificadores | Os identificadores são sequenciais e isso não é problema, porque todo o conteúdo é visível para toda a gente autenticada por decisão do produto |
| Escalada de privilégios | Não aplicável. Não existem privilégios diferenciados |
| Abuso de volume | Ver abaixo |

### Transporte

HTTPS obrigatório, com redirecionamento de HTTP e HSTS ativo fora de ambiente de
desenvolvimento. O certificado é tratado ao nível do IIS.

### Chaves de Data Protection

Têm de ser persistidas numa pasta no disco, com nome de aplicação definido de forma
explícita.

Esta é a configuração que, faltando, causa o problema mais difícil de diagnosticar
desta aplicação: a cada reciclagem do pool de aplicações, o anel de chaves em memória
é perdido, os cookies de autenticação e os tokens antiforgery deixam de ser
desencriptáveis, e os utilizadores são atirados para fora da sessão ou recebem erros
em submissões, aparentemente ao acaso. Ver
[ADR-009](ADR/ADR-009-publicacao-iis.md).

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

| Dado | Origem | Porquê |
|---|---|---|
| Identificador de objeto do Entra ID | Token de início de sessão | Chave estável da pessoa |
| Nome apresentado | Token de início de sessão | Mostrado como autor e na lista de votantes |
| Email | Token de início de sessão | Identificação inequívoca em caso de nomes iguais |
| Data do primeiro início de sessão | Relógio do servidor | Diagnóstico |
| Ideias submetidas e votos | Utilização da aplicação | É o produto |

Nada mais. Sem fotografia, sem departamento, sem hierarquia, sem registo de navegação
e sem analítica de terceiros.

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
