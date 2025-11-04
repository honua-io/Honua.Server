-- Honua authentication & RBAC schema (SQL Server)

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'auth')
    EXEC('CREATE SCHEMA auth');
GO

IF OBJECT_ID('auth.roles', 'U') IS NULL
BEGIN
    CREATE TABLE auth.roles (
        id              NVARCHAR(64)  NOT NULL PRIMARY KEY,
        name            NVARCHAR(128) NOT NULL,
        description     NVARCHAR(512) NOT NULL,
        created_at      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        updated_at      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF OBJECT_ID('auth.users', 'U') IS NULL
BEGIN
    CREATE TABLE auth.users (
        id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        subject             NVARCHAR(256) NULL,
        username            NVARCHAR(128) NULL,
        email               NVARCHAR(256) NULL,
        password_hash       VARBINARY(512) NULL,
        password_salt       VARBINARY(256) NULL,
        hash_algorithm      NVARCHAR(64) NULL,
        hash_parameters     NVARCHAR(MAX) NULL,
        is_active           BIT NOT NULL DEFAULT 1,
        is_locked           BIT NOT NULL DEFAULT 0,
        failed_attempts     INT NOT NULL DEFAULT 0,
        last_failed_at      DATETIME2 NULL,
        last_login_at       DATETIME2 NULL,
        created_at          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        updated_at          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX uq_auth_users_username ON auth.users (username) WHERE username IS NOT NULL;
    CREATE UNIQUE INDEX uq_auth_users_email ON auth.users (email) WHERE email IS NOT NULL;
    CREATE UNIQUE INDEX uq_auth_users_subject ON auth.users (subject) WHERE subject IS NOT NULL;
END
GO

IF OBJECT_ID('auth.user_roles', 'U') IS NULL
BEGIN
    CREATE TABLE auth.user_roles (
        user_id     UNIQUEIDENTIFIER NOT NULL,
        role_id     NVARCHAR(64) NOT NULL,
        granted_at  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        granted_by  UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_auth_user_roles PRIMARY KEY (user_id, role_id),
        CONSTRAINT FK_auth_user_roles_user FOREIGN KEY (user_id) REFERENCES auth.users(id) ON DELETE CASCADE,
        CONSTRAINT FK_auth_user_roles_role FOREIGN KEY (role_id) REFERENCES auth.roles(id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID('auth.credentials_audit', 'U') IS NULL
BEGIN
    CREATE TABLE auth.credentials_audit (
        id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        user_id         UNIQUEIDENTIFIER NOT NULL,
        action          NVARCHAR(64) NOT NULL,
        details         NVARCHAR(MAX) NULL,
        actor_id        UNIQUEIDENTIFIER NULL,
        occurred_at     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF OBJECT_ID('auth.bootstrap_state', 'U') IS NULL
BEGIN
    CREATE TABLE auth.bootstrap_state (
        id              INT NOT NULL PRIMARY KEY,
        completed_at    DATETIME2 NULL,
        mode            NVARCHAR(32) NULL,
        metadata        NVARCHAR(MAX) NULL
    );
END
GO

MERGE auth.roles AS target
USING (VALUES
    ('administrator', 'Administrator', 'Full system control for Honua platform operations.'),
    ('datapublisher', 'Data Publisher', 'Manage datasets and metadata updates within Honua.'),
    ('viewer', 'Viewer', 'Read-only access to metadata inspection and observability dashboards.')
) AS source (id, name, description)
ON target.id = source.id
WHEN MATCHED THEN
    UPDATE SET name = source.name, description = source.description, updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (id, name, description) VALUES (source.id, source.name, source.description);
GO

IF NOT EXISTS (SELECT 1 FROM auth.bootstrap_state WHERE id = 1)
BEGIN
    INSERT INTO auth.bootstrap_state (id, completed_at, mode, metadata)
    VALUES (1, NULL, NULL, NULL);
END
GO

IF OBJECT_ID('auth.trg_roles_updated_at', 'TR') IS NULL
BEGIN
    EXEC('CREATE TRIGGER auth.trg_roles_updated_at ON auth.roles
          AFTER UPDATE AS
          BEGIN
              SET NOCOUNT ON;
              UPDATE auth.roles SET updated_at = SYSUTCDATETIME()
              WHERE id IN (SELECT DISTINCT id FROM inserted);
          END');
END
GO

IF OBJECT_ID('auth.trg_users_updated_at', 'TR') IS NULL
BEGIN
    EXEC('CREATE TRIGGER auth.trg_users_updated_at ON auth.users
          AFTER UPDATE AS
          BEGIN
              SET NOCOUNT ON;
              UPDATE auth.users SET updated_at = SYSUTCDATETIME()
              WHERE id IN (SELECT DISTINCT id FROM inserted);
          END');
END
GO

-- Performance indexes for authentication queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_auth_users_subject' AND object_id = OBJECT_ID('auth.users'))
CREATE INDEX idx_auth_users_subject ON auth.users(subject) WHERE subject IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_auth_users_email' AND object_id = OBJECT_ID('auth.users'))
CREATE INDEX idx_auth_users_email ON auth.users(email) WHERE email IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_auth_users_username' AND object_id = OBJECT_ID('auth.users'))
CREATE INDEX idx_auth_users_username ON auth.users(username) WHERE username IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_auth_users_active' AND object_id = OBJECT_ID('auth.users'))
CREATE INDEX idx_auth_users_active ON auth.users(is_active, id);
GO

-- Role membership lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_auth_user_roles_user' AND object_id = OBJECT_ID('auth.user_roles'))
CREATE INDEX idx_auth_user_roles_user ON auth.user_roles(user_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_auth_user_roles_role' AND object_id = OBJECT_ID('auth.user_roles'))
CREATE INDEX idx_auth_user_roles_role ON auth.user_roles(role_id);
GO

-- Audit trail queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_auth_credentials_audit_user' AND object_id = OBJECT_ID('auth.credentials_audit'))
CREATE INDEX idx_auth_credentials_audit_user ON auth.credentials_audit(user_id, occurred_at DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_auth_credentials_audit_time' AND object_id = OBJECT_ID('auth.credentials_audit'))
CREATE INDEX idx_auth_credentials_audit_time ON auth.credentials_audit(occurred_at DESC);
GO
