# ADR-024. HTTP sem certificado, com o nome curto do servidor e porta

**Estado**: Aceite. **Data**: 13 de novembro de 2026.
**Substitui**: [ADR-013](ADR-013-certificado-no-arranque.md).

## Contexto

O pedido original era arrancar sem HTTPS e rever o assunto mais tarde, para tirar do
caminho crítico a dependência da equipa de infraestrutura.

O [ADR-013](ADR-013-certificado-no-arranque.md) mostrou que isso não era possível com o
Entra ID, e deu três razões. Duas eram sobre o protocolo de autenticação, que o Entra ID
recusa URIs de redirecionamento em HTTP e que os cookies de ligação precisam de `Secure`.
A terceira era sobre o produto, que o cookie de sessão viajaria em texto limpo e
permitiria votar em nome de um colega.

O [ADR-014](ADR-014-autenticacao-integrada-windows.md) tirou o Entra ID do caminho. As
duas primeiras razões caem inteiras, porque deixa de haver registo de aplicação, URI de
redirecionamento e cookie de ligação. A terceira não cai, muda de ator, e está tratada
mais abaixo.

O Hugo decidiu HTTP.

## O ADR-013 valida a sua própria substituição

Vale a pena registar, porque não é ironia e é evidência.

O ADR-013, nas alternativas que pôs de lado, escreve à letra que a troca para Windows
Authentication seria a única forma de ter mesmo HTTP com as pessoas autenticadas, e
rejeita a por ser uma mudança de arquitetura e não um adiamento.

Ou seja, este caminho foi identificado no momento em que foi fechado, e foi fechado pelo
custo da mudança e não pelo mérito dela. Esse custo foi pago. A decisão de hoje não
contradiz o ADR-013, executa a alternativa que ele próprio nomeou.

## Decisão

**HTTP, sem certificado e sem HSTS.**

O endereço é o **nome curto do servidor com porta**. A forma está decidida pelo Hugo. O
valor concreto, qual é o nome e qual é a porta, não está, e é a única coisa que falta
para a Fase 5.

## O que morre com esta decisão, e uma morte que é boa notícia

Morre o certificado emitido pela própria equipa, da autoridade interna ou auto assinado.
Morre a Fase 6 do [05](../05-Plano-de-Entrega.md), que existia só para o substituir por
um certificado confiado.

E morre **a armadilha do HSTS**, que o próprio ADR-013 descreve como a consequência menos
óbvia desta área e a que tem pior resultado: com HSTS ativo, um browser que encontre um
erro de certificado termina a ligação e não oferece forma de prosseguir, o que torna o
site inacessível a toda a gente incluindo a quem o publicou, e não se corrige depressa
porque o browser guarda a instrução pelo tempo declarado.

Essa armadilha desaparece por completo. É o melhor resultado desta decisão, e é maior do
que o incómodo que ela evita, porque era o único modo de falha do projeto que não tinha
recuperação rápida.

## A regra do HSTS sobrevive à morte do certificado, e tem de sobreviver

Aqui está o cuidado que esta substituição obriga a ter.

É tentador apagar a armadilha do HSTS junto com o certificado, por o problema ter deixado
de existir. Seria errado, porque o problema não deixou de existir, deixou de estar
agendado.

**Se o HTTPS voltar algum dia, a ordem continua fixa: certificado confiado primeiro,
HSTS depois, nunca ao contrário.** A regra não dependia do certificado ser emitido pela
equipa. Dependia de haver um certificado não confiado e HSTS ao mesmo tempo, que é uma
combinação que qualquer caminho para HTTPS pode voltar a produzir.

Fica escrita aqui e não no ADR-013, porque um registo substituído não é sítio onde
alguém vá procurar uma regra em vigor.

## O nome DNS definitivo continua obrigatório, e a razão é outra

O ADR-013 exigia o nome definitivo desde o primeiro dia e dava três razões: o
certificado emitido para esse nome, o registo da aplicação no Entra ID, e o certificado
de substituição. **As três morreram.**

Isso cria um risco preciso, e não é o de alguém distraído: quem rever este requisito
daqui a um mês vai verificar os três destinos, encontrar zero, e removê lo por obsoleto.
Um leitor cuidadoso chega lá mais depressa do que um distraído.

**A razão que sobrevive é o início de sessão automático**, e é mais exigente do que as
três que morreram, porque depende da forma exata do nome e não apenas de ele ser estável.
Trocar o nome depois obriga ainda a refazer a comunicação à equipa, os atalhos e os
marcadores de toda a gente, que é custo humano e não técnico.

### O que está verificado sobre a forma do nome, e com que confiança

**Um nome sem pontos entra na zona de intranet local por omissão, e é nessa zona que o
início de sessão automático está ligado por omissão. Um nome com pontos fica fora dela.**
Confiança alta, com a documentação de desenho do Chromium a dizer o inverso de forma
direta, que um host com ponto fica fora da intranet local.

Portanto o nome curto que o Hugo escolheu é a forma favorável a esta propriedade, e o
nome qualificado completo é que precisaria de configuração no parque.

**A condição que limita esta afirmação, e sem a qual ela não é verdadeira.** A deteção
automática de intranet só acontece **quando a lista de servidores de autenticação não
está definida por política**. Se estiver, é a lista que governa, e o que decide passa a
ser o nosso nome estar lá dentro. Num parque gerido, a lista estar definida é o caso
provável e não o improvável.

**Sobre a porta.** As zonas de segurança não são indexadas por porta, e a estrutura do
registo não tem onde guardar uma. Confiança média, porque assenta na estrutura e não numa
fonte que o afirme. Existe um mecanismo real que torna o comportamento sensível à porta,
a política `EnableAuthNegotiatePort` do Chrome e do Edge, que decide se a porta entra no
nome do serviço pedido ao Kerberos quando não é 80 nem 443. Confiança alta, é documento
de desenho do Chromium.

### O que isto manda fazer, e por que ordem

Primeiro, **confirmar numa máquina da equipa** que a nossa aplicação, no nosso servidor,
com o nome curto e com Negotiate ligado, faz início de sessão automático. Só se não
fizer é que se pergunta qual dos mecanismos falhou.

A ordem importa porque dois dos três mecanismos possíveis não têm pedido nenhum
associado à infraestrutura, e uma única página, `edge://policy` numa máquina do parque,
responde a duas perguntas de uma vez, a lista de servidores de autenticação e a política
da porta.

**Houve uma medição noutro servidor** que mostrou início de sessão automático apenas em
três portas, as do Argus e do Katana. Não é propriedade desta aplicação e não está
confirmada no nosso ambiente. E a leitura mais económica dela é que essas três portas são
as únicas onde existe aplicação a pedir autenticação integrada, porque uma porta onde
nenhuma aplicação a pede não pode dar início de sessão automático, com zona ou sem zona.
Nessa leitura, a medição não diz nada sobre a nossa porta.

## O que se perde com HTTP, nomeado e não escondido

**A confidencialidade do conteúdo.** Ideias, nomes de autores e listas nominais de
votantes viajam em claro no segmento de rede.

**A confidencialidade dos tokens antiforgery.** E isto importa mais do que importava com
o Entra ID, não menos, porque com Negotiate as credenciais passam a ser ambientes: o
browser autentica sozinho qualquer pedido àquele host, venha ele de onde vier, que é a
propriedade que tornava o antiforgery necessário quando havia cookies.

**A ligação ao canal.** A proteção forte contra relay de autenticação integrada deriva do
certificado do servidor, e sem TLS não há canal a que ligar. Confiança média, com a
cláusula que a limita: sem TLS a proteção **degrada e não desaparece**, passando a
assentar em ligação ao serviço pelo nome do serviço. Não encontrei uma fonte única da
Microsoft a tratar precisamente esta combinação, e a parte do nome do serviço veio
sobretudo de manuais de teste de intrusão.

### O terceiro fundamento do ADR-013 não cai, muda de ator

O ADR-013 dizia que o cookie de sessão em claro permitiria votar e submeter em nome de um
colega, e que isso destrói o produto e não apenas a segurança, por o valor da plataforma
assentar em os votos serem nominais e verdadeiros.

Com Negotiate não há cookie de sessão para roubar, e essa via fecha.

**Raciocínio meu, não confirmado, e registado como tal.** Quem observa o tráfego em claro
vê o par de antiforgery, o cookie e o campo do formulário. Com esse par, e levando a
pessoa a uma página que submeta um voto, o browser dela junta o Negotiate sozinho e o
voto sai em nome dela. Se isto se confirmar, o fundamento diz exatamente o que dizia, por
outro caminho, e o ator passa de observador passivo com um cookie roubado para observador
passivo mais uma isca.

**A condição em que a minha afirmação é falsa**: se o antiforgery do ASP.NET Core não for
utilizável desta forma por quem apenas o observa, ou se houver ligação ao canal que o
invalide fora dele. Não verifiquei nenhuma das duas. Isto não é fundamento desta decisão,
é um ponto por verificar que pertence a quem decidir reabrir o assunto.

## Alternativas consideradas

**Manter o ADR-013, com certificado emitido pela equipa.** Funcionava, e a razão pela
qual foi posta de lado está no próprio ADR-013: toda a gente veria um aviso de segurança
no primeiro acesso e teria de o aceitar, o que ensina a equipa a clicar em avisos de
certificado, hábito que se leva para sítios onde o aviso é verdadeiro. O ADR-013 chama
lhe formação em más práticas, e tinha razão.

**Pedir um certificado confiado à infraestrutura antes de arrancar.** É o caminho correto
em estado estacionário. Posta de lado pelo mesmo motivo de sempre, que coloca no caminho
crítico uma dependência externa com duração que a equipa não controla, e é precisamente
o que o pedido original queria evitar.

**HTTPS com certificado confiado mais tarde, sem adiar o arranque.** Não é alternativa, é
o que esta decisão deixa em aberto. Ver abaixo.

## Consequências

**Boas.** Ninguém vê aviso nenhum no primeiro acesso, e a equipa não é treinada a
clicar em avisos de certificado. A Fase 6 desaparece do plano. Não há certificado para
expirar nem para renovar. A armadilha do HSTS deixa de existir.

**Más.** O tráfego é legível por quem tiver acesso ao segmento de rede, numa aplicação
cujo valor assenta em os votos serem verdadeiros.

Isto é aceite por ser uma aplicação interna, numa rede corporativa, com trinta pessoas, e
sem dados além de ideias e votos que já são públicos dentro da equipa por desenho do
produto. É uma decisão proporcional a este contexto e não é uma posição sobre HTTP em
geral.

**O que fica em aberto, e é a única coisa que falta para a Fase 5.** O valor do nome e da
porta. A forma está decidida.

**E o que fica para rever.** HTTPS continua a ser possível mais tarde, e passar a ter o
não obriga a mudar arquitetura nenhuma, ao contrário do que aconteceu na direção
inversa. Se for revisto, a ordem do HSTS acima é a primeira coisa a ler.
