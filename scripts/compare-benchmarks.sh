#!/bin/bash
set -e

# Compare benchmark results against baseline
# Usage: ./scripts/compare-benchmarks.sh <baseline.json> <current.json>

BASELINE_FILE="$1"
CURRENT_FILE="$2"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Regression threshold (10% slower is a regression)
REGRESSION_THRESHOLD=10

if [ -z "$BASELINE_FILE" ] || [ -z "$CURRENT_FILE" ]; then
    echo -e "${RED}Usage: $0 <baseline.json> <current.json>${NC}"
    exit 1
fi

if [ ! -f "$BASELINE_FILE" ]; then
    echo -e "${RED}Error: Baseline file not found: $BASELINE_FILE${NC}"
    exit 1
fi

if [ ! -f "$CURRENT_FILE" ]; then
    echo -e "${RED}Error: Current results file not found: $CURRENT_FILE${NC}"
    exit 1
fi

echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}Benchmark Comparison${NC}"
echo -e "${BLUE}======================================${NC}"
echo ""
echo -e "Baseline: ${YELLOW}$BASELINE_FILE${NC}"
echo -e "Current:  ${YELLOW}$CURRENT_FILE${NC}"
echo -e "Regression threshold: ${YELLOW}${REGRESSION_THRESHOLD}%${NC}"
echo ""

# Create a Python script to compare the JSON files
COMPARE_SCRIPT=$(cat <<'EOF'
import json
import sys

def load_results(filename):
    with open(filename, 'r') as f:
        data = json.load(f)
    results = {}
    for benchmark in data.get('Benchmarks', []):
        full_name = benchmark.get('FullName', '')
        # Extract mean time in nanoseconds
        stats = benchmark.get('Statistics', {})
        mean = stats.get('Mean', 0)
        results[full_name] = mean
    return results

def compare_results(baseline, current, threshold):
    regressions = []
    improvements = []
    no_change = []

    all_benchmarks = set(baseline.keys()) | set(current.keys())

    for name in sorted(all_benchmarks):
        if name not in baseline:
            print(f"NEW: {name}")
            continue
        if name not in current:
            print(f"REMOVED: {name}")
            continue

        baseline_time = baseline[name]
        current_time = current[name]

        if baseline_time == 0:
            continue

        change_percent = ((current_time - baseline_time) / baseline_time) * 100

        if change_percent > threshold:
            regressions.append((name, baseline_time, current_time, change_percent))
        elif change_percent < -threshold:
            improvements.append((name, baseline_time, current_time, change_percent))
        else:
            no_change.append((name, baseline_time, current_time, change_percent))

    return regressions, improvements, no_change

def format_time(nanoseconds):
    """Format nanoseconds to human readable"""
    if nanoseconds < 1000:
        return f"{nanoseconds:.2f} ns"
    elif nanoseconds < 1_000_000:
        return f"{nanoseconds/1000:.2f} μs"
    elif nanoseconds < 1_000_000_000:
        return f"{nanoseconds/1_000_000:.2f} ms"
    else:
        return f"{nanoseconds/1_000_000_000:.2f} s"

if __name__ == '__main__':
    if len(sys.argv) != 4:
        print("Usage: script.py <baseline.json> <current.json> <threshold>")
        sys.exit(1)

    baseline_file = sys.argv[1]
    current_file = sys.argv[2]
    threshold = float(sys.argv[3])

    baseline = load_results(baseline_file)
    current = load_results(current_file)

    regressions, improvements, no_change = compare_results(baseline, current, threshold)

    # Print results
    print("\n" + "="*80)
    print("PERFORMANCE REGRESSIONS (slower than baseline)")
    print("="*80)

    if regressions:
        for name, baseline_time, current_time, change in sorted(regressions, key=lambda x: x[3], reverse=True):
            print(f"\n🔴 {name}")
            print(f"   Baseline: {format_time(baseline_time)}")
            print(f"   Current:  {format_time(current_time)}")
            print(f"   Change:   +{change:.2f}% SLOWER")
    else:
        print("\n✅ No regressions found!")

    print("\n" + "="*80)
    print("PERFORMANCE IMPROVEMENTS (faster than baseline)")
    print("="*80)

    if improvements:
        for name, baseline_time, current_time, change in sorted(improvements, key=lambda x: x[3]):
            print(f"\n🟢 {name}")
            print(f"   Baseline: {format_time(baseline_time)}")
            print(f"   Current:  {format_time(current_time)}")
            print(f"   Change:   {abs(change):.2f}% FASTER")
    else:
        print("\nNo significant improvements")

    print("\n" + "="*80)
    print("SUMMARY")
    print("="*80)
    print(f"Total benchmarks:  {len(baseline)}")
    print(f"Regressions:       {len(regressions)}")
    print(f"Improvements:      {len(improvements)}")
    print(f"No change:         {len(no_change)}")

    # Exit with error code if regressions found
    if regressions:
        print("\n⚠️  PERFORMANCE REGRESSIONS DETECTED!")
        sys.exit(1)
    else:
        print("\n✅ No performance regressions detected")
        sys.exit(0)
EOF
)

# Run comparison using Python
python3 -c "$COMPARE_SCRIPT" "$BASELINE_FILE" "$CURRENT_FILE" "$REGRESSION_THRESHOLD"
EXIT_CODE=$?

echo ""
if [ $EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}Benchmark comparison passed${NC}"
else
    echo -e "${RED}Benchmark comparison failed - regressions detected${NC}"
fi

exit $EXIT_CODE
