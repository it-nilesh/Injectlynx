using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("Usage: Injectlynx.Sbom <dotnet-list-package.json> <cyclonedx-output.json> [repository-root]");
    return 2;
}

var inputPath = args[0];
var outputPath = args[1];
var repositoryRoot = Path.GetFullPath(args.Length == 3 ? args[2] : Directory.GetCurrentDirectory());

var input = JsonNode.Parse(await File.ReadAllTextAsync(inputPath));
if (input is not JsonObject root || root["projects"] is not JsonArray projects)
{
    Console.Error.WriteLine("Input is not a dotnet list package JSON document.");
    return 1;
}

var packages = new SortedDictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);

foreach (var project in projects.OfType<JsonObject>())
{
    var projectPath = NormalizeProjectPath((string?)project["path"] ?? string.Empty, repositoryRoot);
    if (project["frameworks"] is not JsonArray frameworks)
    {
        continue;
    }

    foreach (var framework in frameworks.OfType<JsonObject>())
    {
        AddPackages(projectPath, framework["topLevelPackages"] as JsonArray, direct: true, packages);
        AddPackages(projectPath, framework["transitivePackages"] as JsonArray, direct: false, packages);
    }
}

var components = packages.Values
    .OrderBy(static item => item.Id, StringComparer.OrdinalIgnoreCase)
    .ThenBy(static item => item.Version, StringComparer.OrdinalIgnoreCase)
    .Select(static item => new Dictionary<string, object?>
    {
        ["type"] = "library",
        ["bom-ref"] = item.PackageUrl,
        ["name"] = item.Id,
        ["version"] = item.Version,
        ["purl"] = item.PackageUrl,
        ["scope"] = "required",
        ["properties"] = new object[]
        {
            new Dictionary<string, string> { ["name"] = "injectlynx:direct", ["value"] = item.Direct.ToString().ToLowerInvariant() },
            new Dictionary<string, string> { ["name"] = "injectlynx:projects", ["value"] = string.Join(";", item.ProjectPaths.OrderBy(static path => path, StringComparer.Ordinal)) }
        }
    })
    .ToArray();

var sbom = new Dictionary<string, object?>
{
    ["bomFormat"] = "CycloneDX",
    ["specVersion"] = "1.5",
    ["version"] = 1,
    ["metadata"] = new Dictionary<string, object?>
    {
        ["component"] = new Dictionary<string, object?>
        {
            ["type"] = "application",
            ["name"] = "Injectlynx",
            ["version"] = "1.0.0"
        },
        ["tools"] = new object[]
        {
            new Dictionary<string, string>
            {
                ["vendor"] = "Injectlynx",
                ["name"] = "Injectlynx.Sbom"
            }
        }
    },
    ["components"] = components
};

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
await File.WriteAllTextAsync(
    outputPath,
    JsonSerializer.Serialize(sbom, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    }) + Environment.NewLine);

Console.WriteLine($"Wrote CycloneDX SBOM: {outputPath}");
return 0;

static void AddPackages(
    string projectPath,
    JsonArray? packageArray,
    bool direct,
    SortedDictionary<string, PackageInfo> packages)
{
    if (packageArray is null)
    {
        return;
    }

    foreach (var package in packageArray.OfType<JsonObject>())
    {
        var id = (string?)package["id"];
        var version = (string?)package["resolvedVersion"];
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
        {
            continue;
        }

        var key = id + "@" + version;
        if (!packages.TryGetValue(key, out var info))
        {
            info = new PackageInfo(id, version);
            packages.Add(key, info);
        }

        info.Direct |= direct;
        info.ProjectPaths.Add(projectPath);
    }
}

static string NormalizeProjectPath(string projectPath, string repositoryRoot)
{
    if (string.IsNullOrWhiteSpace(projectPath))
    {
        return string.Empty;
    }

    var fullPath = Path.GetFullPath(projectPath);
    var relativePath = Path.GetRelativePath(repositoryRoot, fullPath);
    return relativePath.Replace(Path.DirectorySeparatorChar, '/');
}

internal sealed class PackageInfo(string id, string version)
{
    public string Id { get; } = id;

    public string Version { get; } = version;

    public bool Direct { get; set; }

    public SortedSet<string> ProjectPaths { get; } = new(StringComparer.Ordinal);

    public string PackageUrl => "pkg:nuget/" + Id + "@" + Version;
}
