-- Aplicar APENAS a uma base de dados que ja tem a migracao 20261113171845_EsquemaInicial.
--
-- Uma base de dados nova leva o migracao.sql completo, que contem esta migracao no fim.
-- Este ficheiro existe para o caso contrario, uma base ja em uso, onde o script completo
-- falharia na primeira instrucao por as tabelas ja existirem.
--
-- NAO E IDEMPOTENTE, e nao pode ser: o EF Core recusa gerar scripts idempotentes para
-- SQLite, conforme a ADR-020. O enquadramento e por verificacao e nao por idempotencia:
--
--   1. Ler a __EFMigrationsHistory antes de aplicar. Se 20261116185858_AvisoDeVotosNovos
--      ja la estiver, nao aplicar nada.
--   2. Aplicar com sqlite3 em modo -bail, para parar na primeira instrucao que falhe.
--   3. Confirmar o codigo de saida igual a zero.
--
-- A coluna entra NULL em todas as linhas existentes, de proposito. NULL quer dizer que a
-- pessoa nunca abriu a pagina que lhe mostra os votos recebidos, o que e verdade para toda
-- a gente no momento em que esta coluna passa a existir. Um valor por omissao com a data de
-- hoje declararia em silencio que toda a gente tinha acabado de ler tudo.

BEGIN TRANSACTION;
ALTER TABLE "User" ADD "lastNotificationsSeenAt" TEXT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261116185858_AvisoDeVotosNovos', '10.0.14');

COMMIT;

