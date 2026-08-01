using System.Xml.Linq;

namespace Injectlynx.ArchitectureTests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void CoreProject_RemainsRoslynFree()
    {
        var project = LoadProject("src/Injectlynx.Core/Injectlynx.Core.csproj");
        var references = GetPackageReferences(project)
            .Concat(GetProjectReferences(project))
            .ToArray();

        Assert.DoesNotContain(references, static reference => reference.Contains("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, static reference => reference.Contains("Injectlynx.Generator", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, static reference => reference.Contains("Injectlynx.Analyzers", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, static reference => reference.Contains("Injectlynx.CodeFixes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generator_DoesNotUseRuntimeDiscoveryPatterns()
    {
        var sourceFiles = Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot, "src/Injectlynx.Generator"),
            "*.cs",
            SearchOption.AllDirectories);

        var forbiddenPatterns = new[]
        {
            "Assembly.Load",
            "GetTypes(",
            "GetExportedTypes(",
            "Activator.CreateInstance(",
            "AppDomain.CurrentDomain"
        };

        foreach (var sourceFile in sourceFiles)
        {
            var text = File.ReadAllText(sourceFile);
            foreach (var pattern in forbiddenPatterns)
            {
                Assert.DoesNotContain(pattern, text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void PrimaryPackage_DoesNotReferenceProjectsAsRuntimeAssemblies()
    {
        var project = LoadProject("src/Injectlynx/Injectlynx.csproj");
        var projectReferences = project.Descendants("ProjectReference").ToArray();

        Assert.All(projectReferences, reference =>
        {
            Assert.Equal("false", (string?)reference.Attribute("ReferenceOutputAssembly"));
            Assert.Equal("all", (string?)reference.Attribute("PrivateAssets"));
        });
    }

    private static XDocument LoadProject(string relativePath) =>
        XDocument.Load(Path.Combine(RepositoryRoot, relativePath));

    private static IEnumerable<string> GetPackageReferences(XDocument project) =>
        project.Descendants("PackageReference")
            .Select(static item => (string?)item.Attribute("Include") ?? string.Empty)
            .Where(static item => item.Length > 0);

    private static IEnumerable<string> GetProjectReferences(XDocument project) =>
        project.Descendants("ProjectReference")
            .Select(static item => (string?)item.Attribute("Include") ?? string.Empty)
            .Where(static item => item.Length > 0);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Injectlynx.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
