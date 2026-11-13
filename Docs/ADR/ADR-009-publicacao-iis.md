# ADR-009. IIS in-process, com persistência das chaves de Data Protection

**Estado**: Aceite, parcialmente substituído pelo [ADR-014](ADR-014-autenticacao-integrada-windows.md) quanto ao que as chaves protegem. **Data**: 30 de outubro de 2026.

## Contexto

O destino de publicação é um servidor Windows interno com IIS, já existente e já
gerido. O briefing indicava isso como preferência.

A aplicação usa cookies de autenticação e tokens antiforgery, ambos protegidos pelo
sistema de Data Protection do ASP.NET Core.

## Decisão

Publicação em IIS com **hosting in-process**, através do ASP.NET Core Module v2, com
o pool de aplicações limitado a um processo de trabalho.

As **chaves de Data Protection são persistidas numa pasta no disco**, com nome de
aplicação definido de forma explícita.

A configuração completa está em [06](../06-Operacao-e-Publicacao.md).

## A razão de a persistência das chaves estar neste ADR

Por omissão, o anel de chaves vive em memória. Nessa configuração, cada reciclagem do
pool de aplicações gera chaves novas, e tudo o que foi cifrado com as anteriores
deixa de ser legível.

O sintoma é desagradável de diagnosticar: os utilizadores são atirados para fora da
sessão sem razão aparente, e submissões falham com erros de token antiforgery, de
forma intermitente e aparentemente aleatória, em horas que coincidem com reciclagens
que ninguém está a ver. Tudo isto num ambiente onde a aplicação funciona perfeitamente
durante o teste.

É um problema de configuração de servidor que se manifesta como se fosse um defeito da
aplicação, e por isso está registado como decisão e não enterrado numa lista de
passos. A verificação está na lista de [07](../07-Plano-de-Verificacao.md), e consiste
em reciclar o pool de propósito e confirmar que uma sessão já iniciada sobrevive.

## Alternativas consideradas

**Hosting out-of-process.** Acrescenta um salto de proxy entre o IIS e o Kestrel, com
custo de desempenho e mais uma peça a configurar, sem benefício para esta aplicação.

**Serviço do Windows com Kestrel exposto diretamente.** Dispensaria o IIS, mas
obrigaria a tratar à mão do certificado, do arranque automático e do reinício após
falha, que é exatamente o que o IIS já faz na máquina.

**Contentores.** O servidor de destino não os corre, e introduzi los seria mudar a
infraestrutura da organização para publicar uma aplicação interna de quatro ecrãs.

**Chaves no registo do Windows ou numa base de dados.** Ambas funcionam. A pasta no
disco é a mais simples de configurar, de inspecionar e de incluir no backup, e fica
ao lado da pasta de dados, com o mesmo requisito de permissões.

## Consequências

**Boas.** Aproveita infraestrutura já existente e já gerida, com certificado, arranque
automático e reinício após falha resolvidos. Desempenho melhor do que out-of-process.

**Más.** Prende a aplicação a Windows e a IIS, o que é irrelevante aqui mas deixa de o
ser se alguma vez houver intenção de mudar de alojamento.

Duas pastas fora da aplicação passam a ser estado crítico, a de dados e a de chaves.
Ambas precisam de permissões corretas e ambas têm de entrar no backup. Perder a pasta
de chaves não perde dados, mas termina todas as sessões em curso.
