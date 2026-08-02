# Injectlynx Task List

Last updated: 2026-08-03

This file tracks completed project work and future feature tasks for Injectlynx.

## Completed

### Core Library

- [x] Define Injectlynx as an attribute-free, convention-based compile-time dependency injection toolkit for .NET.
- [x] Add public C# convention DSL through the primary `Injectlynx` package.
- [x] Support module-level service convention configuration.
- [x] Keep service implementation classes free from Injectlynx attributes.
- [x] Target `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0`.
- [x] Package the generator as an analyzer inside the primary NuGet package.

### Source Generator

- [x] Implement Roslyn incremental source generator.
- [x] Generate deterministic `Microsoft.Extensions.DependencyInjection` registrations.
- [x] Support `AsMatchingInterface()`.
- [x] Support `AsImplementedInterfaces()`.
- [x] Support `AsSelf()`.
- [x] Support `AsMatchingInterfaceAndSelf()`.
- [x] Support open generic registration selection.
- [x] Support explicit registration declarations.
- [x] Support keyed registrations.
- [x] Support decorator registration declarations.
- [x] Support generated method name customization.
- [x] Support generated namespace customization.
- [x] Support deterministic registration ordering.

### Configuration DSL

- [x] Add namespace selection.
- [x] Add name prefix and suffix filters.
- [x] Add interface name filters.
- [x] Add open generic assignability filters.
- [x] Add namespace exclusions.
- [x] Add type exclusions.
- [x] Add singleton, scoped, and transient lifetime declarations.
- [x] Add external/framework-provided service declarations.
- [x] Add architecture dependency rule declarations.
- [x] Add diagnostic severity override declarations.

### Member Injection

- [x] Support property injection declarations.
- [x] Support optional property injection declarations.
- [x] Support method injection declarations.
- [x] Support constant method arguments.
- [x] Support service-resolved method arguments.
- [x] Document member injection as an edge-case feature, not the default pattern.

### Diagnostics

- [x] Add diagnostics for missing matching interfaces.
- [x] Add diagnostics for ambiguous matching interfaces.
- [x] Add diagnostics for duplicate registrations.
- [x] Add diagnostics for missing implemented interfaces.
- [x] Add diagnostics for constructor issues.
- [x] Add diagnostics for missing dependencies.
- [x] Add diagnostics for circular and self dependencies.
- [x] Add singleton-depends-on-scoped lifetime diagnostics.
- [x] Add diagnostics with suggested fixes for missing dependencies, ambiguous matches, and invalid conventions.
- [x] Add keyed registration target-framework compatibility diagnostics.
- [x] Add decorator target and decorator lifetime safety diagnostics.
- [x] Add invalid DSL declaration diagnostics.
- [x] Add opt-in development registration report.

### Samples

- [x] Add Minimal API sample.
- [x] Add Web API sample.
- [x] Add Worker Service sample.
- [x] Add Native AOT sample.
- [x] Demonstrate open generic handlers.
- [x] Demonstrate decorators.
- [x] Demonstrate keyed services.
- [x] Demonstrate external service declarations.
- [x] Demonstrate custom generated startup method names.
- [x] Demonstrate property and method injection.

### Tests And Validation

- [x] Add xUnit tests for core models.
- [x] Add generator tests.
- [x] Add architecture boundary tests.
- [x] Add broader generator tests for conflict scenarios, multiple modules, keyed registrations, decorators, and architecture rules.
- [x] Add package smoke validation for fresh consumer projects.
- [x] Add local package validation script.
- [x] Add source-generator performance smoke script for large generated consumers.
- [x] Add sample validation scripts.
- [x] Add Native AOT validation script.
- [x] Add release package verification scripts.
- [x] Add vulnerability verification script.
- [x] Add SBOM generation script.
- [x] Add reproducible package verification script.

### Documentation

- [x] Add README quick start.
- [x] Add configuration DSL documentation.
- [x] Add member injection DSL documentation.
- [x] Add diagnostics documentation.
- [x] Add generator architecture documentation.
- [x] Add Native AOT documentation.
- [x] Add packaging documentation.
- [x] Add sample documentation.
- [x] Add security documentation.
- [x] Add introductory article draft.

### Website

- [x] Add static developer-facing website.
- [x] Add polished landing content for problem, value, usage, speed, and install sections.
- [x] Add generated hero visual asset.
- [x] Add project logo assets.
- [x] Add light and dark mode.
- [x] Add mobile menu.
- [x] Add SEO metadata.
- [x] Add Open Graph and Twitter card metadata.
- [x] Add JSON-LD software metadata.
- [x] Add production canonical URL for `https://injectlynx.inilesh.dev/`.
- [x] Add `robots.txt`.
- [x] Add `sitemap.xml` with main section anchors.

## Recommended Task Order

Use this order when planning implementation. Each phase should be mostly complete before moving to the next phase, unless a bug fix or release need changes the priority.

### Phase 1: Positioning And Documentation

- [x] Publish Injectlynx positioning as a compile-time Microsoft DI registration generator.
- [x] Document the difference between generated `IServiceCollection` registrations, manual registrations, and runtime scanning.
- [x] Add migration guide from manual `IServiceCollection` registrations.
- [x] Add migration guide from runtime scanning libraries.
- [x] Add troubleshooting guide for common diagnostics.
- [x] Add generated-output examples for each major DSL pattern.
- [x] Add benchmark documentation for generated Microsoft DI registrations, startup behavior, and source-generator performance.

### Phase 2: Generator Transparency

- [x] Add richer generated-output examples for convention patterns.
- [x] Add deterministic report output file for CI inspection.
- [x] Add optional generated comments showing the convention that produced each registration.
- [x] Add visual dependency graph/report output for convention matches and generated registrations.
- [x] Add generated registration source snapshots for easier review in tests.

### Phase 3: Diagnostics And Safety

- [x] Add improved diagnostics with suggested fixes for missing dependencies, ambiguous matches, and invalid conventions.
- [x] Add richer diagnostics for ambiguous convention matches with suggested fixes.
- [x] Add stricter validation for decorator chains and decorator lifetime compatibility.
- [x] Add stronger keyed service validation across supported target frameworks.
- [x] Add tests for every documented diagnostic.

### Phase 4: Test Coverage And Benchmarks

- [x] Add broader generator tests for conflict scenarios.
- [x] Add tests for custom generated namespaces and method names across multiple modules.
- [x] Add tests for keyed registrations on modern target frameworks.
- [x] Add tests for decorator registration ordering.
- [x] Add tests for architecture rule enforcement.
- [x] Add package smoke tests for fresh consumer projects.
- [x] Add benchmark documentation comparing Injectlynx-generated Microsoft DI registrations against manual Microsoft DI registrations.
- [x] Add benchmark documentation comparing startup behavior against runtime scanning approaches.
- [x] Add source-generator performance benchmarks for large solutions.

### Phase 5: Analyzer And Code Fix Tooling

- [x] Expand `Injectlynx.Analyzers` beyond placeholder status.
- [x] Add analyzer rules for unsupported DSL usage, invalid convention signatures, and constructability issues.
- [x] Expand `Injectlynx.CodeFixes` beyond placeholder status.
- [x] Add code fixes for missing matching interfaces, self-registration fallback, unsupported DSL arguments, and missing namespace imports.

### Phase 6: CLI Inspection Tooling

- [x] Expand `Injectlynx.Cli` beyond placeholder status.
- [x] Add CLI command to inspect generated registrations.
- [x] Add CLI command to print convention match results.
- [x] Add CLI support to inspect service graphs and explain why each registration was generated.
- [x] Add CLI command to validate a project without packing.
- [x] Add CLI command to export dependency graph diagnostics.

### Phase 7: Native AOT And Trimming

- [x] Add more Native AOT sample scenarios.
- [x] Add trimming validation for Web API and Worker Service samples.
- [x] Add documentation for Native AOT limitations and best practices.
- [x] Add CI-friendly Native AOT validation guidance.

### Phase 8: Dynamic Plugin Loading

- [x] Define the dynamic plugin loading vision and scope separately from compile-time DI generation.
- [x] Keep plugin loading in the main `Injectlynx` package so hosts and plugin authors do not need another package.
- [x] Design the plugin contract and plugin manifest before implementing runtime loading.
- [x] Add discovery, validation, diagnostics, and sample plugin apps.
- [x] Clearly document that dynamic plugin loading is opt-in runtime behavior with different Native AOT and trimming tradeoffs.

### Phase 9: Website, Release, And Community

- [x] Add dedicated documentation pages instead of a single landing page.
- [x] Add live examples section for Minimal API, Web API, Worker Service, and Native AOT.
- [x] Add changelog page.
- [x] Add release/version badge section.
- [x] Add release notes automation.
- [x] Add issue and pull request templates.
- [x] Add compatibility matrix for supported .NET versions.

## Future Feature Tasks By Area

### Generator Features

- [x] Add richer diagnostics for ambiguous convention matches with suggested fixes.
- [x] Add generated registration source snapshots for easier review in tests.
- [ ] Add support for multiple convention modules with clearer conflict reporting.
- [x] Add deterministic report output file for CI inspection.
- [x] Add optional generated comments showing the convention that produced each registration.
- [x] Add stricter validation for decorator chains and decorator lifetime compatibility.
- [x] Add stronger keyed service validation across supported target frameworks.
- [x] Add source-generator performance benchmarks for large solutions.

### Analyzer Features

- [x] Expand `Injectlynx.Analyzers` beyond placeholder status.
- [x] Add analyzer for dynamic or unsupported DSL usage before generator execution.
- [x] Add analyzer for convention classes with incorrect signatures.
- [x] Add analyzer for services that match conventions but cannot be constructed.
- [ ] Add analyzer for unused explicit service declarations.
- [x] Add analyzer documentation and diagnostic examples.

### Code Fix Features

- [x] Expand `Injectlynx.CodeFixes` beyond placeholder status.
- [ ] Add code fix to create missing matching interface.
- [x] Add code fix to switch `AsMatchingInterface()` to `AsSelf()` when appropriate.
- [x] Add code fix to replace unsupported variable DSL arguments with constants.
- [x] Add code fix to add missing generated namespace import in startup code.

### CLI Features

- [x] Expand `Injectlynx.Cli` beyond placeholder status.
- [x] Add CLI command to inspect generated registrations.
- [x] Add CLI command to print convention match results.
- [x] Add CLI command to validate a project without packing.
- [x] Add CLI command to export dependency graph diagnostics.
- [x] Add CLI documentation and examples.

### Dynamic Plugin Loading

- [x] Define the dynamic plugin loading vision and scope separately from compile-time DI generation.
- [x] Keep plugin loading in the main `Injectlynx` package so hosts and plugin authors do not need another package.
- [x] Design a stable plugin contract interface with name, description, order, and service registration.
- [x] Design a plugin manifest format with name, version, entry assembly, supported target framework, dependencies, and entry type metadata.
- [x] Add plugin discovery from configured directories.
- [x] Add plugin discovery from explicit manifest files.
- [x] Add plugin discovery from assemblies that implement `IInjectlynxPlugin` without requiring JSON.
- [x] Add plugin discovery from application configuration.
- [x] Add runtime assembly loading using isolated `AssemblyLoadContext`.
- [x] Add unload support for collectible plugin contexts where possible.
- [x] Add version compatibility checks before loading plugins.
- [x] Add dependency resolution rules for plugin-local assemblies.
- [x] Add conflict detection when multiple plugins register the same service contract.
- [x] Add plugin startup diagnostics with clear load failure messages.
- [x] Add plugin registration diagnostics for duplicate service contracts.
- [x] Add opt-in plugin registration extension for `IServiceCollection`.
- [x] Add support for enabling or disabling plugins by name.
- [x] Add support for plugin ordering.
- [x] Add plugin health/status inspection APIs.
- [x] Add dependency ordering for plugins.
- [x] Add CLI command to list discovered plugins.
- [x] Add CLI command to validate plugin manifests.
- [x] Add CLI command to inspect plugin-provided service registrations.
- [x] Add sample app that loads plugins dynamically at runtime.
- [x] Add sample plugin project with service registrations.
- [x] Document that dynamic loading uses runtime assembly loading and is different from the default compile-time, Native AOT-friendly path.
- [x] Document Native AOT and trimming limitations for dynamic plugin loading.
- [x] Add tests for plugin discovery.
- [x] Add tests for manifest validation.
- [x] Add tests for plugin load failures.
- [x] Add tests for duplicate/conflicting service registrations.
- [x] Add tests for plugin unload behavior where supported.
- [x] Add security guidance for trusted plugin directories.
- [x] Add optional plugin SHA-256 hash verification before loading.

### Native AOT And Trimming

- [x] Add more Native AOT sample scenarios.
- [x] Add trimming validation for Web API and Worker Service samples.
- [x] Add documentation for Native AOT limitations and best practices.
- [x] Add CI-friendly Native AOT validation guidance.

### Testing

- [x] Add broader generator tests for conflict scenarios.
- [x] Add tests for every documented diagnostic.
- [x] Add tests for custom generated namespaces and method names across multiple modules.
- [x] Add tests for keyed registrations on modern target frameworks.
- [x] Add tests for decorator registration ordering.
- [x] Add tests for architecture rule enforcement.
- [x] Add package smoke tests for fresh consumer projects.

### Documentation

- [x] Add migration guide from manual `IServiceCollection` registrations.
- [x] Add migration guide from runtime scanning libraries.
- [x] Add advanced recipe docs for multi-project solutions.
- [x] Add docs for decorators and keyed services.
- [x] Add docs for architecture rules.
- [x] Add troubleshooting guide for common diagnostics.
- [x] Add generated-output examples for each major DSL pattern.

### Website

- [x] Add dedicated documentation pages instead of a single landing page.
- [x] Add copy-to-clipboard buttons for code examples.
- [x] Add live examples section for Minimal API, Web API, Worker Service, and Native AOT.
- [x] Add changelog page.
- [x] Add release/version badge section.
- [x] Add SEO FAQ schema.
- [x] Add social preview image dedicated to the production website.
- [ ] Add performance audit and optimize image payloads.

### Packaging And Release

- [x] Automate release notes generation from changelog entries.
- [x] Add package README validation in release scripts.
- [x] Add NuGet metadata review checklist.
- [ ] Add signed package validation if signing is introduced.
- [x] Add release dry-run documentation.

### Community And Maintenance

- [x] Add issue templates.
- [x] Add pull request template.
- [x] Add contribution roadmap.
- [x] Add examples for common architecture styles.
- [x] Add compatibility matrix for supported .NET versions.
- [x] Add public API approval workflow if API churn increases.
