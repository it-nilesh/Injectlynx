# Community

Injectlynx contributions are easiest to review when they stay close to a concrete developer workflow.

## Good Bug Reports

Include:

- Target framework and SDK version.
- The convention module or explicit registration that triggered the issue.
- Expected generated registration or diagnostic.
- Actual generated registration, diagnostic, or runtime behavior.
- A minimal reproduction when possible.

## Good Feature Requests

Describe:

- The application shape or architecture style.
- The registration code developers write today.
- The generated output you expect Injectlynx to create.
- Native AOT, trimming, analyzer, or code fix implications.

## Pull Requests

Keep changes focused. Add tests for behavioral changes, update docs for public API or diagnostics, and include sample updates when the feature is user-facing.

## Contribution Roadmap

Near-term contribution areas:

- Improve plugin loading ergonomics with configuration binding, dependency ordering, and CLI inspection.
- Expand analyzer and code fix coverage for unused explicit registrations and missing matching interfaces.
- Add focused documentation for advanced architecture styles and multi-project solutions.
- Harden release validation with public API approval when the surface area changes more frequently.

## Architecture Examples

Common project shapes:

- Modular monolith: one convention module per feature or layer, with generated method names such as `AddOrdersServices()` and `AddBillingServices()`.
- Clean architecture: application services registered by convention, infrastructure implementations registered explicitly, and forbidden dependency rules guarding inward dependencies.
- Web API plus workers: shared application services generated once and consumed by both HTTP endpoints and hosted services.
- Runtime extension host: compile-time registration for known host services, plus opt-in plugin loading for separately deployed integrations.

## Public API Approval

Public API changes are reviewed through `PublicApiApprovalTests`. When a public API change is intentional, update the approved baseline:

```bash
INJECTLYNX_UPDATE_PUBLIC_API=1 dotnet test tests/Injectlynx.ArchitectureTests/Injectlynx.ArchitectureTests.csproj -f net10.0 --filter PublicApiApprovalTests
```

Commit the baseline change with the code and documentation updates that justify it.
