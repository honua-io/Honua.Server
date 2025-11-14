#!/usr/bin/env python3
"""
Fix remaining field naming issues by analyzing CS0103 errors
and adding underscore prefixes to field declarations.
"""

import re
import subprocess
from pathlib import Path
from collections import defaultdict

def get_errors():
    """Get all CS0103 errors from build."""
    result = subprocess.run(
        ["dotnet", "build", "src/Honua.Server.Host/Honua.Server.Host.csproj", "--nologo"],
        cwd="/home/mike/projects/Honua.Server",
        capture_output=True,
        text=True
    )

    # Parse: file(line,col): error CS0103: The name '_field' does not exist
    pattern = r"^(.+?)\((\d+),(\d+)\): error CS0103: The name '([^']+)' does not exist"
    errors = []

    for line in (result.stdout + result.stderr).split('\n'):
        match = re.match(pattern, line)
        if match:
            file_path, line_num, col, field = match.groups()
            errors.append({
                'file': file_path,
                'line': int(line_num),
                'field': field
            })

    return errors

def fix_field_declarations(errors):
    """Fix field declarations by adding underscore prefixes."""

    # Group by file and field
    fixes = defaultdict(set)
    for error in errors:
        field = error['field']
        if field.startswith('_'):
            # Remove leading underscore to get original field name
            original = field[1:]
            fixes[error['file']].add(original)

    total_fixes = 0
    files_modified = 0

    for file_path, fields in fixes.items():
        print(f"\nFixing {file_path}")
        print(f"  Fields to fix: {', '.join(sorted(fields))}")

        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()

            original_content = content

            for field in fields:
                # Pattern to match private field declarations
                # Matches various formats:
                # private readonly Type? fieldName;
                # private readonly Type fieldName;
                # private Type? fieldName;
                # private Type fieldName;
                # Also handles arrays, generics, etc.

                # First try: private (readonly) Type fieldName;
                pattern1 = rf'(private\s+(?:readonly\s+)?[A-Za-z0-9_<>?,\[\]\s]+\s+)({field})(\s*[;=])'
                if re.search(pattern1, content):
                    content = re.sub(pattern1, rf'\1_{field}\3', content)
                    print(f"    ✓ Fixed field declaration: {field} -> _{field}")

                # Fix constructor assignments: this.fieldName =
                pattern2 = rf'(\bthis\.)({field})(\s*=)'
                content = re.sub(pattern2, rf'\1_{field}\3', content)

                # Fix direct field access: fieldName (but not _fieldName)
                # This is tricky - we need to be careful not to match:
                # - Parameter names
                # - Local variables
                # - Property names
                # Only fix if it's after 'this.' or at start of expression

                # Fix: await this.fieldName or await _fieldName
                pattern3 = rf'(\bthis\.)({field})(\W)'
                content = re.sub(pattern3, rf'\1_{field}\3', content)

            if content != original_content:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(content)
                total_fixes += 1
                files_modified += 1
                print(f"  ✓ Saved changes")
            else:
                print(f"  - No changes made")

        except Exception as e:
            print(f"  ✗ Error: {e}")

    return files_modified, total_fixes

def main():
    print("Getting build errors...")
    errors = get_errors()
    print(f"Found {len(errors)} CS0103 errors\n")

    if not errors:
        print("No errors to fix!")
        return

    files, fixes = fix_field_declarations(errors)

    print("\n" + "="*60)
    print(f"Summary:")
    print(f"  Files modified: {files}")
    print(f"  Total fixes: {fixes}")
    print("="*60)

    # Rebuild to verify
    print("\nRebuilding...")
    result = subprocess.run(
        ["dotnet", "build", "src/Honua.Server.Host/Honua.Server.Host.csproj", "--nologo"],
        cwd="/home/mike/projects/Honua.Server",
        capture_output=True,
        text=True
    )

    remaining = len([l for l in (result.stdout + result.stderr).split('\n') if 'error CS0103' in l])
    print(f"Remaining CS0103 errors: {remaining}")

if __name__ == '__main__':
    main()
