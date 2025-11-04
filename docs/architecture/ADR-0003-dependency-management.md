# ADR-0003: Dependency Management and Circular Dependency Prevention

**Status:** Accepted
**Date:** 2025-10-17
**Decision Makers:** Development Team
**Related:** [CIRCULAR_DEPENDENCY_ANALYSIS.md](./CIRCULAR_DEPENDENCY_ANALYSIS.md)

## Context

As the HonuaIO codebase grows in complexity, maintaining a clean architecture with well-defined dependencies becomes critical for:

1. **Maintainability** - Understanding code relationships and impact of changes
2. **Testability** - Ability to test components in isolation
3. **Modularity** - Clear boundaries between different system concerns
4. **Build Performance** - Efficient compilation and minimal rebuild requirements
5. **Team Collaboration** - Preventing merge conflicts and architectural drift

A comprehensive analysis was conducted to identify circular dependencies and assess the current dependency structure.

## Decision

We adopt the following dependency management principles and practices:

### 1. Zero Tolerance for Project-Level Circular Dependencies

**Decision:** Maintain zero project-level circular dependencies through automated enforcement.

**Implementation:**
- CI/CD pipeline must verify no circular dependencies exist
- Any PR introducing circular dependencies will be rejected
- Use `dotnet msbuild -t:ResolveProjectReferences` as verification tool

### 2. Dependency Direction Rules

**Decision:** Enforce strict dependency flow rules:

```
Applications → Services → Core → Contracts
(Root)                          (Leaf)
```

**Specific Rules:**
- `Honua.Server.Core` may not reference any Honua.* projects
- `Honua.Cli.AI` may not reference `Honua.Cli`
- Host projects (Honua.Server.Host, Honua.Cli) are entry points and reference downward only
- Shared concerns must be in `Core` or extracted to separate contracts project

### 3. Namespace Organization Standards

**Decision:** Namespace must match project structure.

**Rules:**
- Files in `Honua.Server.Core` use `Honua.Server.Core.*` namespaces
- Files in `Honua.Cli.AI` use `Honua.Cli.AI.*` namespaces
- No exceptions without explicit architectural justification documented in ADR

**Action Items:**
- Review and fix any namespace mismatches in `Honua.Cli.AI` project
- Add namespace linter to pre-commit hooks (future enhancement)

### 4. InternalsVisibleTo Policy

**Decision:** `InternalsVisibleTo` is permitted only for test projects.

**Rules:**
- Format: `{ProjectName}.Tests` or `{ProjectName}.IntegrationTests`
- Production code must not use `InternalsVisibleTo` for other production projects
- Exceptions require ADR documentation and tech lead approval

### 5. Abstractions Project Strategy

**Decision:** Defer creation of abstractions projects until needed.

**Current State:** No circular dependencies exist, so no immediate need for abstraction projects.

**Future Trigger Points:**
- If `Honua.Cli` needs to provide contracts to `Honua.Cli.AI`
- If multiple projects need shared interfaces beyond what `Honua.Server.Core` provides
- If package boundary enforcement is needed for NuGet distribution

**When Needed:**
Create projects following pattern: `{Domain}.Contracts` or `{Domain}.Abstractions`

Example:
```
Honua.Cli.Contracts
  ├── Interfaces for plugins
  ├── DTOs for CLI integration
  └── Events for CLI extensibility
```

### 6. Dependency Review Process

**Decision:** Implement lightweight dependency governance.

**Process:**
1. **Pre-Development**: Consult dependency graph before adding new project references
2. **Code Review**: PRs adding `<ProjectReference>` require explicit reviewer verification
3. **Quarterly Review**: Team reviews dependency graph for technical debt accumulation
4. **Architecture Approval**: New projects require architectural review

## Consequences

### Positive

1. **Zero Circular Dependencies Maintained**
   - Current analysis shows zero project-level circular dependencies
   - Ongoing enforcement prevents regression

2. **Clear Mental Model**
   - Developers can understand dependency flow easily
   - New team members onboard faster

3. **Better Testing**
   - Clean dependencies enable better unit testing
   - Reduced need for complex test harnesses

4. **Faster Builds**
   - Acyclic graph enables parallel builds
   - Minimal rebuild propagation

5. **Modular Growth**
   - Future features can be added without architectural compromises
   - Clear guidance on where new code belongs

### Negative

1. **Initial Learning Curve**
   - Team needs to internalize dependency rules
   - May slow down initial development slightly

2. **Refactoring Overhead**
   - Occasionally need to extract abstractions
   - May require more interfaces than strictly necessary

3. **Namespace Cleanup Required**
   - Existing namespace mismatches need correction
   - Some file moves may be needed

### Neutral

1. **No Immediate Refactoring Required**
   - Current codebase is already compliant
   - Changes are preventive, not corrective

## Current Dependency Graph

```
Entry Points (Applications):
├── Honua.Cli
│   ├── → Honua.Cli.AI
│   │   ├── → Honua.Cli.AI.Secrets
│   │   └── → Honua.Server.Core
│   └── → Honua.Server.Core
│
├── Honua.Server.Host
│   └── → Honua.Server.Core
│
└── Honua.Server.AlertReceiver (Standalone)

Libraries:
├── Honua.Server.Core (No project dependencies)
├── Honua.Server.Enterprise
│   └── → Honua.Server.Core
└── Honua.Cli.AI.Secrets (No project dependencies)
```

**Dependency Metrics:**
- Total Projects: 7
- Max Depth: 3 levels
- Circular Dependencies: 0
- Standalone Projects: 1
- Core Dependencies: 1

## Compliance & Enforcement

### Automated Checks

```bash
# Add to CI/CD pipeline
dotnet msbuild -t:ResolveProjectReferences 2>&1 | grep -i "circular" && exit 1

# Optional: Add dependency analysis tool
dotnet tool install -g dotnet-project-graph
dotnet project-graph --format json > dependency-graph.json
```

### Manual Review Checklist

Before approving PR with new `<ProjectReference>`:
- [ ] Verify no circular dependency introduced
- [ ] Confirm reference follows dependency direction rules
- [ ] Check if abstraction project would be better choice
- [ ] Ensure namespace matches project structure
- [ ] Document rationale in PR description

## Alternatives Considered

### Alternative 1: Strict Layered Architecture

**Description:** Enforce N-tier architecture with explicit layer projects.

**Pros:**
- Very clear boundaries
- Industry standard pattern

**Cons:**
- More projects to manage
- May be over-engineering for current size
- Adds ceremony without current need

**Decision:** Rejected. Current structure is sufficient for project scale.

### Alternative 2: Allow Controlled Circular Dependencies

**Description:** Permit circular dependencies with documentation.

**Pros:**
- More flexibility
- Faster short-term development

**Cons:**
- Technical debt accumulation
- Testing becomes harder
- Build complexity increases

**Decision:** Rejected. Zero tolerance provides better long-term maintainability.

### Alternative 3: Immediate Abstraction Projects

**Description:** Create `Honua.Contracts`, `Honua.Cli.Abstractions` now.

**Pros:**
- Future-proofing
- Forces interface-driven design

**Cons:**
- YAGNI violation - no current need
- Additional projects to maintain
- Premature abstraction

**Decision:** Rejected. Defer until needed (see Decision #5).

### Alternative 4: Merge Projects

**Description:** Consolidate `Honua.Cli` and `Honua.Cli.AI` into single project.

**Pros:**
- Fewer projects
- No dependency management between them

**Cons:**
- Loses separation of concerns
- Makes optional AI features harder to manage
- Increases compile-time dependencies

**Decision:** Rejected. Current separation is valuable.

## Implementation Plan

### Phase 1: Documentation (Completed)
- [x] Create CIRCULAR_DEPENDENCY_ANALYSIS.md
- [x] Create this ADR
- [x] Update architecture documentation

### Phase 2: Namespace Cleanup (Optional)
- [ ] Audit files in `Honua.Cli.AI` for namespace correctness
- [ ] Fix any namespace mismatches (if deemed necessary)
- [ ] Add namespace validation to code reviews

### Phase 3: Automation (Future)
- [ ] Add dependency check to CI/CD pipeline
- [ ] Create pre-commit hook for project reference validation
- [ ] Add dependency graph generation to documentation build

### Phase 4: Team Enablement (Ongoing)
- [ ] Share this ADR with team
- [ ] Add to onboarding documentation
- [ ] Include in architecture review checklist

## References

- [Circular Dependency Analysis Report](./CIRCULAR_DEPENDENCY_ANALYSIS.md)
- [Martin Fowler - Dependency Inversion Principle](https://martinfowler.com/articles/dipInTheWild.html)
- [Microsoft - Project Design Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- [Clean Architecture - Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)

## Review & Updates

This ADR should be reviewed:
- When adding new Honua.* projects
- When significant dependency changes are proposed
- Quarterly as part of architecture review
- When circular dependency prevention becomes burdensome

**Last Review:** 2025-10-17
**Next Review:** 2026-01-17 (Quarterly)
**Status:** Active

---

**Approved By:** Development Team
**Date:** 2025-10-17
**Supersedes:** None
**Superseded By:** None
