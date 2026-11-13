# ADR-020. Migrações por script não idempotente, com verificação antes e depois

**Estado**: Aceite. **Data**: 13 de novembro de 2026.

## Contexto

O plano original mandava aplicar as migrações como passo explícito da publicação, "por
script idempotente", e não automaticamente no arranque da aplicação. A segunda metade
está certa e mantém se. A primeira não é possível.

**O EF Core não gera scripts idempotentes para SQLite.** A opção existe para outros
fornecedores e não para este, porque depende de construções condicionais de DDL que o
SQLite não tem. O script gerado é uma sequência de `CREATE TABLE` que falha se as
tabelas já existirem.

E há uma segunda propriedade do SQLite que torna isto pior do que parece: **um erro de
instrução não desfaz a transação em curso**. Num motor onde um erro aborta a transação,
uma segunda execução falharia inteira e não deixaria rasto. Aqui, uma segunda execução
pode falhar a meio e deixar o ficheiro num estado parcial.

## Decisão

O script é gerado uma vez, aplicado uma vez, e a sua aplicação é enquadrada por
verificação em vez de por idempotência.

O procedimento, por ordem:

1. **Ler a tabela `__EFMigrationsHistory` antes de aplicar.** É ela que diz o que já foi
   aplicado, e é a única fonte fiável disso. Se a migração já lá estiver, não se aplica
   nada.
2. **Aplicar com o `sqlite3` em modo `-bail`**, para que a execução pare na primeira
   instrução que falhe em vez de continuar a tentar as seguintes.
3. **Confirmar o código de saída igual a zero.** Sem isto, o passo anterior é um
   procedimento que parece ter corrido bem.

O script, verificado no ramo `fase1/identidade-windows`, cria a tabela de histórico com
`IF NOT EXISTS` fora da transação, abre a transação, cria as tabelas, insere a linha do
histórico e faz `COMMIT`. A proteção que existe é a da tabela de histórico, e mais
nenhuma.

## A migração inicial foi refeita, e não empilhada

Quando o esquema da tabela de utilizadores mudou com a troca de fornecedor de
identidade, havia duas saídas: acrescentar uma segunda migração que alterasse a
primeira, ou refazer a primeira.

Foi refeita. Existe uma só migração, `20261113171845_EsquemaInicial`, e o script de
publicação foi regenerado a partir dela.

**Isto só é legítimo porque não há nenhuma base de dados publicada.** Não há dados de
ninguém, não há histórico aplicado em lado nenhum, e refazer custa zero. Empilhar uma
migração de alteração produziria um histórico que conta uma evolução que nunca aconteceu
fora da máquina de quem a escreveu.

**E esta decisão tem prazo.** No dia em que existir uma base de dados em uso, refazer
deixa de ser opção e passa a ser perda de dados. A partir desse dia a única saída é
empilhar. Fica escrito aqui porque é exatamente o tipo de atalho que alguém repete por
ter corrido bem da primeira vez, sem reparar que o que o tornava seguro desapareceu
entretanto.

## Alternativas consideradas

**Aplicar as migrações no arranque da aplicação, com `Migrate()`.** Resolve a
idempotência sozinho, porque o EF Core consulta o histórico. Posta de lado porque faz a
alteração de esquema acontecer sem ninguém a olhar, porque dois processos a arrancar ao
mesmo tempo competem pela mesma alteração, e porque o momento da publicação é
precisamente quando se quer uma pessoa a ver o que está a acontecer.

**Escrever o script idempotente à mão**, com guardas por instrução. Posta de lado por
ser trabalho manual sobre um ficheiro gerado, e portanto perdido a cada regeneração, com
o agravante de uma guarda esquecida produzir silêncio em vez de erro.

**Trocar de fornecedor de base de dados para um que suporte scripts idempotentes.** Uma
escolha de arquitetura inteira para resolver um inconveniente de publicação. Fica
registada só para ficar fechada.

## Consequências

**Boas.** A alteração de esquema é um passo visível, executado por uma pessoa, com
estado verificável antes e depois. A tabela de histórico continua a ser a fonte da
verdade sobre o que foi aplicado.

**Más.** O procedimento depende de ser seguido pela ordem certa. Correr o script duas
vezes sem ler o histórico antes é um erro que o SQLite não impede e que pode deixar o
ficheiro a meio, e a única coisa que o previne é a lista escrita.

**O que isto obriga a fazer sempre.** Qualquer migração futura é empilhada, e a cópia de
segurança é tirada antes de aplicar e não depois. O [ADR-002](ADR-002-sqlite.md) já diz
que a cópia se tira por `VACUUM INTO` ou pelo comando próprio, e não por cópia do
ficheiro, o que com WAL ativo não é seguro.
