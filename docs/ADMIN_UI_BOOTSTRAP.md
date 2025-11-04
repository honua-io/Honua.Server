# Admin UI Authentication Bootstrap Guide

**Last Updated:** 2025-11-04

This guide explains how to bootstrap authentication for a fresh Honua Server instance to enable access to the Admin UI.

---

## Overview

For a brand new Honua Server installation, there are no users in the system. You must bootstrap an initial administrator account before you can log into the Admin UI.

Honua supports three authentication modes:
- **QuickStart** - No authentication (development only, NOT for production)
- **Local** - Built-in username/password authentication
- **OIDC** - External OAuth2/OpenID Connect provider

---

## Option 1: QuickStart Mode (Development Only)

**⚠️ WARNING:** QuickStart mode disables authentication entirely. Only use for local development!

### Configuration

Edit `appsettings.json` or `appsettings.Development.json`:

```json
{
  "honua": {
    "authentication": {
      "Mode": "QuickStart",
      "Enforce": false,
      "QuickStart": {
        "Enabled": true
      }
    }
  }
}
```

### Access

Navigate to `https://localhost:7002` - you'll have full access without login.

---

## Option 2: Local Authentication (Recommended)

### Step 1: Configure Local Authentication

Edit `appsettings.json`:

```json
{
  "honua": {
    "authentication": {
      "Mode": "Local",
      "Enforce": true,
      "Local": {
        "Provider": "sqlite",
        "StorePath": "data/auth/auth.db",
        "SessionLifetime": "08:00:00"
      },
      "Bootstrap": {
        "AdminUsername": "admin",
        "AdminEmail": "admin@example.com",
        "AdminPassword": null
      }
    }
  }
}
```

**Configuration Options:**

- **Provider**: Database provider for auth storage
  - `sqlite` - Standalone SQLite database (default)
  - `postgres` or `postgresql` - PostgreSQL database
  - `mysql` - MySQL database
  - `sqlserver` - SQL Server database

- **StorePath**: Path to SQLite database file (only for SQLite provider)

- **SessionLifetime**: How long JWT tokens remain valid (default: 30 minutes)

- **Bootstrap.AdminUsername**: Username for initial admin (default: "admin")

- **Bootstrap.AdminEmail**: Optional email for initial admin

- **Bootstrap.AdminPassword**:
  - If **null or empty**: A random 24-character password will be generated
  - If **set**: Use this password (must be 12+ characters)
  - **⚠️ SECURITY:** Never set this in production `appsettings.json`! Use environment variables or secrets.

### Step 2: Bootstrap the Admin Account

Run the bootstrap command:

```bash
# Using Honua CLI (recommended)
honua auth bootstrap

# Or using dotnet run
dotnet run --project src/Honua.Server.Host -- auth:bootstrap
```

**Sample Output (Generated Password):**

```
✅ Bootstrap completed. Local administrator 'admin' created.
   Generated password: xJ9$kL2@pW8#mN4%qR6^tY1!hB3&fD5*

   ⚠️  IMPORTANT: Save this password securely!
   This is the only time it will be displayed.
```

**Sample Output (Configured Password):**

```
✅ Bootstrap completed. Local administrator 'admin' created.
```

### Step 3: Log Into Admin UI

1. Start the API server:
   ```bash
   cd src/Honua.Server.Host
   dotnet run
   ```

2. Start the Admin UI:
   ```bash
   cd src/Honua.Admin.Blazor
   dotnet run
   ```

3. Navigate to: `https://localhost:7002`

4. Log in with:
   - **Username:** `admin` (or your configured username)
   - **Password:** The password from Step 2

---

## Option 3: OIDC Authentication

### Step 1: Configure OIDC

Edit `appsettings.json`:

```json
{
  "honua": {
    "authentication": {
      "Mode": "Oidc",
      "Enforce": true,
      "Jwt": {
        "Authority": "https://your-oidc-provider.com",
        "Audience": "honua-api",
        "RoleClaimPath": "role",
        "RequireHttpsMetadata": true
      },
      "Bootstrap": {
        "AdminSubject": "your-oidc-subject-id",
        "AdminUsername": "admin",
        "AdminEmail": "admin@example.com"
      }
    }
  }
}
```

**Configuration Options:**

- **Jwt.Authority**: URL of your OIDC provider (e.g., Auth0, Keycloak, Azure AD)
- **Jwt.Audience**: Audience claim to validate in tokens
- **Jwt.RoleClaimPath**: Claim path for user roles (default: "role")
- **Bootstrap.AdminSubject**: The OIDC subject (user ID) of the first administrator

### Step 2: Bootstrap OIDC Admin

```bash
honua auth bootstrap
```

**Output:**
```
✅ Bootstrap completed. OIDC administrator subject 'your-oidc-subject-id' registered.
```

### Step 3: Log In via OIDC

1. Start servers (as in Local auth Step 3)

2. Navigate to `https://localhost:7002`

3. You'll be redirected to your OIDC provider to authenticate

4. After successful authentication, you'll be redirected back to the Admin UI

---

## Security Best Practices

### For Development

- ✅ Use QuickStart mode OR Local mode with generated passwords
- ✅ Keep generated passwords in a local password manager
- ✅ Never commit `appsettings.Development.json` with passwords to Git

### For Production

- ❌ **NEVER** use QuickStart mode
- ❌ **NEVER** set `Bootstrap.AdminPassword` in configuration files
- ✅ Use environment variables or Azure Key Vault for secrets
- ✅ Use OIDC with enterprise identity provider (preferred)
- ✅ Use Local auth with database-backed provider (PostgreSQL/SQL Server)
- ✅ Enable HTTPS and RequireHttpsMetadata
- ✅ Rotate admin passwords regularly

### Environment Variable Example

Instead of setting AdminPassword in `appsettings.json`:

```bash
# Linux/Mac
export HONUA__AUTHENTICATION__BOOTSTRAP__ADMINPASSWORD="YourSecurePassword123!"
honua auth bootstrap

# Windows PowerShell
$env:HONUA__AUTHENTICATION__BOOTSTRAP__ADMINPASSWORD="YourSecurePassword123!"
honua auth bootstrap
```

---

## Troubleshooting

### "Bootstrap already completed"

If you see this error, the admin user already exists. To reset:

1. **SQLite**: Delete `data/auth/auth.db` and re-bootstrap
2. **PostgreSQL/MySQL/SQL Server**: Delete auth tables or use `honua auth reset` command

### "Authentication failed" on login

- Verify the mode in `appsettings.json` matches what you bootstrapped
- Check that the API server is running and accessible
- Ensure `AdminApi:BaseUrl` in Admin UI `appsettings.json` points to the API server

### "Unable to connect to authentication service"

- Verify API server is running on the configured `AdminApi:BaseUrl`
- Check firewall/network settings
- Verify HTTPS certificates are valid

---

## Next Steps

After bootstrapping and logging in:

1. **Create additional users** (if using Local auth)
2. **Configure metadata sources** (PostGIS, GeoPackage, etc.)
3. **Create services** for WMS, WFS, OGC API Features
4. **Set up folders** to organize services
5. **Configure roles and permissions** for team members

---

## Related Documentation

- [Admin UI Architecture](./ADMIN_UI_ARCHITECTURE.md)
- [Admin UI Implementation Plan](./ADMIN_UI_IMPLEMENTATION_PLAN.md)
- [API Authentication](./api/authentication.md)
- [Phase 1 Progress](./PHASE_1_PROGRESS.md)
