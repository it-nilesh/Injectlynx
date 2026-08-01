namespace Injectlynx.Cli;

public static class InjectlynxCli
{
    public static int Run(string[] args, TextWriter output)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            output.WriteLine("Injectlynx CLI");
            output.WriteLine("Future command-line inspection tooling for generated dependency injection registrations.");
            return 0;
        }

        output.WriteLine($"Unknown command: {args[0]}");
        output.WriteLine("Run `injectlynx --help` for available commands.");
        return 1;
    }
}
