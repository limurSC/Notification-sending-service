-- Создание таблицы уведомлений
CREATE TABLE IF NOT EXISTS "Notifications" (
    "Id" UUID PRIMARY KEY,
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

-- Индексы для производительности
CREATE INDEX IF NOT EXISTS idx_notifications_status ON "Notifications" ("Status");
CREATE INDEX IF NOT EXISTS idx_notifications_createdat ON "Notifications" ("CreatedAt");
CREATE INDEX IF NOT EXISTS idx_notifications_type ON "Notifications" ("Type");
CREATE INDEX IF NOT EXISTS idx_notifications_to ON "Notifications" ("To");

-- Создание таблицы для статистики (опционально)
CREATE TABLE IF NOT EXISTS "NotificationStats" (
    "Id" SERIAL PRIMARY KEY,
    "Date" DATE NOT NULL,
    "Type" VARCHAR(50) NOT NULL,
    "SentCount" INTEGER DEFAULT 0,
    "FailedCount" INTEGER DEFAULT 0,
    "RetryCount" INTEGER DEFAULT 0,
    UNIQUE("Date", "Type")
);