# ADR-022. Resolução do nome por claim, depois diretório, e substituição por proveniência

**Estado**: Aceite. **Data**: 13 de novembro de 2026.

## Contexto

O briefing exige que o nome de cada pessoa seja visível como autora de ideias e na lista
de votantes. É a funcionalidade de transparência, e é aquilo em que o valor da
plataforma assenta.

Com autenticação integrada do Windows, o
[ADR-014](ADR-014-autenticacao-integrada-windows.md) deixa de poder contar com um nome
apresentável a vir na identidade. O que vem é uma conta, na forma `DOMINIO\pessoa`, e um
identificador de segurança. Nenhum dos dois se mostra a colegas.

Isto levanta duas perguntas separadas que é tentador tratar como uma: **de onde vem o
nome na primeira vez**, e **quando é que um nome já guardado pode ser trocado por outro**.

## Decisão

### De onde vem o nome

Por esta ordem, e verificado em `UserProvisioningService`:

1. **A claim, se existir.** Quando a identidade já traz um nome, nada mais é consultado e
   o diretório não é tocado.
2. **O diretório, se não existir claim de nome.** É o caso da autenticação integrada, e a
   consulta é feita pelo identificador de segurança.
3. **A própria pessoa**, quando o diretório não respondeu ou não tinha nome. É levada a
   registar um, e **a linha de utilizador não nasce até haver nome para escrever**.

**Não há recurso ao identificador como último recurso.** É a parte deliberada, e a razão
está escrita no código: um identificador mostrado como autor de uma ideia é
suficientemente plausível para sobreviver a uma revisão e chegar aos colegas. Uma
resposta vazia é a honesta, e é a única sobre a qual quem chama sabe agir.

### Quando é que o nome pode ser trocado

**Um nome que a pessoa registou nunca é substituído por um nome da organização.** Um
nome que veio da organização é atualizado quando a organização passar a dizer outra
coisa.

E, em qualquer dos casos, **um valor em branco nunca substitui um valor guardado**, para
que um início de sessão a que falte uma claim não possa apagar aquilo que um anterior
registou corretamente.

É isso que a coluna de proveniência serve, e é a única coisa que ela serve. O nome, por
si, não consegue dizer de onde veio.

## A minha posição era outra, e perdeu por boa razão

Eu defendia não atualizar nunca. O argumento era o da previsibilidade, que um nome
mostrado publicamente não deve mudar nas costas de quem o vê.

Foi substituída, e a razão que a derrubou é concreta: quem casa, quem muda de nome
oficialmente, ou quem foi registado com um nome mal escrito, fica com o nome errado para
sempre, numa aplicação onde esse nome aparece ao lado de cada ideia que submeter. A
previsibilidade que eu queria proteger protegia sobretudo o erro.

A decisão que ganhou é melhor do que as duas posições em cima da mesa, porque separa os
dois casos em vez de escolher um: a organização pode corrigir o que a organização
forneceu, e não pode mexer no que a pessoa escreveu sobre si própria.

## Alternativas consideradas

**Atualizar sempre, em cada início de sessão.** Simples e sem coluna extra. Posta de
lado porque apagaria o nome que alguém registou à mão, precisamente no caso em que o
registo manual existiu por o diretório não ter respondido. A pessoa voltaria a ficar sem
nome apresentável assim que o diretório voltasse a não responder.

**Nunca atualizar.** A minha posição. Descrita acima.

**Guardar três proveniências, com um estado para "desconhecido".** Posta de lado porque
não há estado desconhecido: uma linha sem nome não existe. Acrescentar o terceiro valor
seria modelar um estado que a invariante exclui, e convidar quem lê o enum a defender
esse estado em sítios onde ele não acontece.

## Consequências

**Boas.** Ninguém aparece aos colegas com uma conta de domínio nem com um
identificador. Uma correção de nome feita na organização chega à aplicação sem
intervenção. Um nome escrito pela própria pessoa é dela.

**Más.** Existe um caminho em que alguém autenticado com sucesso ainda não tem linha e é
desviado para registar um nome, e esse caminho tem de existir na interface. É uma página
que só se vê uma vez, ou nunca, e que portanto é das menos exercitadas da aplicação.

**O que fica por verificar fora do domínio.** Se o diretório responde, em que formato
responde, e o que acontece a uma conta sem nome de apresentação. Está na lista de
primeiro arranque em domínio do [ADR-014](ADR-014-autenticacao-integrada-windows.md), e
nada disto é exercitável onde o código foi escrito.
