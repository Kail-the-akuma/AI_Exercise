# ADR-002. EF Core sobre SQLite, em ficheiro

**Estado**: Aceite. **Data**: 30 de outubro de 2026.

## Contexto

Entre quinze e cinquenta utilizadores, dezenas de escritas por dia, e um volume total
que ao fim de um ano se mede em poucos megabytes. A publicação é num servidor interno
com IIS.

O briefing já indicava SQLite como preferência. A alternativa realista é pedir uma
base de dados numa instância de SQL Server corporativa.

## Decisão

SQLite em ficheiro, acedido por EF Core com o provider
`Microsoft.EntityFrameworkCore.Sqlite`, em modo WAL, com o ficheiro fora da pasta de
publicação.

O pool de aplicações do IIS fica limitado a **um processo de trabalho**.

## Alternativas consideradas

**SQL Server numa instância corporativa.** Traz backup, alta disponibilidade e
monitorização já geridos por quem faz isso a sério, o que é um argumento real. Mas
introduz uma dependência externa logo no arranque: um pedido de provisionamento, uma
espera, uma credencial a gerir e um ponto de falha fora do controlo da equipa. Para
uma aplicação desta dimensão, o custo de coordenação é maior do que o benefício.

**PostgreSQL ou outro motor instalado para o efeito.** Instalar e manter um servidor
de base de dados dedicado a esta aplicação seria a infraestrutura a exceder largamente
o problema.

**LiteDB ou persistência em ficheiros.** Menos capaz do que SQLite, sem a mesma
maturidade, e sem ganho nenhum em simplicidade.

## Consequências

**Boas.** Zero infraestrutura. A base de dados é um ficheiro, o backup é um comando, e
o ambiente de desenvolvimento é idêntico ao de produção. Os testes de integração
correm contra o mesmo motor que corre em produção, o que é raro e valioso.

**Más, e todas com resposta documentada em [06](../06-Operacao-e-Publicacao.md).**

O SQLite aceita leituras concorrentes mas uma única escrita de cada vez. Daqui decorre
a limitação a um processo de trabalho no IIS, que é a definição mais fácil de esquecer
e a mais difícil de diagnosticar depois.

Os ficheiros `-wal` e `-shm` vivem ao lado do principal, o que faz com que a permissão
de escrita tenha de ser **na pasta** e não no ficheiro, e com que o backup não possa
ser uma cópia simples.

O ficheiro dentro da pasta de publicação é apagado na primeira republicação, o que
obriga a colocá lo fora.

Não há alta disponibilidade, e não há forma de a acrescentar sem mudar de motor.

## Caminho de saída

Se a aplicação alguma vez precisar de SQL Server, a mudança é contida porque o acesso
a dados passa todo pelo EF Core e nenhum SQL específico de motor está espalhado pelo
código. O trabalho real seria regenerar as migrações e migrar os dados existentes, não
reescrever a aplicação.

Isto é uma propriedade da escolha, não um plano. Não há intenção de migrar.
