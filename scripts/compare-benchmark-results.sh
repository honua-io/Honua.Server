#!/bin/bash

# Compare benchmark results and detect performance regressions
# Usage: ./scripts/compare-benchmark-results.sh baseline.json current.json

set -e

BASELINE_FILE="$1"
CURRENT_FILE="$2"

# Validation
if [ -z "$BASELINE_FILE" ] || [ -z "$CURRENT_FILE" ]; then
  echo "Usage: $0 <baseline.json> <current.json>"
  exit 1
fi

if [ ! -f "$BASELINE_FILE" ]; then
  echo "Error: Baseline file not found: $BASELINE_FILE"
  exit 1
fi

if [ ! -f "$CURRENT_FILE" ]; then
  echo "Error: Current results file not found: $CURRENT_FILE"
  exit 1
fi

echo "# Performance Benchmark Comparison"
echo ""
echo "**Baseline**: \`$(basename "$BASELINE_FILE")\`"
echo "**Current**: \`$(basename "$CURRENT_FILE")\`"
echo ""

# Check if jq is available
if ! command -v jq &> /dev/null; then
  echo "⚠️ Warning: jq is not installed. Install jq for detailed comparison."
  echo ""
  echo "## Results"
  echo ""
  echo "Baseline and current results are available in JSON format."
  echo "Manual comparison required."
  exit 0
fi

# Extract benchmark results
BASELINE_BENCHMARKS=$(jq -r '.Benchmarks[] | "\(.FullName)|\(.Statistics.Mean)|\(.Memory.BytesAllocatedPerOperation // 0)"' "$BASELINE_FILE" 2>/dev/null || echo "")
CURRENT_BENCHMARKS=$(jq -r '.Benchmarks[] | "\(.FullName)|\(.Statistics.Mean)|\(.Memory.BytesAllocatedPerOperation // 0)"' "$CURRENT_FILE" 2>/dev/null || echo "")

if [ -z "$BASELINE_BENCHMARKS" ] || [ -z "$CURRENT_BENCHMARKS" ]; then
  echo "⚠️ Warning: Unable to parse benchmark results."
  echo "Ensure the JSON files are in BenchmarkDotNet format."
  exit 1
fi

# Create associative arrays for comparison
declare -A baseline_mean
declare -A baseline_memory
declare -A current_mean
declare -A current_memory

# Parse baseline
while IFS='|' read -r name mean memory; do
  baseline_mean["$name"]=$mean
  baseline_memory["$name"]=$memory
done <<< "$BASELINE_BENCHMARKS"

# Parse current
while IFS='|' read -r name mean memory; do
  current_mean["$name"]=$mean
  current_memory["$name"]=$memory
done <<< "$CURRENT_BENCHMARKS"

# Regression thresholds
CRITICAL_THRESHOLD=10   # 10% regression
WARNING_THRESHOLD=20    # 20% regression

REGRESSIONS=0
WARNINGS=0
IMPROVEMENTS=0

echo "## Summary"
echo ""
echo "| Benchmark | Baseline | Current | Change | Status |"
echo "|-----------|----------|---------|--------|--------|"

# Compare benchmarks
for name in "${!current_mean[@]}"; do
  baseline=${baseline_mean[$name]:-0}
  current=${current_mean[$name]:-0}

  if [ "$baseline" == "0" ]; then
    echo "| $name | N/A | ${current}ns | NEW | ✨ |"
    continue
  fi

  # Calculate percentage change
  change=$(awk "BEGIN {printf \"%.2f\", (($current - $baseline) / $baseline) * 100}")

  # Determine status
  if (( $(echo "$change > $CRITICAL_THRESHOLD" | bc -l) )); then
    status="❌ REGRESSION"
    REGRESSIONS=$((REGRESSIONS + 1))
  elif (( $(echo "$change > $WARNING_THRESHOLD" | bc -l) )); then
    status="⚠️ WARNING"
    WARNINGS=$((WARNINGS + 1))
  elif (( $(echo "$change < -10" | bc -l) )); then
    status="✅ IMPROVEMENT"
    IMPROVEMENTS=$((IMPROVEMENTS + 1))
  else
    status="✓"
  fi

  echo "| $(basename "$name") | ${baseline}ns | ${current}ns | ${change}% | $status |"
done

echo ""
echo "## Analysis"
echo ""
echo "- ❌ Critical regressions (>$CRITICAL_THRESHOLD%): $REGRESSIONS"
echo "- ⚠️ Warnings (>$WARNING_THRESHOLD%): $WARNINGS"
echo "- ✅ Improvements (>10%): $IMPROVEMENTS"
echo ""

# Memory comparison
echo "## Memory Allocation Changes"
echo ""
echo "| Benchmark | Baseline | Current | Change | Status |"
echo "|-----------|----------|---------|--------|--------|"

for name in "${!current_memory[@]}"; do
  baseline=${baseline_memory[$name]:-0}
  current=${current_memory[$name]:-0}

  if [ "$baseline" == "0" ]; then
    continue
  fi

  change=$(awk "BEGIN {printf \"%.2f\", (($current - $baseline) / $baseline) * 100}")

  if (( $(echo "$change > 20" | bc -l) )); then
    status="⚠️"
  elif (( $(echo "$change < -10" | bc -l) )); then
    status="✅"
  else
    status="✓"
  fi

  baseline_mb=$(awk "BEGIN {printf \"%.2f\", $baseline / 1048576}")
  current_mb=$(awk "BEGIN {printf \"%.2f\", $current / 1048576}")

  echo "| $(basename "$name") | ${baseline_mb}MB | ${current_mb}MB | ${change}% | $status |"
done

echo ""

# Exit with error if critical regressions detected
if [ "$REGRESSIONS" -gt 0 ]; then
  echo "## ❌ Action Required"
  echo ""
  echo "Critical performance regressions detected!"
  echo ""
  echo "**Next Steps**:"
  echo "1. Review the benchmarks showing regressions"
  echo "2. Profile the affected code paths"
  echo "3. Optimize or justify the performance change"
  echo "4. Update baseline if the change is intentional"
  echo ""
  exit 1
fi

if [ "$WARNINGS" -gt 0 ]; then
  echo "## ⚠️ Review Recommended"
  echo ""
  echo "Performance warnings detected. Consider reviewing the affected benchmarks."
  echo ""
fi

if [ "$IMPROVEMENTS" -gt 0 ]; then
  echo "## 🎉 Performance Improvements"
  echo ""
  echo "Great work! Some benchmarks show significant improvements."
  echo ""
fi

exit 0
