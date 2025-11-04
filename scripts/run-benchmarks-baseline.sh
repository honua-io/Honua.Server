#!/bin/bash

# Run performance benchmarks and establish baseline
# Usage: ./scripts/run-benchmarks-baseline.sh [filter]

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BENCHMARKS_DIR="$PROJECT_ROOT/tests/Honua.Server.Benchmarks"
BASELINE_DIR="$PROJECT_ROOT/benchmarks/baseline"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)

# Parse arguments
FILTER="${1:-*}"

echo "================================================="
echo "Honua Performance Benchmarks - Baseline Run"
echo "================================================="
echo "Filter: $FILTER"
echo "Timestamp: $TIMESTAMP"
echo "================================================="
echo ""

# Create baseline directory if it doesn't exist
mkdir -p "$BASELINE_DIR"

# Build project in Release mode
echo "Building project in Release mode..."
dotnet build -c Release "$PROJECT_ROOT/HonuaIO.sln"

# Run benchmarks
echo ""
echo "Running benchmarks..."
cd "$BENCHMARKS_DIR"

dotnet run -c Release \
  --no-build \
  --filter "$FILTER" \
  --exporters json markdown csv

# Copy results to baseline directory
RESULTS_DIR="$BENCHMARKS_DIR/BenchmarkDotNet.Artifacts/results"

if [ -d "$RESULTS_DIR" ]; then
  echo ""
  echo "Copying results to baseline directory..."

  # Find the latest JSON results file
  LATEST_JSON=$(find "$RESULTS_DIR" -name "*.json" -type f -printf '%T@ %p\n' | sort -rn | head -1 | cut -d' ' -f2-)

  if [ -n "$LATEST_JSON" ]; then
    # Save with timestamp
    cp "$LATEST_JSON" "$BASELINE_DIR/baseline-$TIMESTAMP.json"

    # Update latest symlink
    ln -sf "baseline-$TIMESTAMP.json" "$BASELINE_DIR/latest.json"

    echo "Baseline saved: $BASELINE_DIR/baseline-$TIMESTAMP.json"
    echo "Latest symlink updated: $BASELINE_DIR/latest.json"
  fi

  # Copy markdown summary
  LATEST_MD=$(find "$RESULTS_DIR" -name "*.md" -type f -printf '%T@ %p\n' | sort -rn | head -1 | cut -d' ' -f2-)

  if [ -n "$LATEST_MD" ]; then
    cp "$LATEST_MD" "$BASELINE_DIR/baseline-$TIMESTAMP.md"
    echo "Markdown summary saved: $BASELINE_DIR/baseline-$TIMESTAMP.md"
  fi
else
  echo "Error: Results directory not found: $RESULTS_DIR"
  exit 1
fi

# Generate summary
echo ""
echo "================================================="
echo "Baseline Results Summary"
echo "================================================="
cat "$BASELINE_DIR/baseline-$TIMESTAMP.md"
echo "================================================="
echo ""

# Generate performance metrics summary
echo "Generating performance metrics summary..."
cat > "$BASELINE_DIR/baseline-$TIMESTAMP-summary.txt" << EOF
Honua Performance Baseline
==========================

Date: $(date -r "$BASELINE_DIR/baseline-$TIMESTAMP.json" +"%Y-%m-%d %H:%M:%S")
Filter: $FILTER

System Information:
- OS: $(uname -s) $(uname -r)
- CPU: $(grep "model name" /proc/cpuinfo | head -1 | cut -d: -f2 | xargs || echo "N/A")
- CPU Cores: $(nproc)
- Memory: $(free -h | grep "Mem:" | awk '{print $2}')
- .NET: $(dotnet --version)

Results Location:
- JSON: $BASELINE_DIR/baseline-$TIMESTAMP.json
- Markdown: $BASELINE_DIR/baseline-$TIMESTAMP.md
- Summary: $BASELINE_DIR/baseline-$TIMESTAMP-summary.txt

Latest Baseline: $BASELINE_DIR/latest.json
EOF

cat "$BASELINE_DIR/baseline-$TIMESTAMP-summary.txt"

echo ""
echo "================================================="
echo "Baseline run completed successfully!"
echo "================================================="
echo ""
echo "Next steps:"
echo "1. Review the results in: $BASELINE_DIR/baseline-$TIMESTAMP.md"
echo "2. Compare future runs with: ./scripts/compare-benchmark-results.sh"
echo "3. Track performance trends over time"
echo ""
