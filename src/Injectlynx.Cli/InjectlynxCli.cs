using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Injectlynx.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Injectlynx.Cli;

public static class InjectlynxCli
{
    private static readonly Regex ConstStringRegex = new(
        "public const string (?<name>Text|Mermaid) = @\"(?<value>(?:[^\"]|\"\")*)\";",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly string[] ConventionMethods =
    [
        "ModuleName",
        "GeneratedMethod",
        "GeneratedNamespace",
        "FromNamespace",
        "WhereNameStartsWith",
        "WhereNameEndsWith",
        "WhereInterfaceNameStartsWith",
        "WhereInterfaceNameEndsWith",
        "AssignableToOpenGeneric",
        "ExcludeNamespace",
        "ExcludeType",
        "AsMatchingInterface",
        "AsImplementedInterfaces",
        "AsSelf",
        "AsMatchingInterfaceAndSelf",
        "WithSingletonLifetime",
        "WithScopedLifetime",
        "WithTransientLifetime",
        "Register",
        "Decorate",
        "External",
        "FrameworkProvided",
        "ForbidDependency"
    ];

    public static int Run(string[] args, TextWriter output) =>
        Run(args, output, Directory.GetCurrentDirectory());

    public static int Run(string[] args, TextWriter output, string workingDirectory)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            WriteHelp(output);
            return 0;
        }

        var command = args[0];
        var rest = args.Skip(1).ToArray();
        return command switch
        {
            "inspect" => Inspect(rest, output, workingDirectory),
            "conventions" => Conventions(rest, output, workingDirectory),
            "graph" => Graph(rest, output, workingDirectory),
            "diagnostics" => Diagnostics(rest, output, workingDirectory),
            "validate" => Validate(rest, output, workingDirectory),
            "plugins" => Plugins(rest, output, workingDirectory),
            _ => Unknown(command, output)
        };
    }

    private static int Inspect(string[] args, TextWriter output, string workingDirectory)
    {
        var options = ParseOptions(args, workingDirectory);
        if (options.Build && RunBuild(options.Path, output, noRestore: options.NoRestore) != 0)
        {
            return 1;
        }

        var reports = LoadReports(options.Path);
        if (reports.Count == 0)
        {
            output.WriteLine("No Injectlynx report source files found.");
            output.WriteLine("Run `injectlynx validate <project>`, or build with `-p:InjectlynxReportSource=true` first.");
            return 2;
        }

        foreach (var report in reports)
        {
            output.WriteLine(report.Text.TrimEnd());
            output.WriteLine();
        }

        return 0;
    }

    private static int Conventions(string[] args, TextWriter output, string workingDirectory)
    {
        var options = ParseOptions(args, workingDirectory);
        var files = FindCSharpFiles(options.Path).ToArray();
        var matches = new List<string>();

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                if (ConventionMethods.Any(method => line.Contains("." + method, StringComparison.Ordinal) ||
                        line.StartsWith("services." + method, StringComparison.Ordinal)))
                {
                    matches.Add(RelativePath(options.Path, file) + ":" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + line);
                }
            }
        }

        if (matches.Count == 0)
        {
            output.WriteLine("No Injectlynx convention DSL calls found.");
            return 2;
        }

        output.WriteLine("Injectlynx convention matches:");
        foreach (var match in matches)
        {
            output.WriteLine("- " + match);
        }

        return 0;
    }

    private static int Graph(string[] args, TextWriter output, string workingDirectory)
    {
        var options = ParseOptions(args, workingDirectory);
        if (options.Build && RunBuild(options.Path, output, noRestore: options.NoRestore) != 0)
        {
            return 1;
        }

        var reports = LoadReports(options.Path);
        if (reports.Count == 0)
        {
            output.WriteLine("No Injectlynx graph report found.");
            return 2;
        }

        var builder = new StringBuilder();
        foreach (var report in reports)
        {
            builder.AppendLine(options.Format == "text" ? report.Text.TrimEnd() : report.Mermaid.TrimEnd());
            builder.AppendLine();
        }

        return WriteOrPrint(builder.ToString().TrimEnd() + Environment.NewLine, options.OutputPath, output);
    }

    private static int Diagnostics(string[] args, TextWriter output, string workingDirectory)
    {
        var options = ParseOptions(args, workingDirectory);
        if (options.Build && RunBuild(options.Path, output, noRestore: options.NoRestore) != 0)
        {
            return 1;
        }

        var reports = LoadReports(options.Path);
        if (reports.Count == 0)
        {
            output.WriteLine("No Injectlynx report source files found for diagnostics export.");
            return 2;
        }

        var builder = new StringBuilder();
        builder.AppendLine("# Injectlynx Dependency Graph Diagnostics");
        builder.AppendLine();
        foreach (var report in reports)
        {
            builder.AppendLine(report.Text.TrimEnd());
            builder.AppendLine();
            builder.AppendLine("```mermaid");
            builder.AppendLine(report.Mermaid.TrimEnd());
            builder.AppendLine("```");
            builder.AppendLine();
        }

        return WriteOrPrint(builder.ToString(), options.OutputPath, output);
    }

    private static int Validate(string[] args, TextWriter output, string workingDirectory)
    {
        var options = ParseOptions(args, workingDirectory);
        return RunBuild(options.Path, output, noRestore: options.NoRestore);
    }

    private static int Plugins(string[] args, TextWriter output, string workingDirectory)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            output.WriteLine("Usage:");
            output.WriteLine("  injectlynx plugins list [path] [--config file] [--manifest file] [--assembly file]");
            output.WriteLine("  injectlynx plugins validate [path] [--config file] [--manifest file] [--assembly file]");
            output.WriteLine("  injectlynx plugins inspect [path] [--config file] [--manifest file] [--assembly file]");
            return 0;
        }

        var subcommand = args[0];
        var options = ParsePluginOptions(args.Skip(1).ToArray(), workingDirectory);
        var services = new ServiceCollection();
        var beforeCount = services.Count;
        var result = InjectlynxPluginLoader.Load(services, options);

        switch (subcommand)
        {
            case "list":
                foreach (var plugin in result.Plugins)
                {
                    output.WriteLine(plugin.Manifest.Name + " " + plugin.Manifest.Version + " order=" + plugin.Manifest.Order);
                    if (!string.IsNullOrWhiteSpace(plugin.Manifest.Description))
                    {
                        output.WriteLine("  " + plugin.Manifest.Description);
                    }
                }

                WriteDiagnostics(result.Diagnostics, output);
                return result.HasErrors ? 1 : 0;

            case "validate":
                WriteDiagnostics(result.Diagnostics, output);
                output.WriteLine(result.HasErrors ? "Plugin validation failed." : "Plugin validation succeeded.");
                return result.HasErrors ? 1 : 0;

            case "inspect":
                foreach (var plugin in result.Plugins)
                {
                    output.WriteLine(plugin.Manifest.Name + " (" + plugin.Manifest.TypeName + ")");
                    foreach (var descriptor in services.Skip(beforeCount))
                    {
                        output.WriteLine("- " + descriptor.Lifetime + ": " + descriptor.ServiceType.FullName);
                    }
                }

                WriteDiagnostics(result.Diagnostics, output);
                return result.HasErrors ? 1 : 0;

            default:
                output.WriteLine("Unknown plugins command: " + subcommand);
                return 1;
        }
    }

    private static int RunBuild(string path, TextWriter output, bool noRestore)
    {
        var target = ResolveBuildTarget(path);
        var arguments = new List<string>
        {
            "build",
            target,
            "-p:InjectlynxReportSource=true",
            "-p:EmitCompilerGeneratedFiles=true",
            "-p:InjectlynxDevelopmentReport=true",
            "-nr:false"
        };
        if (noRestore)
        {
            arguments.Add("--no-restore");
        }

        var command = "dotnet " + string.Join(" ", arguments.Select(QuoteArgument));
        output.WriteLine("Running: " + command);
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = GetShell(),
            UseShellExecute = false
        };
        foreach (var argument in GetShellArguments(command))
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        if (!process.WaitForExit(milliseconds: 120_000))
        {
            output.WriteLine("Validation build timed out after 120 seconds.");
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            return 124;
        }

        return process.ExitCode;
    }

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ', StringComparison.Ordinal) ? "\"" + argument + "\"" : argument;

    private static string GetShell() =>
        OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";

    private static IEnumerable<string> GetShellArguments(string command)
    {
        if (OperatingSystem.IsWindows())
        {
            yield return "/c";
            yield return command;
            yield break;
        }

        yield return "-c";
        yield return command;
    }

    private static IReadOnlyList<RegistrationReport> LoadReports(string path)
    {
        var root = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        return Directory.EnumerateFiles(root, "Injectlynx.*.Report.g.cs", SearchOption.AllDirectories)
            .Where(static file => file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(static file => file, StringComparer.Ordinal)
            .Select(ParseReport)
            .Where(static report => report is not null)
            .Cast<RegistrationReport>()
            .ToArray();
    }

    private static RegistrationReport? ParseReport(string file)
    {
        var text = File.ReadAllText(file);
        var values = ConstStringRegex.Matches(text)
            .Cast<Match>()
            .ToDictionary(
                static match => match.Groups["name"].Value,
                static match => match.Groups["value"].Value.Replace("\"\"", "\""),
                StringComparer.Ordinal);

        return values.TryGetValue("Text", out var reportText) &&
            values.TryGetValue("Mermaid", out var mermaid)
            ? new RegistrationReport(file, reportText, mermaid)
            : null;
    }

    private static IEnumerable<string> FindCSharpFiles(string path)
    {
        var root = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(static file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(static file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(static file => file, StringComparer.Ordinal);
    }

    private static CliOptions ParseOptions(string[] args, string workingDirectory)
    {
        var path = workingDirectory;
        var outputPath = (string?)null;
        var format = "mermaid";
        var build = false;
        var noRestore = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--build":
                    build = true;
                    break;
                case "--no-restore":
                    noRestore = true;
                    break;
                case "--format" when index + 1 < args.Length:
                    format = args[++index];
                    break;
                case "--output" when index + 1 < args.Length:
                case "-o" when index + 1 < args.Length:
                    outputPath = args[++index];
                    break;
                default:
                    if (!arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        path = Path.GetFullPath(arg, workingDirectory);
                    }

                    break;
            }
        }

        return new CliOptions(path, outputPath is null ? null : Path.GetFullPath(outputPath, workingDirectory), format, build, noRestore);
    }

    private static InjectlynxPluginLoadOptions ParsePluginOptions(string[] args, string workingDirectory)
    {
        var options = new InjectlynxPluginLoadOptions
        {
            UseCollectibleLoadContext = false
        };
        var pathProvided = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--config" when index + 1 < args.Length:
                    options.AddConfiguration(Path.GetFullPath(args[++index], workingDirectory));
                    break;
                case "--manifest" when index + 1 < args.Length:
                    options.AddManifest(Path.GetFullPath(args[++index], workingDirectory));
                    break;
                case "--assembly" when index + 1 < args.Length:
                    options.AddAssembly(Path.GetFullPath(args[++index], workingDirectory));
                    break;
                case "--disable" when index + 1 < args.Length:
                    options.DisablePlugin(args[++index]);
                    break;
                default:
                    if (!arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        options.AddDirectory(Path.GetFullPath(arg, workingDirectory));
                        pathProvided = true;
                    }

                    break;
            }
        }

        if (!pathProvided &&
            options.ConfigurationFiles.Count == 0 &&
            options.ManifestFiles.Count == 0 &&
            options.PluginAssemblies.Count == 0)
        {
            options.AddDirectory(workingDirectory);
        }

        return options;
    }

    private static void WriteDiagnostics(IEnumerable<InjectlynxPluginDiagnostic> diagnostics, TextWriter output)
    {
        foreach (var diagnostic in diagnostics)
        {
            output.WriteLine(diagnostic.Severity + " " + diagnostic.Code + ": " + diagnostic.Message);
        }
    }

    private static string ResolveBuildTarget(string path)
    {
        if (File.Exists(path))
        {
            return path;
        }

        var project = Directory.EnumerateFiles(path, "*.csproj", SearchOption.TopDirectoryOnly)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .FirstOrDefault();
        return project ?? path;
    }

    private static int WriteOrPrint(string text, string? outputPath, TextWriter output)
    {
        if (outputPath is null)
        {
            output.Write(text);
            return 0;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());
        File.WriteAllText(outputPath, text);
        output.WriteLine("Wrote " + outputPath);
        return 0;
    }

    private static string RelativePath(string root, string file)
    {
        var basePath = Directory.Exists(root) ? root : Path.GetDirectoryName(root) ?? Directory.GetCurrentDirectory();
        return Path.GetRelativePath(basePath, file);
    }

    private static int Unknown(string command, TextWriter output)
    {
        output.WriteLine($"Unknown command: {command}");
        output.WriteLine("Run `injectlynx --help` for available commands.");
        return 1;
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("Injectlynx CLI");
        output.WriteLine();
        output.WriteLine("Usage:");
        output.WriteLine("  injectlynx inspect [path] [--build] [--no-restore]");
        output.WriteLine("  injectlynx conventions [path]");
        output.WriteLine("  injectlynx graph [path] [--format mermaid|text] [--output file] [--build]");
        output.WriteLine("  injectlynx diagnostics [path] [--output file] [--build]");
        output.WriteLine("  injectlynx validate [path] [--no-restore]");
        output.WriteLine("  injectlynx plugins list|validate|inspect [path] [--config file] [--manifest file] [--assembly file]");
        output.WriteLine();
        output.WriteLine("Reports are read from generated Injectlynx.*.Report.g.cs files under obj.");
    }

    private sealed record CliOptions(
        string Path,
        string? OutputPath,
        string Format,
        bool Build,
        bool NoRestore);

    private sealed record RegistrationReport(
        string File,
        string Text,
        string Mermaid);
}
