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

### Response Time Commitment

We are committed to responding to security vulnerability reports in a timely manner:

- **Initial Response**: Within 3 business days of report receipt
- **Status Updates**: At least every 7 days until resolution
- **Resolution Target**:
  - Critical vulnerabilities: 30 days or less
  - High severity: 60 days or less
  - Medium/Low severity: 90 days or less

We will acknowledge receipt and work with you to understand and address the issue. If you do not receive an acknowledgment within 3 business days, please follow up through an alternative contact method.

### Bug Bounty Program

Honua does not currently offer a bug bounty program. However, we deeply appreciate security researchers who responsibly disclose vulnerabilities and will:

- Publicly acknowledge your contribution (unless you prefer to remain anonymous)
- Credit you in the security advisory and release notes
- Provide updates on the fix and resolution timeline

We encourage responsible disclosure and will work collaboratively with security researchers to resolve issues.

### Disclosure Policy

Honua follows a coordinated disclosure process:

1. **Private Disclosure**: Reporter submits vulnerability through GitHub Security Advisories or private contact
2. **Acknowledgment**: We acknowledge receipt within 3 business days
3. **Investigation**: We investigate and validate the vulnerability
4. **Fix Development**: We develop and test a fix
5. **Coordinated Release**: We coordinate with the reporter on disclosure timing
6. **Public Disclosure**: We publish a security advisory and release the fix

**Disclosure Timeline:**
- We aim to publicly disclose vulnerabilities within 90 days of the initial report
- We will coordinate with reporters before public disclosure
- Critical vulnerabilities affecting production systems may be disclosed sooner
- We request that reporters do not publicly disclose until a fix is available

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
- Comprehensive secrets management with multiple provider support
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

### Secrets Management

Honua Server implements comprehensive secrets management with support for multiple cloud providers and local development workflows.

**Supported Providers:**
- **Azure Key Vault** - For Azure cloud deployments with Managed Identity support
- **AWS Secrets Manager** - For AWS deployments with IAM role integration
- **HashiCorp Vault** - For on-premises, Kubernetes, and multi-cloud deployments
- **Local Development** - File-based with encryption for development environments

**Configuration Example:**

```json
{
  "Secrets": {
    "Provider": "AzureKeyVault",
    "EnableCaching": true,
    "CacheDurationSeconds": 300,
    "AzureKeyVault": {
      "VaultUri": "https://your-vault.vault.azure.net/",
      "UseManagedIdentity": true
    }
  }
}
```

**Usage in Code:**

```csharp
// In Program.cs
builder.Services.AddSecretsManagement(builder.Configuration);

// In your services
public class MyService
{
    private readonly ISecretsProvider _secrets;

    public MyService(ISecretsProvider secrets)
    {
        _secrets = secrets;
    }

    public async Task DoWorkAsync()
    {
        var apiKey = await _secrets.GetSecretAsync("ApiKeys:OpenAI");
        var cert = await _secrets.GetCertificateAsync("Certificates:Signing");
    }
}
```

**Security Best Practices:**
- Use Managed Identity or IAM roles in production (avoid static credentials)
- Enable secret caching to reduce provider load
- Implement secret rotation strategies
- Never commit secrets to source control
- Use user secrets for local development
- Monitor secret access through provider audit logs
- Set appropriate permissions (least privilege)
- Enable secret versioning where supported

**Documentation:**
See [docs/security/secrets-management.md](docs/security/secrets-management.md) for comprehensive setup guides, examples, and best practices for each provider.

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

## Automated Security Scanning

Honua uses multiple automated tools to detect and prevent security issues:

### Dependabot - Dependency Updates
**Status**: Enabled (weekly scans)

Dependabot monitors and updates dependencies across the project:

- **NuGet Packages**: .NET dependencies in the main project
- **GitHub Actions**: CI/CD workflows and actions
- **Docker Images**: Container base images
- **npm Packages**: JavaScript packages (Map SDK, Power BI connector, Tableau connector)

Configuration: `.github/dependabot.yml`

**How it works**:
1. Dependabot scans dependencies weekly (Mondays 09:00 UTC)
2. Creates pull requests for new updates
3. Groups minor and patch updates to reduce noise
4. Limits to 10 open NuGet PRs, 5 for other ecosystems

**Responding to Dependabot PRs**:
1. Review the PR and changelog
2. Run tests to ensure compatibility
3. Merge after successful testing
4. Delete the branch when merged

**View Dependabot Alerts**:
- Go to: Settings → Security & analysis → Dependabot alerts
- Filter by severity, ecosystem, or status
- Dismiss alerts with justification if needed
- Receive notifications for new vulnerabilities

### CodeQL - Static Application Security Testing (SAST)
**Status**: Enabled (on push, PRs, and weekly schedule)

CodeQL performs deep semantic code analysis to detect security vulnerabilities:

- Scans C# code for SQL injection, path traversal, and other vulnerabilities
- Runs automatically on:
  - Every push to main/master/develop
  - Pull requests to main/master
  - Weekly schedule (Mondays 06:00 UTC)
- Configuration: `.github/codeql/codeql-config.yml`
- Workflow: `.github/workflows/codeql.yml`

**Security Rules**:
- Includes security-extended query pack
- Includes security-and-quality query pack
- Excludes unmanaged code calls (intentional exemption)

**Reviewing CodeQL Results**:
- Go to: Security → Code scanning alerts
- Review alerts by severity and type
- Check SARIF file for detailed information
- Dismiss false positives with documentation

### Dependency Review - Pull Request Vulnerability Check
**Status**: Enabled (on pull requests)

Dependency Review prevents vulnerable dependencies from being merged:

- Blocks pull requests with critical vulnerabilities
- Warns about license compliance issues
- Provides vulnerability details in PR comments
- Configuration: `.github/workflows/dependency-review.yml`

**How it works**:
1. Analyzes dependency changes in PRs
2. Checks against known vulnerability databases
3. Comments with vulnerability severity and guidance
4. Fails check for critical vulnerabilities

### SBOM Generation - Software Bill of Materials
**Status**: Enabled (on release and manual trigger)

SBOM provides complete transparency about all components:

- Generates in multiple formats:
  - CycloneDX (JSON) - Application dependencies
  - SPDX (JSON) - Standard format for container/application
  - Syft JSON - Detailed container inventory
- Includes both application and container SBOMs
- Signed with Cosign for integrity verification
- Attached to releases and container images

**Using SBOMs**:
1. Download from: Releases → Assets
2. Use for compliance (SSRF, EO 14028)
3. Analyze with tools like Dependabot, Grype, or Syft
4. Integrate with your supply chain security (SLSA)

## Dependency Security

Dependencies are monitored using:
- GitHub Dependabot (enabled, weekly scans)
- CodeQL static analysis (enabled, continuous)
- Dependency Review on PRs (enabled)
- SBOM generation for releases (enabled)
- Regular manual review of security advisories

When security updates are available:
1. Dependabot creates pull requests automatically
2. Dependency Review blocks vulnerable PRs
3. Maintainers review and test updates
4. Critical security fixes are prioritized
5. Security advisories are published when resolved

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
