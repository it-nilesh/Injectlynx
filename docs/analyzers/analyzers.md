# Analyzers And Code Fixes

Injectlynx ships IDE analyzers alongside the source generator. The generator remains the source of truth for emitted registrations, while analyzers catch common issues earlier in the editor.

## Analyzer Diagnostics

- `INJA001`: a convention DSL string argument is not compile-time readable. Use a string literal, `nameof(...)`, or a constant string.
- `INJA002`: a convention method is close to the Injectlynx shape but does not match `public static void Configure(IServiceConventionBuilder services)`.
- `INJA003`: a matching-interface service shape has no public constructor or multiple public constructors.

These analyzer diagnostics are warnings by default because generator diagnostics still enforce the final build behavior.

## Code Fixes

The code-fix provider currently offers conservative fixes for common editor workflows:

- Inline a local string literal into an Injectlynx DSL argument for `INJA001`.
- Switch `AsMatchingInterface()` to `AsSelf()` when the generator reports missing matching interface diagnostic `INJ001`.
- Add `using Microsoft.Extensions.DependencyInjection;` when `AddInjectlynxServices()` is missing from startup code.

Future code fixes can add generated-interface creation and richer missing-namespace detection for custom generated namespaces.
