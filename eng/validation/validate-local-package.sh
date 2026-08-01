#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
packages_dir="$repo_root/artifacts/packages"
work_dir="$(mktemp -d)"

echo "Injectlynx local package validation"
echo "Repository: $repo_root"
echo "Workspace: $work_dir"

cleanup() {
  rm -rf "$work_dir"
}
trap cleanup EXIT

package_path="$packages_dir/Injectlynx.1.0.0.nupkg"
if [[ ! -f "$package_path" ]]; then
  echo "Package not found: $package_path" >&2
  echo "Run: dotnet pack src/Injectlynx/Injectlynx.csproj -c Debug -o artifacts/packages" >&2
  exit 2
fi

echo "Using package: $package_path"

mkdir -p "$work_dir/Consumer/Services"

cat > "$work_dir/Consumer/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RestoreSources>$packages_dir</RestoreSources>
    <RestorePackagesPath>$work_dir/packages</RestorePackagesPath>
    <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Injectlynx" Version="1.0.0" />
  </ItemGroup>
</Project>
EOF

cat > "$work_dir/Consumer/ApplicationServiceConventions.cs" <<'EOF'
using Injectlynx;

namespace Consumer;

public static class ApplicationServiceConventions
{
    public static void Configure(IServiceConventionBuilder services)
    {
        services
            .FromNamespace("Consumer.Services")
            .WhereNameEndsWith("Service")
            .AsMatchingInterface()
            .WithScopedLifetime();
    }
}
EOF

cat > "$work_dir/Consumer/Program.cs" <<'EOF'
using Consumer.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInjectlynxServices();

var app = builder.Build();
app.MapGet("/", (IOrderService orders) => orders.GetName());
app.Run();
EOF

cat > "$work_dir/Consumer/Services/IOrderService.cs" <<'EOF'
namespace Consumer.Services;

public interface IOrderService
{
    string GetName();
}
EOF

cat > "$work_dir/Consumer/Services/OrderService.cs" <<'EOF'
namespace Consumer.Services;

public sealed class OrderService : IOrderService
{
    public string GetName() => "sample";
}
EOF

echo "Building fresh consumer..."
dotnet build "$work_dir/Consumer/Consumer.csproj" -v minimal
echo "Local package validation succeeded."
