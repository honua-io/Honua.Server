# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in Honua, please report it responsibly.

**Please do NOT report security vulnerabilities through public GitHub issues.**

Instead, please use one of these methods:

- **Preferred**: Use [GitHub Security Advisories](https://github.com/mikemcdougall/HonuaIO/security/advisories/new) to privately report vulnerabilities
- **Alternative**: Open a private discussion or contact the maintainers directly through GitHub

### What to Include

When reporting a vulnerability, please include:

- Description of the vulnerability
- Steps to reproduce the issue
- Affected versions/components
- Potential impact
- Any suggested fixes (optional)

We will acknowledge receipt and work with you to understand and address the issue.

## Supported Versions

| Version | Status |
| ------- | ------ |
| 2.x (dev branch) | Active development |
| < 2.0 | Not maintained |

Security fixes are applied to the current development branch. Users should stay updated with the latest releases.

## Security Features

Honua implements security controls including:

**Authentication & Authorization**
- JWT-based authentication with Argon2id password hashing
- Role-based access control (RBAC)
- OIDC integration support
- QuickStart mode (development only - disabled in production)

**Input Validation & Protection**
- Parameterized queries (SQL injection prevention)
- Path traversal protection for file operations
- Input sanitization and validation
- Geometry complexity validation

**Network Security**
- HTTPS enforcement in production
- Configurable CORS policies
- Rate limiting capabilities
- Security headers (HSTS, CSP, X-Frame-Options, X-Content-Type-Options)

**Data Protection**
- Secure credential storage
- PII redaction in logs
- Attachment security controls
- Database connection string encryption

## Production Deployment Security

### Critical Requirements

Before deploying to production:

- ✅ Set `ASPNETCORE_ENVIRONMENT=Production`
- ✅ Use HTTPS with valid TLS certificates
- ✅ **Never use QuickStart authentication mode**
- ✅ Configure proper authentication (Local or OIDC)
- ✅ Set strong database credentials
- ✅ Review and configure CORS policies
- ✅ Enable rate limiting
- ✅ Configure firewall rules

### Recommended Practices

- Regular security updates of dependencies
- Log monitoring and alerting
- Database encryption at rest
- Regular backups with tested recovery
- Web Application Firewall (WAF)
- Network segmentation
- Least-privilege access controls

## Security Configuration

### Authentication Modes

```bash
# Production - Use Local or OIDC
HONUA__AUTHENTICATION__MODE=Local  # or OIDC
HONUA__AUTHENTICATION__ENFORCE=true

# Development only - QuickStart
HONUA__AUTHENTICATION__MODE=QuickStart
HONUA_ALLOW_QUICKSTART=true  # Must be explicitly enabled
```

**Warning**: QuickStart mode disables authentication entirely. Production deployments that launch with QuickStart enabled will fail security validation.

### Environment Variables

Sensitive configuration should use environment variables or secure secret management:

```bash
# Database
HONUA__CONNECTIONSTRINGS__DEFAULT="Host=...;Password=..."

# OIDC
HONUA__AUTHENTICATION__OIDC__CLIENTSECRET="..."

# Storage
HONUA__ATTACHMENTS__S3__SECRETKEY="..."
HONUA__ATTACHMENTS__AZURE__CONNECTIONSTRING="..."
```

Never commit credentials to version control. Use `.gitignore` for local configuration files.

## Known Security Considerations

### QuickStart Mode
QuickStart authentication mode is a convenience feature for local development. It completely bypasses authentication and authorization. Production use is prevented by validation checks.

### File Uploads
Attachment uploads support S3, Azure Blob, and filesystem storage. Ensure:
- Appropriate file size limits are configured
- Storage permissions follow least-privilege
- Antivirus scanning is considered for untrusted uploads

### Metadata Configuration
Metadata can be loaded from YAML/JSON files. In production:
- Restrict file system access to metadata directories
- Use read-only file permissions
- Consider Redis-backed metadata for clustered deployments

### Database Access
The application requires database access for geospatial operations:
- Use dedicated service accounts with minimal permissions
- Avoid using database admin credentials
- Enable database connection encryption (SSL/TLS)

## Dependency Security

Dependencies are monitored using:
- GitHub Dependabot (enabled)
- Regular manual review of security advisories

When security updates are available:
1. Dependabot creates pull requests automatically
2. Maintainers review and test updates
3. Critical security fixes are prioritized

## Contributing Security Fixes

Security improvements are welcome. For non-sensitive security enhancements:
1. Open a regular pull request
2. Describe the security benefit in the PR description
3. Follow the project's contribution guidelines

For sensitive issues, use the private reporting methods described above.

## Security Audit History

This is an open-source project without formal security audits. Security improvements come from:
- Community contributions
- Automated dependency scanning
- Code review during development
- Reported issues from users

## Questions

For general security questions (not vulnerability reports):
- Open a GitHub Discussion
- Review existing security documentation in `docs/`

Thank you for helping keep Honua secure.
