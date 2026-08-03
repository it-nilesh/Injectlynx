# Generator Transparency

Injectlynx is designed to make service registration behavior inspectable during development and CI.

## Registration Comments

Generated registration source includes comments by default:

```csharp
// Injectlynx registration
// Reason: OrderService matched convention Shop.Application.Services.
// - Class name ends with Service.
// - Registration strategy is MatchingInterface.
services.AddScoped<IOrderService, OrderService>();
```

Disable these comments when a project wants smaller generated source:

```bash
dotnet build -p:InjectlynxRegistrationComments=false
```

Supported false values are `false`, `0`, `no`, and `off`.

## Development Diagnostic Report

Use the development diagnostic report for local investigation:

```bash
dotnet build -p:InjectlynxDevelopmentReport=true
```

Use warning mode when the report should appear in normal console output:

```bash
dotnet build -p:InjectlynxDevelopmentReport=warning
```

Example diagnostic message:

```text
Module Application: OrderService -> IOrderService (Scoped). Matched convention.
```

Keep warning mode disabled in CI unless the build is intentionally collecting registration output from diagnostics.

## Deterministic Report Source

Enable report source generation for CI-friendly inspection:

```bash
dotnet build -p:InjectlynxReportSource=true
```

Injectlynx emits an additional generated source file per module:

```text
Injectlynx.Application.Report.g.cs
```

The generated report class contains:

- `Text`: deterministic text registration report.
- `Mermaid`: Mermaid flowchart source for visualizing module-to-service registrations.

To write generated files to disk, use the standard compiler generated-file settings:

```bash
dotnet build \
  -p:EmitCompilerGeneratedFiles=true \
  -p:CompilerGeneratedFilesOutputPath=artifacts/generated \
  -p:InjectlynxReportSource=true
```

The generated report source is deterministic and can be archived in CI. It is not intended to be edited by hand.

## Mermaid Graph Example

Report source includes Mermaid text similar to:

```text
flowchart LR
  module["Application module"]
  module -->|Scoped| contract_global__Shop_Application_Services_IOrderService
  contract_global__Shop_Application_Services_IOrderService --> implementation_global__Shop_Application_Services_OrderService
```

Use the Mermaid output for documentation, issue triage, or registration review.

The CLI can inspect the same report source:

```bash
dotnet run --project src/Injectlynx.Cli -f net10.0 -- inspect samples/WebApi --build
dotnet run --project src/Injectlynx.Cli -f net10.0 -- graph samples/WebApi --output artifacts/injectlynx-graph.mmd
```
