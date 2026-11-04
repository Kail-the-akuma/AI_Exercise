# ADR-003. Microsoft Entra ID por OpenID Connect

**Estado**: Aceite, com um ponto por confirmar. **Data**: 30 de outubro de 2026.

## Contexto

O briefing exige que toda a gente esteja autenticada com a conta corporativa, e que o
nome de cada pessoa seja visível como autor de ideias e na lista de votantes. A
identidade tem de ser fiável, porque é apresentada publicamente a colegas.

A organização usa Microsoft Entra ID.

## Decisão

Autenticação por OpenID Connect contra o Entra ID, com `Microsoft.Identity.Web` e
`Microsoft.Identity.Web.UI`, em registo de aplicação de um só tenant.

Autorização por política de fallback que exige utilizador autenticado em todas as
páginas, de forma a que uma página nova nasça protegida por omissão.

Nenhuma permissão do Microsoft Graph é pedida. A aplicação não consulta o Graph em
momento nenhum.

## Ponto por confirmar antes da Fase 1

Uma aplicação que apenas autentica, sem chamar nenhuma API a jusante, deve conseguir
funcionar **sem client secret**, com o fluxo baseado em token de identificação.

Isto tem de ser confirmado contra o registo de aplicação real da organização, e não
assumido. Se as políticas do tenant não o permitirem, passa a ser necessário um
segredo ou um certificado, e isso muda o que a equipa de infraestrutura tem de
provisionar e o que tem de ser renovado antes de expirar.

O resultado do spike volta para este ficheiro.

## Alternativas consideradas

**Windows Authentication, por Negotiate no IIS.** Mais rápido de arrancar, sem registo
de aplicação e sem segredos. Posta de lado por duas razões: só funciona dentro do
domínio, o que exclui quem trabalhe fora da rede, e obter um nome apresentável obriga
a consultar o Active Directory, o que reintroduz uma dependência externa que a escolha
do Entra ID evita.

Fica registada como alternativa viável se o registo de aplicação demorar a ser
aprovado e houver pressa em arrancar.

**Autenticação própria, com contas locais.** Nunca foi hipótese. Guardar palavras
passe de colaboradores numa ferramenta interna de ideias seria criar um risco de
segurança para resolver um problema que a organização já tem resolvido.

## Consequências

**Boas.** Identidade fiável, sem gestão de contas, sem palavras passe e sem
recuperação de acesso. A saída de um colaborador da organização termina o acesso de
imediato, sem ação nenhuma na aplicação. Funciona dentro e fora da rede.

**Más.** Dependência de uma equipa externa para o registo de aplicação, o que coloca
um bloqueio logo na Fase 0 do plano de entrega. Se houver segredo, há uma data de
expiração que alguém tem de vigiar, e o sintoma de a ignorar é a aplicação deixar de
autenticar sem aviso.

A aplicação depende do Entra ID estar disponível para **iniciar** sessão. Quem já
tenha sessão iniciada continua a trabalhar normalmente, porque depois do início de
sessão a aplicação só usa o cookie e a base de dados local.
