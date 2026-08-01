#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
artifacts_dir="$repo_root/artifacts/performance"
thresholds="$repo_root/eng/performance/thresholds.json"
benchmark_binary="$repo_root/benchmarks/Injectlynx.Benchmarks/bin/Release/net10.0/Injectlynx.Benchmarks"
gate_dll="$repo_root/eng/tools/Injectlynx.PerformanceGate/bin/Release/net10.0/Injectlynx.PerformanceGate.dll"

echo "Injectlynx performance gate validation"
echo "Repository: $repo_root"
echo "Artifacts: $artifacts_dir"

mkdir -p "$artifacts_dir"

dotnet build "$repo_root/benchmarks/Injectlynx.Benchmarks/Injectlynx.Benchmarks.csproj" -c Release --no-restore -nr:false /p:UseSharedCompilation=false
dotnet build "$repo_root/eng/tools/Injectlynx.PerformanceGate/Injectlynx.PerformanceGate.csproj" -c Release --no-restore -nr:false /p:UseSharedCompilation=false

"$benchmark_binary" --list flat

dotnet "$gate_dll" run "$thresholds"
echo "Performance gate validation succeeded."
