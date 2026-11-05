-- Бэкап структуры базы планировщика.
-- Данные (пользователи и задачи) приложение создаёт само при первом запуске.
-- Восстановление:
--   psql -U postgres -h localhost -p 5432 -f planner_db.sql
-- Учебные входы после запуска приложения:
--   anna@mail.ru / Anna1234
--   boris@mail.ru / Boris1234

SELECT 'CREATE DATABASE planner_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'planner_db')\gexec

\connect planner_db

CREATE TABLE IF NOT EXISTS app_user (
    "Id" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Email" character varying(120) NOT NULL UNIQUE,
    "DisplayName" character varying(120) NOT NULL,
    "PasswordHash" text NOT NULL,
    "PasswordSalt" text NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS task (
    "Id" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "OwnerId" integer NOT NULL REFERENCES app_user("Id") ON DELETE CASCADE,
    "Title" character varying(200) NOT NULL,
    "Description" text NULL,
    "DueAt" timestamp without time zone NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "IsCompleted" boolean NOT NULL,
    "CompletedAt" timestamp without time zone NULL
);

CREATE TABLE IF NOT EXISTS task_share (
    "TaskId" integer NOT NULL REFERENCES task("Id") ON DELETE CASCADE,
    "UserId" integer NOT NULL REFERENCES app_user("Id") ON DELETE CASCADE,
    PRIMARY KEY ("TaskId", "UserId")
);

CREATE TABLE IF NOT EXISTS refresh_session (
    "Id" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "UserId" integer NOT NULL REFERENCES app_user("Id") ON DELETE CASCADE,
    "TokenHash" text NOT NULL,
    "TokenSalt" text NOT NULL,
    "ExpiresAt" timestamp without time zone NOT NULL
);
