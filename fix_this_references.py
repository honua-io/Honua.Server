#!/usr/bin/env python3
"""
Fix 'this.field' references where field is declared with underscore prefix.
"""

import re
import subprocess
from pathlib import Path
from collections import defaultdict

def get_field_access_errors():
    """Get all 'does not contain a definition for' errors."""
    result = subprocess.run(
        ["dotnet", "build", "src/Honua.Server.Host/Honua.Server.Host.csproj", "--nologo"],
        cwd="/home/mike/projects/Honua.Server",
        capture_output=True,
        text=True
    )

    # Parse: file(line,col): error CS1061: 'Type' does not contain a definition for 'field'
    pattern = r"^(.+?)\(\d+,\d+\): error CS1061: '[^']+' does not contain a definition for '([^']+)'"

    fields_by_file = defaultdict(set)

    for line in (result.stdout + result.stderr).split('\n'):
        match = re.match(pattern, line)
        if match:
            file_path, field = match.groups()
            # Skip method names and things that clearly aren't fields
            if field[0].islower() and not field.startswith('_'):
                fields_by_file[file_path].add(field)

    return fields_by_file

def fix_file_references(file_path, fields):
    """Fix this.field references in a file."""
    print(f"\nFixing {file_path}")
    print(f"  Fields: {', '.join(sorted(fields))}")

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()

        original = content
        fixes = 0

        for field in fields:
            # Check if _field is declared in the file or could be from base class
            if f'_{field}' in content or 'HealthCheckBase' in content or ': IAsyncDisposable' in content:
                # Replace this.field with this._field or _field
                # Pattern 1: this.field
                before = content
                content = re.sub(rf'\bthis\.{field}\b', f'this._{field}', content)
                if content != before:
                    fixes += 1

        if content != original:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"  ✓ Made {fixes} replacements")
            return 1
        else:
            print(f"  - No changes needed")
            return 0

    except Exception as e:
        print(f"  ✗ Error: {e}")
        return 0

def main():
    print("Analyzing field access errors...")
    errors = get_field_access_errors()

    if not errors:
        print("No field access errors found!")
        return

    print(f"\nFound errors in {len(errors)} files\n")

    fixed = 0
    for file_path, fields in errors.items():
        fixed += fix_file_references(file_path, fields)

    print(f"\n{'='*60}")
    print(f"Files fixed: {fixed}")
    print('='*60)

    # Rebuild
    print("\nRebuilding...")
    result = subprocess.run(
        ["dotnet", "build", "src/Honua.Server.Host/Honua.Server.Host.csproj", "--nologo"],
        cwd="/home/mike/projects/Honua.Server",
        capture_output=True,
        text=True
    )

    error_lines = [l for l in (result.stdout + result.stderr).split('\n') if 'error CS' in l]
    print(f"Remaining errors: {len(error_lines)}")

    # Count CS0103 specifically
    cs0103 = len([l for l in error_lines if 'CS0103' in l])
    print(f"CS0103 errors: {cs0103}")

if __name__ == '__main__':
    main()
