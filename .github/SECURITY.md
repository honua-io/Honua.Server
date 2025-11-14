# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in Honua, please report it responsibly.

**Please do NOT report security vulnerabilities through public GitHub issues.**

### Reporting Methods

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

## Security Update Policy

When security vulnerabilities are identified:

1. **Critical/High Severity**: Immediate patch release with security advisory
2. **Medium Severity**: Patch within next planned release or emergency release if warranted
3. **Low Severity**: Addressed in regular release cycle

Security updates are announced through:
- GitHub Security Advisories
- Release notes
- Commit messages tagged with [SECURITY]

## Contact Information

For security-related questions (not vulnerability reports):
- Open a GitHub Discussion
- Review existing security documentation in the repository

For vulnerability reports, use the methods listed above.

## Automated Security

Honua uses automated security scanning:
- **Dependabot**: Weekly dependency vulnerability scans
- **CodeQL**: Static application security testing (SAST)
- **Dependency Review**: Pull request vulnerability checks
- **SBOM Generation**: Software Bill of Materials for releases

See the full [SECURITY.md](../SECURITY.md) in the root directory for comprehensive security documentation including:
- Security features and architecture
- Production deployment security requirements
- Authentication and authorization details
- Network security configuration
- Secrets management
- Incident response procedures

Thank you for helping keep Honua secure.
