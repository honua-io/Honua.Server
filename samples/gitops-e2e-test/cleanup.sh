#!/bin/bash
#
# GitOps E2E Test Cleanup Script
#
# Removes test repository, state directory, and related files
# to reset the GitOps E2E testing environment.
#
# Usage: ./cleanup.sh [--all|--repo|--state|--logs] [--force]
#

set -e

# Configuration
REPO_PATH="${1:-/tmp/honua-gitops-test-repo}"
STATE_PATH="${2:-/tmp/honua-gitops-state}"
LOG_PATH="../../src/Honua.Server.Host/logs"
FORCE=false
CLEANUP_MODE="all"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --all)
            CLEANUP_MODE="all"
            shift
            ;;
        --repo)
            CLEANUP_MODE="repo"
            shift
            ;;
        --state)
            CLEANUP_MODE="state"
            shift
            ;;
        --logs)
            CLEANUP_MODE="logs"
            shift
            ;;
        --force|-f)
            FORCE=true
            shift
            ;;
        --help|-h)
            echo "GitOps E2E Test Cleanup Script"
            echo ""
            echo "Usage: $0 [MODE] [OPTIONS]"
            echo ""
            echo "Cleanup Modes:"
            echo "  --all               Remove everything (default)"
            echo "  --repo              Remove only Git repository"
            echo "  --state             Remove only state directory"
            echo "  --logs              Remove only Honua logs"
            echo ""
            echo "Options:"
            echo "  --force, -f         Skip confirmation prompts"
            echo "  --help, -h          Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0                  Remove everything (with confirmation)"
            echo "  $0 --force          Remove everything (no confirmation)"
            echo "  $0 --state          Remove only state directory"
            echo "  $0 --logs --force   Remove logs without confirmation"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Logging functions
log_info() {
    echo -e "${BLUE}[GitOps Cleanup]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[GitOps Cleanup]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[GitOps Cleanup]${NC} $1"
}

log_error() {
    echo -e "${RED}[GitOps Cleanup]${NC} $1"
}

# Confirmation prompt
confirm_action() {
    if [ "$FORCE" = true ]; then
        return 0
    fi

    local message="$1"
    echo ""
    echo -e "${YELLOW}WARNING:${NC} $message"
    read -p "Are you sure you want to continue? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        log_info "Cleanup cancelled"
        exit 0
    fi
}

# Clean up Git repository
cleanup_repository() {
    log_info "Cleaning up Git repository..."

    if [ -d "$REPO_PATH" ]; then
        # Check if directory exists and count files
        FILE_COUNT=$(find "$REPO_PATH" -type f 2>/dev/null | wc -l)
        COMMIT_COUNT=$(git -C "$REPO_PATH" rev-list --count HEAD 2>/dev/null || echo "0")

        log_info "Repository path: $REPO_PATH"
        log_info "Files: $FILE_COUNT"
        log_info "Commits: $COMMIT_COUNT"

        confirm_action "This will delete the Git repository and all configuration files."

        rm -rf "$REPO_PATH"
        log_success "Git repository removed"
    else
        log_info "Git repository not found at $REPO_PATH (already clean)"
    fi
}

# Clean up state directory
cleanup_state() {
    log_info "Cleaning up state directory..."

    if [ -d "$STATE_PATH" ]; then
        # Count deployment and approval records
        DEPLOYMENT_COUNT=$(find "$STATE_PATH/deployments" -name "*.json" 2>/dev/null | wc -l)
        APPROVAL_COUNT=$(find "$STATE_PATH/approvals" -name "*.json" 2>/dev/null | wc -l)

        log_info "State path: $STATE_PATH"
        log_info "Deployment records: $DEPLOYMENT_COUNT"
        log_info "Approval records: $APPROVAL_COUNT"

        confirm_action "This will delete all deployment and approval records."

        rm -rf "$STATE_PATH"
        log_success "State directory removed"
    else
        log_info "State directory not found at $STATE_PATH (already clean)"
    fi
}

# Clean up log files
cleanup_logs() {
    log_info "Cleaning up Honua log files..."

    if [ -d "$LOG_PATH" ]; then
        # Count log files
        LOG_COUNT=$(find "$LOG_PATH" -name "honua-*.log" -type f 2>/dev/null | wc -l)

        if [ "$LOG_COUNT" -gt 0 ]; then
            log_info "Log path: $LOG_PATH"
            log_info "Log files: $LOG_COUNT"

            confirm_action "This will delete all Honua log files."

            find "$LOG_PATH" -name "honua-*.log" -type f -delete 2>/dev/null
            log_success "Log files removed"
        else
            log_info "No Honua log files found at $LOG_PATH (already clean)"
        fi
    else
        log_info "Log directory not found at $LOG_PATH"
    fi
}

# Clean up reconciled configuration
cleanup_reconciled() {
    log_info "Cleaning up reconciled configuration..."

    local RECONCILED_PATH="$REPO_PATH/reconciled"
    if [ -d "$RECONCILED_PATH" ]; then
        log_info "Removing reconciled configuration at $RECONCILED_PATH"
        rm -rf "$RECONCILED_PATH"
        log_success "Reconciled configuration removed"
    fi
}

# Print cleanup summary
print_summary() {
    log_success "====================================="
    log_success "Cleanup Complete!"
    log_success "====================================="
    echo ""

    case $CLEANUP_MODE in
        all)
            log_info "All GitOps E2E test data has been removed"
            ;;
        repo)
            log_info "Git repository has been removed"
            log_warning "State directory and logs remain. Use --all to remove everything"
            ;;
        state)
            log_info "State directory has been removed"
            log_warning "Git repository and logs remain. Use --all to remove everything"
            ;;
        logs)
            log_info "Log files have been removed"
            log_warning "Git repository and state remain. Use --all to remove everything"
            ;;
    esac

    echo ""
    log_info "To reset the test environment:"
    echo "  1. Run ./init-test-repo.sh to recreate the test repository"
    echo "  2. Copy appsettings.test.json to Honua configuration"
    echo "  3. Start Honua server"
    echo "  4. Run ./validate.sh to verify setup"
    echo ""
}

# Main execution
main() {
    echo ""
    echo -e "${BLUE}================================================${NC}"
    echo -e "${BLUE}    GitOps E2E Test Cleanup${NC}"
    echo -e "${BLUE}================================================${NC}"
    echo ""

    log_info "Cleanup mode: $CLEANUP_MODE"
    if [ "$FORCE" = true ]; then
        log_warning "Force mode enabled (no confirmations)"
    fi
    echo ""

    case $CLEANUP_MODE in
        all)
            log_info "Cleaning up ALL test data..."
            cleanup_repository
            cleanup_state
            cleanup_logs
            ;;
        repo)
            cleanup_repository
            ;;
        state)
            cleanup_state
            ;;
        logs)
            cleanup_logs
            ;;
        *)
            log_error "Unknown cleanup mode: $CLEANUP_MODE"
            exit 1
            ;;
    esac

    echo ""
    print_summary
    echo -e "${BLUE}================================================${NC}"
    echo ""
}

# Run main function
main
