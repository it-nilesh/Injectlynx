#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

echo "Injectlynx Worker Service sample validation"
echo "Repository: $repo_root"

dotnet build "$repo_root/samples/WorkerService/WorkerService.csproj" --no-restore

echo "Worker Service sample validation succeeded."
