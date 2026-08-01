# Security Policy

## Supported Versions

Security updates are provided for the latest source on the active development branch and for any published package versions that are still maintained.

## Reporting A Vulnerability

Please do not open a public GitHub issue for a suspected vulnerability.

Report security concerns privately by using GitHub security advisories for this repository when available, or by contacting the maintainer directly through the repository owner profile.

Include:

- A clear description of the issue.
- A minimal reproduction or affected API/package version.
- Any known impact or exploit path.
- Suggested mitigation, if known.

## Scope

Security reports are especially useful for:

- Generated code that can register unintended services.
- Build-time code execution risks.
- Package contents that include unexpected files.
- Diagnostics or generated source that expose secrets.

Injectlynx should not execute user configuration code, perform runtime assembly scanning, or read application secrets during generation.
