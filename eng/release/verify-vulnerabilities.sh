#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
security_dir="$repo_root/artifacts/security"
report_path="$security_dir/vulnerabilities.json"

echo "Injectlynx dependency vulnerability verification"
echo "Repository: $repo_root"
echo "Report: $report_path"

mkdir -p "$security_dir"

dotnet restore "$repo_root/Injectlynx.slnx" -v minimal

dotnet list "$repo_root/Injectlynx.slnx" package \
  --vulnerable \
  --include-transitive \
  --format json \
  --output-version 1 \
  --no-restore > "$report_path"

if grep -Fq '"vulnerabilities"' "$report_path"; then
  echo "Vulnerable package(s) detected. See $report_path." >&2
  cat "$report_path" >&2
  exit 1
fi

echo "Dependency vulnerability verification succeeded."
