# ADR-018. A aplicação recusa arrancar com configuração em falta

**Estado**: Aceite. **Data**: 13 de novembro de 2026.

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

## Decisão

A configuração é validada no arranque, e a aplicação **recusa arrancar** quando algo
obrigatório está em falta ou errado. A validação devolve a lista completa de problemas,
e não o primeiro.

O conjunto validado, no ramo `fase1/identidade-windows`: a base de dados, as chaves de
Data Protection, o fuso horário, e a consulta ao diretório.

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

Está hoje removida, e no lugar dela está a validação da consulta ao diretório.
Verificado por leitura: no `main` o validador ainda chama `ValidateAzureAd` e exige as
quatro chaves; no ramo da identidade essa chamada desapareceu e entrou
`ValidateDirectoryLookup`.

A generalização que fica escrita: **um validador de arranque é a peça mais acoplada ao
fornecedor de identidade de toda a aplicação, e a única que falha fechando a porta a
toda a gente.** Quem trocar de fornecedor e se esquecer deste ficheiro não recebe um
aviso, recebe uma aplicação que não arranca.

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
