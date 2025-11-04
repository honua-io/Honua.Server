# Honua Deployment Strategy - Start Simple, Scale Up

## The Risk: Over-Engineering

We've designed a comprehensive system with:
- GitOps controller
- Deployment state machines
- Topology awareness
- Multi-environment orchestration
- Advanced safety guardrails

**But do you need all of this on day 1?** Probably not.

Let's break it down into phases you can implement progressively.

## Phase 0: Current State (Manual)

```
User → Manual edits → Restart Honua → Hope it works
```

**Problems:**
- No version control
- No rollback
- Error-prone
- No audit trail

## Phase 1: Git + Manual Deploy (Week 1)

**Goal:** Get metadata in Git, deploy manually

```
User → Edit YAML in Git → Commit → SSH to server → Pull → Restart
```

**What you need:**
```
honua-config/
├── environments/
│   └── production/
│       ├── metadata.yaml
│       └── datasources.yaml
└── README.md
```

**Deployment:**
```bash
# On server
cd /etc/honua
git pull origin main
systemctl restart honua
```

**Benefits:**
✅ Version control
✅ Rollback via git revert
✅ Audit trail (Git history)

**Still manual, but much safer!**

---

## Phase 2: AI Consultant (Read-Only) (Week 2-3)

**Goal:** AI helps understand and propose changes, but doesn't execute

```
User → Chat with AI → AI generates YAML → User reviews → User commits to Git → User deploys manually
```

**What you need:**
- AI chat interface (Claude API)
- Read access to Git repo
- Read access to database (for introspection)

**AI can:**
- ✅ Answer questions about current config
- ✅ Generate metadata for new layers
- ✅ Validate YAML syntax
- ✅ Show diffs
- ❌ Cannot commit to Git
- ❌ Cannot deploy

**Example:**
```typescript
User: "How do I add the bike lanes layer?"

AI: I can help you create the metadata. Here's what I generated:

[Shows YAML]

To add this:
1. Copy the YAML above
2. Create file: environments/production/layers/bike-lanes.yaml
3. Commit to Git
4. SSH to server and run: git pull && systemctl restart honua

Would you like me to explain any of these settings?
```

**Benefits:**
✅ AI assistance without risk
✅ Learn the system safely
✅ Build confidence

---

## Phase 3: AI Creates PRs (Week 4-6)

**Goal:** AI commits to branch and creates PR, humans approve and merge

```
User → Chat with AI → AI creates branch → AI commits → AI opens PR
     → Human reviews → Human merges → Human deploys manually
```

**What you need:**
- GitHub API access (for AI)
- Branch protection rules
- PR review workflow

**AI can:**
- ✅ Create feature branches
- ✅ Commit changes
- ✅ Open PRs
- ✅ Respond to PR comments
- ❌ Cannot merge PRs
- ❌ Cannot deploy

**Example:**
```typescript
User: "Add bike lanes layer"

AI: I'll create a PR for this.

[Analyzes database table]
[Generates metadata]
[Creates branch: feature/add-bike-lanes]
[Commits metadata]
[Opens PR #42]

✓ Pull Request created: https://github.com/city/honua-config/pull/42

The PR includes:
- environments/production/layers/bike-lanes.yaml
- Full configuration based on your table schema
- Automated validation passed ✓

Please review and merge when ready. After merging:
  ssh server
  cd /etc/honua
  git pull
  systemctl restart honua
```

**Benefits:**
✅ AI does the tedious work
✅ Human review before changes
✅ Still simple manual deployment

---

## Phase 4: Add Simple Automation (Week 7-8)

**Goal:** Automate the manual deployment step

**Option A: GitHub Actions (Simple but has firewall issues)**

```yaml
# .github/workflows/deploy.yml
on:
  push:
    branches: [main]
    paths: ['environments/production/**']

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - name: SSH and deploy
        run: |
          ssh production-server "cd /etc/honua && git pull && systemctl restart honua"
```

**Option B: Cron-based Pull (Simpler, firewall-friendly)**

```bash
# On server: /etc/cron.d/honua-sync
*/5 * * * * root /usr/local/bin/honua-sync.sh

# /usr/local/bin/honua-sync.sh
#!/bin/bash
cd /etc/honua
git fetch origin main

# Check if there are new commits
LOCAL=$(git rev-parse HEAD)
REMOTE=$(git rev-parse origin/main)

if [ "$LOCAL" != "$REMOTE" ]; then
  echo "New changes detected, updating..."
  git pull origin main
  systemctl restart honua
  echo "Deployed commit $REMOTE at $(date)" >> /var/log/honua-deploy.log
fi
```

**Flow:**
```
User → Chat with AI → AI creates PR → Human merges
     → Cron pulls every 5 min → Auto-deploys
```

**Benefits:**
✅ Automatic deployment
✅ No firewall issues (outbound only)
✅ Simple bash script
✅ Works anywhere

---

## Phase 5: Add State Tracking (Week 9-10)

**Goal:** Track what's deployed, when, and by whom

**Use the FileStateStore we already built:**

```bash
# Update honua-sync.sh
#!/bin/bash
cd /etc/honua
git fetch origin main

LOCAL=$(git rev-parse HEAD)
REMOTE=$(git rev-parse origin/main)

if [ "$LOCAL" != "$REMOTE" ]; then
  # Create deployment record
  honua deployment create \
    --environment production \
    --commit $REMOTE \
    --initiated-by cron

  # Pull and restart
  git pull origin main
  systemctl restart honua

  # Mark deployment complete
  honua deployment complete
fi
```

**Now you have:**
✅ Deployment history
✅ Audit trail
✅ Rollback capability

---

## Phase 6: GitOps Controller (Month 3+)

**Only add this when you need:**
- Multiple environments (dev, staging, prod)
- Sophisticated rollback
- Health checks
- Topology coordination

**This is the full system we designed earlier.**

---

## Recommended Path

### Start Here (Minimum Viable):

1. **Week 1**: Put metadata in Git ✅
2. **Week 2**: Add AI consultant (read-only) ✅
3. **Week 3**: AI creates PRs ✅
4. **Week 4**: Simple cron-based auto-deploy ✅

**This gets you 80% of the value with 20% of the complexity.**

### Add Later (When You Need It):

5. **Month 2**: State tracking (deployment history)
6. **Month 3**: GitOps controller (if you add staging/dev environments)
7. **Month 4**: Topology awareness (if you add CDN, load balancers, etc.)
8. **Month 5**: Advanced safety (circuit breakers, blast radius limits)

---

## Decision Tree

**Do you have multiple environments (dev/staging/prod)?**
- No → Stop at Phase 4 (cron-based deployment)
- Yes → Consider Phase 6 (GitOps controller)

**Do you have complex infrastructure (CDN, LB, multiple regions)?**
- No → You don't need topology management yet
- Yes → Add topology in Phase 7

**Do you need to deploy multiple times per day?**
- No → Cron every 5 minutes is fine
- Yes → Add webhook support for instant deploys

**Do you need sophisticated rollback?**
- No → `git revert` is fine
- Yes → Add deployment state machine

---

## Simplified Component Matrix

| Component | Phase 1 | Phase 2 | Phase 3 | Phase 4 | Phase 5 | Phase 6 |
|-----------|---------|---------|---------|---------|---------|---------|
| Git repo | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| AI consultant | ❌ | Read-only | Creates PRs | Creates PRs | Creates PRs | Creates PRs |
| Deployment | Manual | Manual | Manual | Cron | Cron | Controller |
| State tracking | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Multi-env | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Topology | ❌ | ❌ | ❌ | ❌ | ❌ | Optional |
| Health checks | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

---

## The Simplest Possible System (Phase 1-4)

**Total components:**
1. Git repository (GitHub/GitLab)
2. AI consultant (Claude API)
3. Cron script (20 lines of bash)

**That's it!**

**Flow:**
```
User: "Add bike lanes layer"
  ↓
AI: [Analyzes table, generates YAML, creates PR]
  ↓
User: [Reviews PR, merges]
  ↓
Cron: [Detects new commit, pulls, restarts Honua]
  ↓
Done! (5 minutes later)
```

**No controller, no state machine, no topology, no webhooks.**

Just:
- Git for version control
- AI for assistance
- Cron for automation

---

## Complexity Comparison

### Minimal System (Phases 1-4):
- **Lines of code**: ~500
- **Components**: 3 (Git, AI, Cron)
- **Infrastructure**: 0 additional services
- **Maintenance**: Low
- **Learning curve**: Low

### Full System (Phase 6):
- **Lines of code**: ~5000+
- **Components**: 10+ (Git, AI, Controller, State store, Topology, etc.)
- **Infrastructure**: Controller service, state database, monitoring
- **Maintenance**: Medium-high
- **Learning curve**: High

**Start minimal, add complexity only when you feel the pain of not having it.**

---

## Red Flags That You Need More

**Consider Phase 5 (State Tracking) when:**
- "Who deployed that change?"
- "When did we deploy version X?"
- "I need to rollback but don't know what commit to revert to"

**Consider Phase 6 (GitOps Controller) when:**
- "Managing dev/staging/prod is getting confusing"
- "GitHub Actions keeps failing because of firewall rules"
- "We need blue/green deployments"

**Consider Topology when:**
- "We added a CDN and cache invalidation is manual"
- "Database migrations aren't coordinated with app deploys"
- "We have 5 different infrastructure components to update"

---

## Recommendation

**Start with Phase 1-4** (Git + AI + Cron).

This gives you:
- ✅ Version control
- ✅ AI assistance
- ✅ Automatic deployment
- ✅ Audit trail (Git)
- ✅ Rollback (git revert)

**All without:**
- ❌ Complex state machines
- ❌ Custom controllers
- ❌ Topology management
- ❌ Webhook infrastructure

**Then add complexity incrementally as you actually need it.**

The full GitOps controller design is there when you're ready, but you might never need it if you keep things simple!
