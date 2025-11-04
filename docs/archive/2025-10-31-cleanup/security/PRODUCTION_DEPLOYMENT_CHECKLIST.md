# Production Deployment Security Checklist

**Use this checklist before deploying Honua to production.**

---

## Pre-Deployment Security Checks

### ✅ Environment Configuration

- [ ] `ASPNETCORE_ENVIRONMENT` set to `Production`
- [ ] `AllowQuickStart` is `false` (or not configured)
- [ ] JWT secret key is strong (256-bit minimum, random)
- [ ] Database credentials stored in environment variables (not config files)
- [ ] TLS certificates valid and properly configured
- [ ] All secrets removed from appsettings.json

**Verify**:
```bash
# Check environment
echo $ASPNETCORE_ENVIRONMENT  # Must be "Production"

# Verify no QuickStart
grep -r "QuickStart" appsettings.Production.json  # Should be false or absent

# Check for hardcoded secrets
grep -rE "(password|secret|key)\s*[:=]\s*['\"]" appsettings*.json
```

---

### ✅ Authentication & Authorization

- [ ] Authentication mode is `Local` or `OIDC` (NOT QuickStart)
- [ ] Strong password policy configured (min 12 chars)
- [ ] Account lockout enabled (5 attempts, 30 min lockout)
- [ ] JWT tokens have reasonable expiration (60 min recommended)
- [ ] All admin endpoints require authorization
- [ ] Default admin account password changed
- [ ] API keys rotated from development

**Test**:
```bash
# Verify QuickStart is blocked
curl -X GET http://localhost/admin/metadata \
  -H "Authorization: Bearer invalid_token"
# Should return 401 Unauthorized (not 200)

# Test rate limiting
for i in {1..150}; do curl http://localhost/healthz; done
# Should get 429 Too Many Requests
```

---

### ✅ Network Security

- [ ] HTTPS enabled and enforced
- [ ] HTTP automatically redirects to HTTPS
- [ ] HSTS header configured (max-age=31536000)
- [ ] TLS 1.2+ only (TLS 1.0/1.1 disabled)
- [ ] Strong cipher suites configured
- [ ] Firewall rules configured (allow only necessary ports)
- [ ] Database not exposed to internet
- [ ] Admin endpoints IP whitelisted (if applicable)

**Verify HTTPS**:
```bash
# Test HTTPS redirect
curl -I http://your-domain.com
# Should return 301/302 redirect to https://

# Check HSTS header
curl -I https://your-domain.com | grep -i strict-transport
# Should see: Strict-Transport-Security: max-age=31536000

# Test TLS version
nmap --script ssl-enum-ciphers -p 443 your-domain.com
# Should show TLS 1.2+ only
```

---

### ✅ Security Headers

- [ ] HSTS header present
- [ ] X-Frame-Options set to DENY
- [ ] X-Content-Type-Options set to nosniff
- [ ] Content-Security-Policy configured
- [ ] X-XSS-Protection enabled
- [ ] Referrer-Policy set
- [ ] Server header removed
- [ ] X-Powered-By header removed

**Test Headers**:
```bash
# Check all security headers
curl -I https://your-domain.com

# Should see:
# Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
# X-Frame-Options: DENY
# X-Content-Type-Options: nosniff
# Content-Security-Policy: default-src 'self'...
# X-XSS-Protection: 1; mode=block
# Referrer-Policy: strict-origin-when-cross-origin

# Should NOT see:
# Server: Kestrel
# X-Powered-By: ASP.NET
```

---

### ✅ Rate Limiting

- [ ] Rate limiting enabled
- [ ] Appropriate limits configured per endpoint
- [ ] Rate limit headers returned (X-RateLimit-*)
- [ ] 429 status returned when limit exceeded
- [ ] Retry-After header present on 429

**Test**:
```bash
# Test rate limit
for i in {1..150}; do 
  curl -w "%{http_code}\n" https://your-domain.com/api/collections
done | grep 429
# Should see 429 after ~100-200 requests
```

---

### ✅ Input Validation

- [ ] File upload size limits enforced (1GB max)
- [ ] File extension whitelist active
- [ ] Path traversal protection verified
- [ ] SQL injection protection verified (parameterized queries)
- [ ] XSS protection enabled
- [ ] Request body size limits configured

**Test**:
```bash
# Test file size limit
dd if=/dev/zero of=large.zip bs=1M count=2000  # 2GB file
curl -X POST https://your-domain.com/admin/data/ingest \
  -F "file=@large.zip"
# Should return 400 Bad Request

# Test invalid extension
echo "malicious" > test.exe
curl -X POST https://your-domain.com/admin/data/ingest \
  -F "file=@test.exe"
# Should return 400 Bad Request

# Test path traversal
curl "https://your-domain.com/attachments/../../etc/passwd"
# Should return 400/404, not file contents
```

---

### ✅ Logging & Monitoring

- [ ] Structured logging configured (JSON)
- [ ] Sensitive data redacted from logs
- [ ] Log aggregation configured (optional)
- [ ] Security events logged:
  - [ ] Failed login attempts
  - [ ] Admin operations
  - [ ] Rate limit violations
  - [ ] File uploads
- [ ] Log retention policy defined
- [ ] Alerts configured for security events (optional)

**Verify**:
```bash
# Check logs don't contain passwords
journalctl -u honua | grep -i password
# Should not show actual passwords

# Test failed login logging
curl -X POST https://your-domain.com/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"wrong"}'

# Check logs
journalctl -u honua -n 20
# Should see failed login attempt logged (without password)
```

---

### ✅ Database Security

- [ ] Database connection encrypted (SSL/TLS)
- [ ] Database user has minimum required permissions
- [ ] Separate database user for application (not admin)
- [ ] Database not accessible from internet
- [ ] Backups encrypted
- [ ] Backup restoration tested
- [ ] Database credentials in environment variables

**Verify**:
```bash
# Test database connection from application server
nc -zv database-host 5432
# Should connect

# Test database connection from internet
nc -zv database-host 5432
# Should fail (connection refused/timeout)

# Check connection string doesn't contain passwords
grep -r "ConnectionStrings" appsettings.Production.json
# Should use environment variable: ${DATABASE_CONNECTION_STRING}
```

---

### ✅ Dependency Security

- [ ] All dependencies up to date
- [ ] No known critical vulnerabilities
- [ ] Dependabot enabled
- [ ] CodeQL scanning enabled
- [ ] Snyk scanning enabled (optional)
- [ ] Vulnerable package (Snowflake.Data) updated

**Check**:
```bash
# Check for vulnerable packages
dotnet list package --vulnerable

# Check for outdated packages
dotnet list package --outdated

# Should show: No vulnerable packages found
```

---

### ✅ CORS Configuration

- [ ] CORS allowed origins explicitly whitelisted
- [ ] No `AllowAnyOrigin` in production
- [ ] AllowCredentials only with specific origins
- [ ] Proper HTTP methods allowed
- [ ] Exposed headers minimized

**Test**:
```bash
# Test CORS from unauthorized origin
curl -H "Origin: https://evil.com" \
  -H "Access-Control-Request-Method: GET" \
  -X OPTIONS https://your-domain.com/api/collections
# Should not include Access-Control-Allow-Origin: https://evil.com

# Test from allowed origin
curl -H "Origin: https://your-domain.com" \
  -H "Access-Control-Request-Method: GET" \
  -X OPTIONS https://your-domain.com/api/collections
# Should include Access-Control-Allow-Origin
```

---

### ✅ File & Directory Permissions

- [ ] Application runs as non-root user
- [ ] Configuration files readable only by application user
- [ ] Logs writable only by application user
- [ ] Upload directory writable only by application user
- [ ] Database files (if SQLite) protected
- [ ] Private keys have 600 permissions

**Check**:
```bash
# Check process user
ps aux | grep Honua
# Should NOT be root

# Check config permissions
ls -la appsettings.Production.json
# Should be: -rw-r----- or more restrictive

# Check certificate permissions
ls -la /path/to/cert.pfx
# Should be: -rw------- (600)
```

---

### ✅ Backup & Recovery

- [ ] Database backups configured
- [ ] Backup encryption enabled
- [ ] Backup restoration tested
- [ ] Disaster recovery plan documented
- [ ] Backup retention policy defined
- [ ] Off-site backup storage configured

---

### ✅ Compliance & Documentation

- [ ] Security policy documented (SECURITY.md)
- [ ] Vulnerability disclosure process active
- [ ] security.txt accessible at /.well-known/security.txt
- [ ] Security contact email monitored
- [ ] OWASP Top 10 assessment completed
- [ ] Privacy policy created (if handling PII)
- [ ] Data retention policy defined
- [ ] Incident response plan documented

---

## Post-Deployment Verification

### Immediate Checks (Within 1 hour)

```bash
# 1. Verify application is running
curl https://your-domain.com/healthz/ready
# Should return: Healthy

# 2. Verify HTTPS is working
curl -I https://your-domain.com
# Should return 200 OK with security headers

# 3. Verify HTTP redirects
curl -I http://your-domain.com
# Should return 301/302 to https://

# 4. Verify security.txt
curl https://your-domain.com/.well-known/security.txt
# Should return security contact info

# 5. Test authentication
curl -X GET https://your-domain.com/admin/metadata
# Should return 401 Unauthorized

# 6. Test rate limiting (run this a few times)
for i in {1..200}; do curl https://your-domain.com/api/collections; done
# Should eventually get 429 Too Many Requests

# 7. Check for information disclosure
curl https://your-domain.com/nonexistent
# Should NOT reveal framework version or stack traces
```

### Security Scan (Within 24 hours)

```bash
# Run security scanner (choose one)

# Option 1: OWASP ZAP
zap.sh -quickurl https://your-domain.com \
  -quickout /tmp/zap-report.html

# Option 2: Nikto
nikto -h https://your-domain.com

# Option 3: SSL Labs
# Visit: https://www.ssllabs.com/ssltest/analyze.html?d=your-domain.com
```

---

## Security Monitoring (Ongoing)

### Daily
- [ ] Check error logs for anomalies
- [ ] Review failed login attempts
- [ ] Monitor rate limit violations

### Weekly
- [ ] Review Dependabot PRs
- [ ] Check GitHub Security alerts
- [ ] Review CodeQL findings

### Monthly
- [ ] Run vulnerability scan
- [ ] Update dependencies
- [ ] Review access logs
- [ ] Check certificate expiration (if <90 days)

### Quarterly
- [ ] Re-run OWASP Top 10 assessment
- [ ] Review and update security.txt
- [ ] Test backup restoration
- [ ] Review security policies
- [ ] Conduct security training

---

## Rollback Plan

If security issues are discovered post-deployment:

1. **Immediate**:
   ```bash
   # Stop the application
   systemctl stop honua
   
   # Or scale down (Kubernetes)
   kubectl scale deployment honua --replicas=0
   ```

2. **Assess**:
   - Determine severity (use CVSS calculator)
   - Check if vulnerability is exploited
   - Review logs for suspicious activity

3. **Respond**:
   - **Critical**: Rollback immediately
   - **High**: Apply hotfix within 24 hours
   - **Medium**: Patch in next release
   - **Low**: Document and schedule fix

4. **Communicate**:
   - Notify stakeholders
   - Update security advisories
   - Email affected users (if data breach)

---

## Common Security Misconfigurations

### ❌ Dangerous Configurations

```json
// NEVER do this in production:
{
  "Honua": {
    "Authentication": {
      "Mode": "QuickStart",  // ❌ NO!
      "AllowQuickStart": true  // ❌ NO!
    }
  },
  "Cors": {
    "AllowedOrigins": ["*"]  // ❌ NO!
  },
  "Logging": {
    "EnableSensitiveDataLogging": true  // ❌ NO!
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Password=secret123"  // ❌ NO!
  }
}
```

### ✅ Correct Configurations

```json
{
  "Honua": {
    "Authentication": {
      "Mode": "Local",
      "Enforce": true
    }
  },
  "Cors": {
    "AllowedOrigins": ["https://your-domain.com"]
  },
  "Logging": {
    "EnableSensitiveDataLogging": false
  },
  "ConnectionStrings": {
    "DefaultConnection": "${DATABASE_CONNECTION_STRING}"
  }
}
```

---

## Security Hotline

**Emergency Security Issues**:
- Email: security@honua.io
- Include: [URGENT] in subject line
- Response: Within 4 hours

**Non-Emergency**:
- GitHub Security Advisories
- Response: Within 48 hours

---

## Sign-Off

**Deployment Date**: _____________  
**Deployed By**: _____________  
**Security Review By**: _____________  
**Approved By**: _____________

**Checklist Completion**: _____ / _____ items complete

**Notes**:
_________________________________
_________________________________
_________________________________

---

**Next Security Review**: [Date + 90 days]

---

*This checklist should be completed for every production deployment.*
