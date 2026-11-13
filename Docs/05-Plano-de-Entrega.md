# 05. Plano de entrega

**Versão 1, 30 de outubro de 2026.**

Sete fases, da zero à seis. Cada uma entrega algo verificável, e nenhuma depende de
trabalho de uma fase posterior.

Não há estimativas de duração neste documento. A equipa que construir fixa as, e
números inventados nesta fase só serviriam para ser citados mais tarde como
compromisso.

## Fase 0. Pré requisitos

Nada aqui é código, e quase tudo depende de outras pessoas. É por isso a fase com
maior probabilidade de atrasar o resto, e deve começar antes de tudo o resto.

| Item | De quem depende | Bloqueia |
|---|---|---|
| Nome de produto e identificador de código decididos | Equipa | Fase 1 |
| Registo de aplicação no Entra ID, com URL de redirect | Equipa de identidade | Fase 1 |
| Servidor interno com IIS e ASP.NET Core Module v2 | Infraestrutura | Fase 5 |
| Nome DNS do site decidido, com registo a apontar para o servidor | Infraestrutura | Fase 5 |
| Certificado HTTPS emitido pela própria equipa | Equipa de desenvolvimento | Fase 5 |
| Pasta de dados e de chaves criadas, com permissões | Infraestrutura | Fase 5 |
| Valores alvo dos critérios de sucesso fixados | Equipa | Lançamento |
| Repositório git local, com `.gitignore` de .NET | Equipa de desenvolvimento | Fase 1 |

### O nome tem de ficar decidido primeiro

O identificador de código fica em namespaces, nomes de projeto, caminhos de ficheiros,
no nome do site do IIS e nos caminhos de dados do servidor. Mudá lo depois é uma
operação transversal e sem valor para ninguém.

`IdeaBoard` é o nome de trabalho usado em toda esta documentação, e serve se não
houver melhor. O nome de produto em prosa é uma decisão separada, pode ser diferente
do identificador de código, e pode ser tomada mais tarde.

### O spike de autenticação

Antes da Fase 1, confirmar se uma aplicação que apenas autentica, sem chamar nenhuma
API a jusante, funciona sem client secret no registo da organização. A resposta muda o
que a infraestrutura tem de provisionar e tem de estar fechada antes de alguém esperar
por um segredo que afinal não era preciso, ou de construir assumindo que não era
preciso e descobrir que era. Ver
[ADR-003](ADR/ADR-003-autenticacao-entra-id.md).

### O certificado no arranque, e o nome que ele carrega

A aplicação arranca com um certificado emitido pela própria equipa, da autoridade
certificadora interna se existir, auto assinado se não existir. Decisão e consequências
em [ADR-013](ADR/ADR-013-certificado-no-arranque.md).

Isto retira a dependência da infraestrutura do caminho crítico, ao preço de um aviso de
segurança no browser até o certificado definitivo chegar. O Entra ID continua a
funcionar sem qualquer alteração, porque quem liga à aplicação é o browser e não o
Entra ID.

**O nome DNS tem de ser o definitivo desde o primeiro dia.** O mesmo nome entra no
certificado, no URI de redirecionamento do registo do Entra ID e, mais tarde, no
certificado de substituição. Emitir o certificado de arranque para um nome provisório
obrigaria a repetir o registo da aplicação no dia da troca, que é precisamente o
trabalho que esta decisão procura evitar.

É também o único destes dois itens que continua a depender da infraestrutura, porque o
registo DNS não é a equipa de desenvolvimento que o cria.

---

## Fase 1. Esqueleto, autenticação e andaime dos testes

**Entrega**: uma aplicação que não faz nada, mas que autentica uma pessoa real contra
o Entra ID, regista a no banco local, e tem uma suite de testes capaz de a arrancar e
navegar nela.

- Solução com os três projetos descritos em [01](01-Arquitetura.md).
- Início e fim de sessão com `Microsoft.Identity.Web`.
- Política de autorização de fallback a exigir autenticação em tudo.
- `UserProvisioningService` com o upsert do utilizador local.
- Persistência das chaves de Data Protection configurada desde já.
- Projetos de teste com o fixture de arranque, o handler de autenticação de teste e um
  cenário mínimo que abre a aplicação autenticada e confirma que carrega.

### Porque é que os testes nascem agora

Construir o andaime depois das funcionalidades significa escrever testes contra código
já dado como feito, o que os transforma em descrição do que ficou em vez de
verificação do que devia ser. O investimento é o mesmo e o retorno é menor.

Além disso, o fixture de arranque e a substituição do esquema de autenticação são a
parte tecnicamente mais incerta da suite. Descobrir um problema aí na Fase 1 custa uma
tarde. Descobri lo na Fase 4 custa a confiança em tudo o que foi construído entretanto.

---

## Fase 2. Modelo de dados

**Entrega**: esquema criado, migração inicial versionada, e a regra de um voto por
pessoa provada contra SQLite real.

- Entidades `User`, `Idea` e `Vote`.
- `IdeaBoardDbContext` com a configuração por Fluent API.
- Primeira migração, incluindo o índice único em `Vote(ideaId, userId)`.
- Script SQL idempotente gerado a partir da migração, e o procedimento de o aplicar.
- Testes de integração a provar que o segundo voto da mesma pessoa na mesma ideia é
  rejeitado pela base de dados.

O detalhe está em [03](03-Modelo-de-Dados.md).

---

## Fase 3. Submissão e listagem

**Entrega**: as pessoas conseguem submeter ideias e vê las.

- Formulário em `/nova`, com validação no servidor e os avisos de permanência.
- Separador Recentes em `/recentes`.
- Página de detalhe em `/ideias/{id}`, ainda sem lista de votantes.
- Estado vazio da lista.
- Cenários ponta a ponta de submissão, de validação e de estado vazio.

A partir desta fase a aplicação já tem utilidade, e pode ser mostrada a alguém de fora
da equipa de desenvolvimento para recolher reação antes de estar completa.

---

## Fase 4. Voto, ranking e votantes

**Entrega**: o produto mínimo completo.

- `VoteService`, com voto, retirada de voto e tratamento idempotente da violação do
  índice único.
- Botão de voto com os dois estados, e o script de melhoria progressiva.
- Ranking em `/`, com a ordenação e o critério de desempate de
  [ADR-007](ADR/ADR-007-ordenacao-e-desempate.md).
- Lista nominal de votantes no detalhe.
- Cenários ponta a ponta de voto, de reversão, de dois utilizadores diferentes e de
  desempate.

---

## Fase 5. Publicação e lançamento

**Entrega**: aplicação a correr no servidor interno, verificada, com conteúdo no dia
em que abre.

- Publicação em IIS conforme [06](06-Operacao-e-Publicacao.md).
- Certificado de arranque emitido e associado ao site, **com HSTS desligado**. A razão
  está em [ADR-013](ADR/ADR-013-certificado-no-arranque.md) e não é opcional.
- Aviso à equipa, na comunicação de lançamento, de que o browser vai mostrar um aviso
  de segurança e porquê. Uma pessoa que encontra um aviso destes sem explicação assume
  que a ligação foi intercetada, e tem razão em assumir.
- Migração aplicada por script, com backup prévio.
- Suite completa verde.
- **Verificação manual do início de sessão real contra o Entra ID**, que nenhum teste
  automático cobre. A lista está em [07](07-Plano-de-Verificacao.md).
- Backup diário configurado e um restauro ensaiado pelo menos uma vez.
- Consultas das métricas de sucesso guardadas e corridas uma vez, para confirmar que
  devolvem o que se espera com dados reais.

### Arranque com conteúdo

O briefing identifica o nascimento vazio como risco real. A resposta não é uma
funcionalidade, é um ato de organização: **a plataforma abre com ideias já lá dentro.**

Antes de anunciar, um pequeno grupo submete as suas próprias ideias, as verdadeiras,
não exemplos de demonstração. Uma lista com cinco ou seis ideias de pessoas conhecidas
convida a participar. Uma lista vazia não.

O briefing diz também que menos de cinco ideias ao fim de duas semanas é motivo para
reconsiderar. Essa contagem só é honesta se as ideias de arranque forem distinguidas
das espontâneas, portanto convém registar quais foram, fora da plataforma.

---

## Fase 6. Substituição do certificado

A única coisa nesta documentação que é trabalho adiado em vez de trabalho excluído.
Fica como fase própria, e não como nota de rodapé, porque tem dono, tem passos
definidos e tem uma data a partir da qual deixa de ser aceitável adiar.

**Entrega**: certificado confiado pelas máquinas da organização, aviso de segurança
eliminado, HSTS ativado.

- Pedido do certificado à infraestrutura, para o mesmo nome DNS já em uso.
- Substituição da associação no IIS. A aplicação não muda, o registo no Entra ID não
  muda, e o nome não muda.
- **Só depois disto, ativar o HSTS.** Pela ordem inversa, o site fica inacessível.
- Confirmar o início de sessão sem aviso, a partir de uma máquina que nunca aceitou a
  exceção do certificado anterior.

O prazo é a decidir com a equipa no lançamento. O que não pode acontecer é ficar sem
prazo nenhum, porque um aviso de segurança que dura meses deixa de ser notado, que é
exatamente o efeito que esta decisão quer evitar.

---

## Depois do lançamento

Não é uma fase, e nada disto é trabalho agendado. São os sinais que o briefing manda
observar, e o sítio onde ficam registados para que a avaliação aos três meses não
comece do zero.

| Sinal | O que indica |
|---|---|
| Ideias no topo há semanas sem atividade à volta | O risco principal, o interesse não se converte em nada |
| Menos de cinco ideias ao fim de duas semanas | O arranque falhou |
| As primeiras ideias permanentemente no topo | A lista cristalizou |
| Pedidos de correção de títulos ou de remoção | A ausência de edição tornou se incómoda, como o briefing prevê |
| Pessoas a dizer que não votam por o voto ser público | A transparência está a inibir. O briefing sugere perguntar ao fim de um mês |
| Mais de cinquenta ideias | A navegação começa a degradar se |

A avaliação formal é aos três meses, com as três métricas de participação definidas no
briefing. As consultas estão em [06](06-Operacao-e-Publicacao.md).

Nenhum destes sinais é, por si só, autorização para acrescentar funcionalidades. São
material para uma conversa humana sobre o âmbito.
