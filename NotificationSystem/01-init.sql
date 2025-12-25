CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

DO $$ 
BEGIN
    IF NOT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'Notifications') THEN
        CREATE TABLE "Notifications" (
            "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
            "Type" VARCHAR(50) NOT NULL,
            "To" VARCHAR(255) NOT NULL,
            "Subject" VARCHAR(500),
            "Body" TEXT,
            "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            "SentAt" TIMESTAMP,
            "Status" VARCHAR(20) NOT NULL DEFAULT 'Pending',
            "RetryCount" INTEGER NOT NULL DEFAULT 0,
            "ErrorDetails" TEXT,
            "AdditionalData" VARCHAR(1000)
        );

        CREATE INDEX idx_notifications_status ON "Notifications" ("Status");
        CREATE INDEX idx_notifications_createdat ON "Notifications" ("CreatedAt");
        CREATE INDEX idx_notifications_type ON "Notifications" ("Type");
        CREATE INDEX idx_notifications_to ON "Notifications" ("To");
        
        RAISE NOTICE 'Таблица Notifications создана';
    ELSE
        RAISE NOTICE 'Таблица Notifications уже существует';
    END IF;
END $$;