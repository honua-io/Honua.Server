# Security Badges for README.md

Add these badges to your README.md to showcase your security posture.

---

## GitHub Security Badges

```markdown
<!-- Security Policy -->
[![Security Policy](https://img.shields.io/badge/security-policy-blue.svg)](SECURITY.md)

<!-- CodeQL Status -->
[![CodeQL](https://github.com/YOUR_ORG/HonuaIO/actions/workflows/codeql.yml/badge.svg)](https://github.com/YOUR_ORG/HonuaIO/actions/workflows/codeql.yml)

<!-- Known Vulnerabilities -->
[![Known Vulnerabilities](https://snyk.io/test/github/YOUR_ORG/HonuaIO/badge.svg)](https://snyk.io/test/github/YOUR_ORG/HonuaIO)

<!-- Dependencies -->
[![Dependencies](https://img.shields.io/badge/dependencies-up%20to%20date-brightgreen.svg)](https://github.com/YOUR_ORG/HonuaIO/network/dependencies)

<!-- OWASP -->
[![OWASP Top 10](https://img.shields.io/badge/OWASP-compliant-green.svg)](docs/security/OWASP_TOP_10_ASSESSMENT.md)
```

---

## Security Section for README

```markdown
## Security

### 🛡️ Security Features

- **Authentication**: JWT with Argon2id password hashing
- **Authorization**: Role-based access control (RBAC)
- **Encryption**: TLS 1.3, HSTS headers
- **Protection**: Rate limiting, CORS, CSP headers
- **Validation**: SQL injection protection, input sanitization
- **Monitoring**: Automated vulnerability scanning

### 🔒 Security Posture

- ✅ **OWASP Top 10 Compliant** (84/100 score)
- ✅ **Zero Known Critical Vulnerabilities**
- ✅ **Automated Security Scanning** (CodeQL + Dependabot)
- ✅ **Responsible Disclosure Program**

### 📋 Security Audit

Last security assessment: **2025-10-06**  
Overall grade: **A**  
See: [OWASP Top 10 Assessment](docs/security/OWASP_TOP_10_ASSESSMENT.md)

### 🐛 Report Security Issues

Found a security vulnerability? Please report it responsibly:

- **Email**: security@honua.io
- **Policy**: [SECURITY.md](SECURITY.md)
- **Disclosure**: [/.well-known/security.txt](/.well-known/security.txt)

We respond to all reports within **48 hours**.

### 🏆 Security Hall of Fame

We recognize security researchers who help keep Honua secure.  
See: [Security Hall of Fame](docs/SECURITY_HALL_OF_FAME.md)
```

---

## Advanced Badges (Optional)

```markdown
<!-- Security Score -->
[![Security Score](https://img.shields.io/badge/security%20score-A-brightgreen.svg)](SECURITY_FIXES_SUMMARY.md)

<!-- OpenSSF Best Practices -->
[![OpenSSF Best Practices](https://bestpractices.coreinfrastructure.org/projects/XXXXX/badge)](https://bestpractices.coreinfrastructure.org/projects/XXXXX)

<!-- License -->
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

<!-- .NET Version -->
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
```

---

## Customization

1. Replace `YOUR_ORG` with your GitHub organization/username
2. Replace `HonuaIO` with your repository name
3. Update `security@honua.io` with your security email
4. Update dates and scores as you conduct assessments

---

## Example Full Security Section

```markdown
# Honua Geospatial Server

[![Security Policy](https://img.shields.io/badge/security-policy-blue.svg)](SECURITY.md)
[![CodeQL](https://github.com/YOUR_ORG/HonuaIO/actions/workflows/codeql.yml/badge.svg)](https://github.com/YOUR_ORG/HonuaIO/actions/workflows/codeql.yml)
[![OWASP Top 10](https://img.shields.io/badge/OWASP-compliant-green.svg)](docs/security/OWASP_TOP_10_ASSESSMENT.md)
[![Security Score](https://img.shields.io/badge/security%20score-A-brightgreen.svg)](SECURITY_FIXES_SUMMARY.md)

Enterprise-grade geospatial data server with security built-in.

## Features

- OGC API Features, WFS, WMS
- Multiple database support (PostgreSQL, MySQL, SQL Server, SQLite)
- Vector tiles (MVT)
- Enterprise data providers

## Security

### 🛡️ Built-in Security

- JWT authentication with Argon2id hashing
- Rate limiting and DDoS protection
- Security headers (HSTS, CSP, X-Frame-Options)
- SQL injection protection
- Path traversal prevention
- Automated vulnerability scanning

### 📊 Security Status

- **Grade**: A
- **OWASP Score**: 84/100
- **Known Vulnerabilities**: 0 critical
- **Last Audit**: 2025-10-06

### 🐛 Report Issues

Found a security vulnerability? Email **security@honua.io**

See our [Security Policy](SECURITY.md) for details.

## Quick Start

\`\`\`bash
dotnet run --project src/Honua.Server.Host
\`\`\`

## Documentation

- [Getting Started](docs/getting-started.md)
- [Security](SECURITY.md)
- [OWASP Assessment](docs/security/OWASP_TOP_10_ASSESSMENT.md)

## License

MIT License - see [LICENSE](LICENSE)
```

---

## Badge Colors

Use these colors for consistency:

- **Security**: `blue` (#007ec6)
- **Pass/Compliant**: `brightgreen` (#44cc11)
- **Warning**: `yellow` (#dfb317)
- **Critical**: `red` (#e05d44)
- **Info**: `lightgrey` (#9f9f9f)

---

## Auto-Update Badges

Some badges auto-update:
- ✅ GitHub Actions status
- ✅ Snyk vulnerability count
- ✅ Dependency status

Manual update needed:
- ❌ Security score
- ❌ OWASP compliance
- ❌ Last audit date

Update these after each security assessment.
