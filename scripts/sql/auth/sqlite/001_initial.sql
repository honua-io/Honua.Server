-- Honua authentication & RBAC schema (SQLite)

CREATE TABLE IF NOT EXISTS auth_roles (
    id              TEXT PRIMARY KEY,
    name            TEXT NOT NULL,
    description     TEXT NOT NULL,
    created_at      TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at      TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS auth_users (
    id                  TEXT PRIMARY KEY,
    subject             TEXT,
    username            TEXT,
    email               TEXT,
    password_hash       BLOB,
    password_salt       BLOB,
    hash_algorithm      TEXT,
    hash_parameters     TEXT,
    is_active           INTEGER NOT NULL DEFAULT 1,
    is_locked           INTEGER NOT NULL DEFAULT 0,
    failed_attempts     INTEGER NOT NULL DEFAULT 0,
    last_failed_at      TEXT,
    last_login_at       TEXT,
    created_at          TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at          TEXT NOT NULL DEFAULT (datetime('now')),
    UNIQUE(username),
    UNIQUE(email),
    UNIQUE(subject)
);

CREATE TABLE IF NOT EXISTS auth_user_roles (
    user_id     TEXT NOT NULL,
    role_id     TEXT NOT NULL,
    granted_at  TEXT NOT NULL DEFAULT (datetime('now')),
    granted_by  TEXT,
    PRIMARY KEY (user_id, role_id),
    FOREIGN KEY (user_id) REFERENCES auth_users(id) ON DELETE CASCADE,
    FOREIGN KEY (role_id) REFERENCES auth_roles(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS auth_credentials_audit (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id         TEXT NOT NULL,
    action          TEXT NOT NULL,
    details         TEXT,
    actor_id        TEXT,
    occurred_at     TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS auth_bootstrap_state (
    id              INTEGER PRIMARY KEY,
    completed_at    TEXT,
    mode            TEXT,
    metadata        TEXT
);

INSERT INTO auth_roles (id, name, description) VALUES
    ('administrator', 'Administrator', 'Full system control for Honua platform operations.'),
    ('datapublisher', 'Data Publisher', 'Manage datasets and metadata updates within Honua.'),
    ('viewer', 'Viewer', 'Read-only access to metadata inspection and observability dashboards.')
ON CONFLICT(id) DO UPDATE SET
    name = excluded.name,
    description = excluded.description,
    updated_at = datetime('now');

INSERT OR IGNORE INTO auth_bootstrap_state (id, completed_at, mode, metadata)
VALUES (1, NULL, NULL, NULL);

CREATE TRIGGER IF NOT EXISTS trg_auth_roles_updated_at
AFTER UPDATE ON auth_roles
FOR EACH ROW
BEGIN
    UPDATE auth_roles SET updated_at = datetime('now') WHERE rowid = NEW.rowid;
END;

CREATE TRIGGER IF NOT EXISTS trg_auth_users_updated_at
AFTER UPDATE ON auth_users
FOR EACH ROW
BEGIN
    UPDATE auth_users SET updated_at = datetime('now') WHERE rowid = NEW.rowid;
END;

-- Performance indexes for authentication queries
CREATE INDEX IF NOT EXISTS idx_auth_users_subject ON auth_users(subject) WHERE subject IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_auth_users_email ON auth_users(email) WHERE email IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_auth_users_username ON auth_users(username) WHERE username IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_auth_users_active ON auth_users(is_active, id);

-- Role membership lookups
CREATE INDEX IF NOT EXISTS idx_auth_user_roles_user ON auth_user_roles(user_id);
CREATE INDEX IF NOT EXISTS idx_auth_user_roles_role ON auth_user_roles(role_id);

-- Audit trail queries
CREATE INDEX IF NOT EXISTS idx_auth_credentials_audit_user ON auth_credentials_audit(user_id, occurred_at DESC);
CREATE INDEX IF NOT EXISTS idx_auth_credentials_audit_time ON auth_credentials_audit(occurred_at DESC);
