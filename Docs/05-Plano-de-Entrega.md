# 05. Plano de entrega

**Versão 2, 13 de novembro de 2026.**

Sete fases, da zero à seis, das quais a última está encerrada por ter deixado de ter
objeto. Cada uma entrega algo verificável, e nenhuma depende de trabalho de uma fase
posterior.

Não há estimativas de duração neste documento. A equipa que construir fixa as, e
números inventados nesta fase só serviriam para ser citados mais tarde como
compromisso.

## Fase 0. Pré requisitos

Nada aqui é código, e quase tudo depende de outras pessoas. É por isso a fase com
maior probabilidade de atrasar o resto, e deve começar antes de tudo o resto.

| Item | De quem depende | Bloqueia | Estado |
|---|---|---|---|
| Identificador de código decidido | Equipa | Fase 1 | Fechado em `IdeaBoard` |
| Nome de produto em prosa decidido | Equipa | Lançamento | **Em aberto** |
| Servidor interno com IIS e ASP.NET Core Module v2 | Infraestrutura | Fase 5 | Por fazer |
| Endereço do site decidido | Equipa | Fases 1 e 5 | Decidido na forma, **valor por preencher** |
| Pasta de dados e de chaves criadas, com permissões | Infraestrutura | Fase 5 | Por fazer |
| Valores alvo dos critérios de sucesso fixados | Equipa | Lançamento | **Em aberto** |
| Repositório git com remoto e `.gitignore` de .NET | Equipa de desenvolvimento | Fase 1 | Fechado |

O **endereço é o nome curto do servidor com porta**, e não o nome qualificado. A forma
está decidida e o valor concreto ainda não, porque o servidor ainda não está escolhido.

> **Os pré requisitos de identidade saíram desta tabela por deixarem de existir.**
> A 13 de novembro de 2026 a autenticação mudou de Microsoft Entra ID para autenticação
> integrada do Windows, registada em
> [ADR-014](ADR/ADR-014-autenticacao-integrada-windows.md), e o transporte passou a HTTP
> sem certificado, registado em [ADR-024](ADR/ADR-024-http-sem-certificado.md).
>
> Com isso deixaram de existir o registo de aplicação no Entra ID, o spike do client
> secret e o certificado HTTPS. Os três saíram daqui, e saíram por não terem objeto e não
> por terem sido resolvidos.
>
> **O que entrou no lugar foi menos e não mais.** A Fase 0 deixou de depender da equipa
> de identidade por inteiro. O que resta dela depende da infraestrutura, para o servidor
> e as pastas, e da equipa, para o endereço e os valores alvo.

### O identificador de código ficou decidido, o nome de produto não

São duas decisões distintas e só uma está fechada.

O identificador de código é **`IdeaBoard`**, e está ratificado. Fica em namespaces,
nomes de projeto, caminhos de ficheiros, no nome do site do IIS e nos caminhos de dados
do servidor. Mudá lo agora seria uma operação transversal e sem valor para ninguém.

O **nome de produto em prosa continua em aberto**, e é uma decisão reservada a quem é
dono do produto. Pode ser diferente do identificador de código e pode ser tomado até ao
lançamento. Nenhum documento deve inventar um, nem tratar `IdeaBoard` como se fosse o
nome de produto.

### O spike de autenticação, encerrado por deixar de se aplicar

Esta subsecção descrevia um spike para confirmar se o registo no Entra ID precisava de
client secret. Com a mudança para autenticação Windows deixa de haver registo de
aplicação e deixa de haver segredo, portanto o spike não tem objeto.

Fica registado que foi encerrado por deixar de se aplicar, e não por ter sido
respondido. O que substitui esta subsecção depende do ADR que substitui o
[ADR-003](ADR/ADR-003-autenticacao-entra-id.md), e é do ARQ.

### O endereço tem de ser o definitivo desde o primeiro dia

Este requisito sobreviveu a duas mudanças de desenho e as suas três razões originais
morreram todas. Fica aqui com a razão que resta, porque sem ela um leitor cuidadoso
verifica os destinos antigos, encontra zero, e remove o requisito por obsoleto.

As razões antigas eram o certificado, o URI de redirecionamento do registo do Entra ID
e o certificado de substituição. Não há certificado e não há registo de aplicação.

**A razão que sobrevive é o início de sessão automático.** A forma do endereço determina
em que zona de segurança o browser coloca o site, e a zona determina se a autenticação
integrada acontece sem pedir credenciais. Um nome sem pontos é colocado na zona de
intranet local por omissão. Isso aplica se quando o parque não tem lista de servidores
de autenticação definida por política, e havendo lista é ela que governa, passando a
decidir o nome constar dela.

O endereço decidido é o **nome curto do servidor com porta**, que é a forma favorável a
esta zona. O valor concreto fica por preencher.

Mudar o endereço depois obriga a mudar a comunicação de lançamento, os atalhos e os
marcadores de toda a gente, o que é razão bastante por si só.

O registo de DNS, se for preciso, continua a depender da infraestrutura, porque não é a
equipa de desenvolvimento que o cria.

---

## Fase 1. Esqueleto, autenticação e andaime dos testes

**Entrega**: uma aplicação que não faz nada, mas que reconhece uma pessoa real pela
identidade que o IIS fornece, cria a linha dela localmente, e tem uma suite de testes
capaz de a arrancar e navegar nela.

- Solução com os projetos descritos em [01](01-Arquitetura.md), conforme
  [ADR-015](ADR/ADR-015-estrutura-de-quatro-projetos.md).
- Esquema de autenticação do IIS registado, conforme
  [ADR-014](ADR/ADR-014-autenticacao-integrada-windows.md).
- Política de autorização de fallback a exigir autenticação em tudo.
- Criação do utilizador local, com resolução do nome pelo diretório e registo manual
  quando a consulta não devolve nada, conforme
  [ADR-022](ADR/ADR-022-resolucao-e-substituicao-do-nome.md).
- Persistência das chaves de Data Protection configurada desde já.
- Projetos de teste com o fixture de arranque sobre o host local, sem handler de
  autenticação de teste, conforme
  [ADR-016](ADR/ADR-016-identidade-nos-testes-por-host-proprio.md), e um cenário mínimo
  que abre a aplicação autenticada e confirma que carrega.

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
- Script SQL gerado a partir da migração, e o procedimento de o aplicar. O script não
  é idempotente, porque o EF Core recusa gerar scripts idempotentes para SQLite. Ver
  [06](06-Operacao-e-Publicacao.md).
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

- Publicação em IIS conforme [06](06-Operacao-e-Publicacao.md), com autenticação Windows
  ativada no site e o acesso anónimo desligado.
- Endereço fixado no valor concreto, com a forma já decidida em
  [ADR-024](ADR/ADR-024-http-sem-certificado.md).
- Migração aplicada por script, com backup prévio.
- Suite completa verde.
- **Verificação manual numa máquina em domínio**, que nenhum teste automático cobre,
  porque nada disto é exercitável no ambiente onde o código foi escrito. A lista está em
  [07](07-Plano-de-Verificacao.md).
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

## Fase 6. Encerrada, por ter deixado de ter objeto

Esta fase existia para substituir o certificado de arranque por um certificado confiado
e só depois ativar o HSTS. Com a decisão de
[HTTP sem certificado](ADR/ADR-024-http-sem-certificado.md) não há certificado para
substituir, e a fase não tem trabalho nenhum.

**Fica aqui encerrada com o motivo, e não apagada.** Era a única coisa nesta
documentação descrita como trabalho adiado em vez de trabalho excluído, e apagá la
deixaria quem a procurasse sem saber se foi feita, se foi cancelada, ou se alguém se
esqueceu dela.

**Encerrada por deixar de se aplicar não é o mesmo que concluída.** Se o HTTPS voltar
algum dia, o trabalho desta fase volta com ele, e volta com a regra de ordem intacta:
certificado confiado primeiro, HSTS depois, nunca ao contrário. Essa regra está em
[04](04-Requisitos-Nao-Funcionais-e-Seguranca.md) e no
[ADR-024](ADR/ADR-024-http-sem-certificado.md), e não aqui, porque uma fase encerrada
não é sítio onde alguém vá procurar uma regra em vigor.

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
