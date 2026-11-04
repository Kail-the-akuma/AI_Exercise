# ADR-004. Tabela local de utilizadores, com o identificador de objeto como chave

**Estado**: Aceite. **Data**: 30 de outubro de 2026.

## Contexto

O briefing torna públicos o autor de cada ideia e a **lista nominal de quem votou**.
Para mostrar uma lista de nomes de votantes, a aplicação tem de saber o nome de cada
pessoa que votou, e não apenas o da pessoa que está autenticada no momento.

As claims do token só descrevem quem está autenticado agora. Não servem para
renderizar nomes de terceiros.

## Decisão

Uma tabela `User` local, com uma linha por pessoa que tenha iniciado sessão pelo menos
uma vez. É preenchida por upsert no início de sessão, a partir das claims do token.

**A chave de correspondência é o identificador de objeto do Entra ID, nunca o email.**

As ideias e os votos referenciam esta tabela por chave estrangeira.

## Alternativas consideradas

**Guardar o nome em cada voto e em cada ideia.** Evitaria a tabela, mas duplicaria o
nome por todas as linhas, e uma mudança de nome deixaria registos inconsistentes, com
a mesma pessoa a aparecer com dois nomes diferentes em sítios diferentes.

**Consultar o Microsoft Graph para resolver nomes.** Obrigaria a pedir permissões,
a gerir tokens de aplicação e a tratar falhas de rede e limites de ritmo, tudo para
mostrar nomes de uma lista de cinquenta pessoas. Introduziria uma dependência externa
em tempo de leitura numa aplicação que de outro modo não tem nenhuma.

**Usar o email como chave.** É legível e tentador. Está errado: o email de uma pessoa
muda, por casamento ou por alteração de nome na organização, e o identificador de
objeto não muda. Com o email como chave, a primeira pessoa a mudar de nome passaria a
ter duas identidades na aplicação, com os seus votos e ideias repartidos entre as
duas.

## Consequências

**Boas.** A lista nominal de votantes renderiza se com uma junção local, sem chamadas
externas. A aplicação continua a funcionar com o Entra ID indisponível, para quem já
tenha sessão. A identidade é estável ao longo do tempo.

**Más.** O nome guardado é o do **primeiro** início de sessão, e pode ficar
desatualizado se não for atualizado nos seguintes.

O upsert atualiza o nome e o email a cada início de sessão, precisamente para que isto
não aconteça. A consequência que resta é que quem muda de nome e não volta a entrar
continua a aparecer com o nome antigo nas ideias e nos votos antigos. É aceitável, e
é também o comportamento que a maioria das pessoas espera ao olhar para um histórico.

**Também má.** A tabela só contém quem já entrou, e não a equipa toda. Isto tem
consequência direta no cálculo da percentagem de participação pedida pelo briefing, e
está explicado em [06](../06-Operacao-e-Publicacao.md). Usar esta tabela como
denominador daria um número indevidamente otimista.
