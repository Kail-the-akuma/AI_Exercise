# ADR-012. Testes ponta a ponta apenas em Chromium

**Estado**: Aceite. **Data**: 30 de outubro de 2026.

## Contexto

O Playwright corre os mesmos testes em Chromium, Firefox e WebKit sem alterações ao
código. Correr nos três é quase gratuito de escrever, e por isso é tentador fazê lo
por omissão.

A aplicação é interna, com um parque de máquinas conhecido e gerido pela organização,
e a interface consiste em listas, um formulário e um botão, com renderização no
servidor e algumas dezenas de linhas de JavaScript.

## Decisão

A suite ponta a ponta corre apenas em Chromium.

## Alternativas consideradas

**Os três motores.** Triplica o tempo de execução e o número de resultados a
interpretar, e triplica também a superfície de falhas intermitentes, que não se
distribuem de forma igual entre motores. O retorno seria detetar incompatibilidades
numa aplicação que praticamente não usa JavaScript e cujos utilizadores usam todos o
mesmo browser.

**Chromium, mais Firefox num segundo conjunto corrido com menos frequência.**
Defensável, e adiado. Enquanto não houver JavaScript com peso suficiente para que uma
incompatibilidade seja plausível, é trabalho sem alvo.

## Consequências

**Boas.** Suite rápida, menos instável, e um único browser a instalar por máquina.

**Más.** Uma incompatibilidade específica de Firefox ou de Safari passaria
despercebida à suite. O risco é baixo pelas razões acima, e não é nulo: alguém pode
abrir a aplicação no telemóvel, e aí o motor é outro.

**Revisão desta decisão.** Deixa de fazer sentido no dia em que o JavaScript da
aplicação crescer para além do script do botão de voto, ou em que o acesso por
telemóvel passar a ser comum. Acrescentar um motor é um parâmetro na configuração da
suite, portanto a decisão é barata de reverter e não vale a pena antecipar.
