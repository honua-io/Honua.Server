# GitOps Security Guide

**Version:** 1.0
**Last Updated:** 2025-10-23
**Audience:** Security Engineers, DevOps, Operations Teams

Security best practices and hardening guidelines for GitOps deployments.

---

## Table of Contents

1. [Security Overview](#security-overview)
2. [Git Repository Security](#git-repository-security)
3. [Authentication and Authorization](#authentication-and-authorization)
4. [Secrets Management](#secrets-management)
5. [Access Control](#access-control)
6. [Network Security](#network-security)
7. [Audit and Compliance](#audit-and-compliance)
8. [Incident Response](#incident-response)

---

## Security Overview

### Threat Model

**Assets to Protect:**
- Configuration repository (contains infrastructure-as-code)
- Deployment state files (contain deployment history)
- Database connection strings and credentials
- SSH keys and access tokens
- Running Honua services

**Threat Actors:**
- External attackers
- Malicious insiders
- Compromised accounts
- Supply chain attacks

**Attack Vectors:**
- Compromised Git credentials
- Unauthorized configuration changes
- State file manipulation
- Man-in-the-middle attacks
- Credential theft

### Security Principles

1. **Least Privilege:** Grant minimum necessary permissions
2. **Defense in Depth:** Multiple layers of security controls
3. **Audit Everything:** Comprehensive logging and monitoring
4. **Secrets Never in Git:** Use external secrets management
5. **Immutable Infrastructure:** Configuration as single source of truth
6. **Automated Response:** Automated rollback and alerting

---

## Git Repository Security

### Repository Access Control

**GitHub: Repository Settings**

```yaml
# Required branch protection rules for main/production branches
Branch Protection Rules:
  - Require pull request reviews: true
    Required approving reviewers: 2
    Dismiss stale reviews: true
    Require review from Code Owners: true

  - Require status checks: true
    Require branches to be up to date: true
    Status checks:
      - json-validation
      - security-scan
      - integration-tests

  - Enforce for administrators: true

  - Restrict push access:
    - @devops-team
    - @security-team

  - Restrict force push: true
  - Restrict deletions: true
```

**Deploy Keys: Read-Only**

```bash
# Generate read-only deploy key
ssh-keygen -t ed25519 -C "honua-gitops-readonly@example.com" -f ~/.ssh/honua_gitops_ro

# Add to GitHub/GitLab as deploy key
# CRITICAL: Do NOT enable "Allow write access"
```

**SECURITY REQUIREMENT:** GitOps should NEVER have write access to the configuration repository. This prevents:
- Compromised servers from modifying configurations
- Malicious code execution via git hooks
- Unauthorized configuration drift

### Signed Commits

**Require GPG-signed commits for production:**

```bash
# Setup GPG key
gpg --gen-key

# Configure Git
git config --global user.signingkey YOUR_KEY_ID
git config --global commit.gpgsign true

# Sign commits
git commit -S -m "feat: Add new service layer"
```

**GitHub: Require signed commits**

```yaml
Branch Protection:
  - Require signed commits: true
```

### Dependency Scanning

**Scan configuration files for vulnerabilities:**

```yaml
# .github/workflows/security-scan.yml
name: Security Scan

on: [push, pull_request]

jobs:
  scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Scan for secrets
        uses: trufflesecurity/trufflehog@main
        with:
          path: ./

      - name: Validate JSON
        run: |
          for file in $(find environments -name "*.json"); do
            jq empty "$file" || exit 1
          done

      - name: Check for hardcoded credentials
        run: |
          if grep -r "password\|secret\|token" environments/ --include="*.json"; then
            echo "Found hardcoded credentials!"
            exit 1
          fi
```

### Audit Trail in Git

**Enforce meaningful commit messages:**

```bash
# .git/hooks/commit-msg
#!/bin/bash
commit_msg=$(cat "$1")

# Require format: "type: description [TICKET-123]"
if ! echo "$commit_msg" | grep -qE "^(feat|fix|chore|docs|refactor|security): .+ \[.+-[0-9]+\]"; then
  echo "Commit message must follow format: 'type: description [TICKET-123]'"
  exit 1
fi
```

**Track who approved changes:**

```yaml
# CODEOWNERS file in repository root
# Production changes require specific approvals

# Production environment
/environments/production/ @security-team @ops-lead @devops-lead

# Staging environment
/environments/staging/ @devops-team

# Development environment
/environments/development/ @dev-team
```

---

## Authentication and Authorization

### SSH Key Management

**Best Practices:**

1. **Dedicated Key per Environment:**
   ```bash
   # Production key
   ssh-keygen -t ed25519 -C "honua-prod" -f ~/.ssh/honua_prod

   # Staging key
   ssh-keygen -t ed25519 -C "honua-staging" -f ~/.ssh/honua_staging
   ```

2. **Key Rotation Schedule:**
   - Production: Every 90 days
   - Staging: Every 180 days
   - Development: Every 365 days

3. **Key Storage:**
   ```bash
   # Restrictive permissions
   chmod 600 ~/.ssh/honua_*
   chown honua:honua ~/.ssh/honua_*

   # Separate from user SSH keys
   mkdir -p /etc/honua/ssh
   mv ~/.ssh/honua_* /etc/honua/ssh/
   chown -R honua:honua /etc/honua/ssh
   chmod 700 /etc/honua/ssh
   ```

4. **Passphrase Protection:**
   ```bash
   # Use passphrase-protected keys
   ssh-keygen -t ed25519 -C "honua-prod" -f ~/.ssh/honua_prod
   # Enter strong passphrase when prompted

   # Store passphrase in secure vault
   # Load via systemd environment
   ```

### HTTPS Token Management

**If SSH is not available:**

```bash
# Use Personal Access Tokens (PAT)
# GitHub: Settings > Developer settings > Personal access tokens > Fine-grained tokens

Token Permissions (Minimum):
  - Repository access: Read-only
  - Contents: Read
  - Metadata: Read

Expiration: 90 days maximum
```

**Store token securely:**

```bash
# NEVER in appsettings.json
# Use environment variable from secure source

# Option 1: systemd secret
sudo systemctl edit honua
[Service]
Environment="GITHUB_TOKEN=${GITHUB_TOKEN}"

# Option 2: Secrets manager
Environment="GITHUB_TOKEN=$(vault kv get -field=token secret/honua/git)"
```

### Service Account Management

**Dedicated Service Account:**

```bash
# Create dedicated user
sudo useradd -r -s /bin/bash -d /var/honua -m honua

# Restrict login
sudo passwd -l honua  # Lock password login
sudo usermod -L honua  # Prevent password changes

# Sudo access only for specific commands (if needed)
sudo visudo
# Add:
# honua ALL=(ALL) NOPASSWD: /usr/bin/systemctl restart honua
```

**Multi-Factor Authentication:**

For human access to Git repositories:
- Enable 2FA/MFA on GitHub/GitLab accounts
- Use hardware security keys (YubiKey) for production access
- Require MFA for approval operations

---

## Secrets Management

### Never Store Secrets in Git

**CRITICAL SECURITY RULE:** No secrets in configuration files.

**Bad (Insecure):**
```json
{
  "datasources": [
    {
      "id": "postgres",
      "connectionString": "Host=db;Username=admin;Password=supersecret123"
    }
  ]
}
```

**Good (Secure):**
```json
{
  "datasources": [
    {
      "id": "postgres",
      "connectionString": "${DB_CONNECTION_STRING}"
    }
  ]
}
```

### Secrets Management Options

**Option 1: Environment Variables (Basic)**

```bash
# /etc/systemd/system/honua.service
[Service]
Environment="DB_CONNECTION_STRING=Host=db;Username=admin;Password=..."
EnvironmentFile=/etc/honua/secrets.env

# /etc/honua/secrets.env (chmod 600)
DB_CONNECTION_STRING=Host=db;Username=admin;Password=...
API_KEY=...
```

**Option 2: HashiCorp Vault (Recommended)**

```bash
# Store secrets
vault kv put secret/honua/production \
  db_connection_string="Host=..." \
  api_key="..."

# Retrieve at runtime
export DB_CONNECTION_STRING=$(vault kv get -field=db_connection_string secret/honua/production)
```

**Option 3: AWS Secrets Manager**

```bash
# Store secrets
aws secretsmanager create-secret \
  --name honua/production/db \
  --secret-string "Host=..."

# Retrieve at runtime
export DB_CONNECTION_STRING=$(aws secretsmanager get-secret-value \
  --secret-id honua/production/db \
  --query SecretString \
  --output text)
```

**Option 4: Kubernetes Secrets**

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: honua-secrets
type: Opaque
stringData:
  db-connection-string: "Host=..."
  api-key: "..."

---
apiVersion: v1
kind: Pod
spec:
  containers:
    - name: honua
      env:
        - name: DB_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: honua-secrets
              key: db-connection-string
```

### Secret Rotation

**Automated Secret Rotation:**

```bash
#!/bin/bash
# /usr/local/bin/rotate-honua-secrets

# 1. Generate new secret
NEW_PASSWORD=$(openssl rand -base64 32)

# 2. Update in database
psql -U admin -c "ALTER USER honua WITH PASSWORD '$NEW_PASSWORD';"

# 3. Update in vault
vault kv put secret/honua/production \
  db_connection_string="Host=db;Username=honua;Password=$NEW_PASSWORD"

# 4. Restart service to pick up new secret
systemctl restart honua

# 5. Verify deployment
sleep 10
systemctl is-active honua || systemctl restart honua

# 6. Audit log
echo "$(date): Secret rotated successfully" >> /var/log/honua/secret-rotation.log
```

**Schedule rotation:**
```bash
# Cron: Rotate every 90 days
0 2 1 */3 * /usr/local/bin/rotate-honua-secrets
```

---

## Access Control

### File System Permissions

**Strict Permissions:**

```bash
# Repository directory
chown -R honua:honua /var/honua/gitops-repo
chmod 700 /var/honua/gitops-repo

# State directory
chown -R honua:honua /var/honua/deployments
chmod 700 /var/honua/deployments

# State files
chmod 600 /var/honua/deployments/*.json

# SSH keys
chmod 600 /etc/honua/ssh/*
chown honua:honua /etc/honua/ssh/*

# Configuration files
chmod 640 /etc/honua/appsettings.Production.json
chown root:honua /etc/honua/appsettings.Production.json
```

**SELinux Policies (RHEL/CentOS):**

```bash
# Define custom SELinux policy
semanage fcontext -a -t honua_var_t "/var/honua(/.*)?"
semanage fcontext -a -t honua_config_t "/etc/honua(/.*)?"
restorecon -R /var/honua /etc/honua

# Create policy module
cat > honua.te <<'EOF'
policy_module(honua, 1.0.0)

type honua_t;
type honua_exec_t;
type honua_var_t;
type honua_config_t;

# Allow Honua to read config
allow honua_t honua_config_t:file read;

# Allow Honua to write state
allow honua_t honua_var_t:file { read write create };

# Allow Git operations
allow honua_t git_exec_t:file execute;
EOF

checkmodule -M -m -o honua.mod honua.te
semodule_package -o honua.pp -m honua.mod
semodule -i honua.pp
```

### Approval Workflow Security

**Multi-Level Approvals:**

```json
{
  "DeploymentPolicy": {
    "RequiresApproval": true,
    "ApprovalRules": [
      {
        "Environment": "production",
        "MinimumApprovers": 2,
        "RequiredApprovers": ["security-team", "ops-lead"],
        "ForbiddenApprovers": ["deployment-initiator"]
      }
    ]
  }
}
```

**Separation of Duties:**
- Developers: Create configuration changes
- DevOps: Review and merge to staging
- Security: Approve production deployments
- Operations: Monitor and respond to issues

**Approval Audit:**

```bash
# Log all approvals
cat > /usr/local/bin/honua-approve <<'EOF'
#!/bin/bash
DEPLOYMENT_ID=$1
APPROVER=$2
APPROVAL_TIME=$(date -u +%Y-%m-%dT%H:%M:%SZ)

# Log approval
logger -t honua-gitops "Deployment $DEPLOYMENT_ID approved by $APPROVER at $APPROVAL_TIME"

# Send to SIEM
echo "{\"event\":\"deployment_approved\",\"deployment_id\":\"$DEPLOYMENT_ID\",\"approver\":\"$APPROVER\",\"timestamp\":\"$APPROVAL_TIME\"}" | \
  curl -X POST https://siem.example.com/events -H "Content-Type: application/json" -d @-

# Create approval file
# ... (actual approval logic)
EOF
```

---

## Network Security

### Firewall Configuration

**Outbound Rules (GitOps Server):**

```bash
# Allow Git over SSH
sudo iptables -A OUTPUT -p tcp --dport 22 -m state --state NEW,ESTABLISHED -j ACCEPT

# Allow Git over HTTPS
sudo iptables -A OUTPUT -p tcp --dport 443 -m state --state NEW,ESTABLISHED -j ACCEPT

# Allow DNS
sudo iptables -A OUTPUT -p udp --dport 53 -j ACCEPT

# Allow NTP (for time sync)
sudo iptables -A OUTPUT -p udp --dport 123 -j ACCEPT

# Block all other outbound (default deny)
sudo iptables -P OUTPUT DROP

# Save rules
sudo iptables-save > /etc/iptables/rules.v4
```

**Inbound Rules:**

```bash
# GitOps server should NOT accept inbound Git connections
# Only application traffic (if serving requests)

# SSH for administration (restrict to bastion/admin network)
sudo iptables -A INPUT -p tcp --dport 22 -s 10.0.1.0/24 -j ACCEPT

# Application traffic (OGC API, etc.)
sudo iptables -A INPUT -p tcp --dport 5000 -j ACCEPT

# Default deny
sudo iptables -P INPUT DROP
```

### TLS Configuration

**Git over HTTPS:**

```bash
# Verify TLS certificate
git config --global http.sslVerify true

# Pin specific CA certificates
git config --global http.sslCAInfo /etc/ssl/certs/ca-certificates.crt

# Minimum TLS version
git config --global http.sslVersion tlsv1.2
```

**Mutual TLS (mTLS) for Git:**

```bash
# Configure client certificate
git config --global http.sslCert /etc/honua/ssl/client.crt
git config --global http.sslKey /etc/honua/ssl/client.key
```

### Network Segmentation

**Recommended Architecture:**

```
┌─────────────────────────────────────────┐
│         Management Network              │
│  (Bastion, Admin Access)                │
│  10.0.1.0/24                            │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────┼───────────────────────┐
│     Application Network                 │
│  (Honua Servers)                        │
│  10.0.10.0/24                           │
│                                         │
│  ┌──────────────┐    ┌──────────────┐  │
│  │ Honua Prod   │    │ Honua Stage  │  │
│  │ GitOps       │    │ GitOps       │  │
│  └──────┬───────┘    └──────┬───────┘  │
└─────────┼───────────────────┼───────────┘
          │                   │
┌─────────┼───────────────────┼───────────┐
│  Database Network                       │
│  10.0.20.0/24                           │
│                                         │
│  ┌──────────────┐    ┌──────────────┐  │
│  │ Postgres Prod│    │ Postgres Stage│ │
│  └──────────────┘    └──────────────┘  │
└─────────────────────────────────────────┘
```

**Firewall Rules Between Segments:**

- Management → Application: SSH (22)
- Application → Database: PostgreSQL (5432)
- Application → Internet: HTTPS (443), SSH (22) for Git
- Database → Internet: DENY

---

## Audit and Compliance

### Comprehensive Logging

**Enable Audit Logging:**

```bash
# System audit (auditd)
sudo auditctl -w /var/honua/deployments -p wa -k honua-deployments
sudo auditctl -w /var/honua/gitops-repo -p wa -k honua-config
sudo auditctl -w /etc/honua -p wa -k honua-config-changes

# Git audit
cd /var/honua/gitops-repo
git config core.logAllRefUpdates true

# Application audit logging
# Configure in appsettings.json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/honua/audit-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 365
        }
      },
      {
        "Name": "Syslog",
        "Args": {
          "host": "siem.example.com",
          "port": 514
        }
      }
    ]
  }
}
```

### Security Events to Monitor

**Critical Events:**

| Event | Severity | Alert Threshold |
|-------|----------|-----------------|
| Failed Git authentication | High | 1 occurrence |
| Unauthorized config change | Critical | 1 occurrence |
| Deployment approval override | High | 1 occurrence |
| State file modification | Critical | 1 occurrence |
| Failed rollback | Critical | 1 occurrence |
| Deployment outside window | Medium | 1 occurrence |

**Monitoring Rules:**

```bash
# Alert on failed Git authentication
sudo journalctl -u honua | grep -i "authentication failed" && \
  mail -s "ALERT: GitOps Auth Failure" security@example.com

# Alert on state file changes
auditctl -l | grep honua-deployments && \
  tail -f /var/log/audit/audit.log | grep honua-deployments
```

### Compliance Requirements

**SOC 2 Compliance:**

1. Access Control:
   - Multi-factor authentication for human access
   - Service accounts with minimal permissions
   - Regular access reviews

2. Change Management:
   - All changes via Pull Requests
   - Approval workflow for production
   - Automated rollback capability

3. Audit Trail:
   - All Git commits logged
   - All approvals recorded
   - All deployments tracked in state files

4. Encryption:
   - TLS for all Git operations
   - Encryption at rest for state files
   - Secrets in external vault, not Git

**GDPR Compliance:**

1. Data Minimization:
   - No PII in configuration files
   - Limit data in deployment logs

2. Right to Erasure:
   - Ability to purge deployment history
   - Secure deletion of state files

**Example Compliance Report:**

```bash
#!/bin/bash
# Generate compliance report

echo "GitOps Security Compliance Report"
echo "Generated: $(date)"
echo ""

echo "1. Access Control"
echo "  - SSH key rotation: $(stat -c %y /etc/honua/ssh/honua_prod)"
echo "  - Service account: $(id honua)"
echo "  - File permissions: $(ls -ld /var/honua/deployments)"

echo ""
echo "2. Audit Logging"
echo "  - Audit rules: $(auditctl -l | grep honua | wc -l)"
echo "  - Log retention: $(find /var/log/honua -name 'audit-*' | wc -l) days"

echo ""
echo "3. Change Management"
echo "  - Recent deployments: $(cat /var/honua/deployments/production.json | jq '.history | length')"
echo "  - Approvals required: $(cat /etc/honua/appsettings.Production.json | jq '.GitOps.DeploymentPolicy.RequiresApproval')"

echo ""
echo "4. Encryption"
echo "  - Git protocol: SSH (verified)"
echo "  - State encryption: $(lsblk | grep honua-state)"
```

---

## Incident Response

### Security Incident Playbook

**Suspected Compromise:**

```bash
# 1. IMMEDIATE ACTIONS
# Stop GitOps to prevent further damage
sudo systemctl stop honua

# Rotate all credentials immediately
vault kv put secret/honua/production-rotated ...

# Review recent changes
git -C /var/honua/gitops-repo log --since="24 hours ago" --all

# 2. INVESTIGATION
# Collect evidence
tar -czf /tmp/incident-evidence-$(date +%s).tar.gz \
  /var/honua/deployments \
  /var/log/honua \
  /var/log/audit/audit.log

# Review audit logs
ausearch -k honua-deployments -ts recent

# Check for unauthorized changes
git -C /var/honua/gitops-repo log --all --source --graph

# 3. CONTAINMENT
# Revert to known good state
cd /var/honua/gitops-repo
LAST_KNOWN_GOOD="<commit-sha>"
git reset --hard $LAST_KNOWN_GOOD

# Verify state files
for file in /var/honua/deployments/*.json; do
  jq empty "$file" || echo "Corrupted: $file"
done

# 4. RECOVERY
# Restart with monitoring
sudo systemctl start honua
sudo journalctl -u honua -f

# 5. POST-INCIDENT
# Conduct root cause analysis
# Update security controls
# Document lessons learned
```

### Unauthorized Change Detection

```bash
#!/bin/bash
# Monitor for unexpected changes

REPO_PATH="/var/honua/gitops-repo"
STATE_PATH="/var/honua/deployments"

# Check for unsigned commits
UNSIGNED=$(git -C $REPO_PATH log --show-signature | grep -c "No signature")
if [ $UNSIGNED -gt 0 ]; then
  echo "ALERT: Found $UNSIGNED unsigned commits"
  # Alert security team
fi

# Check for changes outside business hours
RECENT_COMMIT_TIME=$(git -C $REPO_PATH log -1 --format=%ct)
CURRENT_HOUR=$(date +%H)
if [ $CURRENT_HOUR -lt 9 ] || [ $CURRENT_HOUR -gt 17 ]; then
  echo "WARNING: Commit outside business hours"
fi

# Check state file integrity
for file in $STATE_PATH/*.json; do
  if ! jq empty "$file" 2>/dev/null; then
    echo "CRITICAL: State file corruption detected: $file"
    # Immediate alert
  fi
done
```

---

## Security Checklist

### Pre-Deployment Security Review

- [ ] SSH keys generated with strong algorithm (ed25519)
- [ ] Deploy keys configured as read-only
- [ ] Branch protection rules enabled
- [ ] Signed commits required
- [ ] Secrets externalized (not in Git)
- [ ] Service account created with minimal permissions
- [ ] File permissions configured correctly
- [ ] Firewall rules implemented
- [ ] TLS configured for Git operations
- [ ] Audit logging enabled
- [ ] Monitoring and alerting configured
- [ ] Incident response plan documented
- [ ] Compliance requirements validated

### Ongoing Security Operations

- [ ] Weekly: Review audit logs
- [ ] Monthly: Access control review
- [ ] Quarterly: Rotate SSH keys and tokens
- [ ] Quarterly: Security scan of configuration repository
- [ ] Annually: Penetration testing
- [ ] Annually: Compliance audit

---

## References

- [GitOps Deployment Runbook](./gitops-deployment-runbook.md)
- [GitOps Configuration Reference](./gitops-configuration-reference.md)
- [GitOps Best Practices](./gitops-best-practices.md)
- NIST Cybersecurity Framework
- CIS Benchmarks
- OWASP Secure Coding Practices

---

**Last Updated:** 2025-10-23
**Version:** 1.0
