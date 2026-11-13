# ADR-019. O modo WAL é verificado no arranque, e não aplicado

**Estado**: Aceite. **Data**: 13 de novembro de 2026.

## Contexto

O [ADR-002](ADR-002-sqlite.md) escolheu SQLite em ficheiro, e o modo de journal em
write ahead logging é parte do que torna essa escolha segura com vários leitores e um
escritor.

O modo é uma propriedade **persistente do ficheiro**, e não da ligação. Fica gravado
quando é definido e lá permanece, o que levanta a pergunta de quem o define e de quando.

A minha posição inicial era que a aplicação o aplicasse no arranque, pelo argumento
óbvio de que assim nunca estaria por aplicar. Essa posição foi substituída, e este
registo existe para guardar a razão, que é melhor do que o argumento que ela derrubou.

## Decisão

A aplicação **confirma** no arranque que o ficheiro está em WAL, e **não o aplica**.

Um ficheiro que não esteja em WAL significa que o passo de aprovisionamento não foi
feito, e a aplicação diz isso em vez de o corrigir por baixo.

## Porque é que aplicar seria pior do que não verificar

Se a aplicação aplicasse o pragma e só depois o lesse, a verificação passaria a
confirmar aquilo que o próprio código acabara de fazer. Continuaria verde para sempre, e
deixaria de dar informação nenhuma sobre o estado em que o ficheiro chegou.

Uma verificação que não consegue falhar não é uma verificação. É a mesma classe de
defeito dos testes que afirmam sobre valores que eles próprios geraram, e que também já
apareceu neste projeto.

## E a parte que foi medida, em vez de assumida

É a parte que me convenceu, e não foi o argumento anterior.

Uma base de dados criada pelas ferramentas do EF Core **já vem em WAL**, porque o EF
Core ativa o modo nas bases de dados que cria. Contra uma dessas, esta verificação nunca
dispararia, e seria código a não fazer nada.

Só que o procedimento publicado não usa essas ferramentas. Aplica o script SQL gerado a
um ficheiro, e esse caminho deixa o ficheiro no modo por omissão. É exatamente aí que a
verificação ganha o seu lugar, e é um caminho que nenhum teste local percorre.

Apanha ainda outras três situações, todas reais: uma cópia de segurança restaurada, um
ficheiro produzido por outra ferramenta, e um modo alterado à mão por alguém a
diagnosticar um problema.

A diferença entre este raciocínio e o meu é que este sabe **contra o que é que a
verificação dispara**, e o meu só sabia que seria bom verificar.

## Alternativas consideradas

**A aplicação aplica o pragma no arranque.** Era a minha posição. Posta de lado pela
razão acima, que a verificação deixaria de poder falhar. Vale a pena notar que ela não é
errada por produzir um resultado mau, é errada por produzir um resultado que já não
informa.

**Não verificar de todo, e confiar no procedimento de publicação.** Posta de lado porque
o procedimento é executado por uma pessoa a seguir uma lista, e o sintoma de o saltar é
contenção de escrita sob carga, que é o tipo de problema que aparece tarde e se
diagnostica mal.

## Consequências

**Boas.** O estado do ficheiro é confirmado contra o que o procedimento diz, e a
confirmação tem capacidade real de falhar, que é a única coisa que a torna útil.

**Más.** Um ficheiro que chegue sem WAL impede o arranque, e a correção é manual.
Aceite, porque é precisamente o caso que se quer travar antes de haver escritas.

**Dependência de uma afirmação sobre ferramenta externa.** O raciocínio assenta em o EF
Core ativar WAL nas bases que cria. Se isso deixar de ser verdade numa versão futura, a
verificação não fica errada, fica apenas a disparar em sítios onde hoje não dispara, o
que é o lado seguro de a premissa mudar.
