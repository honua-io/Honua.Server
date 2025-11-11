#!/bin/bash
# Copyright (c) Honua.io. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.
#
# check-warnings.sh - Analyzer Warning Checker
#
# Purpose: Check for specific categories of analyzer warnings in the codebase
# Usage: ./scripts/check-warnings.sh [--phase <1-4>] [--category <category>] [--all] [--report]
#
# This script temporarily re-enables specific analyzer warnings to check for violations
# It's used to track progress on the analyzer warnings remediation plan

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_PROPS="$PROJECT_ROOT/Directory.Build.props"
BUILD_PROPS_BACKUP="$PROJECT_ROOT/Directory.Build.props.backup"

# Default options
PHASE=""
CATEGORY=""
CHECK_ALL=false
REPORT_ONLY=false
VERBOSE=false
OUTPUT_FILE=""

# Warning categories
declare -A PHASE1_WARNINGS=(
    ["compiler"]="CS0168;CS4014;CS0169"
    ["basic_ca"]="CA1805;CA1825;CA1826;CA1854;CA1860;CA1862;CA1866;CA1867;CA1869;CA1835;CA1844;CA1845;CA1850"
)

declare -A PHASE2_WARNINGS=(
    ["nullable"]="CS8600;CS8601;CS8602;CS8603;CS8604;CS8605;CS8606;CS8607;CS8608;CS8609;CS8610;CS8611;CS8612;CS8613;CS8614;CS8615;CS8616;CS8617;CS8618;CS8619;CS8620;CS8621;CS8622;CS8623;CS8624;CS8625;CS8626;CS8627;CS8628;CS8629;CS8630;CS8631;CS8632;CS8633;CS8634"
    ["api_design"]="CA1024;CA1028;CA1051;CA1052;CA1054;CA1055;CA1063"
    ["performance"]="CA2000;CA1816;VSTHRD003"
)

declare -A PHASE3_WARNINGS=(
    ["quality"]="CA1008;CA1019;CA1304;CA1310;CA1311;CA1508;CA1513;CA1515;CA1711;CA1716;CA1725;CA1806;CA1812;CA1814;CA1823;CA1847;CA1851;CA1852"
    ["sonar"]="S1135;S2139;S3398;S1144;S101;S127;S927;S2094;S2486;S3881;S1125;S1871;S3923;S3928;S4144;S4487;S6610;S6667"
    ["stylecop"]="SA1600;SA1601;SA1602;SA1615;SA1616;SA1617;SA1618;SA1619;SA1620;SA1621;SA1622;SA1623;SA1624;SA1400;SA1401;SA1402;SA1403;SA1404;SA1300;SA1301;SA1302;SA1303;SA1304;SA1305;SA1306;SA1307;SA1308;SA1309;SA1310"
)

# Usage information
usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Check for analyzer warnings in the Honua.Server codebase.

Options:
    --phase <1-4>          Check warnings for specific remediation phase
    --category <name>      Check specific category (compiler, nullable, api_design, etc.)
    --all                  Check all warnings (may take a long time)
    --report              Generate report only, don't fail on warnings
    --output <file>       Write report to file
    --verbose             Show detailed output
    -h, --help            Show this help message

Categories:
    Phase 1:
        compiler          - CS0168, CS4014, CS0169
        basic_ca          - CA1805, CA1826, CA1854, etc.

    Phase 2:
        nullable          - CS8600-CS8634 (nullable reference types)
        api_design        - CA1024, CA1051, CA1063, etc.
        performance       - CA2000, CA1816, VSTHRD003

    Phase 3:
        quality           - CA1008, CA1304, CA1310, etc.
        sonar             - S1135, S2139, S3398, etc.
        stylecop          - SA1600, SA1400, SA1300 (subset)

Examples:
    # Check Phase 1 warnings
    $0 --phase 1

    # Check nullable warnings
    $0 --category nullable

    # Generate report for all warnings
    $0 --all --report --output warnings-report.txt

    # Check compiler warnings with verbose output
    $0 --category compiler --verbose

Exit Codes:
    0 - No warnings found (or --report mode)
    1 - Warnings found
    2 - Error occurred

See: docs/development/analyzer-warnings-remediation.md for full remediation plan
EOF
    exit 0
}

# Parse command line arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --phase)
                PHASE="$2"
                if [[ ! "$PHASE" =~ ^[1-4]$ ]]; then
                    echo -e "${RED}Error: Phase must be 1, 2, 3, or 4${NC}" >&2
                    exit 2
                fi
                shift 2
                ;;
            --category)
                CATEGORY="$2"
                shift 2
                ;;
            --all)
                CHECK_ALL=true
                shift
                ;;
            --report)
                REPORT_ONLY=true
                shift
                ;;
            --output)
                OUTPUT_FILE="$2"
                shift 2
                ;;
            --verbose)
                VERBOSE=true
                shift
                ;;
            -h|--help)
                usage
                ;;
            *)
                echo -e "${RED}Error: Unknown option $1${NC}" >&2
                usage
                ;;
        esac
    done
}

# Backup Directory.Build.props
backup_build_props() {
    if [[ -f "$BUILD_PROPS" ]]; then
        cp "$BUILD_PROPS" "$BUILD_PROPS_BACKUP"
        if [[ "$VERBOSE" == true ]]; then
            echo -e "${BLUE}Backed up Directory.Build.props${NC}"
        fi
    else
        echo -e "${RED}Error: Directory.Build.props not found at $BUILD_PROPS${NC}" >&2
        exit 2
    fi
}

# Restore Directory.Build.props
restore_build_props() {
    if [[ -f "$BUILD_PROPS_BACKUP" ]]; then
        mv "$BUILD_PROPS_BACKUP" "$BUILD_PROPS"
        if [[ "$VERBOSE" == true ]]; then
            echo -e "${BLUE}Restored Directory.Build.props${NC}"
        fi
    fi
}

# Cleanup on exit
cleanup() {
    restore_build_props
}

trap cleanup EXIT

# Get warnings to check based on options
get_warnings_to_check() {
    local warnings=""

    if [[ "$CHECK_ALL" == true ]]; then
        # Combine all warnings
        for category in "${!PHASE1_WARNINGS[@]}"; do
            warnings="$warnings;${PHASE1_WARNINGS[$category]}"
        done
        for category in "${!PHASE2_WARNINGS[@]}"; do
            warnings="$warnings;${PHASE2_WARNINGS[$category]}"
        done
        for category in "${!PHASE3_WARNINGS[@]}"; do
            warnings="$warnings;${PHASE3_WARNINGS[$category]}"
        done
    elif [[ -n "$PHASE" ]]; then
        case $PHASE in
            1)
                for category in "${!PHASE1_WARNINGS[@]}"; do
                    warnings="$warnings;${PHASE1_WARNINGS[$category]}"
                done
                ;;
            2)
                for category in "${!PHASE2_WARNINGS[@]}"; do
                    warnings="$warnings;${PHASE2_WARNINGS[$category]}"
                done
                ;;
            3)
                for category in "${!PHASE3_WARNINGS[@]}"; do
                    warnings="$warnings;${PHASE3_WARNINGS[$category]}"
                done
                ;;
            4)
                # Phase 4 checks all warnings
                warnings="ALL"
                ;;
        esac
    elif [[ -n "$CATEGORY" ]]; then
        if [[ -n "${PHASE1_WARNINGS[$CATEGORY]:-}" ]]; then
            warnings="${PHASE1_WARNINGS[$CATEGORY]}"
        elif [[ -n "${PHASE2_WARNINGS[$CATEGORY]:-}" ]]; then
            warnings="${PHASE2_WARNINGS[$CATEGORY]}"
        elif [[ -n "${PHASE3_WARNINGS[$CATEGORY]:-}" ]]; then
            warnings="${PHASE3_WARNINGS[$CATEGORY]}"
        else
            echo -e "${RED}Error: Unknown category '$CATEGORY'${NC}" >&2
            echo -e "${YELLOW}Use --help to see available categories${NC}" >&2
            exit 2
        fi
    else
        echo -e "${RED}Error: Must specify --phase, --category, or --all${NC}" >&2
        usage
    fi

    # Remove leading semicolon
    warnings="${warnings#;}"

    echo "$warnings"
}

# Modify Directory.Build.props to enable warnings
enable_warnings() {
    local warnings="$1"

    if [[ "$warnings" == "ALL" ]]; then
        # Remove all NoWarn suppressions
        sed -i 's/<NoWarn>.*<\/NoWarn>/<NoWarn><\/NoWarn>/g' "$BUILD_PROPS"
        echo -e "${YELLOW}Enabled ALL warnings (removed all suppressions)${NC}"
    else
        # Remove specific warnings from NoWarn
        IFS=';' read -ra WARNING_ARRAY <<< "$warnings"
        for warning in "${WARNING_ARRAY[@]}"; do
            # Remove the warning from NoWarn list
            sed -i "s/;$warning//g; s/$warning;//g" "$BUILD_PROPS"
        done
        echo -e "${YELLOW}Enabled warnings: $warnings${NC}"
    fi
}

# Build solution and capture warnings
build_and_check() {
    local build_output
    local build_log="$PROJECT_ROOT/build-warnings.log"

    echo -e "${BLUE}Building solution...${NC}"

    # Build solution and capture output
    if [[ "$VERBOSE" == true ]]; then
        dotnet build "$PROJECT_ROOT/Honua.Server.sln" \
            --no-incremental \
            --verbosity normal \
            2>&1 | tee "$build_log"
    else
        dotnet build "$PROJECT_ROOT/Honua.Server.sln" \
            --no-incremental \
            --verbosity quiet \
            2>&1 > "$build_log"
    fi

    local build_exit_code=$?

    if [[ $build_exit_code -ne 0 ]]; then
        echo -e "${RED}Build failed!${NC}" >&2
        if [[ "$VERBOSE" == false ]]; then
            echo -e "${YELLOW}Run with --verbose to see full build output${NC}" >&2
        fi
        return 1
    fi

    echo "$build_log"
}

# Parse and display warnings
parse_warnings() {
    local build_log="$1"
    local warning_count
    local error_count

    # Count warnings and errors
    warning_count=$(grep -c "warning" "$build_log" 2>/dev/null || echo "0")
    error_count=$(grep -c "error" "$build_log" 2>/dev/null || echo "0")

    if [[ $warning_count -eq 0 && $error_count -eq 0 ]]; then
        echo -e "${GREEN}✓ No warnings found!${NC}"
        return 0
    fi

    echo ""
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}Warning Summary${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

    # Group warnings by code
    echo -e "\n${BLUE}Warnings by Code:${NC}"
    grep "warning" "$build_log" 2>/dev/null | \
        sed -E 's/.*warning ([A-Z]+[0-9]+).*/\1/' | \
        sort | uniq -c | sort -rn | \
        while read -r count code; do
            echo -e "  ${YELLOW}$code${NC}: $count occurrence(s)"
        done

    # Group warnings by project
    echo -e "\n${BLUE}Warnings by Project:${NC}"
    grep "warning" "$build_log" 2>/dev/null | \
        sed -E 's/.*\/([^\/]+\.csproj).*/\1/' | \
        sort | uniq -c | sort -rn | head -20 | \
        while read -r count project; do
            echo -e "  ${YELLOW}$project${NC}: $count warning(s)"
        done

    # Show sample warnings
    echo -e "\n${BLUE}Sample Warnings (first 10):${NC}"
    grep "warning" "$build_log" 2>/dev/null | head -10 | \
        while IFS= read -r line; do
            echo -e "  ${YELLOW}→${NC} $line"
        done

    echo ""
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${RED}Total Warnings: $warning_count${NC}"
    if [[ $error_count -gt 0 ]]; then
        echo -e "${RED}Total Errors: $error_count${NC}"
    fi
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
    echo -e "${BLUE}Full build log: $build_log${NC}"

    return 1
}

# Generate report file
generate_report() {
    local build_log="$1"
    local output_file="$2"

    {
        echo "Analyzer Warnings Report"
        echo "Generated: $(date)"
        echo "Project: Honua.Server"
        echo ""
        echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        echo ""

        local warning_count
        warning_count=$(grep -c "warning" "$build_log" 2>/dev/null || echo "0")

        echo "Total Warnings: $warning_count"
        echo ""

        echo "Warnings by Code:"
        echo "─────────────────"
        grep "warning" "$build_log" 2>/dev/null | \
            sed -E 's/.*warning ([A-Z]+[0-9]+).*/\1/' | \
            sort | uniq -c | sort -rn

        echo ""
        echo "Warnings by Project:"
        echo "────────────────────"
        grep "warning" "$build_log" 2>/dev/null | \
            sed -E 's/.*\/([^\/]+\.csproj).*/\1/' | \
            sort | uniq -c | sort -rn

        echo ""
        echo "All Warnings:"
        echo "─────────────"
        grep "warning" "$build_log" 2>/dev/null

    } > "$output_file"

    echo -e "${GREEN}Report saved to: $output_file${NC}"
}

# Main function
main() {
    parse_args "$@"

    if [[ ! -f "$BUILD_PROPS" ]]; then
        echo -e "${RED}Error: Directory.Build.props not found!${NC}" >&2
        echo -e "${YELLOW}Expected location: $BUILD_PROPS${NC}" >&2
        exit 2
    fi

    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}Honua.Server Analyzer Warning Checker${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""

    # Get warnings to check
    local warnings
    warnings=$(get_warnings_to_check)

    if [[ -z "$warnings" ]]; then
        echo -e "${RED}Error: No warnings to check${NC}" >&2
        exit 2
    fi

    # Backup and modify build props
    backup_build_props
    enable_warnings "$warnings"

    # Build and check
    local build_log
    build_log=$(build_and_check)
    local build_result=$?

    # Parse warnings
    local has_warnings=0
    if ! parse_warnings "$build_log"; then
        has_warnings=1
    fi

    # Generate report if requested
    if [[ -n "$OUTPUT_FILE" ]]; then
        generate_report "$build_log" "$OUTPUT_FILE"
    fi

    # Clean up build log if not in verbose mode
    if [[ "$VERBOSE" == false ]] && [[ -z "$OUTPUT_FILE" ]]; then
        rm -f "$build_log"
    fi

    # Exit with appropriate code
    if [[ "$REPORT_ONLY" == true ]]; then
        echo -e "${BLUE}Report mode: exiting with success${NC}"
        exit 0
    elif [[ $has_warnings -eq 1 ]]; then
        echo -e "${RED}✗ Warnings found${NC}"
        echo -e "${YELLOW}See docs/development/analyzer-warnings-remediation.md for remediation plan${NC}"
        exit 1
    else
        echo -e "${GREEN}✓ Success: No warnings found!${NC}"
        exit 0
    fi
}

# Run main function
main "$@"
