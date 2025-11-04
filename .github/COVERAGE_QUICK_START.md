# Code Coverage Quick Start

This is a quick reference for working with code coverage in Honua.

## TL;DR

```bash
# Check coverage (runs tests + generates report)
./scripts/check-coverage.sh

# View HTML report
open ./CoverageReport/index.html
```

## Coverage Thresholds

| Project | Threshold |
|---------|-----------|
| Honua.Server.Core | 65% |
| Honua.Server.Host | 60% |
| Honua.Cli.AI | 55% |
| Honua.Cli | 50% |
| Overall | 60% |

## Common Commands

### Run Tests with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

### Generate Report

```bash
reportgenerator \
  -reports:"./TestResults/**/coverage.opencover.xml" \
  -targetdir:"./CoverageReport" \
  -reporttypes:"Html;JsonSummary"
```

### Check Thresholds Only

```bash
./scripts/check-coverage.sh --threshold-only
```

## CI/CD Integration

Coverage is automatically checked on every PR:

- ✅ Tests run with coverage collection
- ✅ Thresholds enforced per project
- ✅ HTML report uploaded as artifact
- ✅ Coverage summary posted to PR
- ✅ Codecov badge updated

## What Gets Excluded?

- Test projects (`*.Tests`)
- Benchmarks
- Migrations
- DTOs and generated code
- GlobalUsings files

## Troubleshooting

### Coverage too low?

1. Run `./scripts/check-coverage.sh`
2. Open `./CoverageReport/index.html`
3. Look for red (uncovered) lines
4. Add tests for those areas

### CI failing but local passing?

```bash
# Clean and re-run
rm -rf TestResults CoverageReport
./scripts/check-coverage.sh
```

### ReportGenerator not found?

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

## More Information

See [docs/CODE_COVERAGE.md](../docs/CODE_COVERAGE.md) for complete documentation.
