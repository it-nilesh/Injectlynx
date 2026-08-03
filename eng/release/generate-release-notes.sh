#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
changelog_path="${2:-CHANGELOG.md}"
output_path="${3:-release-notes.md}"

if [[ -z "$version" ]]; then
  echo "Usage: bash eng/release/generate-release-notes.sh VERSION [CHANGELOG] [OUTPUT]" >&2
  exit 1
fi

version="${version#v}"

if [[ ! -f "$changelog_path" ]]; then
  echo "Changelog file was not found: $changelog_path" >&2
  exit 1
fi

section_file="$(mktemp)"
trap 'rm -f "$section_file"' EXIT

awk -v version="$version" '
  /^## / {
    if (capture) {
      exit
    }

    if ($0 == "## " version || $0 == "## [" version "]" || $0 == "## Unreleased") {
      capture = 1
      next
    }
  }

  capture {
    print
  }
' "$changelog_path" > "$section_file"

if [[ ! -s "$section_file" ]]; then
  echo "No changelog section found for $version or Unreleased." >&2
  exit 1
fi

{
  echo "# Injectlynx $version"
  echo
  echo "## NuGet Package"
  echo
  echo "- [Injectlynx](https://www.nuget.org/packages/Injectlynx/$version)"
  echo
  echo "## Changes"
  echo
  sed '/^[[:space:]]*$/d' "$section_file"
  echo
  echo "## Supported Application Targets"
  echo
  echo "- .NET 8"
  echo "- .NET 9"
  echo "- .NET 10"
} > "$output_path"

echo "Wrote release notes to $output_path"
