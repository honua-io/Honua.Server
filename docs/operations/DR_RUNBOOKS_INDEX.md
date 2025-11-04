# Disaster Recovery Runbooks - Master Index

**Last Updated**: 2025-10-18
**Version**: 1.0
**Status**: Production Ready

## Overview

This document provides a comprehensive index of all Disaster Recovery (DR) runbooks for the Honua GIS Platform. These runbooks are designed to restore service in catastrophic failure scenarios.

## Quick Reference

| Runbook | Scenario | RTO | RPO | Severity | Use When |
|---------|----------|-----|-----|----------|----------|
| **[DR-01](./DR_RUNBOOK_01_DATABASE_RECOVERY.md)** | Database Recovery | 30-90 min | 1 hour | P1 | Database corruption, data loss |
| **[DR-02](./DR_RUNBOOK_02_CERTIFICATE_RECOVERY.md)** | Certificate Recovery | 15-60 min | N/A | P1 | Certificate expiration, compromise |
| **[DR-03](./DR_RUNBOOK_03_INFRASTRUCTURE_RECREATION.md)** | Infrastructure Recreation | 2-4 hours | 4 hours | P0 | Complete infrastructure loss |
| **[DR-04](./DR_RUNBOOK_04_DATACENTER_FAILOVER.md)** | Datacenter Failover | 30-90 min | 15 min | P0 | Regional outage |
| **[DR-05](./DR_RUNBOOK_05_COMPLETE_SYSTEM_RESTORE.md)** | Complete System Restore | 4-8 hours | 4 hours | P0 | Total system loss |

## Recovery Objectives Summary

### RTO (Recovery Time Objective)

Maximum acceptable downtime for each scenario:

```
DR-02 (Certificates):     15 minutes   ████░░░░░░░░░░░░░░░░ (fastest)
DR-04 (Failover):         30 minutes   ████████░░░░░░░░░░░░
DR-01 (Database):         30 minutes   ████████░░░░░░░░░░░░
DR-03 (Infrastructure):   2 hours      ████████████████░░░░
DR-05 (Complete):         4 hours      ████████████████████ (slowest)
```

### RPO (Recovery Point Objective)

Maximum acceptable data loss for each scenario:

```
DR-02 (Certificates):     N/A (stateless)
DR-04 (Failover):         15 minutes   ████░░░░░░░░░░░░░░░░ (minimal loss)
DR-01 (Database):         1 hour       ████████░░░░░░░░░░░░
DR-03 (Infrastructure):   4 hours      ████████████████░░░░
DR-05 (Complete):         4 hours      ████████████████░░░░ (max loss)
```

---

## Runbook Summaries

### DR-01: Database Recovery from Backup

**Purpose**: Restore PostgreSQL database from backup after corruption or data loss

**Key Procedures**:
- Full database restore (PostgreSQL Flexible Server)
- Point-in-time recovery (PITR)
- Cross-region database recovery
- Self-hosted database restore

**Recovery Time**: 30-90 minutes

**Data Loss**: Up to 1 hour (based on backup frequency)

**When to Use**:
- Database corruption detected
- Accidental data deletion
- Failed schema migration
- Ransomware attack on database
- Complete database deletion

**Key Steps**:
1. Stop application traffic
2. Verify backup availability
3. Restore database from backup
4. Verify data integrity
5. Update connection strings
6. Resume application traffic
7. Validate functionality

**Prerequisites**:
- Verified database backups (daily)
- Key Vault access for credentials
- Kubernetes cluster access
- Database admin permissions

---

### DR-02: Certificate Recovery and Reissuance

**Purpose**: Recover or reissue TLS/SSL certificates for HTTPS services

**Key Procedures**:
- Emergency Let's Encrypt reissuance
- Certificate recovery from backup
- Emergency self-signed certificate
- Certificate revocation and reissuance

**Recovery Time**: 15-60 minutes

**Data Loss**: N/A (certificates are stateless)

**When to Use**:
- Certificate expired unexpectedly
- Private key compromised
- Certificate revoked by CA
- Certificates lost in infrastructure failure
- Let's Encrypt rate limits hit

**Key Steps**:
1. Assess certificate status
2. Configure DNS challenge provider
3. Deploy emergency ClusterIssuer
4. Issue new certificate
5. Monitor certificate issuance
6. Update ingress controllers
7. Test HTTPS endpoints

**Prerequisites**:
- DNS provider access (Cloudflare/Route53)
- Kubernetes cluster access
- cert-manager installed
- Key Vault access
- Domain ownership verification

---

### DR-03: Infrastructure Recreation from Scratch

**Purpose**: Rebuild entire Honua infrastructure from zero in catastrophic scenarios

**Key Procedures**:
- Azure complete infrastructure recreation
- Terraform state backend creation
- Multi-phase infrastructure deployment
- Database and application restoration

**Recovery Time**: 2-4 hours

**Data Loss**: Up to 4 hours (based on backup age)

**When to Use**:
- Complete datacenter failure
- Cloud account deleted/compromised
- Ransomware attack on infrastructure
- Regulatory compliance requires rebuild
- Migration to new cloud provider

**Four-Phase Approach**:
1. **Foundation (45 min)**: Cloud accounts, networking, DNS, state backend
2. **Data Layer (60 min)**: Database, Redis, object storage
3. **Application (60 min)**: Kubernetes, services, ingress, certificates
4. **Monitoring (30 min)**: Prometheus, Grafana, logging, alerting

**Prerequisites**:
- Terraform configurations (in vault)
- Offline backup vault access
- Cloud provider root credentials
- Complete credential set
- Infrastructure-as-Code repository

---

### DR-04: Data Center Failover

**Purpose**: Failover services from primary to secondary datacenter/region

**Key Procedures**:
- Active-passive regional failover
- Planned datacenter maintenance
- Database performance failover
- Multi-region traffic rebalancing

**Recovery Time**: 30-90 minutes

**Data Loss**: 5-15 minutes (active-passive), 0 minutes (active-active)

**When to Use**:
- Regional cloud outage (AWS/Azure region down)
- Natural disaster affecting datacenter
- Planned maintenance window
- Primary region performance degradation
- Compliance requirement (geographic shift)

**Key Steps**:
1. Declare incident and assess
2. Verify secondary region status
3. Promote secondary database to primary
4. Update application configuration
5. Update DNS to secondary region
6. Verify failover success
7. Monitor and communicate completion

**Prerequisites**:
- Secondary region infrastructure deployed
- Database replication configured
- DNS failover configured (Traffic Manager)
- Application deployed in secondary
- Tested failover procedures

---

### DR-05: Complete System Restore

**Purpose**: Master procedure for total system restoration from catastrophic loss

**Key Procedures**:
- Offline vault recovery
- New cloud account creation
- Complete infrastructure rebuild
- Full data restoration
- Application and monitoring deployment

**Recovery Time**: 4-8 hours

**Data Loss**: Up to 4 hours (or backup age if vault older)

**When to Use**:
- Total system compromise (malicious deletion)
- Ransomware attack (all systems encrypted)
- Multi-region cascading failures
- Company-wide disaster (building destroyed)
- Regulatory seizure of all cloud resources

**Four-Phase Approach**:
1. **Foundation (1 hour)**: New accounts, networking, DNS, monitoring
2. **Data Layer (2 hours)**: Database infrastructure, restore, object storage
3. **Application (2 hours)**: Kubernetes, secrets, services, DNS
4. **Complete System (3 hours)**: Monitoring, validation, reconciliation

**Critical Prerequisites**:
- Offline disaster recovery vault (USB drives, safe)
- Emergency access laptop (air-gapped)
- C-level authorization
- Complete credential set
- Verified backup integrity (tested monthly)

---

## Decision Tree

Use this decision tree to choose the correct runbook:

```
Is the system completely unavailable across all regions?
├─ YES → DR-05 (Complete System Restore)
└─ NO
   │
   Is only the database affected?
   ├─ YES → DR-01 (Database Recovery)
   └─ NO
      │
      Are TLS certificates the issue?
      ├─ YES → DR-02 (Certificate Recovery)
      └─ NO
         │
         Is only one region/datacenter down?
         ├─ YES → DR-04 (Datacenter Failover)
         └─ NO
            │
            Is all infrastructure deleted but database intact?
            ├─ YES → DR-03 (Infrastructure Recreation)
            └─ NO → Contact incident commander for assessment
```

---

## Recovery Priority Matrix

### By Business Impact

| Priority | Component | RTO | Runbook | Justification |
|----------|-----------|-----|---------|---------------|
| **P0** | DNS | 5 min | DR-04 | No access = no service |
| **P0** | TLS Certificates | 15 min | DR-02 | HTTPS required for security |
| **P0** | Database | 30 min | DR-01 | Core data access |
| **P0** | Core Services | 60 min | DR-03/04 | API functionality |
| **P1** | Caching (Redis) | 90 min | DR-03/04 | Performance impact only |
| **P1** | Monitoring | 120 min | DR-03 | Blind but functional |
| **P2** | CI/CD | 4 hours | DR-03 | Can deploy manually |
| **P3** | Dev/Test | 24 hours | DR-03 | Non-production |

---

## Testing Schedule

All DR runbooks must be tested regularly:

| Runbook | Frequency | Environment | Last Tested | Next Test |
|---------|-----------|-------------|-------------|-----------|
| **DR-01** | Monthly | Staging | 2025-10-15 | 2025-11-15 |
| **DR-02** | Monthly | Staging | 2025-10-10 | 2025-11-10 |
| **DR-03** | Quarterly | Staging | 2025-10-01 | 2026-01-01 |
| **DR-04** | Quarterly | Production | 2025-10-01 | 2026-01-01 |
| **DR-05** | Annually | Staging | 2025-09-01 | 2026-09-01 |

### Testing Requirements

#### Monthly (DR-01, DR-02)
- Test in staging environment
- Use real backups
- Verify all steps
- Time the procedure
- Document issues

#### Quarterly (DR-03, DR-04)
- Test in staging (DR-03) or production (DR-04)
- Full end-to-end execution
- Involve all team members
- Measure against SLA
- Update runbook based on findings

#### Annually (DR-05)
- Executive-sponsored drill
- Simulate complete loss
- Use offline vault
- Test with backup team members
- Board-level report

---

## Validation Checklist Template

Use this checklist after ANY recovery:

### Infrastructure
- [ ] All cloud resources created
- [ ] Networking configured correctly
- [ ] DNS resolving to correct IPs
- [ ] TLS certificates valid

### Data
- [ ] Database accessible
- [ ] Data integrity verified
- [ ] Row counts match expectations (within RPO)
- [ ] Backups configured and working

### Applications
- [ ] All services running
- [ ] Health endpoints returning 200 OK
- [ ] No critical errors in logs
- [ ] Performance within SLA

### Security
- [ ] Secrets restored
- [ ] RBAC policies applied
- [ ] Network policies active
- [ ] Audit logging enabled

### Monitoring
- [ ] Metrics collecting
- [ ] Dashboards accessible
- [ ] Alerts configured
- [ ] Logs aggregating

### Functionality
- [ ] Can read data
- [ ] Can write data
- [ ] Search queries work
- [ ] File uploads work

---

## Communication Templates

### Incident Notification (Start)

```
Subject: [P0] Disaster Recovery Initiated - Honua Platform

PRIORITY: CRITICAL
STATUS: RECOVERY IN PROGRESS
ETA: [X] hours

Summary:
- Incident: [Brief description]
- Impact: [Services affected]
- Recovery: [Runbook being executed]
- Team: [Who is working on it]
- Next Update: [When]

DO NOT REPLY TO THIS EMAIL
Updates: https://status.honua.io
War Room: [Link]
```

### Recovery Complete (End)

```
Subject: [RESOLVED] Disaster Recovery Complete - Honua Platform

PRIORITY: INFORMATIONAL
STATUS: OPERATIONAL
TOTAL DOWNTIME: [X] minutes

Summary:
- Service restored at [time]
- Root cause: [Brief description]
- Data loss: [Amount within RPO]
- Monitoring: Active for 24 hours

Post-Incident:
- Post-mortem: [Date/Time]
- Root cause analysis: [Timeline]
- Compensation: [If applicable]

Questions: support@honua.io
```

---

## Emergency Contacts

### Honua Team

| Role | Primary | Backup | Phone | Email |
|------|---------|--------|-------|-------|
| **Incident Commander** | CTO | VP Eng | +1-xxx-xxx-xxxx | oncall@honua.io |
| **Database Lead** | Sr. DBA | DBA | +1-xxx-xxx-xxxx | dba@honua.io |
| **Infrastructure Lead** | Principal Architect | Sr. DevOps | +1-xxx-xxx-xxxx | platform@honua.io |
| **Security Lead** | CISO | Security Eng | +1-xxx-xxx-xxxx | security@honua.io |
| **Communications** | VP Product | Customer Success | +1-xxx-xxx-xxxx | support@honua.io |

### Vendor Support

| Vendor | Support Level | Phone | Portal | SLA |
|--------|---------------|-------|--------|-----|
| **AWS** | Enterprise | +1-866-xxx-xxxx | console.aws.amazon.com | 15 min |
| **Azure** | Premier | +1-800-xxx-xxxx | portal.azure.com | 15 min |
| **Cloudflare** | Enterprise | +1-888-xxx-xxxx | dash.cloudflare.com | 1 hour |
| **Let's Encrypt** | Community | N/A | community.letsencrypt.org | N/A |

---

## Runbook Maintenance

### Quarterly Review

Each quarter, the platform team must:

1. Review all runbooks for accuracy
2. Test procedures in staging
3. Update time estimates based on testing
4. Document any changes to infrastructure
5. Update contact information

### After Each Incident

After any DR event:

1. Update runbook with lessons learned
2. Document actual vs. estimated times
3. Note any steps that were unclear
4. Add any missing procedures
5. Update validation checklists

### Annual Audit

Once per year:

1. Full review by external auditor
2. Gap analysis vs. industry standards
3. Insurance/compliance verification
4. Board presentation on DR readiness
5. Budget review for DR improvements

---

## Appendix: Backup Inventory

### Database Backups

- **Location**: Azure Blob Storage (stbkphonua123456/database-backups/)
- **Frequency**: Daily at 2:00 AM UTC
- **Retention**: 90 days daily, 1 year weekly, 7 years monthly
- **Size**: ~500 MB compressed
- **Verification**: Automated daily restore test

### Configuration Backups

- **Location**: Azure Blob Storage (stbkphonua123456/config-backups/)
- **Frequency**: Daily at 3:00 AM UTC
- **Retention**: 30 days
- **Includes**: Kubernetes manifests, Terraform state, secrets metadata

### Object Storage Backups

- **Location**: AWS S3 (honua-dr-backups/storage/)
- **Frequency**: Weekly snapshot
- **Retention**: 90 days
- **Size**: ~50 GB
- **Note**: Geo-replicated automatically

### Offline Vault

- **Location**: Corporate HQ Safe
- **Update Frequency**: Monthly
- **Contents**: Latest backups (all types), credentials, printed docs
- **Responsible**: CTO + CEO

---

## Related Documentation

- [Operations Guide](./PROCESS_FRAMEWORK_OPERATIONS.md) - Daily operations
- [Operational Runbooks](./RUNBOOKS.md) - Non-disaster procedures
- [Deployment Guide](../deployment/README.md) - Initial deployment
- [Security Architecture](../security/SECURITY_ARCHITECTURE.md) - Security design

---

**Document Classification**: CONFIDENTIAL
**Access Level**: Leadership + Platform Team
**Distribution**: Controlled - Need to Know Basis

**Document Version**: 1.0
**Last Updated**: 2025-10-18
**Next Review**: 2025-11-18
**Owner**: Platform Engineering Team
**Approved By**: CTO, VP Engineering, CISO
