# ADR-013. Certificado emitido pela equipa no arranque, certificado confiado depois

**Estado**: Substituído pelo [ADR-024](ADR-024-http-sem-certificado.md). **Data**: 4 de novembro de 2026.

## Contexto

O pedido inicial foi arrancar sem HTTPS e rever o assunto mais tarde, de forma a tirar
do caminho crítico a dependência da equipa de infraestrutura.

Isso não é possível com a autenticação escolhida, e a razão é concreta e não
negociável por configuração:

- O Entra ID **recusa** URIs de redirecionamento em HTTP, com a única exceção de
  `localhost`. Sem HTTPS não há sequer onde registar a aplicação.
- O token é devolvido por POST a partir de outro domínio, e os cookies temporários que
  ligam o pedido à resposta precisam de `SameSite=None`, que o browser só aceita em
  conjunto com `Secure`, que só viaja em HTTPS. Sem HTTPS o cookie é descartado e o
  início de sessão entra em ciclo.
- O cookie de sessão seguiria em texto limpo numa rede interna, o que permitiria a
  quem visse o tráfego votar e submeter ideias em nome de um colega. Numa plataforma
  cujo valor assenta em os votos serem nominais e verdadeiros, isso destrói o produto,
  e não apenas a segurança.

A alternativa de trocar para Windows Authentication, que funcionaria sobre HTTP, foi
posta de lado por ser uma mudança de arquitetura e não um adiamento.

## Decisão

HTTPS desde o primeiro dia, com um certificado que a **própria equipa emite**, sem
depender de pedido externo:

1. Da autoridade certificadora interna da organização, se existir.
2. Auto assinado, criado no servidor, se não existir.

O certificado é emitido para o **nome DNS definitivo**, e substituído por um
certificado confiado na Fase 6 de [05](../05-Plano-de-Entrega.md).

**O HSTS fica desligado até essa substituição estar feita.**

## A armadilha do HSTS

É a consequência menos óbvia desta decisão, e a que tem pior resultado.

Com HSTS ativo, um browser que encontre um erro de certificado termina a ligação e
**não oferece forma de prosseguir**. O botão de continuar deixa de existir. Ativar o
HSTS com um certificado auto assinado torna o site inacessível a toda a gente,
incluindo a quem o publicou.

E não se corrige depressa: o browser guarda a instrução pelo tempo declarado, portanto
desligar o HSTS no servidor não desbloqueia quem já a recebeu.

Por isso a ordem é fixa: certificado confiado primeiro, HSTS depois. Nunca ao
contrário.

## Alternativas consideradas

**Pedir o certificado à infraestrutura antes de arrancar.** É o caminho correto em
estado estacionário, e foi posto de lado por colocar uma dependência externa no
caminho crítico, com duração que a equipa não controla.

**Windows Authentication sobre HTTP.** A única forma de ter mesmo HTTP com as pessoas
autenticadas. Fica registada em [ADR-003](ADR-003-autenticacao-entra-id.md) como
alternativa, e recusada aqui porque só funciona dentro do domínio, obriga a ir buscar
o nome ao Active Directory, e trocar para Entra ID mais tarde seria refazer a
autenticação, não mudar uma definição.

**Adiar a publicação no servidor até haver certificado.** Nada mudaria na arquitetura,
mas a equipa ficaria sem aplicação acessível entretanto, o que contraria o objetivo de
ter a plataforma a funcionar cedo.

## Consequências

**Boas.** A Fase 5 deixa de depender de terceiros. A arquitetura não muda, e a troca
do certificado mais tarde não toca na aplicação, no registo do Entra ID, nem no nome
do site. O Entra ID funciona desde o primeiro dia, porque quem liga à aplicação é o
browser e não o Entra ID, e um certificado não confiado é um aviso, não um bloqueio.

**Más.** Toda a gente vê um aviso de segurança no primeiro acesso, e tem de o aceitar.

Isso tem um custo que não é apenas estético: ensina a equipa a clicar em avisos de
certificado, que é um hábito que se leva para outros sítios onde o aviso é verdadeiro.
É a razão pela qual a substituição é uma fase com dono e prazo, em
[05](../05-Plano-de-Entrega.md), e não uma intenção vaga. Um aviso que dura duas
semanas é um incómodo. Um aviso que dura um ano é formação em más práticas.

A comunicação de lançamento tem de explicar o aviso antes de as pessoas o encontrarem.
Quem vê um aviso destes sem explicação conclui que a ligação foi intercetada, e está
a raciocinar bem.

**Dependência que fica.** O registo DNS continua a depender da infraestrutura, e o
nome tem de ser o definitivo desde o início. Um nome provisório obrigaria a repetir o
registo no Entra ID no dia da troca, que é o trabalho que esta decisão procura evitar.
