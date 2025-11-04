# Dependency Management Quick Reference

**Last Updated:** 2025-10-17
**For:** Developers working on HonuaIO

## TL;DR

**HonuaIO has ZERO circular dependencies. Keep it that way.**

---

## Quick Rules

### ✅ DO

- Reference projects at lower layers from higher layers
- Keep `Honua.Server.Core` dependency-free (no Honua project refs)
- Use dependency injection for cross-cutting concerns
- Add project references only after consulting dependency graph
- Document rationale when adding new `<ProjectReference>`

### ❌ DON'T

- Add circular project references (build will fail anyway)
- Make `Honua.Server.Core` depend on other Honua projects
- Use `InternalsVisibleTo` between production projects
- Skip code review for PRs with new project references

---

## Dependency Hierarchy

```
Level 3: Applications (Entry Points)
  ├── Honua.Cli
  └── Honua.Server.Host

Level 2: Domain Services
  ├── Honua.Cli.AI
  └── Honua.Server.Enterprise

Level 1: Foundation
  ├── Honua.Server.Core (NEVER references other Honua projects)
  ├── Honua.Cli.AI.Secrets
  └── Honua.Server.AlertReceiver (standalone)
```

**Rule:** Higher numbers can reference lower numbers, never the reverse.

---

## Common Scenarios

### "I need to share code between Cli and Cli.AI"

**Options:**
1. Put it in `Honua.Server.Core` if it's core logic
2. Put it in `Honua.Cli.AI` and let `Honua.Cli` reference it
3. Create `Honua.Cli.Abstractions` if you need contracts (consult team first)

**Never:** Make `Honua.Cli.AI` reference `Honua.Cli`

### "I need to access Server.Host from Server.Core"

**Answer:** You don't. Restructure your code.

**Why:** Core is the foundation. It can't depend on hosts.

**Solution:**
- Extract interface to Core
- Implement in Host
- Use dependency injection

### "Can I use InternalsVisibleTo?"

**Answer:** Only for test projects.

**Format:** `{ProjectName}.Tests` or `{ProjectName}.IntegrationTests`

**Never:** Production code to production code

---

## Before Adding Project Reference

**Checklist:**
1. [ ] Verify no circular dependency created
2. [ ] Confirm reference direction follows hierarchy
3. [ ] Consider if abstraction would be better
4. [ ] Check namespace matches project structure
5. [ ] Document why reference is needed in PR

**Quick Check:**
```bash
# Add your reference to .csproj
# Then run:
dotnet build YourProject.csproj
# If it builds without circular warnings, you're good
```

---

## Namespace Rules

**Simple Rule:** Namespace MUST match project name.

| Project | Namespace Pattern | Example |
|---------|------------------|---------|
| Honua.Server.Core | `Honua.Server.Core.*` | `Honua.Server.Core.Data` |
| Honua.Cli.AI | `Honua.Cli.AI.*` | `Honua.Cli.AI.Services` |
| Honua.Server.Host | `Honua.Server.Host.*` | `Honua.Server.Host.Ogc` |

**No Exceptions.**

---

## Verification Commands

### Check for Circular Dependencies
```bash
# Should return nothing
dotnet msbuild -t:ResolveProjectReferences 2>&1 | grep -i circular
```

### View Project Dependencies
```bash
# For your project
dotnet list src/YourProject/YourProject.csproj reference
```

### Count Namespace Usage
```bash
# Example: Check if Core uses Host (should be 0)
find src -name "*.cs" -path "*/Honua.Server.Core/*" -exec grep -l "Honua\.Server\.Host\." {} \; | wc -l
```

---

## When in Doubt

1. **Read:** [ADR-0003-dependency-management.md](./ADR-0003-dependency-management.md)
2. **Check:** [DEPENDENCY_GRAPH.md](./DEPENDENCY_GRAPH.md)
3. **Ask:** Team lead or architect
4. **Don't:** Just add the reference and hope for the best

---

## CI/CD Integration

**Automated Check (Coming Soon):**
```bash
# Add to .github/workflows or CI pipeline
dotnet msbuild -t:ResolveProjectReferences 2>&1 | grep -i "circular" && exit 1
```

---

## Common Violations & Fixes

### Violation: Core Referencing Host

```diff
<!-- Honua.Server.Core.csproj -->
- <ProjectReference Include="..\Honua.Server.Host\Honua.Server.Host.csproj" />
```

**Fix:** Extract interface to Core, implement in Host

### Violation: Cli.AI Referencing Cli

```diff
<!-- Honua.Cli.AI.csproj -->
- <ProjectReference Include="..\Honua.Cli\Honua.Cli.csproj" />
```

**Fix:** Move shared code to Core or Cli.AI, or create Abstractions project

### Violation: Wrong Namespace

```diff
- namespace Honua.Cli.Services;
+ namespace Honua.Cli.AI.Services;
```

**Fix:** Update namespace to match project

---

## Architecture Contacts

**Questions about:**
- **Adding project reference:** Check with tech lead
- **Circular dependency concern:** Consult architect
- **Namespace organization:** Code review process

---

## Related Documentation

- **Full Analysis:** [CIRCULAR_DEPENDENCY_ANALYSIS.md](./CIRCULAR_DEPENDENCY_ANALYSIS.md)
- **Decision Record:** [ADR-0003-dependency-management.md](./ADR-0003-dependency-management.md)
- **Dependency Graph:** [DEPENDENCY_GRAPH.md](./DEPENDENCY_GRAPH.md)
- **Summary Report:** [../CIRCULAR_DEPENDENCY_REFACTORING_SUMMARY.md](../../CIRCULAR_DEPENDENCY_REFACTORING_SUMMARY.md)

---

**Remember:** We have zero circular dependencies. Let's keep it that way! 🎯
