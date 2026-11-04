# ADR-011. Autenticação por handler de teste, fora do projeto web

**Estado**: Aceite. **Data**: 30 de outubro de 2026.

## Contexto

Toda a aplicação exige sessão iniciada. Nenhum teste ponta a ponta consegue chegar a
nada sem autenticar primeiro.

Pior do que isso, os cenários que mais interessa verificar precisam de **duas pessoas
diferentes na mesma execução**: a contagem de votos, a lista nominal de votantes, e a
prova de que o voto é por pessoa. Um só utilizador autenticado não chega para provar
nada disso.

## Decisão

O fixture dos testes ponta a ponta substitui o esquema de autenticação por um handler
de teste que lê um header com o identificador do utilizador pretendido e devolve as
claims correspondentes.

**O handler vive no projeto `IdeaBoard.E2E` e nunca em `IdeaBoard.Web`.** O projeto
publicado não contém código de autenticação de teste, nem sequer protegido por uma
condição de ambiente.

O fixture fornece valores fictícios para a secção `AzureAd`, para que o arranque do
`Microsoft.Identity.Web` não falhe, e só depois sobrepõe o esquema por omissão.

## Porque é que a condição de ambiente não chega

A alternativa óbvia é registar o handler de teste dentro do projeto web, guardado por
uma condição do tipo "só se o ambiente for de teste". É uma linha de código, é fácil
de escrever, e é como muita gente resolve isto.

É também um mecanismo de contorno da autenticação que **existe** no artefacto
publicado, e que fica à distância de uma variável de ambiente mal definida, de uma
configuração copiada entre servidores, ou de uma alteração futura feita por alguém que
não conhece a razão da condição.

Código que não existe no artefacto não pode ser ativado por engano. É uma garantia de
natureza diferente de uma condição bem escrita, e o custo de a obter é configurar o
esquema no fixture em vez de no `Program`.

## Alternativas consideradas

**Contas reais do Entra ID, com sessão guardada e reutilizada.** Exercitaria a cadeia
completa, que é o seu argumento. Fica de fora porque quebra com autenticação
multifator e com políticas de acesso condicional, que são precisamente o que uma
organização tem ativo, exige duas ou três contas de teste criadas e mantidas, e
obriga a guardar credenciais algures. Uma suite que falha por motivos externos deixa
de ser consultada, e uma suite que não é consultada não vale nada.

**Um só utilizador autenticado, com os cenários de duas pessoas verificados apenas em
testes de integração.** Perderia a prova de que a lista nominal e a contagem aparecem
corretamente na interface, que é onde a funcionalidade de transparência do briefing
realmente vive.

## Consequências

**Boas.** Suite determinística, sem credenciais, sem dependência de rede e sem
autenticação multifator pelo meio. Vários utilizadores no mesmo teste. Corre em
qualquer máquina com o .NET e o browser instalados.

**Más, e esta é a consequência que tem de estar visível.** A suite **nunca** exercita
o fluxo real do Entra ID. O redirecionamento, a validação do token emitido pelo
tenant, o URI de redirecionamento do registo de aplicação e o aprovisionamento do
utilizador local a partir de claims verdadeiras ficam todos por verificar.

Uma suite verde não prova que a entrada em sessão funciona.

A compensação é a verificação manual obrigatória antes de cada lançamento, listada em
[07](../07-Plano-de-Verificacao.md), que inclui iniciar sessão com uma conta real de
alguém que nunca entrou antes. Essa compensação não é opcional, e é a razão pela qual
esta decisão é aceitável.
