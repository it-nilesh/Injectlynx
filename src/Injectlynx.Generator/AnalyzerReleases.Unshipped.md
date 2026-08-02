; Unshipped analyzer release

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
INJ001 | Injectlynx.Registration | Error | Missing matching interface.
INJ002 | Injectlynx.Registration | Error | Ambiguous service contract.
INJ003 | Injectlynx.Registration | Warning | Duplicate service registration.
INJ004 | Injectlynx.Registration | Error | Missing implemented interfaces.
INJ005 | Injectlynx.Registration | Warning | Keyed registration target may be unsupported.
INJ101 | Injectlynx.Constructors | Error | No public constructor.
INJ102 | Injectlynx.Constructors | Error | Ambiguous constructors.
INJ201 | Injectlynx.Dependencies | Warning | Missing dependency.
INJ202 | Injectlynx.Dependencies | Error | Circular dependency.
INJ203 | Injectlynx.Dependencies | Error | Self dependency.
INJ210 | Injectlynx.Lifetimes | Error | Captive scoped dependency.
INJ301 | Injectlynx.Decorators | Error | Decorator target is not generated.
INJ302 | Injectlynx.Decorators | Error | Decorator does not implement service contract.
INJ303 | Injectlynx.Decorators | Error | Decorator targets only keyed registrations.
INJ304 | Injectlynx.Decorators | Error | Decorator captures scoped dependency.
INJ401 | Injectlynx.Architecture | Error | Forbidden architecture dependency.
INJ504 | Injectlynx.ConfigurationDsl | Error | Invalid Injectlynx convention DSL.
INJ900 | Injectlynx.Development | Info | Opt-in development registration report.
