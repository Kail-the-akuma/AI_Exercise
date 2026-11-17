-- Aplicar APENAS a uma base de dados que ja tem as migracoes 20261113171845_EsquemaInicial
-- e 20261116185858_AvisoDeVotosNovos.
--
-- Uma base de dados nova leva o migracao.sql completo, que contem esta migracao no fim.
-- Este ficheiro existe para o caso contrario, uma base ja em uso, onde o script completo
-- falharia na primeira instrucao por as tabelas ja existirem.
--
-- NAO E IDEMPOTENTE, e nao pode ser: o EF Core recusa gerar scripts idempotentes para
-- SQLite, conforme a ADR-020. O enquadramento e por verificacao e nao por idempotencia:
--
--   1. Ler a __EFMigrationsHistory antes de aplicar. Se 20261117102626_Comentarios ja la
--      estiver, nao aplicar nada.
--   2. Aplicar com sqlite3 em modo -bail, para parar na primeira instrucao que falhe.
--   3. Confirmar o codigo de saida igual a zero.
--
-- Nao ha coluna a marcar um comentario como apagado, e isso e decisao e nao omissao. Quem
-- escreveu um comentario pode remove-lo, e a linha sai da tabela. Quem for procurar aqui
-- um campo de apagado logico nao o encontra porque nunca existiu.
--
-- As chaves estrangeiras ficam em RESTRICT como todas as outras: apagar um comentario e
-- uma pessoa a apagar as proprias palavras, e nao ha nada neste produto que apague uma
-- ideia ou uma pessoa.

BEGIN TRANSACTION;
CREATE TABLE "Comment" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_Comment" PRIMARY KEY AUTOINCREMENT,
    "ideaId" INTEGER NOT NULL,
    "authorUserId" INTEGER NOT NULL,
    "text" TEXT NOT NULL,
    "createdAt" TEXT NOT NULL,
    "updatedAt" TEXT NULL,
    CONSTRAINT "FK_Comment_Idea_ideaId" FOREIGN KEY ("ideaId") REFERENCES "Idea" ("id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Comment_User_authorUserId" FOREIGN KEY ("authorUserId") REFERENCES "User" ("id") ON DELETE RESTRICT
);

CREATE INDEX "IX_Comment_authorUserId" ON "Comment" ("authorUserId");

CREATE INDEX "IX_Comment_ideaId_createdAt" ON "Comment" ("ideaId", "createdAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261117102626_Comentarios', '10.0.14');

COMMIT;

