# ADR-016. Identidade nos testes por host próprio, em vez de esquema sobreposto

**Estado**: Aceite. **Data**: 13 de novembro de 2026.
**Substitui**: [ADR-011](ADR-011-autenticacao-nos-testes-e2e.md).

## Contexto

O [ADR-011](ADR-011-autenticacao-nos-testes-e2e.md) resolveu um problema real, que
continua a existir tal e qual: toda a aplicação exige sessão iniciada, e os cenários
que mais interessam precisam de **duas pessoas diferentes na mesma execução**. Nada
disso mudou.

O que mudou foi a forma. O ADR-011 mandava o fixture sobrepor o esquema de autenticação
por um handler de teste que lia um header, mandava esse handler viver no projeto
`IdeaBoard.E2E`, e mandava o fixture fornecer valores fictícios para a secção `AzureAd`
para que o arranque do `Microsoft.Identity.Web` não falhasse.

Com a estrutura do [ADR-015](ADR-015-estrutura-de-quatro-projetos.md), nenhuma dessas
três coisas é precisa.

## Decisão

A suite ponta a ponta arranca o **`IdeaBoard.Local`** como processo, e não o
`IdeaBoard.Web`. A identidade vem da fonte própria desse host, no
`IdeaBoard.LocalIdentity`, e é escolhida por quem corre o teste.

Verificado no ramo `fase1/e2e`: o `IdeaBoard.E2E.csproj` referencia o
`src/IdeaBoard.Local`, e o `ApplicationProcess` arranca `IdeaBoard.Local.dll`. Não há
secção `AzureAd` em lado nenhum da suite, não há header de identidade, e não há esquema
sobreposto.

## Três coisas desaparecem, e uma fica mais forte

**Desaparece o handler no projeto de testes.** O handler existe, mas vive num projeto
de produto, o `IdeaBoard.LocalIdentity`, porque o host local é uma aplicação que uma
pessoa arranca e usa com o browser e não apenas um alvo de testes. Pô lo no projeto de
testes obrigaria a duas cópias.

**Desaparece a sobreposição do esquema.** Não se sobrepõe nada, porque o host que a
suite arranca nunca teve um esquema de produção para ser sobreposto. A diferença não é
de arrumação: sobrepor um esquema é uma operação que acontece em tempo de arranque e
que pode falhar, ou pior, pode parecer ter acontecido sem ter acontecido.

**Desaparecem os valores fictícios do `AzureAd`.** Eram um andaime para impedir que uma
biblioteca de autenticação se queixasse na partida. Essa biblioteca não está no fecho
de projetos do host local, e portanto não há nada a calar.

**E fica mais forte a única coisa que importava.** A garantia do ADR-011 era que o
artefacto publicado não contém código de autenticação de teste. Era uma convenção sobre
onde um ficheiro vive, defendida por revisão de código. Passa a ser um facto
estrutural: o `IdeaBoard.LocalIdentity` não é alcançável a partir do `IdeaBoard.Web`, e
a guarda do `Directory.Build.targets` recusa a construção de quem tentar torná lo
alcançável.

O raciocínio do ADR-011 sobre a condição de ambiente não ser suficiente sobrevive
inteiro e aplica se com mais força. Continua a ser verdade que código que não existe no
artefacto não pode ser ativado por engano, e agora há quem recuse o contrário em vez de
alguém ter de reparar.

## Evidência posterior à decisão, e a razão pela qual ela conta

Esta decisão foi tomada de manhã, por um motivo: com autenticação integrada, o atalho
de autenticação deixava de ser necessário e o host local passava a ser o sítio óbvio
para a identidade de teste.

Horas depois o fornecedor de identidade de produção mudou por inteiro, do Entra ID para
Negotiate, e **o arnês não mudou uma linha**. Medido pelo QA, e a razão é que a suite
nunca esteve apontada ao host cujo fornecedor mudou.

Registo isto como evidência e não como confirmação, e a diferença está em que a razão
pela qual decidi não é a razão pela qual compensou. Se a suite tivesse ficado sobre o
`IdeaBoard.Web`, como o `Docs/07` original mandava, esta mudança teria partido a suite
inteira, e nenhum dos motivos que me levaram a decidir tinha antecipado isso.

É o terceiro caso do mesmo padrão neste projeto: uma restrição adotada pela razão A a
pagar pela razão B. O âmbito fechado do briefing evitou trabalho que ninguém previu, e
o revezamento sequencial dos agentes evitou documentação errada. Vale a pena o padrão
estar escrito, porque quem propuser desfazer qualquer uma destas restrições tem de
contar o que ela paga além do que foi comprada para pagar.

## Alternativas consideradas

**Manter o ADR-011 tal e qual, com o esquema sobreposto sobre o `IdeaBoard.Web`.** É o
que testaria o artefacto que vai publicado, que é o seu argumento e não é pequeno.
Posta de lado porque o artefacto publicado passa a autenticar por Negotiate contra um
controlador de domínio, e sobrepor isso num teste é substituir exatamente aquilo que se
queria testar. O que resta de testável no host publicado é se ele arranca e serve, e
isso tem o seu próprio teste.

**Arrancar o host local em processo, com `WebApplicationFactory`.** Mais rápido e sem
caminhos de ficheiro para construir. Posta de lado porque a suite precisa de um
servidor a sério numa porta, para o browser do Playwright lhe chegar.

## Consequências

**Boas.** Nada de credenciais, nada de rede, nada de autenticação multifator, e dois
utilizadores no mesmo teste. O arnês ficou independente do fornecedor de identidade de
produção, o que já se provou valer.

**Más.** A suite arranca um processo a partir de um caminho construído para a pasta de
saída do `IdeaBoard.Local`. Isso é mais frágil do que arrancar em processo, e parte se a
disposição da saída de construção mudar, com um sintoma que não diz o que correu mal.

**E a consequência que tem de continuar visível, porque só mudou de nome.** A suite
nunca exercita o fluxo de autenticação real. Era o redirecionamento e o token do tenant,
passa a ser o desafio de Negotiate, a consulta ao diretório pelo identificador de
segurança, e o aprovisionamento a partir de claims verdadeiras do Windows.

Uma suite verde continua a não provar que a entrada em sessão funciona, e a compensação
continua a ser verificação manual obrigatória. O que mudou é a lista: a do
[ADR-014](ADR-014-autenticacao-integrada-windows.md), que só se cumpre numa máquina em
domínio, substitui a verificação contra uma conta real do Entra ID.
