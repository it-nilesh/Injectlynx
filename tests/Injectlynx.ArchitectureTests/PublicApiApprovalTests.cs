using System.Reflection;

namespace Injectlynx.ArchitectureTests;

public sealed class PublicApiApprovalTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void InjectlynxPublicApi_MatchesApprovedBaseline()
    {
        var baselinePath = Path.Combine(RepositoryRoot, "tests", "Injectlynx.ArchitectureTests", "PublicApi", "Injectlynx.approved.txt");
        var publicApi = GetPublicApi(typeof(Injectlynx.IServiceConventionBuilder).Assembly);

        if (Environment.GetEnvironmentVariable("INJECTLYNX_UPDATE_PUBLIC_API") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.WriteAllText(baselinePath, publicApi);
        }

        Assert.True(File.Exists(baselinePath), "Public API baseline is missing. Set INJECTLYNX_UPDATE_PUBLIC_API=1 to create it.");
        Assert.Equal(File.ReadAllText(baselinePath), publicApi);
    }

    private static string GetPublicApi(Assembly assembly)
    {
        var lines = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(static item => item.FullName, StringComparer.Ordinal))
        {
            lines.Add("T: " + GetTypeName(type));

            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .OrderBy(static item => string.Join(",", item.GetParameters().Select(static parameter => GetTypeName(parameter.ParameterType))), StringComparer.Ordinal))
            {
                lines.Add("  C: " + GetParameters(constructor));
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .OrderBy(static item => item.Name, StringComparer.Ordinal))
            {
                lines.Add("  P: " + property.Name + " : " + GetTypeName(property.PropertyType));
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static item => !item.IsSpecialName)
                .OrderBy(static item => item.Name, StringComparer.Ordinal)
                .ThenBy(static item => string.Join(",", item.GetParameters().Select(static parameter => GetTypeName(parameter.ParameterType))), StringComparer.Ordinal))
            {
                lines.Add("  M: " + method.Name + GetParameters(method) + " : " + GetTypeName(method.ReturnType));
            }
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string GetParameters(MethodBase method) =>
        "(" + string.Join(", ", method.GetParameters().Select(static parameter => GetTypeName(parameter.ParameterType) + " " + parameter.Name)) + ")";

    private static string GetTypeName(Type type)
    {
        if (type.IsByRef)
        {
            return GetTypeName(type.GetElementType()!) + "&";
        }

        if (type.IsPointer)
        {
            return GetTypeName(type.GetElementType()!) + "*";
        }

        if (type.IsArray)
        {
            return GetTypeName(type.GetElementType()!) + "[]";
        }

        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (type.IsGenericType)
        {
            var name = type.GetGenericTypeDefinition().FullName ?? type.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0)
            {
                name = name.Substring(0, tick);
            }

            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(GetTypeName)) + ">";
        }

        return type.FullName ?? type.Name;
    }

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
