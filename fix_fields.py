#!/usr/bin/env python3
"""
Script to fix field naming issues by replacing references to fields that were renamed
with underscore prefixes but not all references were updated.
"""

import re
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

def get_build_errors():
    """Run build and extract CS0103 errors."""
    print("Building project to capture errors...")
    result = subprocess.run(
        ["dotnet", "build", "src/Honua.Server.Host/Honua.Server.Host.csproj", "--nologo"],
        cwd="/home/mike/projects/Honua.Server",
        capture_output=True,
        text=True
    )

    # Parse errors: file(line,col): error CS0103: The name '_field' does not exist
    error_pattern = r"^(.+?)\((\d+),(\d+)\): error CS0103: The name '([^']+)' does not exist"

    errors = []
    for line in (result.stdout + result.stderr).split('\n'):
        match = re.match(error_pattern, line)
        if match:
            file_path, line_num, col_num, field_name = match.groups()
            errors.append({
                'file': file_path,
                'line': int(line_num),
                'col': int(col_num),
                'field': field_name
            })

    print(f"Found {len(errors)} CS0103 errors")
    return errors

def group_errors_by_file(errors):
    """Group errors by file for efficient processing."""
    by_file = defaultdict(list)
    for error in errors:
        by_file[error['file']].append(error)
    return by_file

def fix_file(file_path, errors_in_file):
    """Fix all field references in a single file."""
    print(f"Fixing {file_path}...")

    # Read file
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except Exception as e:
        print(f"  Error reading file: {e}")
        return 0

    # Track which lines we've modified
    modified_lines = set()
    fixes_made = 0

    # Sort errors by line number (descending) to avoid line number shifts
    errors_sorted = sorted(errors_in_file, key=lambda e: e['line'], reverse=True)

    for error in errors_sorted:
        line_idx = error['line'] - 1  # Convert to 0-based index
        if line_idx < 0 or line_idx >= len(lines):
            continue

        line = lines[line_idx]
        field = error['field']

        # The error says the field doesn't exist - it should be without underscore
        # We need to replace references without underscore with underscore version
        # But the error already shows the underscore version, so we need to determine
        # what the actual incorrect reference is

        # If field is _field, the actual code likely has 'field' (without underscore)
        # If field already has underscore, it means the field definition exists but
        # the reference is wrong - skip these as they might be more complex

        if not field.startswith('_'):
            continue  # Field doesn't have underscore in error, skip

        # The error is about _field not existing, which means:
        # - Either the code has _field but should have field (unlikely for private fields)
        # - Or the code has field but should have _field (more likely)

        # Let's check what's actually in the code at that position
        col = error['col'] - 1  # Convert to 0-based

        # Extract the identifier at that position
        # Look for word boundaries around the position
        identifier_pattern = r'\b\w+\b'
        for match in re.finditer(identifier_pattern, line):
            if match.start() <= col < match.end():
                actual_text = match.group()

                # If actual text is the underscore version, something is wrong
                if actual_text == field:
                    # The code has _field but it doesn't exist - this is the error
                    # We need to find what it should be - likely without underscore
                    correct_field = field[1:]  # Remove underscore

                    # Replace this occurrence
                    new_line = line[:match.start()] + correct_field + line[match.end():]
                    lines[line_idx] = new_line
                    modified_lines.add(line_idx)
                    fixes_made += 1
                    print(f"  Line {error['line']}: '{field}' -> '{correct_field}'")
                    break
                elif actual_text + '_' == field or '_' + actual_text == field:
                    # The code has 'field' but should have '_field'
                    # Replace this occurrence
                    new_line = line[:match.start()] + field + line[match.end():]
                    lines[line_idx] = new_line
                    modified_lines.add(line_idx)
                    fixes_made += 1
                    print(f"  Line {error['line']}: '{actual_text}' -> '{field}'")
                    break

    # Write back if we made changes
    if fixes_made > 0:
        try:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            print(f"  Saved {fixes_made} fixes")
        except Exception as e:
            print(f"  Error writing file: {e}")
            return 0

    return fixes_made

def main():
    """Main function."""
    # Get all errors
    errors = get_build_errors()
    if not errors:
        print("No CS0103 errors found!")
        return 0

    # Group by file
    errors_by_file = group_errors_by_file(errors)
    print(f"\nErrors found in {len(errors_by_file)} files\n")

    # Fix each file
    total_fixes = 0
    files_modified = 0

    for file_path, file_errors in errors_by_file.items():
        fixes = fix_file(file_path, file_errors)
        if fixes > 0:
            total_fixes += fixes
            files_modified += 1

    print(f"\n{'='*60}")
    print(f"Summary:")
    print(f"  Files modified: {files_modified}")
    print(f"  Total fixes: {total_fixes}")
    print(f"{'='*60}\n")

    return total_fixes

if __name__ == '__main__':
    total = main()
    sys.exit(0 if total > 0 else 1)
