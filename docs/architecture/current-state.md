# Current State

## Repository Status

Injectlynx is now a DSL-first compile-time dependency injection toolkit. The active solution keeps the core model, generator, primary package, Minimal API sample, Web API sample, Worker Service sample, Native AOT sample, and focused tests. Reserved future tooling projects are present for analyzers, code fixes, and CLI inspection work, but they are not part of the primary package surface.

## Existing Functionality

Implemented functionality includes:

- Strongly typed module-level C# convention DSL for attribute-free service discovery.
- Incremental source generation for convention-based, implemented-interface, self, matching-interface-and-self, and open generic registrations.
- Constructor, dependency graph, lifetime, duplicate registration, and DSL diagnostics.
- NuGet packaging for the primary package.
- Consumer validation scripts for packaged generation.
- Native AOT validation through a DSL-based sample.
- Release verification scripts for package contents, checksums, dependency vulnerabilities, SBOM output, package payload manifests, and normalized reproducible packages.

## Package Boundaries

`Injectlynx.Core` contains Roslyn-free semantic models. `Injectlynx.Generator` owns Roslyn discovery and generated source output. `Injectlynx` assembles the consumer package and public DSL surface.

`Injectlynx.Analyzers`, `Injectlynx.CodeFixes`, and `Injectlynx.Cli` are reserved for future tooling. Keep them buildable, but do not package or document them as consumer-facing features until their rules and UX are stable.

## Generator Architecture

The generator uses incremental Roslyn APIs, syntax-first discovery, restricted C# DSL parsing, immutable models, deterministic ordering, and direct `Microsoft.Extensions.DependencyInjection` registration output. Runtime service discovery and assembly scanning remain intentionally out of scope.
