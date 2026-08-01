#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
rid="${1:-osx-arm64}"

echo "Injectlynx Native AOT validation"
echo "Repository: $repo_root"
echo "Runtime identifier: $rid"

generator_dll="$repo_root/src/Injectlynx.Generator/bin/Release/netstandard2.0/Injectlynx.Generator.dll"
if [[ ! -f "$generator_dll" ]]; then
  echo "Generator Release output not found: $generator_dll" >&2
  echo "Run: dotnet build src/Injectlynx.Generator/Injectlynx.Generator.csproj -c Release" >&2
  exit 2
fi

dotnet publish "$repo_root/samples/NativeAot/NativeAot.csproj" -c Release -r "$rid" --no-restore

publish_dir="$repo_root/samples/NativeAot/bin/Release/net10.0/$rid/publish"
echo "Native AOT publish output: $publish_dir"
