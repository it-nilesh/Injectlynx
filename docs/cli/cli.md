# CLI Inspection Tooling

`Injectlynx.Cli` provides local inspection commands for generated registrations and convention sources.

## Commands

Inspect generated registration reports:

```bash
dotnet run --project src/Injectlynx.Cli -f net10.0 -- inspect samples/WebApi --build
```

Print convention DSL matches found in source files:

```bash
dotnet run --project src/Injectlynx.Cli -f net10.0 -- conventions samples/WebApi
```

Export a Mermaid graph from generated report source:

```bash
dotnet run --project src/Injectlynx.Cli -f net10.0 -- graph samples/WebApi --build --output artifacts/injectlynx-graph.mmd
```

Export a Markdown diagnostics bundle with the text report and Mermaid graph:

```bash
dotnet run --project src/Injectlynx.Cli -f net10.0 -- diagnostics samples/WebApi --build --output artifacts/injectlynx-diagnostics.md
```

Validate a project without packing:

```bash
dotnet run --project src/Injectlynx.Cli -f net10.0 -- validate samples/WebApi --no-restore
```

`validate` runs `dotnet build` with `InjectlynxReportSource=true`, `EmitCompilerGeneratedFiles=true`, and `InjectlynxDevelopmentReport=true`, so generated registration reports are available for later `inspect`, `graph`, and `diagnostics` commands.

Inspect dynamic runtime plugins:

```bash
dotnet run --project src/Injectlynx.Cli -f net10.0 -- plugins list samples/PluginSample/bin/Debug/net10.0
dotnet run --project src/Injectlynx.Cli -f net10.0 -- plugins validate samples/PluginSample/bin/Debug/net10.0
dotnet run --project src/Injectlynx.Cli -f net10.0 -- plugins inspect samples/PluginSample/bin/Debug/net10.0
```

Plugin commands accept `--config`, `--manifest`, `--assembly`, and `--disable` options:

```bash
dotnet run --project src/Injectlynx.Cli -f net10.0 -- plugins list --config injectlynx.plugins.json
dotnet run --project src/Injectlynx.Cli -f net10.0 -- plugins validate --manifest plugins/Reports/injectlynx.plugin.json
dotnet run --project src/Injectlynx.Cli -f net10.0 -- plugins inspect --assembly plugins/GreetingPlugin/PluginSample.dll
```

`plugins list` prints the discovered plugin identity, version, order, and description. `plugins validate` reports manifest, compatibility, dependency, hash, and load diagnostics. `plugins inspect` prints plugin types and the service registrations added by each plugin.

## Report Source

The CLI reads `Injectlynx.*.Report.g.cs` files generated under `obj`. Build with report source enabled when no report files exist:

```bash
dotnet build -p:InjectlynxReportSource=true -p:EmitCompilerGeneratedFiles=true
```

The report contains:

- Registration text with contract, implementation, lifetime, keys, decorators, member injection, and reasons.
- Mermaid graph source for visual dependency review.
