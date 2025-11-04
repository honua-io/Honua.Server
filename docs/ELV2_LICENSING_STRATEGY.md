# HonuaIO Licensing Strategy with Elastic License 2.0

## Overview

HonuaIO uses the **Elastic License 2.0 (ELv2)**, which allows us to keep everything in a **single, source-available repository** while protecting our commercial interests and controlling feature access through licensing.

## Key Advantages of ELv2

### 1. Single Repository
- ✅ All code (Core + Enterprise) lives in `HonuaIO` repo
- ✅ No separate `HonuaIO-Enterprise` repository needed
- ✅ Simpler development, testing, and deployment
- ✅ Easier to maintain consistency across features
- ✅ One CI/CD pipeline for everything

### 2. Source-Available, Not Open Source
- ✅ Source code is publicly visible (builds trust)
- ✅ Users can evaluate all features before purchasing
- ✅ Not OSI-approved open source (maintains control)
- ❌ Cannot be offered as a hosted/managed service by third parties
- ❌ License checks cannot be bypassed or removed

### 3. Runtime License Enforcement
- ✅ License validation code is in the repo (fully visible)
- ✅ Features are gated by license tier checks at runtime
- ✅ JWT-based license keys with signed claims
- ✅ Three tiers: Free, Professional, Enterprise

## Existing Licensing Infrastructure

### Already Implemented ✅

Located in `src/Honua.Server.Core/Licensing/`:

1. **LicenseManager** - Generate, upgrade, downgrade, renew, revoke licenses
2. **LicenseValidator** - JWT validation and signature verification
3. **LicenseStore** - Persistence layer for license records
4. **LicenseModels** - Data models for licenses and features

### License Tiers Defined

**Free Tier:**
- Max 1 user
- Max 10 collections
- Basic data providers (PostgreSQL, MySQL, SQLite)
- Vector tiles enabled
- 10K API requests/day
- 5GB storage
- No advanced analytics
- No cloud integrations

**Professional Tier ($):**
- Max 10 users
- Max 100 collections
- All Free features +
- Advanced analytics
- Cloud integrations
- STAC catalog
- Raster processing
- 100K API requests/day
- 100GB storage

**Enterprise Tier ($$$):**
- Unlimited users
- Unlimited collections
- All Professional features +
- Cloud data providers (Snowflake, BigQuery, Redshift)
- Priority support
- Unlimited API requests
- Unlimited storage

## How It Works

### 1. License Key Generation

```csharp
// License manager generates JWT-based license keys
var license = await _licenseManager.GenerateLicenseAsync(new LicenseGenerationRequest
{
    CustomerId = "customer-123",
    Email = "user@example.com",
    Tier = LicenseTier.Professional,
    DurationDays = 365
});

// Returns JWT like: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### 2. Feature Gating at Runtime

```csharp
// Example feature gate for cloud batch processing
public async Task<Result> ProcessLargeDataset()
{
    var license = await _licenseValidator.ValidateAsync(_licenseKey);

    if (!license.IsValid)
    {
        throw new LicenseException("Invalid or expired license");
    }

    if (license.License.Tier < LicenseTier.Enterprise)
    {
        throw new LicenseException(
            "Cloud batch processing requires Enterprise license. " +
            "Contact sales@honuaio.com to upgrade.");
    }

    // Enterprise feature code continues...
    return await _cloudBatchProcessor.ProcessAsync();
}
```

### 3. Data Provider Gating

```csharp
// BigQuery provider checks license
public async Task<IDataReader> QueryAsync(string sql)
{
    if (!_licenseValidator.HasFeature(LicenseFeature.CloudIntegrations))
    {
        throw new LicenseException(
            "BigQuery requires Enterprise license with cloud integrations");
    }

    // BigQuery code continues...
}
```

## Project Structure (Single Repository)

```
src/
├── Honua.Server.Core/
│   ├── Licensing/                  ← License validation
│   ├── Data/
│   │   ├── Postgres/              ← Free tier
│   │   ├── MySql/                 ← Free tier
│   │   ├── Sqlite/                ← Free tier
│   │   └── SqlServer/             ← Pro tier
│   └── Features/
│       └── FeatureGates.cs        ← Runtime checks
│
├── Honua.Server.Enterprise/       ← Enterprise-only
│   ├── Data/
│   │   ├── BigQuery/              ← Enterprise tier
│   │   ├── Snowflake/             ← Enterprise tier
│   │   ├── Redshift/              ← Enterprise tier
│   │   ├── CosmosDb/              ← Enterprise tier
│   │   └── MongoDB/               ← Enterprise tier
│   └── Geoprocessing/
│       └── CloudBatchExecutor.cs  ← Enterprise tier
│
└── Honua.Server.Host/             ← ASP.NET host (all tiers)
```

**All code is in one repository, protected by ELv2**

## Distribution Model

### Docker Images (Public)
```bash
# Anyone can pull the image
docker pull ghcr.io/honuaio/honua-server:latest

# But features require valid license
docker run -e HONUA__LICENSE_KEY=<jwt-license-key> honua-server
```

### Without License Key
- Runs in evaluation mode or Free tier
- Enterprise features return license errors
- Users see clear upgrade prompts

### With License Key
- JWT validated on startup
- Features unlocked based on tier
- License expiration checked periodically

## Compliance with ELv2

### What ELv2 Protects

**From LICENSE file:**
> "You may not provide the software to third parties as a hosted or managed
> service, where the service provides users with access to any substantial
> set of the features or functionality of the software."

**This means:**
- ❌ AWS/Google/Azure cannot offer "HonuaIO as a Service"
- ❌ Users cannot bypass license checks (protected by ELv2)
- ❌ Forks cannot remove license validation code
- ✅ Users CAN self-host internally
- ✅ Users CAN modify for their own use
- ✅ Users CAN evaluate all features

**From LICENSE file:**
> "You may not move, change, disable, or circumvent the license key
> functionality in the software"

**This legally protects:**
- License validation code visibility
- Feature gate enforcement
- Commercial licensing model

## Comparison: Old Plan vs ELv2 Reality

| Aspect | Old MIT+Private Repo Plan | Current ELv2 Reality |
|--------|---------------------------|----------------------|
| **Repositories** | 2 (FOSS + Enterprise) | **1 (single repo)** |
| **Distribution** | NuGet packages | **Docker images** |
| **License** | MIT (core) + Proprietary (enterprise) | **ELv2 (everything)** |
| **Source visibility** | Core only | **Everything visible** |
| **Feature separation** | Physical (different repos) | **Logical (license gates)** |
| **CI/CD** | Two pipelines | **One pipeline** |
| **Maintenance** | Complex syncing | **Simple** |
| **Trust** | Limited (core only visible) | **High (all code visible)** |

## What Needs To Be Updated

### 1. Archive Old Strategy ✅ Recommended
Move `docs/archive/legacy/ENTERPRISE_MODULE_STRATEGY.md` to archive with note:
```
This document is obsolete. We now use Elastic License 2.0 with a single
repository and runtime license checks. See docs/ELV2_LICENSING_STRATEGY.md
```

### 2. GitHub Actions ✅ Already Good
- No changes needed
- Workflows don't assume separate repos
- Build and push Docker images from single repository

### 3. Documentation Updates
Update any references from:
- ❌ "Open source core with proprietary enterprise"
- ❌ "MIT licensed"
- ❌ "Separate enterprise repository"

To:
- ✅ "Source-available under Elastic License 2.0"
- ✅ "Single repository with runtime license enforcement"
- ✅ "All code visible, enterprise features require paid license"

## Next Steps

### Immediate (Done ✅)
- [x] LICENSE file updated to ELv2
- [x] README.md updated with licensing info
- [x] CONTRIBUTING.md clarifies no contributions policy
- [x] License headers added to all 1,565 source files

### Short Term (Recommended)
- [ ] Test license validation in development
- [ ] Add license check to server startup
- [ ] Create license key generation tooling
- [ ] Document license management for ops team
- [ ] Add license tier checks to enterprise features

### Medium Term (Future)
- [ ] Build customer license portal
- [ ] Implement automated license renewal reminders
- [ ] Add usage metrics for license compliance
- [ ] Create sales/marketing collateral explaining tiers
- [ ] Set up commercial license purchase workflow

## Customer Journey

**Evaluation → Purchase → Activation**

1. **Download** - Pull Docker image or download binaries
2. **Evaluate** - Run without license (Free tier or eval mode)
3. **Experience** - See enterprise features with upgrade prompts
4. **Purchase** - Buy Professional or Enterprise license
5. **Activate** - Apply license key to unlock features
6. **Renew** - Automatic or manual renewal before expiration

## Legal Protection

ELv2 legally prevents:
- Cloud providers offering HonuaIO as a managed service
- Removal or bypassing of license checks
- Commercial exploitation without license
- Competitive forks without license

ELv2 legally allows:
- Self-hosting for internal use
- Source code inspection and auditing
- Modifications for own purposes
- Building applications on top of HonuaIO

## Summary

**With Elastic License 2.0:**
- ✅ Simpler architecture (one repo)
- ✅ Full source visibility (builds trust)
- ✅ Runtime license enforcement (protects revenue)
- ✅ No separate enterprise packages needed
- ✅ Easier to maintain and develop
- ✅ Legal protection against cloud provider competition

**The licensing code already exists - just needs to be hooked up to enterprise features.**
