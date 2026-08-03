#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
service_count="${SERVICE_COUNT:-500}"
target_framework="${TARGET_FRAMEWORK:-net10.0}"
package_version="${PACKAGE_VERSION:-1.0.0}"
work_dir="$(mktemp -d)"

cleanup() {
  rm -rf "$work_dir"
}
trap cleanup EXIT

echo "Injectlynx generator performance smoke"
echo "Repository: $repo_root"
echo "Workspace: $work_dir"
echo "Services: $service_count"
echo "Target framework: $target_framework"
echo "Package version: $package_version"

package_path="$repo_root/artifacts/packages/Injectlynx.$package_version.nupkg"
if [[ ! -f "$package_path" ]]; then
  echo "Package not found. Packing $package_path..."
  dotnet pack "$repo_root/src/Injectlynx/Injectlynx.csproj" -c Debug --no-build --no-restore -o "$repo_root/artifacts/packages"
fi

mkdir -p "$work_dir/LargeConsumer/Services"

cat > "$work_dir/LargeConsumer/LargeConsumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>$target_framework</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RestoreSources>$repo_root/artifacts/packages;https://api.nuget.org/v3/index.json</RestoreSources>
    <RestorePackagesPath>$work_dir/packages</RestorePackagesPath>
    <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Injectlynx" Version="$package_version" />
  </ItemGroup>
</Project>
EOF

cat > "$work_dir/LargeConsumer/ApplicationServiceConventions.cs" <<'EOF'
using Injectlynx;

namespace LargeConsumer;

public static class ApplicationServiceConventions
{
    public static void Configure(IServiceConventionBuilder services)
    {
        services
            .FromNamespace("LargeConsumer.Services")
            .WhereNameEndsWith("Service")
            .AsMatchingInterface()
            .WithScopedLifetime();
    }
}
EOF

cat > "$work_dir/LargeConsumer/Program.cs" <<'EOF'
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInjectlynxServices();
var app = builder.Build();
app.MapGet("/", () => "ok");
app.Run();
EOF

echo "Writing $service_count service pairs..."
for index in $(seq 1 "$service_count"); do
  cat > "$work_dir/LargeConsumer/Services/IService$index.cs" <<EOF
namespace LargeConsumer.Services;

public interface IService$index
{
    int GetValue();
}
EOF

  cat > "$work_dir/LargeConsumer/Services/Service$index.cs" <<EOF
namespace LargeConsumer.Services;

public sealed class Service$index : IService$index
{
    public int GetValue() => $index;
}
EOF
done

echo "Warm build..."
dotnet build "$work_dir/LargeConsumer/LargeConsumer.csproj" -v quiet >/dev/null

echo "Measured no-restore rebuild..."
start_time="$(date +%s)"
dotnet build "$work_dir/LargeConsumer/LargeConsumer.csproj" --no-restore -v minimal
end_time="$(date +%s)"

echo "Elapsed seconds: $((end_time - start_time))"
echo "Generated consumer path was temporary and has been removed."
