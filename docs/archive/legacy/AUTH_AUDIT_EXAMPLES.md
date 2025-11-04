# Authentication Audit Trail - Query Examples

This document provides example queries for analyzing the authentication audit trail.

## Table Schema

```sql
CREATE TABLE auth_credentials_audit (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id         TEXT NOT NULL,
    action          TEXT NOT NULL,
    details         TEXT,
    old_value       TEXT,
    new_value       TEXT,
    actor_id        TEXT,
    ip_address      TEXT,
    user_agent      TEXT,
    occurred_at     TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (user_id) REFERENCES auth_users(id) ON DELETE CASCADE
);

CREATE INDEX idx_audit_user_id ON auth_credentials_audit(user_id);
CREATE INDEX idx_audit_occurred_at ON auth_credentials_audit(occurred_at);
CREATE INDEX idx_audit_action ON auth_credentials_audit(action);
```

## Events Audited

| Action | Description | Old Value | New Value |
|--------|-------------|-----------|-----------|
| `login_success` | Successful authentication | - | - |
| `login_failed` | Failed authentication attempt | - | - |
| `account_locked` | Account locked due to too many failures | - | - |
| `password_changed` | Password was updated | `***` | `***` |
| `roles_changed` | User roles were modified | Previous roles | New roles |
| `user_created` | New user account created | - | Assigned roles |

## Example Queries

### 1. Get All Audit Records for a User

```sql
SELECT *
FROM auth_credentials_audit
WHERE user_id = 'user-id-here'
ORDER BY occurred_at DESC
LIMIT 100;
```

### 2. Find All Failed Login Attempts in Last 24 Hours

```sql
SELECT user_id, ip_address, user_agent, occurred_at
FROM auth_credentials_audit
WHERE action = 'login_failed'
  AND occurred_at >= datetime('now', '-1 day')
ORDER BY occurred_at DESC;
```

### 3. Detect Brute Force Attacks (Same IP, Multiple Users)

```sql
SELECT ip_address, COUNT(DISTINCT user_id) as targeted_users, COUNT(*) as attempts
FROM auth_credentials_audit
WHERE action IN ('login_failed', 'account_locked')
  AND occurred_at >= datetime('now', '-1 hour')
GROUP BY ip_address
HAVING attempts > 10
ORDER BY attempts DESC;
```

### 4. Find All Account Lockouts

```sql
SELECT u.username, a.ip_address, a.user_agent, a.occurred_at
FROM auth_credentials_audit a
JOIN auth_users u ON u.id = a.user_id
WHERE a.action = 'account_locked'
ORDER BY a.occurred_at DESC;
```

### 5. Track Password Changes

```sql
SELECT u.username, a.actor_id, a.ip_address, a.occurred_at
FROM auth_credentials_audit a
JOIN auth_users u ON u.id = a.user_id
WHERE a.action = 'password_changed'
ORDER BY a.occurred_at DESC;
```

### 6. Monitor Role Assignments

```sql
SELECT u.username, a.old_value as old_roles, a.new_value as new_roles,
       a.actor_id, a.occurred_at
FROM auth_credentials_audit a
JOIN auth_users u ON u.id = a.user_id
WHERE a.action = 'roles_changed'
ORDER BY a.occurred_at DESC;
```

### 7. Find Suspicious Activities (Multiple Failed Logins from Different IPs)

```sql
SELECT user_id, COUNT(DISTINCT ip_address) as distinct_ips, COUNT(*) as attempts
FROM auth_credentials_audit
WHERE action = 'login_failed'
  AND occurred_at >= datetime('now', '-1 hour')
GROUP BY user_id
HAVING distinct_ips >= 3
ORDER BY attempts DESC;
```

### 8. Audit Trail for Compliance (All Changes by Administrator)

```sql
SELECT a.action, u.username as affected_user, a.details, a.occurred_at
FROM auth_credentials_audit a
JOIN auth_users u ON u.id = a.user_id
WHERE a.actor_id = 'admin-user-id'
  AND a.action IN ('password_changed', 'roles_changed', 'user_created')
ORDER BY a.occurred_at DESC;
```

### 9. Geographic Anomaly Detection (Same User, Different IPs)

```sql
SELECT user_id, COUNT(DISTINCT ip_address) as distinct_ips,
       GROUP_CONCAT(DISTINCT ip_address) as ip_list
FROM auth_credentials_audit
WHERE action = 'login_success'
  AND occurred_at >= datetime('now', '-1 day')
GROUP BY user_id
HAVING distinct_ips > 1
ORDER BY distinct_ips DESC;
```

### 10. Purge Old Audit Records (Retention Policy)

```sql
-- Delete records older than 90 days
DELETE FROM auth_credentials_audit
WHERE occurred_at < datetime('now', '-90 days');
```

## API Methods

### C# Examples

```csharp
// Get audit records for a user
var auditRecords = await authRepository.GetAuditRecordsAsync(userId, limit: 50);

// Get all login failures
var failures = await authRepository.GetAuditRecordsByActionAsync("login_failed", limit: 100);

// Get recent failed authentications (last 1 hour)
var recentFailures = await authRepository.GetRecentFailedAuthenticationsAsync(TimeSpan.FromHours(1));

// Purge old audit records (older than 90 days)
var deletedCount = await authRepository.PurgeOldAuditRecordsAsync(TimeSpan.FromDays(90));
```

## Metrics

The following metrics are automatically recorded:

- `auth.login.success` - Counter of successful logins (tagged by ip_address)
- `auth.login.failure` - Counter of failed logins (tagged by ip_address)
- `auth.account.locked` - Counter of account lockouts (tagged by ip_address)
- `auth.password.changed` - Counter of password changes (tagged by actor_id)
- `auth.roles.changed` - Counter of role changes (tagged by actor_id)
- `auth.user.created` - Counter of users created (tagged by actor_id)
- `auth.failed_attempts.count` - Histogram of failed attempts before lockout

## Security Recommendations

1. **Review audit logs daily** for suspicious patterns
2. **Set up alerts** for:
   - Multiple failed login attempts from the same IP
   - Account lockouts
   - Password changes outside business hours
   - Role elevation to administrator
3. **Implement log retention** based on compliance requirements (typically 90-365 days)
4. **Export critical audit events** to a SIEM system for centralized monitoring
5. **Regularly purge old records** to maintain performance

## Compliance

This audit trail helps meet requirements for:

- **SOC 2** - Access logging and monitoring
- **HIPAA** - Information system activity review
- **PCI DSS** - Requirement 10 (Track and monitor all access)
- **GDPR** - Article 32 (Security of processing)
- **ISO 27001** - A.12.4.1 (Event logging)
