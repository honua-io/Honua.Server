-- Honua authentication & RBAC schema (PostgreSQL)

CREATE SCHEMA IF NOT EXISTS auth;

CREATE TABLE IF NOT EXISTS auth.roles (
    id              TEXT PRIMARY KEY,
    name            TEXT NOT NULL,
    description     TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS auth.users (
    id                  UUID PRIMARY KEY,
    subject             TEXT,
    username            TEXT,
    email               TEXT,
    password_hash       BYTEA,
    password_salt       BYTEA,
    hash_algorithm      TEXT,
    hash_parameters     JSONB,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    is_locked           BOOLEAN NOT NULL DEFAULT FALSE,
    failed_attempts     INTEGER NOT NULL DEFAULT 0,
    last_failed_at      TIMESTAMPTZ,
    last_login_at       TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_auth_users_username UNIQUE NULLS NOT DISTINCT (username),
    CONSTRAINT uq_auth_users_email UNIQUE NULLS NOT DISTINCT (email),
    CONSTRAINT uq_auth_users_subject UNIQUE NULLS NOT DISTINCT (subject)
);

CREATE TABLE IF NOT EXISTS auth.user_roles (
    user_id     UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    role_id     TEXT NOT NULL REFERENCES auth.roles(id) ON DELETE CASCADE,
    granted_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    granted_by  UUID,
    PRIMARY KEY (user_id, role_id)
);

CREATE TABLE IF NOT EXISTS auth.credentials_audit (
    id              BIGSERIAL PRIMARY KEY,
    user_id         UUID NOT NULL,
    action          TEXT NOT NULL,
    details         JSONB,
    actor_id        UUID,
    occurred_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS auth.bootstrap_state (
    id              INTEGER PRIMARY KEY DEFAULT 1,
    completed_at    TIMESTAMPTZ,
    mode            TEXT,
    metadata        JSONB
);

-- Optional helper to keep updated_at fresh
CREATE OR REPLACE FUNCTION auth.set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at := NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_trigger
        WHERE tgname = 'trg_auth_roles_updated_at'
    ) THEN
        CREATE TRIGGER trg_auth_roles_updated_at
        BEFORE UPDATE ON auth.roles
        FOR EACH ROW EXECUTE FUNCTION auth.set_updated_at();
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_trigger
        WHERE tgname = 'trg_auth_users_updated_at'
    ) THEN
        CREATE TRIGGER trg_auth_users_updated_at
        BEFORE UPDATE ON auth.users
        FOR EACH ROW EXECUTE FUNCTION auth.set_updated_at();
    END IF;
END;
$$;

INSERT INTO auth.roles (id, name, description)
VALUES
    ('administrator', 'Administrator', 'Full system control for Honua platform operations.'),
    ('datapublisher', 'Data Publisher', 'Manage datasets and metadata updates within Honua.'),
    ('viewer', 'Viewer', 'Read-only access to metadata inspection and observability dashboards.')
ON CONFLICT (id) DO UPDATE
SET name = EXCLUDED.name,
    description = EXCLUDED.description,
    updated_at = NOW();

INSERT INTO auth.bootstrap_state (id, completed_at, mode, metadata)
VALUES (1, NULL, NULL, NULL)
ON CONFLICT (id) DO NOTHING;
