# GitHub Actions Workflows

This directory contains CI/CD workflows for Honua.

## Workflow Overview

| Workflow | Purpose | Triggers | Duration |
|----------|---------|----------|----------|
| **integration-tests.yml** | Cloud storage integration tests | Push/PR to `master`/`main`/`dev`, Manual | ~20-30 min |
| **ci.yml** | Main CI pipeline | Push/PR | ~15-25 min |
| **nightly-tests.yml** | Extended test suite | Scheduled (nightly) | ~45-60 min |
| **docker-tests.yml** | Container builds | Push/PR | ~10-15 min |
| **ogc-conformance-*.yml** | OGC API compliance | Nightly, Manual | ~20-30 min |
| **performance-monitoring.yml** | Performance tracking | Push to main | ~15-20 min |
| **codeql.yml** | Security analysis | Push/PR, Weekly | ~10-15 min |
| **secrets-scanning.yml** | Secret detection | Push/PR | ~2-5 min |
| **mutation-testing.yml** | Mutation testing | Manual | ~30-40 min |
| **sonarcloud.yml** | Code quality | Push/PR | ~10-15 min |
| **sbom.yml** | SBOM generation | Push to main | ~5-10 min |
| **dependency-review.yml** | Dependency analysis | PR | ~3-5 min |

## Integration Tests Workflow

The `integration-tests.yml` workflow is specifically designed for testing cloud storage providers with emulators.

### Quick Start

**Trigger manually:**
1. Go to Actions tab → Integration Tests with Cloud Storage Emulators
2. Click "Run workflow"
3. Select branch and optional parameters
4. Click "Run workflow"

**Automatic triggers:**
- Push to `master`, `main`, or `dev` branches
- Pull requests targeting `master`, `main`, or `dev`

### Workflow Structure

```
integration-tests.yml
├── unit-tests (Job 1)
│   ├── Setup .NET 9.0
│   ├── Install GDAL
│   ├── Cache NuGet packages
│   ├── Build solution
│   ├── Run unit tests (exclude IntegrationTests)
│   └── Upload results & coverage
│
├── integration-tests (Job 2)
│   ├── Setup .NET 9.0
│   ├── Install GDAL
│   ├── Cache NuGet packages
│   ├── Start emulators
│   │   ├── LocalStack (S3) - Port 4566
│   │   ├── Azurite (Azure) - Port 10000
│   │   └── GCS Emulator - Port 4443
│   ├── Health checks (120s timeout)
│   ├── Build solution
│   ├── Run integration tests (filter: IntegrationTests)
│   ├── Upload results & coverage
│   └── Cleanup (always runs)
│
└── test-summary (Job 3)
    ├── Download all test results
    ├── Generate combined report
    ├── Calculate coverage summary
    └── Post PR comment (if applicable)
```

### Key Features

- **Separate Jobs**: Unit and integration tests run independently
- **Health Checks**: Automatic verification that emulators are ready
- **Timeout Protection**: 30-minute job timeout, 120s health check timeout
- **Always Cleanup**: Emulators stopped even on failure
- **Artifact Upload**: Test results retained for 7 days
- **Coverage Reporting**: Integrated with Codecov
- **PR Comments**: Automatic coverage report on PRs

### Test Filters

```bash
# Unit tests (exclude integration tests)
--filter "FullyQualifiedName!~IntegrationTests"

# Integration tests only
--filter "FullyQualifiedName~IntegrationTests"
```

## Development Workflow

### Local Testing

Before pushing changes, test locally:

```bash
# 1. Start emulators
cd tests/Honua.Server.Core.Tests
docker-compose -f docker-compose.storage-emulators.yml up -d

# 2. Wait for health
../../scripts/wait-for-emulators.sh

# 3. Run tests
dotnet test

# 4. Stop emulators
docker-compose -f docker-compose.storage-emulators.yml down -v
```

### Adding New Tests

1. **Unit tests**: Automatically run if they don't match `*IntegrationTests` pattern
2. **Integration tests**: Mark with `[Collection("StorageEmulators")]` and name with `IntegrationTests` suffix

### Debugging Failures

1. **Check workflow logs**: Actions tab → Failed workflow → View logs
2. **Review emulator logs**: Automatically uploaded on failure
3. **Run locally**: Reproduce with same Docker images
4. **Health checks**: Use `./scripts/wait-for-emulators.sh` to verify setup

## Emulator Configuration

### Endpoints

| Service | URL | Health Check |
|---------|-----|--------------|
| LocalStack (S3) | http://localhost:4566 | `/_localstack/health` |
| Azurite (Azure) | http://localhost:10000 | `/devstoreaccount1?comp=list` |
| GCS Emulator | http://localhost:4443 | `/storage/v1/b` |

### Docker Compose

File: `tests/Honua.Server.Core.Tests/docker-compose.storage-emulators.yml`

Start: `docker-compose -f docker-compose.storage-emulators.yml up -d`
Stop: `docker-compose -f docker-compose.storage-emulators.yml down -v`

## Best Practices

### For Contributors

- ✓ Run unit tests locally before pushing
- ✓ Run integration tests if touching storage code
- ✓ Keep tests fast and focused
- ✓ Clean up resources in tests
- ✓ Use meaningful test names

### For Maintainers

- ✓ Monitor workflow execution times
- ✓ Update emulator versions regularly
- ✓ Review and optimize caching strategy
- ✓ Keep timeout values reasonable
- ✓ Ensure cleanup always runs
- ✓ Update documentation with workflow changes

## Troubleshooting

### Common Issues

**Emulator health check timeout:**
- Check Docker daemon is running
- Verify port availability
- Review emulator logs in workflow

**Tests skipped:**
- Emulators not healthy
- Test filter incorrect
- Missing `[Collection("StorageEmulators")]` attribute

**Out of disk space:**
- Old Docker volumes not cleaned up
- Increase retention policies
- Review artifact sizes

### Getting Help

1. Check [CI/CD Documentation](../../docs/CI_CD.md)
2. Check [Testing Guide](../../docs/TESTING.md)
3. Review workflow logs
4. Open issue with workflow run link

## Related Documentation

- **[CI/CD Documentation](../../docs/CI_CD.md)** - Comprehensive CI/CD guide
- **[Testing Guide](../../docs/TESTING.md)** - How to write and run tests
- **[Storage Integration Tests](../../tests/Honua.Server.Core.Tests/STORAGE_INTEGRATION_TESTS.md)** - Cloud storage testing details

## Maintenance Schedule

- **Weekly**: Review failed workflows
- **Monthly**: Update action versions
- **Quarterly**: Review and optimize workflow performance
- **As needed**: Update emulator versions
- **As needed**: Adjust timeout values

## Contact

For workflow issues or questions:
- Open an issue with the `ci/cd` label
- Include workflow run link
- Provide relevant logs
