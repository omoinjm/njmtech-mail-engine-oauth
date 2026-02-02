CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260202150053_InitialCreate') THEN
    CREATE TABLE "FailedMessages" (
        "Id" uuid NOT NULL,
        "MessageId" uuid NOT NULL,
        "Topic" text NOT NULL,
        "Subscription" text NOT NULL,
        "ErrorMessage" text NOT NULL,
        "ErrorStackTrace" text NOT NULL,
        "FailedAtUtc" timestamp with time zone NOT NULL,
        "Status" text NOT NULL,
        "RetryCount" integer NOT NULL,
        "ResolvedAtUtc" timestamp with time zone,
        "MessageContent" text NOT NULL,
        CONSTRAINT "PK_FailedMessages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260202150053_InitialCreate') THEN
    CREATE TABLE "OAuthTokens" (
        "Id" uuid NOT NULL,
        "UserMailAccountId" uuid NOT NULL,
        "AccessToken" text NOT NULL,
        "RefreshToken" text NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_OAuthTokens" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260202150053_InitialCreate') THEN
    CREATE TABLE "UserMailAccounts" (
        "Id" uuid NOT NULL,
        "EmailAddress" text NOT NULL,
        "ProviderType" integer NOT NULL,
        CONSTRAINT "PK_UserMailAccounts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260202150053_InitialCreate') THEN
    CREATE INDEX "IX_FailedMessages_Status" ON "FailedMessages" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260202150053_InitialCreate') THEN
    CREATE INDEX "IX_FailedMessages_Topic" ON "FailedMessages" ("Topic");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260202150053_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260202150053_InitialCreate', '8.0.0');
    END IF;
END $EF$;
COMMIT;

