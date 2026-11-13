CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "User" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_User" PRIMARY KEY AUTOINCREMENT,
    "entraObjectId" TEXT NOT NULL,
    "displayName" TEXT NOT NULL,
    "email" TEXT NULL,
    "firstSeenAt" TEXT NOT NULL
);

CREATE TABLE "Idea" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_Idea" PRIMARY KEY AUTOINCREMENT,
    "title" TEXT NOT NULL,
    "description" TEXT NOT NULL,
    "authorUserId" INTEGER NOT NULL,
    "createdAt" TEXT NOT NULL,
    CONSTRAINT "FK_Idea_User_authorUserId" FOREIGN KEY ("authorUserId") REFERENCES "User" ("id") ON DELETE RESTRICT
);

CREATE TABLE "Vote" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_Vote" PRIMARY KEY AUTOINCREMENT,
    "ideaId" INTEGER NOT NULL,
    "userId" INTEGER NOT NULL,
    "createdAt" TEXT NOT NULL,
    CONSTRAINT "FK_Vote_Idea_ideaId" FOREIGN KEY ("ideaId") REFERENCES "Idea" ("id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Vote_User_userId" FOREIGN KEY ("userId") REFERENCES "User" ("id") ON DELETE RESTRICT
);

CREATE INDEX "IX_Idea_authorUserId" ON "Idea" ("authorUserId");

CREATE INDEX "IX_Idea_createdAt" ON "Idea" ("createdAt" DESC);

CREATE UNIQUE INDEX "IX_User_entraObjectId" ON "User" ("entraObjectId");

CREATE UNIQUE INDEX "IX_Vote_ideaId_userId" ON "Vote" ("ideaId", "userId");

CREATE INDEX "IX_Vote_userId" ON "Vote" ("userId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261113161344_EsquemaInicial', '10.0.14');

COMMIT;

