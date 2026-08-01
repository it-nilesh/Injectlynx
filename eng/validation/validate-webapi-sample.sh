#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

echo "Injectlynx Web API sample validation"
echo "Repository: $repo_root"

for tfm in net8.0 net9.0 net10.0; do
  dotnet build "$repo_root/samples/WebApi/WebApi.csproj" -f "$tfm" --no-restore
done
echo "Web API sample validation succeeded."
