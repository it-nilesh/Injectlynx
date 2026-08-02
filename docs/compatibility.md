# Compatibility Matrix

Injectlynx keeps the main package broad enough for library adoption while validating modern application targets through samples and release scripts.

| Capability | Target | Status | Validation |
| --- | --- | --- | --- |
| Main package | `netstandard2.0` | Supported | `dotnet build Injectlynx.slnx` |
| Main package | `net8.0`, `net9.0`, `net10.0` | Supported | Full solution build |
| Source generator | Analyzer asset in the main package | Supported | Package verification and consumer validation |
| Minimal API sample | .NET 8, .NET 9, .NET 10 | Validated | Sample build |
| Web API sample | .NET 8, .NET 9, .NET 10 | Validated | `eng/validation/validate-webapi-sample.sh` |
| Worker Service sample | .NET 8, .NET 9, .NET 10 | Validated | `eng/validation/validate-worker-service-sample.sh` |
| Native AOT sample | Modern .NET publish path | Validated | `eng/validation/validate-native-aot.sh` |
| Dynamic plugin loading | Runtime plugin hosts | Opt-in | Plugin loader tests and sample host run |

The default compile-time DI path is the recommended choice for Native AOT and trimming-sensitive applications. Dynamic plugin loading intentionally uses runtime assembly loading and has different deployment tradeoffs.
