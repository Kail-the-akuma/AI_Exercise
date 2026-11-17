# Briefing: plataforma interna de ideias

**Versão 2, 17 de novembro de 2026.**

## Resumo

Uma aplicação web interna onde qualquer pessoa da área de TI e produto propõe uma
ideia e vota nas dos outros. O resultado é uma lista pública ordenada por apoio real.

O propósito é promover iniciativa e colaboração. As ideias mais votadas servem para a
equipa aprender e explorar em conjunto, não para alimentar um backlog de entrega.
Ninguém se compromete a construir o que ganha, e isso é intencional.

## Problema

Hoje não existe sítio onde uma ideia possa ser posta à frente da equipa e ganhar
visibilidade pelo mérito. As sugestões vivem em conversas e desaparecem, e quem tem
iniciativa não tem como saber se mais alguém partilha o mesmo interesse.

Falta um espaço comum onde o interesse coletivo se torne visível.

## Objetivo

Dar à equipa um lugar permanente para propor e descobrir temas que valem a pena
explorar em conjunto, e tornar visível quem se interessa por quê.

O sucesso é as pessoas participarem. Não é o número de ideias implementadas.

## Utilizadores

Área de TI e produto, entre 15 e 50 pessoas, todas autenticadas com a conta
corporativa. Não há distinção de papéis, toda a gente propõe e vota em igualdade.

## Produto mínimo

Quatro coisas, e nada mais.

**Submeter uma ideia.** Título e descrição. Fica associada ao nome de quem a propôs.

**Votar.** Um voto por pessoa por ideia, reversível a qualquer momento. Sem voto
negativo e sem orçamento de votos.

**Ver o ranking.** Separador principal com as mais votadas, segundo separador com as
mais recentes. Lista sempre aberta, sem ciclos nem datas de fecho.

**Ver quem está por trás.** O autor de cada ideia e a lista nominal de quem votou são
públicos para toda a equipa. A transparência é uma escolha deliberada, não uma
omissão.

Qualquer ideia é bem vinda, sem restrição de tema. Tecnologias a experimentar,
melhorias ao dia a dia, protótipos, temas de partilha, tudo cabe na mesma lista.

## Fora do âmbito

Decidido, não esquecido.

Sem comentários. Sem categorias. Sem administração. Sem edição ou remoção de ideias
depois de submetidas. Sem deteção de duplicados. Sem estados, triagem ou aprovação. Sem
mecanismo formal de exploração, inscrição em ideias ou registo do que se aprendeu.

O que acontece a uma ideia muito votada fica em aberto, por decisão. Quem quiser
pegar nela organiza se fora da plataforma.

### O âmbito foi alargado a 16 de novembro de 2026

**Decisão do Hugo, tomada com esta lista à frente, e não deslize de construção.** Fica
registado aqui com a data porque esta lista é a referência contra a qual tudo o resto se
mede, e uma exclusão que desaparece sem explicação é indistinguível de uma que ninguém
respeitou.

Saiu daqui a **pesquisa**, que era excluída pela palavra e passou a existir, por título e
por quem propôs.

Entraram, sem estarem nesta lista mas contra o espírito dela de produto mínimo, um
**separador pessoal** com as ideias propostas e apoiadas, um **aviso de votos novos** nas
ideias de cada pessoa, as **iniciais de quem votou** ao lado de cada ideia na lista, e o
**número de pessoas que já entrou no quadro**.

O resto desta lista mantém se inteiro. Em particular continua a não haver comentários,
categorias, administração, edição, remoção, deteção de duplicados, estados nem triagem.

**O que isto não é.** Não é autorização para continuar a alargar. A lista encolheu uma
vez, por decisão explícita de quem é dono do produto, e volta a precisar do mesmo para
encolher outra vez.

## Critérios de sucesso

Avaliação aos três meses, medindo participação.

- Número de ideias submetidas.
- Percentagem de pessoas que votou pelo menos uma vez.
- Número de pessoas distintas que submeteu pelo menos uma ideia, que distingue uma
  comunidade de um monólogo.

Valores alvo a fixar antes do lançamento. Como referência para discussão, numa equipa
de 30 pessoas, vinte ideias e metade da equipa a votar seria um resultado saudável.

## Riscos assumidos

Nenhum destes é motivo para mudar o âmbito. São consequências conhecidas das decisões
tomadas e devem ser observadas depois do lançamento.

**Sem mecanismo de exploração, o interesse pode não se converter em nada.** É o risco
principal. A plataforma mede entusiasmo mas não o transforma em ação, e a motivação
para votar diminui quando nada acontece às ideias vencedoras. Sinal a vigiar: ideias no
topo há semanas sem qualquer atividade à volta.

**Sem ação de arranque, a plataforma pode nascer vazia.** Uma lista sem conteúdo não
convida ninguém a escrever o primeiro. Se ao fim de duas semanas houver menos de cinco
ideias, vale a pena reconsiderar.

**Lista contínua cristaliza.** Sem ciclos nem decaimento, as primeiras ideias
acumulam votos e ficam no topo permanentemente, o que desincentiva quem chega depois.

**Sem edição nem administração, os erros são permanentes.** Um título mal escrito ou
uma ideia submetida por engano ficam para sempre. É a decisão com maior probabilidade
de se tornar incómoda primeiro.

**A lista pública de votantes inibe.** Há quem não vote em ideias que impliquem
criticar trabalho existente. Vale a pena perguntar às pessoas passado um mês.

**Sem categorias nem pesquisa, a navegação degrada se.** Acima de cerca de cinquenta
ideias a lista torna se difícil de percorrer.

> Risco parcialmente mitigado a 16 de novembro de 2026. A pesquisa por título e por quem
> propôs passou a existir. As categorias continuam fora do âmbito, e a lista continua a
> ser percorrida por inteiro quando ninguém procura nada, portanto o sinal a observar
> mantém se.



## Notas técnicas

Fora do âmbito deste briefing, registado apenas para continuidade. Preferência
indicada: ASP.NET Core com Razor Pages, EF Core sobre SQLite, autenticação por
Microsoft Entra ID, publicação em IIS num servidor interno.

## Próximo passo

Validar este briefing com a equipa, fixar os valores alvo dos critérios de sucesso e
só depois abrir a conversa de desenho e construção.
