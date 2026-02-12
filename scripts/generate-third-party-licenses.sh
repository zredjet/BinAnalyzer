#!/usr/bin/env bash
set -euo pipefail

# Generate THIRD-PARTY-LICENSES.txt from NuGet package dependencies.
# Uses `dotnet list package --format json` and NuGet API to extract license info.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_FILE="$REPO_ROOT/THIRD-PARTY-LICENSES.txt"
CLI_PROJECT="$REPO_ROOT/src/BinAnalyzer.Cli/BinAnalyzer.Cli.csproj"

NUGET_FLAT_CONTAINER="https://api.nuget.org/v3-flatcontainer"

# Get package list as JSON
package_json=$(dotnet list "$CLI_PROJECT" package --include-transitive --format json)

# Extract package id and version pairs
# Format: "id version" per line
packages=$(echo "$package_json" | \
  python3 -c "
import json, sys
data = json.load(sys.stdin)
for project in data.get('projects', []):
    for framework in project.get('frameworks', []):
        for pkg_group in ['topLevelPackages', 'transitivePackages']:
            for pkg in framework.get(pkg_group, []):
                print(pkg['id'] + ' ' + pkg['resolvedVersion'])
" | sort -u)

if [ -z "$packages" ]; then
  echo "ERROR: No packages found." >&2
  exit 1
fi

# Write header
cat > "$OUTPUT_FILE" <<'HEADER'
Third-Party Software Licenses
==============================

This file lists the third-party NuGet packages used by BinAnalyzer
and their license information.

HEADER

# Process each package
while IFS=' ' read -r pkg_id pkg_version; do
  echo "Processing: $pkg_id $pkg_version"

  # NuGet flat container uses lowercase IDs
  pkg_id_lower=$(echo "$pkg_id" | tr '[:upper:]' '[:lower:]')
  nuspec_url="$NUGET_FLAT_CONTAINER/$pkg_id_lower/$pkg_version/$pkg_id_lower.nuspec"

  nuspec=$(curl -sS --fail "$nuspec_url" 2>/dev/null) || {
    echo "  WARNING: Failed to fetch nuspec for $pkg_id $pkg_version" >&2
    cat >> "$OUTPUT_FILE" <<EOF
Package: $pkg_id
Version: $pkg_version
License: (failed to retrieve)
----------------------------------------

EOF
    continue
  }

  # Extract license info from nuspec XML
  # Try <license> element first, then <licenseUrl>
  license=$(echo "$nuspec" | sed -n 's/.*<license[^>]*>\(.*\)<\/license>.*/\1/p' | head -1)
  if [ -z "$license" ]; then
    license=$(echo "$nuspec" | sed -n 's/.*<licenseUrl>\(.*\)<\/licenseUrl>.*/\1/p' | head -1)
    if [ -n "$license" ]; then
      license="See $license"
    else
      license="(not specified)"
    fi
  fi

  copyright=$(echo "$nuspec" | sed -n 's/.*<copyright>\(.*\)<\/copyright>.*/\1/p' | head -1)
  if [ -z "$copyright" ]; then
    copyright="(not specified)"
  fi

  project_url=$(echo "$nuspec" | sed -n 's/.*<projectUrl>\(.*\)<\/projectUrl>.*/\1/p' | head -1)

  cat >> "$OUTPUT_FILE" <<EOF
Package: $pkg_id
Version: $pkg_version
License: $license
Copyright: $copyright
EOF

  if [ -n "$project_url" ]; then
    echo "Project URL: $project_url" >> "$OUTPUT_FILE"
  fi

  echo "----------------------------------------" >> "$OUTPUT_FILE"
  echo "" >> "$OUTPUT_FILE"

done <<< "$packages"

echo ""
echo "Generated: $OUTPUT_FILE"
