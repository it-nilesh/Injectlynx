#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
rid="${1:-osx-arm64}"
tfm="${2:-net10.0}"
no_restore="${NO_RESTORE:-0}"

echo "Injectlynx trimming validation"
echo "Repository: $repo_root"
echo "Runtime identifier: $rid"
echo "Target framework: $tfm"

projects=(
  "$repo_root/samples/WebApi/WebApi.csproj"
  "$repo_root/samples/WorkerService/WorkerService.csproj"
)

for project in "${projects[@]}"; do
  echo "Publishing trimmed: $project"
  command=(
    dotnet publish "$project"
    -c Release \
    -f "$tfm" \
    -r "$rid" \
    --self-contained true \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial \
    -p:EnableTrimAnalyzer=true \
    -p:SuppressTrimAnalysisWarnings=false
  )

  if [[ "$no_restore" == "1" ]]; then
    command+=(--no-restore)
  fi

  "${command[@]}"
done

echo "Trimming validation succeeded."
