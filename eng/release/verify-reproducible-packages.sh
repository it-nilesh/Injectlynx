#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
work_dir="$(mktemp -d)"
first_dir="$work_dir/first"
second_dir="$work_dir/second"
first_normalized_dir="$work_dir/first-normalized"
second_normalized_dir="$work_dir/second-normalized"

echo "Injectlynx reproducible package verification"
echo "Repository: $repo_root"
echo "Workspace: $work_dir"

cleanup() {
  rm -rf "$work_dir"
}
trap cleanup EXIT

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1" >&2
    exit 2
  fi
}

pack_all() {
  local output_dir="$1"

  mkdir -p "$output_dir"
  dotnet pack "$repo_root/src/Injectlynx/Injectlynx.csproj" -c Release --no-restore -o "$output_dir" -p:ContinuousIntegrationBuild=true
}

compare_package() {
  local package_name="$1"
  local first="$first_normalized_dir/$package_name"
  local second="$second_normalized_dir/$package_name"

  if [[ ! -f "$first" || ! -f "$second" ]]; then
    echo "Expected normalized package was not produced in both pack runs: $package_name" >&2
    exit 1
  fi

  local first_hash
  local second_hash
  first_hash="$(shasum -a 256 "$first" | awk '{print $1}')"
  second_hash="$(shasum -a 256 "$second" | awk '{print $1}')"

  if [[ "$first_hash" != "$second_hash" ]]; then
    echo "Package is not byte-for-byte reproducible: $package_name" >&2
    echo "First:  $first_hash" >&2
    echo "Second: $second_hash" >&2
    exit 1
  fi

  cmp -s "$first" "$second"
  echo "Reproducible after NuGet metadata normalization: $package_name $first_hash"
}

normalize_package() {
  local package="$1"
  local output_dir="$2"
  local package_name
  local extract_dir
  local core_properties_dir
  local core_properties_file

  package_name="$(basename "$package")"
  extract_dir="$work_dir/normalize-$package_name-${output_dir##*/}"
  rm -rf "$extract_dir"
  mkdir -p "$extract_dir" "$output_dir"

  unzip -q "$package" -d "$extract_dir"

  core_properties_dir="$extract_dir/package/services/metadata/core-properties"
  core_properties_file="$(find "$core_properties_dir" -type f -name '*.psmdcp' | sort | head -n 1)"
  if [[ -z "$core_properties_file" ]]; then
    echo "Package is missing NuGet core properties metadata: $package_name" >&2
    exit 1
  fi

  mv "$core_properties_file" "$core_properties_dir/core-properties.psmdcp"
  awk '
    /<Relationship / {
      count++;
      gsub(/Target="\/package\/services\/metadata\/core-properties\/[^"]+\.psmdcp"/, "Target=\"/package/services/metadata/core-properties/core-properties.psmdcp\"");
      sub(/Id="[^"]+"/, "Id=\"R" count "\"");
    }
    { print }
  ' "$extract_dir/_rels/.rels" > "$extract_dir/_rels/.rels.normalized"
  mv "$extract_dir/_rels/.rels.normalized" "$extract_dir/_rels/.rels"

  find "$extract_dir" -exec touch -t 198001010000 {} +
  (
    cd "$extract_dir"
    LC_ALL=C find . -type f | sed 's#^\./##' | LC_ALL=C sort | zip -X -q "$output_dir/$package_name" -@
  )
}

require_command awk
require_command cmp
require_command shasum
require_command unzip
require_command zip

pack_all "$first_dir"
pack_all "$second_dir"

shopt -s nullglob

for package in "$first_dir"/*.nupkg "$first_dir"/*.snupkg; do
  normalize_package "$package" "$first_normalized_dir"
done

for package in "$second_dir"/*.nupkg "$second_dir"/*.snupkg; do
  normalize_package "$package" "$second_normalized_dir"
done

compare_package "Injectlynx.1.0.0.nupkg"

echo "Reproducible package verification succeeded."
