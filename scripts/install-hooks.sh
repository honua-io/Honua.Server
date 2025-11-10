#!/bin/bash
# Install git hooks for Honua.Server

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
HOOKS_DIR="$REPO_ROOT/.githooks"
GIT_HOOKS_DIR="$REPO_ROOT/.git/hooks"

echo "Installing git hooks..."

# Configure git to use .githooks directory
git config core.hooksPath "$HOOKS_DIR"

echo "✅ Git hooks installed successfully!"
echo "Pre-commit hook will run on every commit."
echo ""
echo "To skip hooks (not recommended), use: git commit --no-verify"
