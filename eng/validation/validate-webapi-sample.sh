#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

echo "Injectlynx Web API sample validation"
echo "Repository: $repo_root"

dotnet build "$repo_root/samples/WebApi/WebApi.csproj" --no-restore
echo "Web API sample validation succeeded."
