# Sample Trivy Scan Output

This document shows example output from Trivy vulnerability scans of the Honua production Docker image.

## Image Information

- **Image**: `honua:latest`
- **Base Image**: `mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled`
- **OS**: Debian 12 (Bookworm) - Chiseled variant
- **Scan Date**: 2025-10-18
- **Trivy Version**: 0.48.0

## Executive Summary

```
Total Vulnerabilities: 12
├── CRITICAL: 0
├── HIGH: 2
├── MEDIUM: 5
└── LOW: 5

Total Misconfigurations: 3
├── CRITICAL: 0
├── HIGH: 1
├── MEDIUM: 2
└── LOW: 0
```

## Detailed Scan Results

### Table Format Output

```
honua:latest (debian 12.8)

Total: 12 (UNKNOWN: 0, LOW: 5, MEDIUM: 5, HIGH: 2, CRITICAL: 0)

┌────────────────────┬────────────────────┬──────────┬────────┬───────────────────┬───────────────────┬──────────────────────────────────────┐
│      Library       │   Vulnerability    │ Severity │ Status │  Installed Ver.   │    Fixed Ver.     │                Title                 │
├────────────────────┼────────────────────┼──────────┼────────┼───────────────────┼───────────────────┼──────────────────────────────────────┤
│ libssl3            │ CVE-2024-5535      │ HIGH     │ fixed  │ 3.1.4-2           │ 3.1.5-1           │ OpenSSL: SSL_select_next_proto       │
│                    │                    │          │        │                   │                   │ buffer overread                      │
├────────────────────┼────────────────────┼──────────┼────────┼───────────────────┼───────────────────┼──────────────────────────────────────┤
│ libcrypto3         │ CVE-2024-5535      │ HIGH     │ fixed  │ 3.1.4-2           │ 3.1.5-1           │ OpenSSL: SSL_select_next_proto       │
│                    │                    │          │        │                   │                   │ buffer overread                      │
├────────────────────┼────────────────────┼──────────┼────────┼───────────────────┼───────────────────┼──────────────────────────────────────┤
│ libc6              │ CVE-2024-2961      │ MEDIUM   │ fixed  │ 2.37-12           │ 2.37-13           │ glibc: Out of bounds write in        │
│                    │                    │          │        │                   │                   │ iconv ISO-2022-CN-EXT                │
├────────────────────┼────────────────────┼──────────┼────────┼───────────────────┼───────────────────┼──────────────────────────────────────┤
│ libgcc-s1          │ CVE-2023-4039      │ MEDIUM   │ fixed  │ 12.3.0-1          │ 12.3.0-2          │ gcc: -fstack-protector fails to      │
│                    │                    │          │        │                   │                   │ guard dynamic stack allocations      │
├────────────────────┼────────────────────┼──────────┼────────┼───────────────────┼───────────────────┼──────────────────────────────────────┤
│ libstdc++6         │ CVE-2023-4039      │ MEDIUM   │ fixed  │ 12.3.0-1          │ 12.3.0-2          │ gcc: -fstack-protector fails to      │
│                    │                    │          │        │                   │                   │ guard dynamic stack allocations      │
├────────────────────┼────────────────────┼──────────┼────────┼───────────────────┼───────────────────┼──────────────────────────────────────┤
│ zlib1g             │ CVE-2023-45853     │ MEDIUM   │ fixed  │ 1:1.2.13.dfsg-1   │ 1:1.2.13.dfsg-2   │ zlib: Integer overflow and           │
│                    │                    │          │        │                   │                   │ resultant heap-based buffer overflow │
├────────────────────┼────────────────────┼──────────┼────────┼───────────────────┼───────────────────┼──────────────────────────────────────┤
│ libgnutls30        │ CVE-2024-28834     │ MEDIUM   │ fixed  │ 3.8.0-1           │ 3.8.1-1           │ gnutls: vulnerable to                │
│                    │                    │          │        │                   │                   │ Minerva side-channel information leak│
├────────────────────┼────────────────────┼──────────┼────────┼───────────────────┼───────────────────┼──────────────────────────────────────┤
│ libsystemd0        │ CVE-2023-50387     │ LOW      │ fixed  │ 252.12-1          │ 252.13-1          │ bind9: KeyTrap - Extreme CPU         │
│                    │                    │          │        │                   │                   │ consumption in DNSSEC validator      │
├────────────────────┼────────────────────┼──────────┼────────┼───────────────────┼───────────────────┼──────────────────────────────────────┤
│ libexpat1          │ CVE-2024-45490     │ LOW      │ fixed  │ 2.5.0-1           │ 2.5.0-2           │ libexpat: Negative Length Parsing    │
│                    │                    │          │        │                   │                   │ Vulnerability in libexpat            │
├────────────────────┼────────────────────┼──────────┼────────┼───────────────────┼───────────────────┼──────────────────────────────────────┤
│ libxml2            │ CVE-2024-40896     │ LOW      │ fixed  │ 2.11.4-1          │ 2.11.5-1          │ libxml2: Use-after-free when         │
│                    │                    │          │        │                   │                   │ handling DTD parsing errors          │
├────────────────────┼────────────────────┼──────────┼────────┼───────────────────┼───────────────────┼──────────────────────────────────────┤
│ libtasn1-6         │ CVE-2024-28835     │ LOW      │ fixed  │ 4.19.0-1          │ 4.19.0-2          │ libtasn1: Memory corruption          │
│                    │                    │          │        │                   │                   │ in asn1_get_length_ber               │
├────────────────────┼────────────────────┼──────────┼────────┼───────────────────┼───────────────────┼──────────────────────────────────────┤
│ libpcre2-8-0       │ CVE-2024-45231     │ LOW      │ fixed  │ 10.42-1           │ 10.42-2           │ pcre2: Integer overflow in           │
│                    │                    │          │        │                   │                   │ substitute function                  │
└────────────────────┴────────────────────┴──────────┴────────┴───────────────────┴───────────────────┴──────────────────────────────────────┘
```

### Application Dependencies

```
.NET Application Dependencies (NuGet packages)

Total: 0 (UNKNOWN: 0, LOW: 0, MEDIUM: 0, HIGH: 0, CRITICAL: 0)

No vulnerabilities found in .NET dependencies.

Note: The application uses .NET 9.0 LTS runtime which is regularly updated through base image updates.
Dependency scanning for .NET packages is handled by:
  - NuGet vulnerability scanning in CI (dotnet list package --vulnerable)
  - Dependabot alerts
  - GitHub Security Advisory Database
```

## Configuration Scan Results

```
Dockerfile Scan Results

Tests: 23 (SUCCESSES: 20, FAILURES: 3, EXCEPTIONS: 0)
Failures: 3 (UNKNOWN: 0, LOW: 0, MEDIUM: 2, HIGH: 1, CRITICAL: 0)

HIGH: Ensure that HEALTHCHECK instructions have been added to container images
════════════════════════════════════════════════════════════════════════════════════════
HEALTHCHECK is present but using wget from busybox. Consider using native health checks.

Type: Dockerfile Security Check
ID: DS026
Message: HEALTHCHECK instruction should use minimal tooling
Resolution: Use ASP.NET Core health checks endpoint directly if possible

   9 │
  10 │ FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled AS runtime
  11 │ WORKDIR /app
  12 │
  13 │ ENV ASPNETCORE_URLS=http://+:8080
  14 │
  15 │ # Copy curl from busybox for health checks (chiseled images don't include it)
  16 │ COPY --from=busybox:stable-glibc /bin/wget /bin/wget
     │ ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  17 │
  18 │ COPY --from=build --chown=app:app /app/publish ./
────────────────────────────────────────────────────────────────────────────────────────


MEDIUM: Ensure update instructions are not used alone in the Dockerfile
════════════════════════════════════════════════════════════════════════════════════════
Not applicable - chiseled image doesn't use apt-get update

Type: Dockerfile Security Check
ID: DS002
Status: PASSED (chiseled images don't have package managers)


MEDIUM: Only COPY necessary files to the container
════════════════════════════════════════════════════════════════════════════════════════
The Dockerfile copies the entire build context initially. Consider using .dockerignore.

Type: Dockerfile Security Check
ID: DS014
Message: Ensure .dockerignore file exists and is properly configured
Resolution: Review .dockerignore to exclude test files, documentation, and build artifacts

  11 │ COPY NuGet.Config ./
  12 │ COPY src/Honua.Server.Core/Honua.Server.Core.csproj src/Honua.Server.Core/
  13 │ COPY src/Honua.Server.Host/Honua.Server.Host.csproj src/Honua.Server.Host/
  14 │ RUN dotnet restore src/Honua.Server.Host/Honua.Server.Host.csproj
  15 │
  16 │ COPY . ./
     │ ^^^^^^^^^^
────────────────────────────────────────────────────────────────────────────────────────
```

## Vulnerability Details

### CVE-2024-5535 (OpenSSL - HIGH)

**Package**: libssl3, libcrypto3
**Severity**: HIGH (CVSS 7.5)
**Status**: Fix Available
**Installed**: 3.1.4-2
**Fixed In**: 3.1.5-1

**Description**:
Issue summary: Calling the OpenSSL API function SSL_select_next_proto with an
empty supported client protocols buffer may cause a crash or memory contents to
be sent to the peer.

**Impact Analysis**:
- **Honua Usage**: LOW - Honua uses HTTPS via Kestrel/ASP.NET Core, not direct OpenSSL API calls
- **Attack Vector**: Network (requires specific TLS handshake scenario)
- **Exploitability**: LOW - requires specific application behavior
- **Risk**: MEDIUM - Update recommended in next patch release

**Remediation**:
Update base image to version with OpenSSL 3.1.5 or later:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled@sha256:<new-digest>
```

**References**:
- https://nvd.nist.gov/vuln/detail/CVE-2024-5535
- https://www.openssl.org/news/secadv/20240627.txt

---

### CVE-2024-2961 (glibc - MEDIUM)

**Package**: libc6
**Severity**: MEDIUM (CVSS 6.5)
**Status**: Fix Available
**Installed**: 2.37-12
**Fixed In**: 2.37-13

**Description**:
Out-of-bounds write in the iconv conversion from ISO-2022-CN-EXT.

**Impact Analysis**:
- **Honua Usage**: NONE - Application doesn't use ISO-2022-CN-EXT encoding
- **Attack Vector**: Requires processing untrusted ISO-2022-CN-EXT encoded data
- **Exploitability**: LOW - specific encoding required
- **Risk**: LOW - Safe to defer to next maintenance window

**Remediation**:
Update in next scheduled maintenance window.

**References**:
- https://nvd.nist.gov/vuln/detail/CVE-2024-2961
- https://sourceware.org/bugzilla/show_bug.cgi?id=30843

## Summary Statistics

### Vulnerability Breakdown by Package Type

| Package Type        | Total | Critical | High | Medium | Low |
|---------------------|-------|----------|------|--------|-----|
| OS Packages         | 12    | 0        | 2    | 5      | 5   |
| .NET Runtime        | 0     | 0        | 0    | 0      | 0   |
| Application Deps    | 0     | 0        | 0    | 0      | 0   |
| **Total**           | **12**| **0**    | **2**| **5**  | **5**|

### Remediation Status

| Status              | Count | Percentage |
|---------------------|-------|------------|
| Fix Available       | 12    | 100%       |
| Will Not Fix        | 0     | 0%         |
| No Fix Available    | 0     | 0%         |

### Configuration Issues

| Severity | Count | Status |
|----------|-------|--------|
| HIGH     | 1     | Review Required |
| MEDIUM   | 2     | Acceptable Risk |
| LOW      | 0     | - |

## Recommendations

### Immediate Actions (0-24 hours)

None required. No CRITICAL vulnerabilities detected.

### Short-term Actions (1-3 days)

1. **Update base image** to include OpenSSL 3.1.5+ (CVE-2024-5535)
   - Update Dockerfile base image tag
   - Rebuild and test image
   - Deploy to staging environment
   - Verify functionality
   - Deploy to production

2. **Review HEALTHCHECK implementation**
   - Consider using native .NET health checks instead of wget
   - Evaluate if health check probe is necessary given K8s liveness probes

### Medium-term Actions (1-2 weeks)

1. **Update glibc** (CVE-2024-2961) in next base image update
2. **Review .dockerignore** to minimize copied files
3. **Implement automated base image update checks**
   - GitHub Actions workflow to check for new base image versions
   - Automated PR creation for base image updates

### Long-term Improvements

1. **Reduce attack surface**
   - Continue using chiseled images (already implemented)
   - Minimize installed packages
   - Consider distroless images for even smaller footprint

2. **Automate remediation**
   - Dependabot for Dockerfile base image updates
   - Automated vulnerability triage workflow
   - Integration with incident response system

3. **Security metrics dashboard**
   - Track vulnerability trends over time
   - Monitor mean time to remediate (MTTR)
   - Measure false positive rate

## Scan Metadata

```json
{
  "SchemaVersion": 2,
  "ArtifactName": "honua:latest",
  "ArtifactType": "container_image",
  "Metadata": {
    "OS": {
      "Family": "debian",
      "Name": "12.8"
    },
    "ImageID": "sha256:abc123def456...",
    "DiffIDs": [
      "sha256:layer1...",
      "sha256:layer2...",
      "sha256:layer3..."
    ],
    "RepoTags": [
      "honua:latest"
    ],
    "RepoDigests": [],
    "ImageConfig": {
      "architecture": "amd64",
      "os": "linux"
    }
  },
  "Results": [
    {
      "Target": "honua:latest (debian 12.8)",
      "Class": "os-pkgs",
      "Type": "debian",
      "Vulnerabilities": [
        "... 12 vulnerabilities ..."
      ]
    }
  ]
}
```

## Comparison with Previous Scan

| Metric                    | Previous (2025-10-11) | Current (2025-10-18) | Change |
|---------------------------|-----------------------|----------------------|--------|
| Total Vulnerabilities     | 15                    | 12                   | -3 ↓   |
| Critical                  | 0                     | 0                    | -      |
| High                      | 3                     | 2                    | -1 ↓   |
| Medium                    | 6                     | 5                    | -1 ↓   |
| Low                       | 6                     | 5                    | -1 ↓   |
| Config Issues             | 3                     | 3                    | -      |

**Trend**: ✅ Improving - 3 vulnerabilities remediated through base image updates

## Conclusion

The Honua production Docker image has a **GOOD** security posture:

✅ **Strengths**:
- No CRITICAL vulnerabilities
- All vulnerabilities have fixes available
- Uses minimal, hardened base image (debian-chiseled)
- Runs as non-root user
- No application dependency vulnerabilities
- Clean .NET 9.0 runtime

⚠️ **Areas for Improvement**:
- Update base image to remediate 2 HIGH severity OpenSSL vulnerabilities
- Review HEALTHCHECK implementation
- Enhance .dockerignore configuration

**Overall Risk Rating**: **LOW-MEDIUM**

The current vulnerabilities are primarily in base OS packages and have fixes available.
None of the vulnerabilities are in direct application code or critical paths.
Recommend updating base image within the next 3 days to address HIGH severity issues.

---

**Scan Performed**: 2025-10-18 14:32:15 UTC
**Trivy Version**: 0.48.0
**Database Version**: 2025-10-18 12:00:00 UTC
**Scan Duration**: 34 seconds
