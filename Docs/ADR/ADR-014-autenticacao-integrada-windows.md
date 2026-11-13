# ADR-014. Autenticação integrada do Windows, em substituição do Entra ID

**Estado**: Aceite, com a decisão tomada e a implementação por fazer. **Data**: 13 de novembro de 2026.
**Substitui**: [ADR-003](ADR-003-autenticacao-entra-id.md).

## Contexto

O [ADR-013](ADR-013-certificado-no-arranque.md) mostrou que o pedido de arrancar sem
HTTPS era incompatível com o Entra ID, e não por configuração mas por três razões de
fundo. A resposta na altura foi manter o Entra ID e emitir um certificado pela própria
equipa.

O mesmo ADR-013, nas alternativas que pôs de lado, escreve que a troca para Windows
Authentication seria a única forma de ter mesmo HTTP com as pessoas autenticadas, e
rejeita a por ser uma mudança de arquitetura e não um adiamento.

O Hugo decidiu pagar esse custo e fazer a mudança de arquitetura.

Convém registar que esta alternativa não apareceu agora. O próprio ADR-003 já a tinha
considerado e já a tinha deixado registada como alternativa viável se o registo de
aplicação demorasse a ser aprovado e houvesse pressa em arrancar. O que mudou não foi o
conhecimento da opção, foi a disposição para pagar o preço dela.

## Decisão

Autenticação integrada do Windows, por Negotiate no IIS, em vez de OpenID Connect
contra o Entra ID.

A identidade da pessoa passa a ser o **identificador de segurança da conta**, lido da
claim `http://schemas.microsoft.com/ws/2008/06/identity/claims/primarysid`, e é por ele
que a linha local é encontrada. A política de fallback que exige utilizador autenticado
em todas as páginas mantém se tal e qual, porque continua a ser o que faz uma página
nova nascer protegida.

O nome apresentável deixa de vir numa claim do fornecedor de identidade e passa a ser
obtido por consulta ao diretório pelo identificador de segurança, com registo manual
pela própria pessoa quando a consulta não devolve nada, e com a proveniência do nome
guardada para decidir se uma consulta posterior o pode substituir.

## As duas rejeições do ADR-003 têm destinos diferentes, e isso é o essencial

O ADR-003 pôs esta opção de lado por duas razões. Uma foi resolvida e a outra foi
aceite, e tratá las como se tivessem o mesmo destino seria o erro mais fácil de
cometer aqui.

**Obter um nome apresentável obriga a consultar o Active Directory.** Continua verdade,
e deixou de ser objeção. A consulta existe, falha de forma contida, e a falha tem
saída: quem chega sem nome regista o seu, e a linha só nasce quando há nome para
escrever. É isso que mantém a invariante de que toda a linha de utilizador tem nome,
em vez de cada página que mostra um autor ter de defender a ausência dele.

**Só funciona dentro do domínio, o que exclui quem trabalhe fora da rede.** Continua
verdade e **não foi resolvida. Foi aceite.** Quem estiver fora da rede corporativa
deixa de conseguir entrar, e não há contrapartida nenhuma escrita para esse caso.

A diferença importa porque uma destas é uma objeção vencida e a outra é um custo
assumido. Quem ler este registo daqui a um ano e precisar de reabrir o assunto tem de
saber qual é qual, senão arrisca procurar solução para o que já tem, e tratar como
resolvido o que nunca foi.

## O ponto por confirmar do ADR-003 fica sem resposta por ficar sem objeto

O ADR-003 deixou em aberto se a aplicação conseguiria funcionar sem client secret, e
mandou o resultado do spike voltar para aquele ficheiro.

Esse spike não vai acontecer e a pergunta não vai ser respondida. Não há registo de
aplicação, não há segredo, e portanto não há data de expiração para alguém vigiar. O
ponto não foi esclarecido, foi anulado, e é por isso que o estado do ADR-003 passa a
substituído em vez de resolvido.

## Estado da implementação, que não é o estado da decisão

**A troca ainda não está feita no código.** Verificado por leitura hoje: tanto em `main`
como em `origin/fase1/identidade-windows`, o `Program.cs` do `IdeaBoard.Web` continua a
chamar `AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)` e
`AddMicrosoftIdentityWebApp`, nas linhas 29 e 30. A palavra `negotiate` aparece em todo
o código apenas em dois comentários e em nenhuma configuração.

O que já está feito é o que fica a jusante da identidade: o esquema da tabela de
utilizadores, o resolvedor de nomes pelo diretório, o registo manual, e a proveniência
do nome. O que falta é a troca do esquema de autenticação propriamente dita, retida à
espera da decisão de HTTP contra HTTPS que pertence ao Hugo.

Este registo descreve uma decisão tomada, e não uma implementação existente. A
distinção está aqui escrita de propósito, porque um ADR que descreva como feito aquilo
que está apenas decidido é documentação falsa no momento em que é escrita.

## O que sai junto com o pacote, e um teste que fica a passar pelo motivo errado

A saída do `Microsoft.Identity.Web.UI` leva consigo coisas que ninguém decidiu remover.

**O fim de sessão desaparece.** Não existe botão de terminar sessão em página nenhuma
da aplicação, e o `Docs/02` não o menciona. A rota vinha do `AddMicrosoftIdentityUI()`,
chamado no `Program.cs`, e sai com a biblioteca. Com autenticação integrada isto é o
comportamento normal e não um defeito, porque não há sessão aplicacional para terminar.
O item manual do `Docs/07` que manda terminar sessão e confirmar que o acesso volta a
pedir autenticação não fica por fazer, fica **sem objeto**.

**E há um segundo item que é pior, porque não fica obviamente vazio.** O `Docs/07` manda
reciclar o pool de aplicações e confirmar que uma sessão já iniciada continua válida, e
diz que é esse o teste que prova que a persistência das chaves de Data Protection
funciona.

Com Negotiate não há cookie de autenticação. Cada pedido volta a autenticar se, por
isso uma sessão iniciada **continua válida depois de reciclar o pool mesmo que as
chaves não estejam a ser persistidas de todo**. O teste passa, e deixa de provar aquilo
para que foi escrito.

A persistência das chaves continua a ser necessária, porque os tokens antiforgery
continuam a depender dela. O que muda é o teste que a prova: tem de ser carregar uma
página com formulário, reciclar o pool, e só então submeter. Se as chaves não
persistirem, é aí que falha.

Isto não é observação sobre testes. É a razão pela qual o
[ADR-009](ADR-009-publicacao-iis.md) sobrevive com a justificação encolhida. A
persistência das chaves foi decidida para proteger duas coisas, e passa a proteger uma.

## Alternativas consideradas

**Manter o Entra ID e aceitar HTTPS no primeiro dia.** Era a decisão do ADR-013, e
funcionava. Posta de lado por decisão do Hugo, que preferiu pagar a mudança de
arquitetura a aceitar o certificado e o aviso que toda a equipa teria de clicar no
primeiro acesso.

**Suportar os dois esquemas, e escolher por configuração.** Daria caminho de volta sem
refazer nada. Posta de lado porque duplica a superfície de identidade de uma aplicação
para trinta pessoas, e porque o caminho que ficaria de reserva é precisamente o que tem
a dependência externa que esta decisão procura cortar. Um caminho de reserva que
depende de um registo de aplicação por aprovar não é reserva nenhuma.

## Consequências

**Boas.** A Fase 0 deixa de depender de uma equipa externa para o registo de aplicação,
e não há segredo nem certificado de cliente com data de expiração para alguém vigiar. A
saída de um colaborador continua a terminar o acesso de imediato, porque é a conta de
domínio que o dá.

**Más.** Quem trabalhe fora da rede corporativa perde o acesso, sem contrapartida.

**E essa exclusão chega à medição do sucesso, que é onde ninguém a vai procurar.** O
briefing mede o sucesso por percentagem de pessoas que participa. Quem trabalha fora da
rede deixa de poder participar, e **não é um não participante, é um impedido**. Contado
no denominador, faz a participação parecer mais fraca do que é, por decisão de desenho e
não por desinteresse da equipa.

A escolha do denominador é decisão de produto e não é tomada aqui. O que é tomado aqui, e
tem de ficar escrito ao lado da decisão que o causa, é que esta autenticação **cria uma
classe de pessoas impedidas que a métrica, tal como está definida, não distingue de quem
simplesmente não quis participar**. Quem olhar para o número aos três meses sem saber
disto vai tirar a conclusão errada sobre a adesão.

A dependência externa não desapareceu, mudou de nome. Era o Entra ID estar disponível
para iniciar sessão, passa a ser o controlador de domínio estar disponível para a
consulta do nome, e o diretório responder no formato que a consulta espera.

**O que fica por verificar, e só se verifica numa máquina em domínio.** O filtro contra
um diretório a responder, o tempo limite contra um controlador real, o formato do nome
de conta em domínio, e a conta sem nome de apresentação. Nada disto é exercitável no
ambiente onde o código foi escrito, e está recolhido na lista de primeiro arranque em
domínio.

**O que esta decisão não decide.** HTTP contra HTTPS continua em aberto e pertence ao
Hugo. Dos três fundamentos do ADR-013, dois caem inteiros com a saída do Entra ID. O
terceiro, o tráfego em claro, não cai: muda de ator, porque deixa de haver cookie de
sessão para roubar e passa a haver tokens antiforgery visíveis, numa aplicação onde as
credenciais passam a ser ambientes e o antiforgery passa a importar mais do que
importava. Essa análise não está confirmada e não é fundamento deste registo.
