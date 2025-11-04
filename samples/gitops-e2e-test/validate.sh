#!/bin/bash
#
# GitOps E2E Validation Script
#
# Validates that GitOps components are functioning correctly by checking:
# - Git repository accessibility
# - State directory and deployment records
# - Recent reconciliation activity
# - Log file analysis for errors
#
# Usage: ./validate.sh [--verbose] [--repo-path PATH] [--state-path PATH]
#

set -e

# Configuration
REPO_PATH="${GITOPS_REPO_PATH:-/tmp/honua-gitops-test-repo}"
STATE_PATH="${GITOPS_STATE_PATH:-/tmp/honua-gitops-state}"
LOG_PATH="../../src/Honua.Server.Host/logs"
VERBOSE=false

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --verbose|-v)
            VERBOSE=true
            shift
            ;;
        --repo-path)
            REPO_PATH="$2"
            shift 2
            ;;
        --state-path)
            STATE_PATH="$2"
            shift 2
            ;;
        --help|-h)
            echo "GitOps E2E Validation Script"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --verbose, -v           Enable verbose output"
            echo "  --repo-path PATH        Git repository path (default: /tmp/honua-gitops-test-repo)"
            echo "  --state-path PATH       State directory path (default: /tmp/honua-gitops-state)"
            echo "  --help, -h              Show this help message"
            echo ""
            echo "Environment Variables:"
            echo "  GITOPS_REPO_PATH        Override default repository path"
            echo "  GITOPS_STATE_PATH       Override default state path"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Counters for summary
CHECKS_PASSED=0
CHECKS_FAILED=0
CHECKS_WARNING=0

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[✓]${NC} $1"
    ((CHECKS_PASSED++))
}

log_warning() {
    echo -e "${YELLOW}[⚠]${NC} $1"
    ((CHECKS_WARNING++))
}

log_error() {
    echo -e "${RED}[✗]${NC} $1"
    ((CHECKS_FAILED++))
}

log_section() {
    echo ""
    echo -e "${CYAN}=== $1 ===${NC}"
}

log_verbose() {
    if [ "$VERBOSE" = true ]; then
        echo -e "${BLUE}[VERBOSE]${NC} $1"
    fi
}

# Validation functions

check_prerequisites() {
    log_section "Checking Prerequisites"

    # Check git is installed
    if command -v git &> /dev/null; then
        GIT_VERSION=$(git --version | awk '{print $3}')
        log_success "Git is installed (version $GIT_VERSION)"
    else
        log_error "Git is not installed"
        return 1
    fi

    # Check jq is installed (helpful but not critical)
    if command -v jq &> /dev/null; then
        JQ_VERSION=$(jq --version | cut -d'-' -f2)
        log_success "jq is installed (version $JQ_VERSION)"
    else
        log_warning "jq is not installed (optional, but recommended for JSON parsing)"
    fi
}

check_repository() {
    log_section "Checking Git Repository"

    # Check if repository directory exists
    if [ -d "$REPO_PATH" ]; then
        log_success "Repository directory exists at $REPO_PATH"
    else
        log_error "Repository directory not found at $REPO_PATH"
        log_info "Run ./init-test-repo.sh to create the test repository"
        return 1
    fi

    # Check if it's a valid Git repository
    if git -C "$REPO_PATH" rev-parse --git-dir > /dev/null 2>&1; then
        log_success "Valid Git repository"
    else
        log_error "Directory exists but is not a valid Git repository"
        return 1
    fi

    # Get current commit
    CURRENT_COMMIT=$(git -C "$REPO_PATH" rev-parse --short HEAD 2>/dev/null)
    if [ -n "$CURRENT_COMMIT" ]; then
        log_success "Current commit: $CURRENT_COMMIT"
        log_verbose "Full commit hash: $(git -C "$REPO_PATH" rev-parse HEAD)"
    else
        log_error "Could not get current commit"
        return 1
    fi

    # Check for commits
    COMMIT_COUNT=$(git -C "$REPO_PATH" rev-list --count HEAD 2>/dev/null || echo "0")
    if [ "$COMMIT_COUNT" -gt 0 ]; then
        log_success "Repository has $COMMIT_COUNT commits"

        if [ "$VERBOSE" = true ]; then
            log_verbose "Recent commits:"
            git -C "$REPO_PATH" log --oneline -5 2>/dev/null | while read line; do
                log_verbose "  $line"
            done
        fi
    else
        log_warning "Repository has no commits"
    fi

    # Check environment directories
    log_verbose "Checking environment directories..."
    for env in development staging production common; do
        if [ -d "$REPO_PATH/environments/$env" ]; then
            log_verbose "  ✓ environments/$env exists"
        else
            log_warning "Environment directory missing: environments/$env"
        fi
    done

    # Check for required files in development environment
    if [ -f "$REPO_PATH/environments/development/metadata.json" ]; then
        log_verbose "  ✓ Development metadata.json exists"

        if command -v jq &> /dev/null; then
            if jq empty "$REPO_PATH/environments/development/metadata.json" 2>/dev/null; then
                log_verbose "  ✓ metadata.json is valid JSON"
            else
                log_warning "metadata.json has invalid JSON syntax"
            fi
        fi
    else
        log_warning "Development metadata.json not found"
    fi
}

check_state_directory() {
    log_section "Checking State Directory"

    # Check if state directory exists
    if [ -d "$STATE_PATH" ]; then
        log_success "State directory exists at $STATE_PATH"
    else
        log_error "State directory not found at $STATE_PATH"
        log_info "Directory should be created automatically by Honua or init-test-repo.sh"
        return 1
    fi

    # Check for deployments subdirectory
    if [ -d "$STATE_PATH/deployments" ]; then
        log_success "Deployments subdirectory exists"

        # Count deployment files
        DEPLOYMENT_COUNT=$(find "$STATE_PATH/deployments" -name "*.json" 2>/dev/null | wc -l)
        if [ "$DEPLOYMENT_COUNT" -gt 0 ]; then
            log_success "Found $DEPLOYMENT_COUNT deployment records"

            if [ "$VERBOSE" = true ]; then
                log_verbose "Recent deployments:"
                find "$STATE_PATH/deployments" -name "*.json" -type f -printf "%T@ %p\n" 2>/dev/null | \
                    sort -rn | head -5 | while read timestamp file; do
                    log_verbose "  $(basename "$file")"
                done
            fi
        else
            log_warning "No deployment records found (GitOps may not have run yet)"
        fi
    else
        log_warning "Deployments subdirectory not found"
    fi

    # Check for approvals subdirectory
    if [ -d "$STATE_PATH/approvals" ]; then
        log_success "Approvals subdirectory exists"

        APPROVAL_COUNT=$(find "$STATE_PATH/approvals" -name "*.json" 2>/dev/null | wc -l)
        if [ "$APPROVAL_COUNT" -gt 0 ]; then
            log_info "Found $APPROVAL_COUNT approval records"

            if command -v jq &> /dev/null && [ "$VERBOSE" = true ]; then
                log_verbose "Approval statuses:"
                find "$STATE_PATH/approvals" -name "*.json" 2>/dev/null | while read file; do
                    STATE=$(jq -r '.State' "$file" 2>/dev/null || echo "unknown")
                    ENV=$(jq -r '.Environment' "$file" 2>/dev/null || echo "unknown")
                    log_verbose "  $(basename "$file"): $STATE ($ENV)"
                done
            fi
        fi
    else
        log_verbose "Approvals subdirectory not found (will be created on first approval)"
    fi

    # Check directory permissions
    if [ -w "$STATE_PATH" ]; then
        log_success "State directory is writable"
    else
        log_error "State directory is not writable"
        log_info "Fix with: chmod 755 $STATE_PATH"
        return 1
    fi
}

check_latest_deployment() {
    log_section "Checking Latest Deployment"

    # Find most recent deployment file
    LATEST_DEPLOYMENT=$(find "$STATE_PATH/deployments" -name "*.json" -type f -printf "%T@ %p\n" 2>/dev/null | \
        sort -rn | head -1 | cut -d' ' -f2)

    if [ -z "$LATEST_DEPLOYMENT" ]; then
        log_warning "No deployment records found"
        log_info "This is normal if GitOps hasn't run yet or no changes have been made"
        return 0
    fi

    log_success "Latest deployment found: $(basename "$LATEST_DEPLOYMENT")"

    if command -v jq &> /dev/null; then
        # Extract deployment details
        DEPLOYMENT_ID=$(jq -r '.Id' "$LATEST_DEPLOYMENT" 2>/dev/null || echo "unknown")
        ENVIRONMENT=$(jq -r '.Environment' "$LATEST_DEPLOYMENT" 2>/dev/null || echo "unknown")
        STATE=$(jq -r '.State' "$LATEST_DEPLOYMENT" 2>/dev/null || echo "unknown")
        COMMIT=$(jq -r '.Commit' "$LATEST_DEPLOYMENT" 2>/dev/null | cut -c1-8)
        STARTED_AT=$(jq -r '.StartedAt' "$LATEST_DEPLOYMENT" 2>/dev/null || echo "unknown")
        COMPLETED_AT=$(jq -r '.CompletedAt' "$LATEST_DEPLOYMENT" 2>/dev/null || echo "null")

        log_info "  Deployment ID: $DEPLOYMENT_ID"
        log_info "  Environment: $ENVIRONMENT"
        log_info "  State: $STATE"
        log_info "  Commit: $COMMIT"
        log_info "  Started: $STARTED_AT"

        if [ "$COMPLETED_AT" != "null" ]; then
            log_info "  Completed: $COMPLETED_AT"
        fi

        # Check deployment state
        case "$STATE" in
            "Completed")
                log_success "Deployment completed successfully"
                ;;
            "Applying")
                log_warning "Deployment is currently in progress"
                ;;
            "PendingApproval")
                log_warning "Deployment is waiting for approval"
                ;;
            "Failed")
                log_error "Deployment failed"
                ERROR_MSG=$(jq -r '.ErrorMessage' "$LATEST_DEPLOYMENT" 2>/dev/null || echo "No error message")
                log_info "  Error: $ERROR_MSG"
                ;;
            *)
                log_warning "Deployment state: $STATE"
                ;;
        esac

        # Check validation results
        if [ "$VERBOSE" = true ]; then
            VALIDATION_COUNT=$(jq '.ValidationResults | length' "$LATEST_DEPLOYMENT" 2>/dev/null || echo "0")
            if [ "$VALIDATION_COUNT" -gt 0 ]; then
                log_verbose "Validation results ($VALIDATION_COUNT):"
                jq -r '.ValidationResults[] | "  [\(.Type)] \(if .Success then "✓" else "✗" end) \(.Message)"' \
                    "$LATEST_DEPLOYMENT" 2>/dev/null | while read line; do
                    log_verbose "$line"
                done
            fi
        fi
    else
        log_warning "jq not installed, skipping detailed deployment analysis"
        log_info "Install jq for detailed JSON parsing: apt-get install jq (Ubuntu) or brew install jq (macOS)"
    fi
}

check_reconciliation_activity() {
    log_section "Checking Recent Reconciliation Activity"

    # Check when last deployment was created
    if [ -d "$STATE_PATH/deployments" ]; then
        LATEST_FILE=$(find "$STATE_PATH/deployments" -name "*.json" -type f -printf "%T@ %p\n" 2>/dev/null | \
            sort -rn | head -1)

        if [ -n "$LATEST_FILE" ]; then
            TIMESTAMP=$(echo "$LATEST_FILE" | cut -d' ' -f1)
            CURRENT_TIMESTAMP=$(date +%s)
            AGE_SECONDS=$((CURRENT_TIMESTAMP - ${TIMESTAMP%.*}))
            AGE_MINUTES=$((AGE_SECONDS / 60))

            if [ "$AGE_SECONDS" -lt 60 ]; then
                log_success "Last reconciliation: $AGE_SECONDS seconds ago"
            elif [ "$AGE_MINUTES" -lt 60 ]; then
                log_success "Last reconciliation: $AGE_MINUTES minutes ago"
            else
                AGE_HOURS=$((AGE_MINUTES / 60))
                log_warning "Last reconciliation: $AGE_HOURS hours ago (may be stale)"
            fi
        else
            log_warning "No reconciliation activity found"
        fi
    fi
}

check_log_files() {
    log_section "Checking Log Files"

    # Check if log directory exists
    if [ -d "$LOG_PATH" ]; then
        log_success "Log directory exists at $LOG_PATH"
    else
        log_warning "Log directory not found at $LOG_PATH"
        log_info "Logs may be in a different location or Honua may not be running"
        return 0
    fi

    # Find most recent log file
    LATEST_LOG=$(find "$LOG_PATH" -name "honua-*.log" -type f -printf "%T@ %p\n" 2>/dev/null | \
        sort -rn | head -1 | cut -d' ' -f2)

    if [ -z "$LATEST_LOG" ]; then
        log_warning "No log files found"
        log_info "This is normal if Honua hasn't been started yet"
        return 0
    fi

    log_success "Latest log file: $(basename "$LATEST_LOG")"

    # Search for GitOps-related log entries
    if grep -q "GitWatcher started" "$LATEST_LOG" 2>/dev/null; then
        log_success "GitWatcher is running"

        if [ "$VERBOSE" = true ]; then
            BRANCH=$(grep "GitWatcher started" "$LATEST_LOG" 2>/dev/null | tail -1 | grep -oP "branch '\K[^']+")
            INTERVAL=$(grep "GitWatcher started" "$LATEST_LOG" 2>/dev/null | tail -1 | grep -oP "every \K[0-9]+")
            log_verbose "  Watching branch: $BRANCH"
            log_verbose "  Poll interval: $INTERVAL seconds"
        fi
    else
        log_warning "GitWatcher startup not found in logs"
        log_info "GitOps may not be enabled or Honua may not have started successfully"
    fi

    # Check for recent commit detection
    if grep -q "Detected new commit" "$LATEST_LOG" 2>/dev/null; then
        DETECTION_COUNT=$(grep -c "Detected new commit" "$LATEST_LOG" 2>/dev/null)
        log_success "Git changes detected ($DETECTION_COUNT times)"

        if [ "$VERBOSE" = true ]; then
            log_verbose "Recent detections:"
            grep "Detected new commit" "$LATEST_LOG" 2>/dev/null | tail -3 | while read line; do
                log_verbose "  $line"
            done
        fi
    else
        log_info "No git changes detected yet (normal if no commits made since startup)"
    fi

    # Check for reconciliation success
    if grep -q "Successfully completed reconciliation" "$LATEST_LOG" 2>/dev/null; then
        SUCCESS_COUNT=$(grep -c "Successfully completed reconciliation" "$LATEST_LOG" 2>/dev/null)
        log_success "Successful reconciliations: $SUCCESS_COUNT"
    else
        log_info "No successful reconciliations yet"
    fi

    # Check for errors
    ERROR_COUNT=$(grep -c "\\[ERR\\]" "$LATEST_LOG" 2>/dev/null || echo "0")
    if [ "$ERROR_COUNT" -eq 0 ]; then
        log_success "No errors found in logs"
    else
        log_warning "Found $ERROR_COUNT error entries in logs"

        if [ "$VERBOSE" = true ]; then
            log_verbose "Recent errors:"
            grep "\\[ERR\\]" "$LATEST_LOG" 2>/dev/null | tail -3 | while read line; do
                log_verbose "  $line"
            done
        fi
    fi

    # Check for specific GitOps errors
    GITOPS_ERROR_COUNT=$(grep -c "Error during reconciliation\|GitWatcher.*error\|GitOps.*failed" "$LATEST_LOG" 2>/dev/null || echo "0")
    if [ "$GITOPS_ERROR_COUNT" -gt 0 ]; then
        log_error "Found $GITOPS_ERROR_COUNT GitOps-related errors"

        log_info "Recent GitOps errors:"
        grep -i "Error during reconciliation\|GitWatcher.*error\|GitOps.*failed" "$LATEST_LOG" 2>/dev/null | \
            tail -5 | while read line; do
            log_info "  $line"
        done
    fi
}

check_reconciled_files() {
    log_section "Checking Reconciled Configuration Files"

    # Check if reconciled directory exists
    RECONCILED_PATH="$REPO_PATH/reconciled"
    if [ -d "$RECONCILED_PATH" ]; then
        log_success "Reconciled configuration directory exists"

        # Check for environment-specific reconciled files
        for env in development staging production; do
            if [ -d "$RECONCILED_PATH/$env" ]; then
                log_verbose "  ✓ Reconciled config exists for: $env"

                if [ "$VERBOSE" = true ]; then
                    for file in metadata.json datasources.json; do
                        if [ -f "$RECONCILED_PATH/$env/$file" ]; then
                            log_verbose "    ✓ $file"
                            if command -v jq &> /dev/null; then
                                if jq empty "$RECONCILED_PATH/$env/$file" 2>/dev/null; then
                                    log_verbose "      Valid JSON"
                                else
                                    log_warning "      Invalid JSON"
                                fi
                            fi
                        fi
                    done
                fi
            fi
        done
    else
        log_info "Reconciled configuration directory not found (created during first reconciliation)"
    fi
}

print_summary() {
    log_section "Validation Summary"

    TOTAL_CHECKS=$((CHECKS_PASSED + CHECKS_FAILED + CHECKS_WARNING))

    echo ""
    echo -e "Total checks: $TOTAL_CHECKS"
    echo -e "${GREEN}Passed: $CHECKS_PASSED${NC}"
    echo -e "${YELLOW}Warnings: $CHECKS_WARNING${NC}"
    echo -e "${RED}Failed: $CHECKS_FAILED${NC}"
    echo ""

    # Determine overall status
    if [ "$CHECKS_FAILED" -eq 0 ]; then
        if [ "$CHECKS_WARNING" -eq 0 ]; then
            echo -e "${GREEN}Status: HEALTHY${NC}"
            echo "All GitOps components are functioning correctly."
        else
            echo -e "${YELLOW}Status: HEALTHY (with warnings)${NC}"
            echo "GitOps is functioning but some checks produced warnings."
        fi
        return 0
    else
        echo -e "${RED}Status: UNHEALTHY${NC}"
        echo "Some GitOps components are not functioning correctly."
        echo "Review the failed checks above and consult the troubleshooting guide."
        return 1
    fi
}

print_recommendations() {
    echo ""
    log_section "Recommendations"

    if [ "$CHECKS_FAILED" -gt 0 ]; then
        echo "• Review failed checks and follow suggested remediation steps"
        echo "• Check Honua logs for detailed error messages"
        echo "• Verify GitOps configuration in appsettings.json"
        echo "• Ensure Honua server is running: cd ../../src/Honua.Server.Host && dotnet run"
    fi

    if [ ! -d "$REPO_PATH" ]; then
        echo "• Initialize test repository: ./init-test-repo.sh"
    fi

    if [ "$CHECKS_WARNING" -gt 0 ]; then
        echo "• Run with --verbose flag for detailed information: ./validate.sh --verbose"
    fi

    echo "• Review test scenarios in test-scenarios/ directory"
    echo "• Consult docs/dev/gitops-e2e-testing.md for comprehensive guide"
}

# Main execution
main() {
    echo ""
    echo -e "${CYAN}================================================${NC}"
    echo -e "${CYAN}    GitOps E2E Validation Report${NC}"
    echo -e "${CYAN}================================================${NC}"
    echo ""
    echo "Date: $(date '+%Y-%m-%d %H:%M:%S')"
    echo "Repository: $REPO_PATH"
    echo "State Directory: $STATE_PATH"
    echo ""

    # Run all checks
    check_prerequisites || true
    check_repository || true
    check_state_directory || true
    check_latest_deployment || true
    check_reconciliation_activity || true
    check_log_files || true
    check_reconciled_files || true

    echo ""

    # Print summary and recommendations
    print_summary
    EXIT_CODE=$?

    print_recommendations

    echo ""
    echo -e "${CYAN}================================================${NC}"
    echo ""

    exit $EXIT_CODE
}

# Run main function
main
