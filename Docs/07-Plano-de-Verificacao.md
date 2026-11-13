# 07. Plano de verificação

**Versão 2, 13 de novembro de 2026.**

## Três níveis, sem sobreposição

Cada nível responde a uma pergunta diferente. Um teste que responda à pergunta de
outro nível está no sítio errado, e o custo disso é uma suite lenta que falha em
vários sítios pelo mesmo motivo.

| Nível | Projeto | Pergunta a que responde |
|---|---|---|
| Unidade | `IdeaBoard.Tests` | As regras estão certas? |
| Integração | `IdeaBoard.Tests` | A base de dados faz o que assumimos? |
| Ponta a ponta | `IdeaBoard.E2E` | Uma pessoa consegue usar isto? |

xUnit em todos, para que a solução tenha um único framework de teste.

---

## Unidade

Sem base de dados, sem servidor, sem browser. Regras de domínio em isolamento.

- Validação dos limites de título e de descrição, incluindo o caso do texto feito só
  de espaços.
- Remoção de espaços no início e no fim antes de validar.
- Critério de ordenação do ranking, incluindo o desempate pela data mais recente.
- Decisão de votar ou de retirar o voto, consoante o estado atual.

É o nível onde o desempate se verifica de forma exaustiva, com casos construídos à
mão, porque é mais barato do que montar dados reais para a mesma coisa.

---

## Integração

Serviços contra **SQLite real**, numa base de dados temporária criada e destruída pelo
teste.

Não se usa um substituto em memória de outro fornecedor. O que interessa verificar
aqui é precisamente o comportamento do SQLite, e um substituto com semântica diferente
daria confiança em algo que não é o que corre em produção.

- **O índice único bloqueia mesmo o segundo voto.** Uma inserção direta de um voto
  repetido tem de falhar ao nível da base de dados.
- **A violação do índice único é tratada como sucesso idempotente**, e não propagada
  como erro. Este é o teste que protege o comportamento descrito em
  [02](02-Especificacao-Funcional.md).
- Retirar um voto que não existe não falha e não altera nada.
- As consultas do ranking e do detalhe devolvem a ordem correta com dados que forçam
  empates.
- O upsert do utilizador local cria na primeira vez e não duplica nas seguintes,
  mesmo quando o nome ou o email mudam entre inícios de sessão.
- A consulta do estado do botão de voto resolve se num só acesso, e não num por linha.

---

## Ponta a ponta

`Microsoft.Playwright.Xunit`, com browser real.

### Como a aplicação é arrancada

Um fixture de coleção xUnit arranca a aplicação em Kestrel numa porta livre, pedindo a
porta zero e lendo o endereço atribuído, com um ficheiro SQLite novo numa pasta
temporária por execução, migrado no arranque e apagado no fim.

Nada de base de dados partilhada entre execuções, e nada de IIS. A suite corre em
qualquer máquina que tenha o .NET e os browsers instalados, sem preparação prévia de
dados.

**A confirmar na Fase 1**: se o `WebApplicationFactory` do .NET 10 já expuser um
servidor Kestrel real de forma nativa, o fixture usa isso. Caso contrário arranca o
host diretamente. A escolha não tem consequências para o desenho dos testes.

### Como a autenticação é resolvida

O fixture substitui o esquema de autenticação por um handler de teste que lê um header
com o identificador do utilizador pretendido e devolve as claims correspondentes.

Isto dá o que o produto precisa de exercitar e o Entra ID não permite exercitar de
forma estável: **dois utilizadores diferentes na mesma execução**. Sem isso não é
possível provar que o voto é por pessoa, que a lista nominal mostra os nomes certos, e
que a contagem soma os dois.

Duas salvaguardas, porque um atalho de autenticação é exatamente o tipo de código que
não pode chegar a produção:

1. **O handler de teste vive no projeto de testes, nunca no projeto web.** O projeto
   publicado não contém nenhum código de autenticação de teste, nem sequer protegido
   por uma condição de ambiente. Uma condição de ambiente é uma linha que alguém pode
   inverter, e código que não existe não pode ser ativado por engano.
2. O fixture fornece valores fictícios para a secção `AzureAd`, para que o arranque do
   `Microsoft.Identity.Web` não falhe, e só depois sobrepõe o esquema por omissão.

Ver [ADR-011](ADR/ADR-011-autenticacao-nos-testes-e2e.md).

### O que esta suite não cobre

**O fluxo real do Entra ID nunca é exercitado.** O redirecionamento, a validação do
token emitido pelo tenant, o URI de redirecionamento do registo de aplicação e o
aprovisionamento do utilizador local a partir de claims verdadeiras ficam todos fora.

Isto está escrito aqui de propósito. É a diferença entre uma suite verde e uma
aplicação que funciona, e a compensação é a verificação manual mais abaixo, que é
obrigatória antes de cada lançamento e não opcional.

### Cenários

Um por funcionalidade do produto mínimo, mais os dois casos de limite que mais custam
se falharem.

| Cenário | O que prova |
|---|---|
| Submeter uma ideia e vê la em Recentes, com o nome do autor | F1 e parte de F4 |
| Votar, confirmar o incremento, retirar o voto, confirmar o decremento | F2, com a reversibilidade a ser o ponto |
| Dois utilizadores votam na mesma ideia, e ambos aparecem na lista nominal | F4, e a contagem por pessoa |
| Ranking ordenado por votos, com o desempate pela data mais recente | F3, incluindo [ADR-007](ADR/ADR-007-ordenacao-e-desempate.md) |
| Lista vazia mostra o convite a submeter a primeira ideia | O primeiro ecrã do dia do lançamento |
| Formulário rejeita título vazio e título acima do limite | Validação no servidor |

### Browsers

**Chromium apenas.** Aplicação interna, parque de máquinas conhecido, e cobrir três
motores seria custo sem risco correspondente. Ver
[ADR-012](ADR/ADR-012-chromium-apenas.md).

### Execução

Localmente, por quem desenvolve, e antes de publicar.

Pré requisito de máquina, uma vez por máquina e não um passo de build:

```
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

**Nota de contexto, não item de trabalho**: o repositório tem remoto, em
`https://github.com/Kail-the-akuma/AI_Exercise.git`, e o trabalho chega a `main` por
pull request. Não há pipeline de integração contínua configurada, e este plano não
assume nenhuma.

Correr esta suite automaticamente é portanto possível hoje, e a única preparação
necessária seria a instalação dos browsers no agente. Se passa a haver pipeline é uma
decisão por tomar, e não um pressuposto deste plano.

---

## Verificação manual antes do lançamento

Obrigatória. Cobre o que nenhum teste automático desta solução cobre, e passa pelas
definições de servidor que são invisíveis no código.

### Autenticação real

- [ ] Iniciar sessão com uma conta corporativa real, de alguém que **nunca** entrou
      antes, e confirmar que a linha de `User` é criada com o nome e o email certos.
- [ ] Confirmar que o nome apresentado na aplicação é o esperado.
- [ ] Terminar sessão e confirmar que o acesso a `/` volta a pedir autenticação.
- [ ] Confirmar que um acesso sem sessão a `/ideias/1` pede autenticação e, depois
      dela, chega à página pedida, e não à página inicial.

### Configuração de servidor

- [ ] Pool de aplicações com **um só processo de trabalho**.
- [ ] Chaves de Data Protection a serem escritas em `C:\IdeaBoard\keys\`. Confirmar
      que o ficheiro existe, e não apenas que a pasta está configurada.
- [ ] Reciclar o pool de aplicações e confirmar que uma sessão já iniciada **continua
      válida**. É o teste que prova que a persistência das chaves funciona, e falha
      silenciosamente se não for feito.
- [ ] Base de dados em `C:\IdeaBoard\data\`, fora da pasta da aplicação.
- [ ] Modo WAL ativo no ficheiro da base de dados.
- [ ] HTTPS a servir o site, com HTTP a redirecionar.
- [ ] Certificado emitido para o **nome DNS definitivo**, e não para o nome da máquina
      nem para um endereço IP.
- [ ] **HSTS desligado**, enquanto o certificado não for confiado. Confirmar que a
      resposta não traz o cabeçalho `Strict-Transport-Security`, e não apenas que
      ninguém se lembra de o ter ligado.
- [ ] Início de sessão completo a partir de uma máquina que nunca aceitou a exceção do
      certificado, aceitando o aviso pelo caminho. É o percurso que toda a equipa vai
      fazer no primeiro dia.

### Dados e operação

- [ ] Backup diário configurado e a produzir ficheiro.
- [ ] **Restauro ensaiado uma vez**, para uma cópia, com verificação de que os dados
      lá estão.
- [ ] As três consultas de métricas correm e devolvem valores plausíveis com dados
      reais.

### Produto

- [ ] Submeter uma ideia real e confirmar que aparece nos dois separadores.
- [ ] Votar a partir de duas contas diferentes e confirmar a contagem e a lista
      nominal.
- [ ] Confirmar que o aviso de voto público e o aviso de submissão definitiva estão
      visíveis **antes** da ação, e não depois.
- [ ] Percorrer a aplicação só com teclado.
- [ ] Desativar o JavaScript e confirmar que votar continua a funcionar, com
      recarregamento da página.

---

## Critério de pronto

Uma fase está pronta quando a suite dos três níveis passa, quando os cenários ponta a
ponta dessa fase existem e passam, e quando os itens manuais aplicáveis foram
verificados por uma pessoa.

Uma suite verde não é, por si só, critério de pronto. É condição necessária.
