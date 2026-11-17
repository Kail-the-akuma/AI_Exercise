# ADR-018. A aplicação recusa arrancar com configuração em falta

**Estado**: Aceite, emendado pelo [ADR-026](ADR-026-onde-vive-a-configuracao.md).
**Data**: 13 de novembro de 2026.

## Contexto

Esta aplicação depende de coisas que estão fora do código e que só se descobrem
erradas no pior momento possível.

A pasta das chaves de Data Protection é o exemplo que define o problema. Se estiver mal
configurada, nada falha no arranque, nada falha no primeiro acesso, e nada falha nos
testes. Falha no dia em que o pool de aplicações reciclar, com toda a gente a ser
atirada para fora ao mesmo tempo e sem nenhuma mensagem que ligue o sintoma à causa.

O caminho da base de dados tem a mesma forma. Mal configurado, a aplicação cria um
ficheiro dentro da pasta de publicação e funciona na perfeição, até à republicação
seguinte apagar os dados todos.

**E isto aconteceu mesmo, a outra equipa, no servidor para onde vamos.** O documento de
instalação do RepositorioVizinhoA regista um incidente de 13 de outubro de 2026, no primeiro
arranque do servidor novo: um caminho de base de dados desactualizado no `web.config` fez a
aplicação **ler e criar em silêncio uma base de dados diferente e vazia**, correr as
migrações nela, e responder que a pessoa não tinha conta. Perderam a maior parte de um dia a
investigar e a excluir, uma a uma, teorias sobre Kerberos, SPN duplicados, replicação do
Active Directory e políticas de restrição de NTLM, todas reais e nenhuma relacionada.

Lido no documento deles a 18 de novembro de 2026, e é afirmação deles e não medição nossa.
Fica aqui porque **é exactamente a falha que esta decisão existe para impedir**, com a
forma completa: nada falhou, nada ficou vermelho, e o sintoma apareceu noutro sítio
disfarçado de outra coisa. Uma decisão justificada por raciocínio passou a ter um incidente
de fora por trás, e é a diferença entre ser defensável e ser difícil de reabrir.

## Decisão

A configuração é validada no arranque, e a aplicação **recusa arrancar** quando algo
obrigatório está em falta ou errado. A validação devolve a lista completa de problemas,
e não o primeiro.

O conjunto validado é a base de dados, as chaves de Data Protection, o fuso horário e a
consulta ao diretório. Verificado por leitura do `StartupConfigurationValidator` a 17 de
novembro de 2026, com a identidade do Windows já no ramo principal.

Devolver a lista inteira em vez do primeiro problema é deliberado. Quem está a
configurar um servidor pela primeira vez quer saber tudo o que falta de uma vez, e não
descobrir um item por cada tentativa de arranque.

## O conjunto validado segue o fornecedor de identidade, e isso tem de estar visível

Esta é a parte que custou a aprender e que justifica o registo existir.

Enquanto a autenticação era por Entra ID, o validador exigia quatro chaves da secção
`AzureAd`, a instância, o tenant, o cliente e o caminho de retorno. Era a coisa certa a
fazer, e teve uma consequência que na altura pareceu detalhe: foi por causa dela que o
arnês de testes teve de fornecer valores fictícios para essa secção, como o
[ADR-011](ADR-011-autenticacao-nos-testes-e2e.md) regista.

Quando o fornecedor de identidade mudou, essa mesma exigência passou a ser um bloqueio
total. Uma aplicação que autentica por Negotiate não tem tenant nem cliente, e um
validador a exigi los recusaria o arranque a toda a gente, por falta de configuração de
um fornecedor que já não existe.

Está removida, e no lugar dela está a validação da consulta ao diretório.

**Esta secção descreveu durante dias um estado que já não era verdade.** Dizia que no
`main` o validador ainda chamava `ValidateAzureAd`, o que era exacto no dia em que foi
escrito, com a identidade do Windows ainda num ramo. Esse ramo fundiu, e a frase ficou a
afirmar com ar de verificada uma coisa falsa: `ValidateAzureAd` não existe em parte nenhuma
do código hoje, confirmado por procura a 17 de novembro de 2026, e o que o validador chama
é `ValidateDirectoryLookup`. É o defeito que o [ADR-014](ADR-014-autenticacao-integrada-windows.md)
nomeia, apanhado aqui pelo ORQ.

A generalização que fica escrita: **um validador de arranque é a peça mais acoplada ao
fornecedor de identidade de toda a aplicação, e a única que falha fechando a porta a
toda a gente.** Quem trocar de fornecedor e se esquecer deste ficheiro não recebe um
aviso, recebe uma aplicação que não arranca.

## O que o validador deixou de poder garantir, e é maior do que uma chave a menos

A remoção das quatro chaves da secção `AzureAd` não foi uma limpeza de configuração morta.
**Levou com ela uma garantia, e não há nada no lugar dela dentro do processo.**

Enquanto a autenticação era por Entra ID, exigir aquelas quatro chaves equivalia a dizer
que sem configuração de autenticação a aplicação não faz nada de útil, e era verificável
porque a configuração estava ao alcance da aplicação.

Com a autenticação integrada do Windows não está. As definições vivem no `web.config` do
site e na configuração do IIS, fora de tudo o que este processo consegue ler no arranque. O
host arranca na perfeição com a autenticação do Windows desligada e a anónima ligada, e
**nada aqui dá por isso**. O sintoma chega como um 401 no primeiro pedido, visível e sem
explicação.

Não repor uma verificação aqui contra aquelas chaves. Com elas fora da configuração,
qualquer verificação escrita contra elas recusaria o arranque ao próprio host que devia
guardar, que é o erro descrito na secção anterior.

Para onde foi a detecção, e porque é que o `web.config` passou a ser versionado, está no
[ADR-026](ADR-026-onde-vive-a-configuracao.md), que emenda este registo sem o substituir: a
decisão de recusar arrancar está de pé, o que mudou foi o conjunto validado e quem guarda a
autenticação.

## O que estas guardas nunca puderam verificar, e não é defeito por corrigir

Isto é um limite conhecido e fica escrito como limite. **Não é trabalho por fazer, e a
secção seguinte diz o que o cobre**, para que ninguém leia isto como uma lacuna e vá tentar
tapá la aqui dentro.

O que o arranque verifica sobre a base de dados é que a cadeia de ligação existe, que ela
traz uma origem de dados, que o caminho é absoluto, e que não aponta para dentro da pasta
da aplicação. O verificador de journal acrescenta que o ficheiro está em write ahead
logging.

**Nenhuma destas verifica que é o ficheiro certo, e nenhuma pode.** Um caminho absoluto,
fora da pasta da aplicação, a apontar para uma base de dados em WAL com o esquema todo lá
dentro, passa as duas guardas inteiras e pode ser o ficheiro errado.

**E a razão de não poder é a que vale o registo.** Para provar a identidade do ficheiro, a
guarda teria de saber qual era o esperado, e esse valor viria da mesma configuração que já
pode estar errada. Seria a configuração a verificar se a si própria, que é a classe de
defeito que a secção acima sobre aplicar o pragma já descreve: uma verificação que não
consegue falhar não é uma verificação. **"Certo" não é uma propriedade que o arranque
consiga ver.**

### O sintoma, e porque é que o nosso é pior do que o deles

O incidente de 13 de outubro de 2026 registado no Contexto é exactamente esta falha, vivida
por outra equipa no servidor para onde vamos. Lá, a aplicação respondia que a pessoa não
tinha conta.

**Na nossa forma o sintoma mente melhor.** Quem chega sem linha é mandado para a página do
nome, regista o, **é aceite**, e no pedido seguinte volta a ser lhe pedido o nome. A escrita
foi para um ficheiro e a leitura veio de outro, e as duas correram sem erro nenhum.

Isso **parece um defeito da nossa página de registo** e não um problema de configuração, o
que manda quem diagnostica para o código errado. Quem for ler o `UserProvisioningService` à
procura da falha não encontra nada, porque não há lá nada: aquilo funcionou as duas vezes,
em bases diferentes.

### O que tapa isto é operacional, e já existe

A verificação que falta não é de arranque, é de observação, e a diferença é o que a torna
possível: **em vez de verificar a configuração, ver qual ficheiro foi mesmo aberto.**

Depois de alguém usar o site, os ficheiros `-wal` e `-shm` aparecem ao lado da base de dados
que o processo abriu de facto. Se aparecerem ao lado do ficheiro que esperamos, é esse o que
está a ser lido e escrito. Se aparecerem noutro sítio, ou não aparecerem em sítio nenhum, a
aplicação está noutro ficheiro.

Proposta do papel de QA, relatada pelo ORQ a 18 de novembro de 2026. Fica escrita aqui ao
lado do limite de propósito, porque um limite sem o que o cobre lê se como trabalho em
dívida.

## Alternativas consideradas

**Validação de opções da própria plataforma, com `ValidateOnStart`.** É o mecanismo que
o ASP.NET Core oferece para isto, e pela regra de preferir o que a plataforma já dá
devia ser a primeira escolha. Cobre bem a parte de haver um valor e ele ter a forma
certa.

Não cobre a parte que aqui interessa mais, que é o valor apontar para um sítio que
existe e onde a identidade do pool consegue escrever. Essas verificações atravessam
secções de configuração e tocam no sistema de ficheiros, e não são propriedade de nenhum
tipo de opções em particular. É essa a razão pela qual a validação é própria, e vale a
pena dizer que a razão não é forte ao ponto de impedir que parte disto venha a ser
movida para o mecanismo da plataforma.

**Falhar no primeiro uso, com uma boa mensagem de erro.** Mais simples, e é o que
acontece por omissão. Posta de lado porque os dois casos que motivaram esta decisão,
as chaves e o caminho da base de dados, não falham no primeiro uso. Falham semanas
depois, e nessa altura a boa mensagem de erro não existe.

## Consequências

**Boas.** Um servidor mal configurado é detetado por quem o está a configurar, e no
momento em que o está a configurar. A publicação deixa de poder parecer bem sucedida com
a aplicação a funcionar por acaso.

**Más.** Uma aplicação que recusa arrancar é um modo de falha ruidoso, e um erro no
próprio validador tira o serviço a toda a gente. É o preço de a verificação ser um
bloqueio em vez de um aviso, e é aceite porque os problemas que ela apanha custam mais
do que um arranque recusado.

**E uma obrigação permanente.** Qualquer mudança de fornecedor de identidade, de local
de dados ou de mecanismo de proteção de chaves obriga a rever este ficheiro no mesmo
passo. Não é uma recomendação de higiene, é a condição para a aplicação arrancar.
