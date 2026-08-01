#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
sbom_dir="$repo_root/artifacts/sbom"
package_list_path="$sbom_dir/dotnet-packages.json"
sbom_path="$sbom_dir/injectlynx.cdx.json"

echo "Injectlynx SBOM generation"
echo "Repository: $repo_root"
echo "SBOM: $sbom_path"

mkdir -p "$sbom_dir"

dotnet list "$repo_root/Injectlynx.slnx" package \
  --include-transitive \
  --format json \
  --output-version 1 \
  --no-restore > "$package_list_path"

dotnet run --project "$repo_root/eng/tools/Injectlynx.Sbom/Injectlynx.Sbom.csproj" -- "$package_list_path" "$sbom_path" "$repo_root"

if ! grep -Fq '"bomFormat": "CycloneDX"' "$sbom_path"; then
  echo "Generated SBOM is missing CycloneDX marker." >&2
  exit 1
fi

if ! grep -Fq '"components": [' "$sbom_path"; then
  echo "Generated SBOM does not contain components." >&2
  exit 1
fi

echo "SBOM generation succeeded."
