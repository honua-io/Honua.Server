#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/src/Honua.Cli/Honua.Cli.csproj"

exec dotnet run --project "$PROJECT" -- "$@"
