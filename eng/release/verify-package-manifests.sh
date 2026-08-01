#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
packages_dir="$repo_root/artifacts/packages"
manifests_dir="$repo_root/artifacts/manifests"
package_version="${PACKAGE_VERSION:-1.0.0}"

echo "Injectlynx package payload manifest verification"
echo "Repository: $repo_root"
echo "Packages: $packages_dir"
echo "Manifests: $manifests_dir"

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1" >&2
    exit 2
  fi
}

write_manifest() {
  local package="$1"
  local package_name
  local extract_dir
  local manifest_path

  package_name="$(basename "$package")"
  extract_dir="$(mktemp -d /private/tmp/injectlynx-manifest.XXXXXX)"
  manifest_path="$manifests_dir/$package_name.payload.sha256"

  unzip -q "$package" -d "$extract_dir"

  # NuGet creates a random core-properties part and relationship. Exclude these
  # package-container files so the manifest tracks the actual shipped payload.
  rm -rf "$extract_dir/package/services/metadata"
  rm -f "$extract_dir/_rels/.rels"

  (
    cd "$extract_dir"
    find . -type f -print | LC_ALL=C sort | while IFS= read -r file; do
      shasum -a 256 "$file"
    done
  ) > "$manifest_path"

  rm -rf "$extract_dir"
  echo "Wrote payload manifest: $manifest_path"
}

require_command unzip
require_command find
require_command shasum

mkdir -p "$manifests_dir"

for package in \
  "$packages_dir/Injectlynx.$package_version.nupkg"; do
  if [[ ! -f "$package" ]]; then
    echo "Required package not found: $package" >&2
    exit 2
  fi

  write_manifest "$package"
done

echo "Package payload manifest verification succeeded."
