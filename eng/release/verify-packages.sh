#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
packages_dir="$repo_root/artifacts/packages"
checksums_dir="$repo_root/artifacts/checksums"
package_version="${PACKAGE_VERSION:-1.0.0}"

echo "Injectlynx release package verification"
echo "Repository: $repo_root"
echo "Packages: $packages_dir"
echo "Checksums: $checksums_dir"

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1" >&2
    exit 2
  fi
}

require_file() {
  if [[ ! -f "$1" ]]; then
    echo "Required package not found: $1" >&2
    exit 2
  fi
}

package_contains() {
  local package="$1"
  local entry="$2"
  local entries

  entries="$(unzip -Z1 "$package")"
  if ! grep -Fx "$entry" <<< "$entries" >/dev/null; then
    echo "Package $(basename "$package") is missing required entry: $entry" >&2
    exit 1
  fi
}

package_not_contains_prefix() {
  local package="$1"
  local prefix="$2"
  local entries

  entries="$(unzip -Z1 "$package")"
  if grep -F "$prefix" <<< "$entries" >/dev/null; then
    echo "Package $(basename "$package") contains forbidden entry prefix: $prefix" >&2
    exit 1
  fi
}

write_checksum() {
  local package="$1"
  local checksum_file="$checksums_dir/$(basename "$package").sha256"

  (
    cd "$(dirname "$package")"
    shasum -a 256 "$(basename "$package")"
  ) > "$checksum_file"

  echo "Wrote checksum: $checksum_file"
}

require_command unzip
require_command grep
require_command shasum

primary="$packages_dir/Injectlynx.$package_version.nupkg"

require_file "$primary"

echo "Verifying primary package contents..."
package_contains "$primary" "README.md"
package_contains "$primary" "buildTransitive/Injectlynx.props"
package_contains "$primary" "lib/netstandard2.0/Injectlynx.dll"
package_contains "$primary" "analyzers/dotnet/cs/Injectlynx.Generator.dll"
package_contains "$primary" "analyzers/dotnet/cs/Injectlynx.Generator.pdb"
package_contains "$primary" "analyzers/dotnet/cs/Injectlynx.Core.dll"
package_contains "$primary" "analyzers/dotnet/cs/Injectlynx.Core.pdb"
package_not_contains_prefix "$primary" "analyzers/dotnet/cs/Microsoft.CodeAnalysis"

mkdir -p "$checksums_dir"
write_checksum "$primary"

echo "Release package verification succeeded."
