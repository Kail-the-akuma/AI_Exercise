# ADR-026. Onde vive a configuração sem a qual a aplicação não arranca

**Estado**: Aceite. **Data**: 17 de novembro de 2026.
**Emenda**: [ADR-018](ADR-018-validacao-de-configuracao-no-arranque.md).

## Contexto

O [ADR-018](ADR-018-validacao-de-configuracao-no-arranque.md) decidiu que a aplicação
recusa arrancar com configuração obrigatória em falta, e nomeia o que falta. Deixou por
responder a pergunta seguinte, que é a que bloqueia mesmo o primeiro arranque: **onde é
que quem publica põe os valores.**

Uma guarda que pára o arranque e nomeia duas chaves só serve de alguma coisa a quem saiba
onde as escrever. A quem não saiba, a guarda não é proteção, é uma porta fechada com o
nome da fechadura escrito na frente.

O `appsettings.json` publicado traz as duas chaves **vazias de propósito**, verificado em
`src/IdeaBoard.Web/appsettings.json`: `ConnectionStrings:IdeaBoard` e
`DataProtection:KeysPath` são cadeias vazias. A ausência de um valor por omissão é
deliberada, porque um valor por omissão é exatamente aquilo em que um servidor mal
configurado cai para funcionar errado em vez de parar.

## Decisão

As duas chaves vivem no **`web.config` do site, como variáveis de ambiente do módulo**, e
esse ficheiro é versionado no repositório, em `src/IdeaBoard.Web/web.config`.

| nome da variável | chave de configuração |
|---|---|
| `ConnectionStrings__IdeaBoard` | `ConnectionStrings:IdeaBoard` |
| `DataProtection__KeysPath` | `DataProtection:KeysPath` |

O duplo sublinhado é como uma secção de configuração se escreve numa variável de ambiente.

O mesmo ficheiro carrega as duas definições de autenticação do site, a do Windows ligada e
a anónima desligada, pelas razões que a secção sobre o ADR-018 trata mais abaixo.

## A publicação transforma este ficheiro, não o gera

Isto é um facto sobre a ferramenta e não uma preferência, e decide se o ficheiro é código
fonte ou artefacto.

**Verificado por comparação dos dois ficheiros**, o de origem e o publicado em
`publish/IdeaBoard/web.config`: os comentários sobrevivem inteiros, as duas variáveis de
ambiente sobrevivem com os valores escritos à mão, e o que a publicação acrescenta é o
`stdoutLogFile`, um comentário com o identificador do projeto, e o colapso do
espaçamento. Um ficheiro gerado de raiz não traria nada disto.

**Corroboração independente, do ORQ**: ao verificar o PR #23 publicou o artefacto, e as
duas variáveis estavam lá dentro com os valores que lhes tinham sido escritos.

A consequência prática é que **o ficheiro a editar está em `src/`, e a cópia no servidor é
uma saída**. Editar a cópia do servidor é trabalho que a publicação seguinte apaga, o que
continua a ser verdade e é mais forte agora do que era, porque antes perdia se uma edição
e agora perde se uma decisão versionada.

O `Docs/06` ainda diz que este ficheiro é gerado pela publicação, numa secção acima da que
descreve a decisão. Não é corrigido aqui, porque esse documento não é deste registo, e
está entregue a quem o mantém.

## A fronteira que tem de ficar escrita: isto não é sítio para segredos

Esta é a parte que vai ser mal lida, e por isso é a que fica escrita com mais força.

O precedente que esta decisão cria lê se como **"a configuração vai no `web.config`, e vai
versionada"**. A próxima pessoa que precise de uma palavra passe, de uma cadeia de ligação
com credenciais ou de uma chave de serviço vai encontrar este registo, vai achar que
responde à pergunta dela, e vai ter razão na forma e não no fundo.

**O que está aqui são caminhos de um servidor interno**, que já estão impressos no
`Docs/06` à vista de quem lá chegue. Não há credencial nenhuma e não é preciso haver,
porque a base de dados é um ficheiro no disco e a identidade vem do servidor web, conforme
o [ADR-014](ADR-014-autenticacao-integrada-windows.md) e o
[ADR-002](ADR-002-sqlite.md).

**Um valor que seja segredo não entra aqui.** Um ficheiro versionado é um ficheiro que fica
no histórico mesmo depois de corrigido, e um segredo que lá entre não sai mais, nem com
uma emenda nem com um commit por cima. Se um segredo passar a ser necessário, o que se
reabre é esta decisão, e não se acrescenta mais uma linha à tabela acima.

## Cada nome num sítio só, e a documentação não é consistente sobre o porquê

A documentação do módulo foi consultada diretamente para este ponto, e **diz duas coisas
que puxam em sentidos opostos na mesma página.**

Num sítio diz que as variáveis definidas nesta secção **têm precedência** sobre as do
sistema. Noutro avisa que uma variável definida nos dois sítios faz o valor do `web.config`
ser **acrescentado** ao do sistema, e dá o exemplo `ASPNETCORE_ENVIRONMENT:
Development;Development`, que impede a aplicação de arrancar.

Não é preciso decidir qual das duas está certa, e é melhor não tentar. **A regra que
sobrevive às duas leituras é não definir estes nomes em mais lado nenhum**, nem no ambiente
da máquina nem no do pool de aplicações.

Importa porque a avaria não se parece com a causa: o arranque falha a queixar se de uma
cadeia de ligação, e o caminho que aparece na mensagem é um que ninguém escreveu, porque é
a concatenação de dois.

## O que este registo emenda no ADR-018, e uma lacuna que fica aberta

O ADR-018 decidiu recusar arrancar, e essa decisão está de pé inteira. O que mudou foi o
conjunto validado e, sobretudo, **quem guarda a autenticação.**

Enquanto a identidade vinha do Entra ID, o validador exigia quatro chaves da secção
`AzureAd`. Com a autenticação integrada do Windows essas definições saíram da configuração
da aplicação e passaram para o `web.config` do site e para a configuração do IIS, fora do
alcance deste processo no arranque. **O host arranca na perfeição com a autenticação do
Windows desligada e a anónima ligada, e nada dentro dele dá por isso.** O sintoma é um 401
no primeiro pedido, visível e sem explicação.

É por isso que a autenticação do site viaja neste ficheiro em vez de ficar ao critério de
quem configura o servidor, e é por isso que o `anonymousAuthentication` está lá escrito a
`false` de forma explícita em vez de ficar entregue a um valor por omissão.

**E aqui está a lacuna, que fica escrita por ser do tipo que passa despercebido.** Três
sítios no código dizem que a detecção desta falha passou para a lista de verificação
manual: o comentário no `StartupConfigurationValidator`, o do `StartupGuardTests` e o do
`PublishedHostTests`. **A lista do `Docs/07` não tem esse item.** Tem três itens sobre a
configuração que chega ao host e três sobre autenticação real, e nenhum deles pergunta se
a autenticação anónima está desligada. Procurado de duas maneiras, por termo em toda a
documentação e por leitura da própria lista.

O que existe mesmo é o `deploy/provisionar.ps1`, que liga uma, desliga a outra, relê as
duas do IIS no fim e pára o script se não ficaram como devia. É melhor do que uma caixa
para assinalar, e tem um limite que convém não confundir: **corre uma vez, no
provisionamento.** Ninguém volta a verificar no dia em que alguém mexer nas definições do
site no gestor do IIS.

Fechar a lacuna é acrescentar o item ao `Docs/07`, e não é feito aqui porque esse documento
não é deste registo.

## Alternativas consideradas

**Um `appsettings.Production.json` ao lado da aplicação.** É o sítio que a plataforma
sugere e seria a primeira escolha pela regra de preferir o que ela já dá. Posta de lado
porque a pasta da aplicação é esvaziada pela publicação seguinte, que é a mesma falha que
este repositório já documenta para uma base de dados guardada lá. O sintoma é pior do que o
da base de dados: o site deixa de arrancar e a mensagem fala de configuração em falta, que
não se parece nada com "alguém publicou por cima". A lista do `Docs/07` ganhou um item a
verificar que esse ficheiro não existe na pasta, precisamente por isto.

**Variáveis de ambiente na máquina ou no pool de aplicações.** Sobrevivem à publicação
como estas, e foram postas de lado por duas razões. Não ficam versionadas, portanto a
configuração de que a aplicação depende deixa de ser revista por quem revê o resto. E
colidem com estas pelo mecanismo descrito acima, de modo que escolher ambas é escolher a
avaria.

**Pôr os valores no `appsettings.json` versionado, que já existe.** É a alternativa mais
simples de todas e é a que está mais perto de acontecer por acidente. Posta de lado porque
**as duas cadeias vazias são a guarda**, e não um descuido por preencher. Um valor ali é um
valor por omissão, e um valor por omissão é aquilo em que um servidor mal configurado cai
para arrancar errado em silêncio, que é exatamente o que o ADR-018 existe para impedir.

## Consequências

**Boas.** A configuração de que a aplicação não consegue prescindir viaja com o artefacto,
sobrevive a cada publicação e muda por pull request como o resto, portanto passa pelos
mesmos olhos. Quem publica não precisa de saber nada que não esteja no repositório, e a
pergunta que bloqueava o primeiro arranque deixa de depender de alguém se lembrar.

**Más.** Configuração de instalação dentro do repositório. Um valor errado é publicado como
se fosse código, e corrigi lo obriga a publicar outra vez, ao contrário de uma variável de
ambiente que se muda no servidor em segundos. É o preço de ela ser revista, e é aceite
porque os dois valores são caminhos que mudam quase nunca.

**E a que tem de ser vigiada, porque não dá sinal.** O precedente convida a próxima pessoa
a pôr aqui um segredo. Nada no repositório a impede: não há verificação, não há guarda de
publicação, e um segredo escrito neste ficheiro passa numa revisão distraída exatamente
como passou tudo o que está lá hoje. A única coisa que existe contra isso é esta secção, o
que é uma defesa fraca e fica dita como tal.
