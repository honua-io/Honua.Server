# TODO Management Policy

**Version:** 1.0
**Effective Date:** 2025-11-06
**Owned By:** Engineering Team
**Approved By:** [To be filled after review]

---

## Table of Contents

1. [Purpose](#purpose)
2. [Scope](#scope)
3. [TODO vs. GitHub Issue Decision Tree](#todo-vs-github-issue-decision-tree)
4. [TODO Format Standard](#todo-format-standard)
5. [TODO Lifecycle](#todo-lifecycle)
6. [Priority Levels](#priority-levels)
7. [Code Review Requirements](#code-review-requirements)
8. [Automation & Enforcement](#automation--enforcement)
9. [Metrics & Reporting](#metrics--reporting)
10. [Examples](#examples)
11. [Exceptions & Escalations](#exceptions--escalations)

---

## Purpose

This policy establishes standards for using TODO comments in the Honua.Server codebase to ensure:

1. **Accountability** - Every TODO is tracked and assigned
2. **Visibility** - TODOs don't become forgotten technical debt
3. **Quality** - Code quality isn't compromised by temporary solutions
4. **Security** - Security gaps are never left as TODOs

**Goal:** Reduce average TODO lifetime to < 30 days and maintain < 20 active TODOs.

---

## Scope

This policy applies to:

- All `.cs` (C#) source files in the Honua.Server repository
- All code merged to `main`, `develop`, or release branches
- All pull requests submitted for review

This policy does NOT apply to:

- Documentation files (`.md`)
- Configuration files (`.json`, `.yml`)
- Third-party libraries or vendored code
- Prototype/spike branches (that will be deleted)

---

## TODO vs. GitHub Issue Decision Tree

### Use a TODO Comment When:

✅ **Implementation is straightforward** (< 2 hours of work)
```csharp
// TODO(#456): Add input validation for negative coordinates
// Context: Waiting for GeometryValidator PR #455 to merge
if (coordinates.Any(c => c.X < 0 || c.Y < 0))
{
    throw new ArgumentException("Negative coordinates not supported");
}
```

✅ **Waiting for a dependency** (library update, external API, another PR)
```csharp
// TODO(#789): Switch to new API when v2.0 is released
// Context: v1.0 API is deprecated but v2.0 not yet GA
await _apiClient.LegacyMethod();
```

✅ **Minor refactoring** (code cleanup, variable renaming)
```csharp
// TODO(#234): Extract this logic to a helper method
// Context: Duplicated in 3 places, needs DRY refactoring
var result = ComplexCalculation(a, b, c);
```

✅ **Adding error handling to existing code**
```csharp
// TODO(#567): Add retry logic for transient failures
// Context: Issue #567 tracks implementing Polly retry policy
await _httpClient.GetAsync(url);
```

### Create a GitHub Issue Immediately When:

❌ **Security vulnerability or authorization gap**
```csharp
// ❌ WRONG - Never use TODO for security
// TODO: Add authorization check

// ✅ CORRECT - Create issue immediately, reference in comment
// See issue #123: Missing authorization check (P0 - Critical)
// Implementation blocked until AuthService PR #120 merges
if (!await _authService.IsAuthorizedAsync(user, resource, "admin"))
{
    return Forbid();
}
```

❌ **New feature requiring multiple files/days of work**
```csharp
// ❌ WRONG
// TODO: Implement alert publishing logic

// ✅ CORRECT - Create detailed issue with acceptance criteria
// Feature implementation: Alert Publishing (Issue #456)
// See .github/ISSUE_TEMPLATE/todo-005-alert-publishing.md for spec
throw new NotImplementedException("Alert publishing - see issue #456");
```

❌ **Breaking change or API redesign**
```csharp
// ❌ WRONG
// TODO: Change this to async

// ✅ CORRECT - Create issue with migration plan
// Breaking change: Convert synchronous API to async (Issue #789)
// Migration guide: docs/migrations/sync-to-async.md
public SyncResult Process() { ... }
```

❌ **Blocked by external dependency or design decision**
```csharp
// ❌ WRONG
// TODO: Figure out how multi-tenancy should work

// ✅ CORRECT - Create issue with design discussion
// Design decision needed: Multi-tenancy architecture (Issue #234)
// Options analysis: docs/architecture/multi-tenancy-options.md
var tenantId = "default"; // Placeholder until architecture decided
```

❌ **Requires stakeholder input or product decision**
```csharp
// ❌ WRONG
// TODO: Should we support CSV export?

// ✅ CORRECT - Create feature request issue
// Feature request: CSV export support (Issue #567)
// Product decision needed - assigned to PM
// Current: JSON-only export
```

---

## TODO Format Standard

### Required Format

All TODOs MUST follow this format:

```csharp
// TODO(#issue-number): Brief description (< 80 characters)
// Context: Why this is needed and what's blocking it
// [Optional] Example: How it should work when implemented
```

### Format Rules

1. **Issue Number Required:** `TODO(#123)` - Must reference a GitHub issue
2. **Brief Description:** One-line summary (max 80 characters)
3. **Context Line:** Explain why TODO exists and what's blocking implementation
4. **Optional Example:** Show intended implementation (for complex cases)

### Examples

#### ✅ Good TODO - Simple Case
```csharp
// TODO(#456): Add null check for optional parameter
// Context: Edge case discovered in issue #455 testing
if (optionalParam == null)
{
    optionalParam = GetDefaultValue();
}
```

#### ✅ Good TODO - Complex Case
```csharp
// TODO(#789): Implement connection pooling for MongoDB
// Context: Performance optimization - blocked until load testing complete
// Example: Use MongoClient with connection pool settings:
//   var settings = MongoClientSettings.FromConnectionString(connStr);
//   settings.MaxConnectionPoolSize = 100;
//   var client = new MongoClient(settings);
await _mongoDatabase.GetCollection<T>(collectionName).Find(filter).ToListAsync();
```

#### ❌ Bad TODO - Missing Issue Number
```csharp
// TODO: Fix this later
// ❌ VIOLATION: No issue number
// ❌ VIOLATION: Vague description
// ❌ VIOLATION: No context
```

#### ❌ Bad TODO - Security Gap
```csharp
// TODO(#123): Add authorization
// ❌ VIOLATION: Security TODOs not allowed - must be fixed before merge
```

#### ❌ Bad TODO - Large Feature
```csharp
// TODO(#456): Implement entire alert system with Slack, email, PagerDuty
// ❌ VIOLATION: Too large for TODO - should be tracked as feature issue
// ❌ Use NotImplementedException with issue reference instead
```

---

## TODO Lifecycle

### 1. Creation

**When creating a TODO:**

1. **Create GitHub issue first** (or reference existing issue)
2. **Add TODO comment** using standard format
3. **Add to TODO_TRACKING.md** (if P0 or P1)
4. **Assign issue** to team member
5. **Add to sprint backlog** (if P0 or P1)

### 2. Review

**During code review:**

1. **Reviewer checks** TODO format compliance
2. **Verify GitHub issue** exists and has context
3. **Assess priority** - Is this truly a TODO or should it be implemented now?
4. **Security check** - No security TODOs allowed
5. **Approve or request changes**

### 3. Resolution

**When implementing a TODO:**

1. **Implement the fix** as described in GitHub issue
2. **Remove TODO comment** completely
3. **Close GitHub issue** with PR reference
4. **Update TODO_TRACKING.md** (mark as Fixed)
5. **Add test coverage** for the fix

### 4. Expiration

**TODOs older than 30 days:**

1. **Automated report** generated weekly
2. **Team reviews** stale TODOs in sprint planning
3. **Decision:**
   - **Implement now** - Assign to current sprint
   - **Defer** - Update issue with new timeline
   - **Remove** - No longer needed, close issue

---

## Priority Levels

### P0 - Critical (Fix in Sprint 1)

**Criteria:**
- Security vulnerability
- Authorization gap
- Data corruption risk
- Production blocker

**Action:**
- ❌ **NOT ALLOWED as TODO** - Must be fixed before merge
- ✅ Create issue, implement immediately
- ✅ If blocked, add mitigation and mark as high-risk

**Example:**
```csharp
// ❌ NOT ALLOWED
// TODO(#123): Add authentication check

// ✅ CORRECT - Implement before merge or add mitigation
if (!User.Identity.IsAuthenticated)
{
    return Unauthorized();
}
```

### P1 - High (Fix in Sprint 2)

**Criteria:**
- Core feature incomplete
- User-facing bug
- Performance issue
- Compliance requirement

**Action:**
- ⚠️ **Allowed as TODO** - If blocked by dependency
- ✅ Must have detailed GitHub issue
- ✅ Assigned to next sprint
- ✅ Tracked in TODO_TRACKING.md

**Example:**
```csharp
// TODO(#456): Implement connection test for MongoDB
// Context: Waiting for MongoDB driver upgrade to v2.20 (PR #455)
// Example: await _mongoClient.Cluster.Description.Servers.FirstOrDefault()?.IsConnected
return new ConnectionTestResult { Success = false, Message = "Not implemented yet" };
```

### P2 - Medium (Fix in Month 2)

**Criteria:**
- Enhancement
- Optimization
- Configuration improvement
- Code quality improvement

**Action:**
- ✅ **Allowed as TODO**
- ✅ Should have GitHub issue
- ✅ Reviewed monthly
- ✅ Prioritized by product value

**Example:**
```csharp
// TODO(#789): Make batch size configurable
// Context: Currently hardcoded, should be in appsettings.json
const int batchSize = 1000;
```

### P3 - Low (Backlog)

**Criteria:**
- Nice-to-have
- Minor refactoring
- Code style improvement
- Future consideration

**Action:**
- ✅ **Allowed as TODO**
- ⚠️ May or may not have GitHub issue
- ✅ Reviewed quarterly
- ⚠️ May be closed as "won't fix"

**Example:**
```csharp
// TODO(#234): Consider using LINQ query syntax for readability
// Context: Fluent syntax works fine, query syntax might be clearer
var results = items.Where(i => i.IsActive).Select(i => i.Name).ToList();
```

---

## Code Review Requirements

### Reviewer Checklist for TODOs

**Before approving PR with TODOs:**

- [ ] ✅ **Format compliance**
  - All TODOs have issue numbers: `TODO(#123)`
  - All TODOs have context line
  - Description is clear and concise (< 80 chars)

- [ ] ✅ **GitHub issue exists**
  - Issue number is valid and exists
  - Issue has description, acceptance criteria
  - Issue is assigned or in backlog

- [ ] ✅ **Priority assessment**
  - P0 (Critical): ❌ **REJECT PR** - Must be fixed
  - P1 (High): ⚠️ Verify issue is assigned to next sprint
  - P2 (Medium): ✅ Acceptable if tracked
  - P3 (Low): ✅ Acceptable

- [ ] ✅ **Security check**
  - No TODOs bypassing authentication
  - No TODOs bypassing authorization
  - No TODOs bypassing validation
  - No TODOs bypassing encryption

- [ ] ✅ **Scope check**
  - TODO is small enough (< 2 hours work)
  - Not a feature disguised as TODO
  - Not a breaking change

- [ ] ✅ **Alternative check**
  - Could this be implemented now?
  - Is the dependency real or assumed?
  - Should this be a feature flag instead?

### Rejection Criteria

**Reject PR if:**

- ❌ Any P0 TODO (security, auth, data integrity)
- ❌ TODO without issue number
- ❌ TODO without context
- ❌ TODO for large feature (> 2 hours)
- ❌ More than 3 new TODOs in single PR
- ❌ TODO duplicates existing TODO
- ❌ Vague TODO ("fix this later", "improve performance")

### Approval with Conditions

**Approve but flag for follow-up if:**

- ⚠️ P1 TODO without sprint assignment
- ⚠️ TODO older than 30 days being re-added
- ⚠️ TODO count in file exceeds 5
- ⚠️ TODO pattern indicates architectural issue

---

## Automation & Enforcement

### GitHub Actions - CI/CD Checks

**File:** `.github/workflows/todo-check.yml`

```yaml
name: TODO Policy Enforcement

on:
  pull_request:
    paths:
      - '**.cs'

jobs:
  check-todo-format:
    name: Verify TODO Format Compliance
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
        with:
          fetch-depth: 0  # Full history for diff

      - name: Check for TODOs without issue numbers
        run: |
          echo "Checking TODO format compliance..."

          # Find all TODOs in C# files
          TODO_FILES=$(grep -rl "TODO:" --include="*.cs" src/ tests/ || true)

          if [ -z "$TODO_FILES" ]; then
            echo "✅ No TODOs found"
            exit 0
          fi

          # Check each TODO has issue number format: TODO(#123)
          INVALID_TODOS=$(grep -rn "TODO:" --include="*.cs" src/ tests/ | grep -v "TODO(#[0-9]\+)" || true)

          if [ -n "$INVALID_TODOS" ]; then
            echo "❌ Found TODOs without issue numbers:"
            echo "$INVALID_TODOS"
            echo ""
            echo "Required format: TODO(#123): Description"
            echo "See TODO_MANAGEMENT_POLICY.md for details"
            exit 1
          fi

          echo "✅ All TODOs have issue numbers"

      - name: Check for P0 TODOs in critical paths
        run: |
          echo "Checking for security TODOs..."

          SECURITY_PATHS="src/Honua.Server.Host/Admin src/Honua.Server.Host/Authentication src/Honua.Server.Host/Security"

          # Check if any new TODOs added to security paths
          NEW_TODOS=$(git diff origin/main...HEAD --unified=0 -- $SECURITY_PATHS | grep "^+.*TODO:" || true)

          if [ -n "$NEW_TODOS" ]; then
            echo "❌ New TODOs found in security-critical paths:"
            echo "$NEW_TODOS"
            echo ""
            echo "Security TODOs are not allowed. Please:"
            echo "1. Implement the fix now, or"
            echo "2. Add mitigation and create high-priority issue"
            exit 1
          fi

          echo "✅ No TODOs in security-critical paths"

      - name: Check TODO count limit
        run: |
          echo "Checking TODO count..."

          # Count new TODOs added in this PR
          NEW_TODO_COUNT=$(git diff origin/main...HEAD --unified=0 | grep "^+.*TODO:" | wc -l || true)

          if [ "$NEW_TODO_COUNT" -gt 3 ]; then
            echo "❌ This PR adds $NEW_TODO_COUNT TODOs (limit: 3)"
            echo ""
            echo "Adding many TODOs suggests:"
            echo "1. Feature is incomplete - consider feature flag"
            echo "2. Changes too large - split into smaller PRs"
            echo "3. Issues should be tracked separately"
            exit 1
          fi

          echo "✅ TODO count within limit ($NEW_TODO_COUNT/3)"
```

### SonarQube Rules

**File:** `sonar-project.properties`

```properties
# TODO tracking rules
sonar.issue.enforce.multicriteria=todo1,todo2,todo3

# Fail build on TODOs in admin/auth paths
sonar.issue.enforce.multicriteria.todo1.ruleKey=csharpsquid:S1135
sonar.issue.enforce.multicriteria.todo1.resourceKey=**/Admin/**/*.cs

sonar.issue.enforce.multicriteria.todo2.ruleKey=csharpsquid:S1135
sonar.issue.enforce.multicriteria.todo2.resourceKey=**/Authentication/**/*.cs

sonar.issue.enforce.multicriteria.todo3.ruleKey=csharpsquid:S1135
sonar.issue.enforce.multicriteria.todo3.resourceKey=**/Security/**/*.cs

# Track TODO as technical debt
sonar.issue.ignore.multicriteria=todo_debt
sonar.issue.ignore.multicriteria.todo_debt.ruleKey=csharpsquid:S1135
sonar.issue.ignore.multicriteria.todo_debt.resourceKey=**/*.cs
```

### Weekly TODO Report

**File:** `scripts/weekly-todo-report.sh`

```bash
#!/bin/bash
# Generate weekly TODO report for team review

REPORT_FILE="reports/todo-report-$(date +%Y-%m-%d).md"
mkdir -p reports

cat > "$REPORT_FILE" << EOF
# TODO Report - Week $(date +%W-%Y)
**Generated:** $(date)
**Repository:** Honua.Server

---

## Summary

EOF

# Count total TODOs
TOTAL_TODOS=$(grep -r "TODO:" --include="*.cs" src/ tests/ | wc -l)
echo "- **Total TODOs:** $TOTAL_TODOS" >> "$REPORT_FILE"

# Count by priority (based on file path)
P0_TODOS=$(grep -r "TODO:" --include="*.cs" src/Honua.Server.Host/Admin src/Honua.Server.Host/Authentication src/Honua.Server.Host/Security 2>/dev/null | wc -l)
echo "- **P0 (Critical):** $P0_TODOS ⚠️" >> "$REPORT_FILE"

# TODOs added this week
ADDED=$(git log --since='1 week ago' --all --oneline --no-merges -p | grep "^+.*TODO:" | wc -l)
echo "- **Added this week:** $ADDED" >> "$REPORT_FILE"

# TODOs removed this week
REMOVED=$(git log --since='1 week ago' --all --oneline --no-merges -p | grep "^-.*TODO:" | wc -l)
echo "- **Removed this week:** $REMOVED" >> "$REPORT_FILE"

# Net change
NET=$((ADDED - REMOVED))
echo "- **Net change:** $NET" >> "$REPORT_FILE"

echo "" >> "$REPORT_FILE"
echo "## Trend" >> "$REPORT_FILE"
echo "" >> "$REPORT_FILE"

if [ $NET -lt 0 ]; then
  echo "✅ **Improving** - Removing more TODOs than adding" >> "$REPORT_FILE"
elif [ $NET -eq 0 ]; then
  echo "➡️ **Stable** - TODO count unchanged" >> "$REPORT_FILE"
else
  echo "⚠️ **Increasing** - Adding more TODOs than removing" >> "$REPORT_FILE"
fi

echo "" >> "$REPORT_FILE"
echo "## Action Items" >> "$REPORT_FILE"
echo "" >> "$REPORT_FILE"

if [ $P0_TODOS -gt 0 ]; then
  echo "- ⚠️ **URGENT:** $P0_TODOS P0 TODOs in security paths - must be resolved" >> "$REPORT_FILE"
fi

if [ $TOTAL_TODOS -gt 50 ]; then
  echo "- ⚠️ Total TODO count exceeds 50 - review and close stale issues" >> "$REPORT_FILE"
fi

if [ $NET -gt 5 ]; then
  echo "- ⚠️ TODO count increasing rapidly - review PR process" >> "$REPORT_FILE"
fi

echo "" >> "$REPORT_FILE"
echo "## Details" >> "$REPORT_FILE"
echo "" >> "$REPORT_FILE"
echo "See \`TODO_TRACKING.md\` for complete TODO inventory" >> "$REPORT_FILE"

echo "Report generated: $REPORT_FILE"
cat "$REPORT_FILE"
```

---

## Metrics & Reporting

### Key Metrics

| Metric | Target | Current | Tracking |
|--------|--------|---------|----------|
| **Total TODOs** | < 20 | 78 | Weekly |
| **P0 TODOs** | 0 | 15 | Daily |
| **P1 TODOs** | < 5 | 18 | Weekly |
| **Average TODO Age** | < 30 days | TBD | Weekly |
| **TODOs per 1000 LOC** | < 0.5 | TBD | Monthly |
| **TODO Resolution Rate** | > 80% | TBD | Monthly |

### Reporting Schedule

- **Daily:** P0 TODO count (automated alert if > 0)
- **Weekly:** TODO trend report (added vs. removed)
- **Monthly:** Full TODO audit and cleanup
- **Quarterly:** Policy review and metrics retrospective

### Dashboards

**SonarQube Dashboard:**
- Technical Debt Ratio (target < 5%)
- TODO count by priority
- TODO lifetime distribution
- Files with most TODOs

**GitHub Project Board:**
- TODO Issues by status (Open, In Progress, Closed)
- TODO Issues by priority
- Stale TODOs (> 30 days)

---

## Examples

### Example 1: Simple Validation TODO

**Scenario:** Need to add validation but PR is focused on other feature

```csharp
public async Task<IResult> CreateGeoEvent(CreateGeoEventRequest request)
{
    // TODO(#456): Add validation for coordinate bounds
    // Context: Coordinates validation tracked in issue #456
    // Example: if (request.Latitude < -90 || request.Latitude > 90) throw ArgumentException
    var geoEvent = new GeoEvent
    {
        Latitude = request.Latitude,
        Longitude = request.Longitude
    };

    await _store.SaveAsync(geoEvent);
    return Results.Created($"/api/geoevents/{geoEvent.Id}", geoEvent);
}
```

**GitHub Issue #456:**
- Title: "Add coordinate bounds validation"
- Priority: P2
- Assigned to: Next sprint
- Acceptance criteria defined

### Example 2: Dependency-Blocked TODO

**Scenario:** Waiting for library update before implementing feature

```csharp
public async Task<byte[]> ExportToGeoParquet(IEnumerable<Feature> features)
{
    // TODO(#789): Switch to GeoParquet when Apache.Arrow v12.0 is released
    // Context: Current version (v11.0) doesn't support GeoParquet extension
    // Blocked by: https://github.com/apache/arrow/issues/12345
    // Workaround: Export to GeoJSON for now
    throw new NotSupportedException("GeoParquet export not yet supported. Use GeoJSON export.");
}
```

**GitHub Issue #789:**
- Title: "Add GeoParquet export support"
- Priority: P2
- Labels: blocked, external-dependency
- Linked to upstream Apache Arrow issue

### Example 3: Refactoring TODO

**Scenario:** Code duplication to be refactored

```csharp
public class AlertService
{
    public async Task SendEmailAlert(Alert alert)
    {
        // TODO(#234): Extract email sending to EmailNotificationProvider
        // Context: Duplicated in 3 places - needs DRY refactoring
        var smtp = new SmtpClient(_config.SmtpHost);
        await smtp.SendMailAsync(_config.FromAddress, alert.Recipients, alert.Message);
    }

    public async Task SendSlackAlert(Alert alert)
    {
        // Similar duplication for Slack...
    }
}
```

**GitHub Issue #234:**
- Title: "Refactor notification sending to provider pattern"
- Priority: P3
- Technical debt label
- Design doc: `docs/architecture/notification-providers.md`

### Example 4: Configuration TODO

**Scenario:** Hardcoded value should be configurable

```csharp
public class QueryService
{
    public async Task<List<Feature>> QueryFeatures(FeatureQuery query)
    {
        // TODO(#567): Make page size configurable via appsettings
        // Context: Currently hardcoded, should be in QueryOptions section
        // Example: _options.Value.DefaultPageSize ?? 1000
        const int pageSize = 1000;

        return await _repository.GetFeaturesAsync(query, pageSize);
    }
}
```

**GitHub Issue #567:**
- Title: "Make query page size configurable"
- Priority: P2
- Configuration change needed in appsettings.json

---

## Exceptions & Escalations

### When to Request Exception

Exceptions to this policy may be requested for:

1. **Prototype code** in spike branches (not merging to main)
2. **Third-party code** being integrated temporarily
3. **Emergency hotfixes** (with post-fix TODO cleanup)
4. **Legacy code** migration (gradual refactoring plan)

### Exception Request Process

1. **Create GitHub issue** explaining why exception is needed
2. **Tag issue** with `policy-exception` label
3. **Get approval** from team lead or architect
4. **Document expiration** date for exception
5. **Add to TODO_TRACKING.md** with exception notes

### Escalation Path

**For disputes about TODO priority or implementation:**

1. **Developer** and **Reviewer** discuss in PR comments
2. **Team Lead** makes decision if consensus not reached
3. **Architect** reviews if architectural impact
4. **CTO** final decision for policy changes

---

## Policy Review & Updates

**Review Schedule:** Quarterly (every 3 months)

**Review Process:**
1. Team retrospective on TODO policy effectiveness
2. Review metrics and trends
3. Identify policy gaps or issues
4. Propose amendments
5. Vote on changes
6. Update policy document
7. Communicate changes to team

**Version History:**

| Version | Date | Changes | Approved By |
|---------|------|---------|-------------|
| 1.0 | 2025-11-06 | Initial policy | [Pending] |

---

## References

- [TODO_TRACKING.md](TODO_TRACKING.md) - Complete TODO inventory
- [TODO_RESOLUTION_SUMMARY.md](TODO_RESOLUTION_SUMMARY.md) - Resolution progress
- [.github/ISSUE_TEMPLATE/](.github/ISSUE_TEMPLATE/) - Issue templates for TODOs
- [SonarQube TODO Rules](https://rules.sonarsource.com/csharp/RSPEC-1135) - S1135 rule

---

## Questions & Support

**For questions about this policy:**
- Team chat: `#engineering` channel
- Documentation: `TODO_MANAGEMENT_POLICY.md` (this document)
- Policy owner: Engineering Team Lead

**For TODO-related issues:**
- Check [TODO_TRACKING.md](TODO_TRACKING.md) first
- Create issue with `todo` label
- Tag team lead for urgent issues

---

**Document maintained by:** Engineering Team
**Last updated:** 2025-11-06
**Next review:** 2025-02-06 (Quarterly)
