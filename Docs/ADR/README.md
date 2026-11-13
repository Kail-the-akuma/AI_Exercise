# Registos de decisão de arquitetura

Uma decisão por ficheiro, com o contexto em que foi tomada, as alternativas que foram
postas de lado e as consequências que ficam.

Um ADR não se edita quando a decisão muda. Escreve se um novo que substitua o
anterior, e o anterior fica com o estado alterado. O valor destes ficheiros está em
mostrar o que se pensava na altura, não em estar sempre certo.

| Registo | Decisão | Estado |
|---|---|---|
| [ADR-001](ADR-001-razor-pages.md) | ASP.NET Core Razor Pages, com renderização no servidor | Aceite |
| [ADR-002](ADR-002-sqlite.md) | EF Core sobre SQLite, em ficheiro | Aceite |
| [ADR-003](ADR-003-autenticacao-entra-id.md) | Microsoft Entra ID por OpenID Connect | Substituído pelo ADR-014 |
| [ADR-004](ADR-004-tabela-local-de-utilizadores.md) | Tabela local de utilizadores, com o identificador de objeto como chave | Aceite |
| [ADR-005](ADR-005-contagem-calculada.md) | Contagem de votos calculada, e não denormalizada | Aceite |
| [ADR-006](ADR-006-projeto-unico.md) | Um projeto de aplicação, com separação por pastas | Aceite, parcialmente substituído pelo ADR-015 |
| [ADR-007](ADR-007-ordenacao-e-desempate.md) | Ordenação por votos, com desempate pela data mais recente | Aceite |
| [ADR-008](ADR-008-sem-administracao.md) | Sem administração, com correção por procedimento manual | Aceite |
| [ADR-009](ADR-009-publicacao-iis.md) | IIS in-process, com persistência das chaves de Data Protection | Aceite |
| [ADR-010](ADR-010-idempotencia-do-voto.md) | Um voto por pessoa garantido por índice único, com tratamento idempotente | Aceite |
| [ADR-011](ADR-011-autenticacao-nos-testes-e2e.md) | Autenticação por handler de teste, fora do projeto web | Substituído pelo ADR-016 |
| [ADR-012](ADR-012-chromium-apenas.md) | Testes ponta a ponta apenas em Chromium | Aceite |
| [ADR-013](ADR-013-certificado-no-arranque.md) | Certificado emitido pela equipa no arranque, certificado confiado depois | Aceite |
| [ADR-014](ADR-014-autenticacao-integrada-windows.md) | Autenticação integrada do Windows, por Negotiate no IIS | Aceite, implementação por fazer |
| [ADR-015](ADR-015-estrutura-de-quatro-projetos.md) | Quatro projetos, com o produto numa biblioteca e dois hosts irmãos | Aceite |
| [ADR-016](ADR-016-identidade-nos-testes-por-host-proprio.md) | Identidade nos testes por host próprio, em vez de esquema sobreposto | Aceite |
