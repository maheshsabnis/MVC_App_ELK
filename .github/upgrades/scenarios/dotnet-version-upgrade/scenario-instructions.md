# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0 (.NET 10 LTS)
- **Scope**: MVC_App.sln
- **Assessment Input Mode**: solution
- **Source Control**: No Git repository detected; user confirmed direct workspace changes.

## Key Decisions Log
- User confirmed .NET 10 upgrade with automatic execution and direct workspace changes.
- Preserve existing Razor Pages functionality and application architecture.
- User confirmed the All-at-Once upgrade approach.
- Post-upgrade telemetry feature: user selected OpenTelemetry Collector plus standalone Aspire Dashboard, with telemetry services only in Docker Compose and the application running in Visual Studio. Include an optional application Dockerfile; preserve the existing SQL Server configuration.

## Upgrade Options
**Source**: .github/upgrades/scenarios/dotnet-version-upgrade/upgrade-options.md

### Strategy
- Upgrade Strategy: All-at-Once

## Strategy
**Selected**: All-at-Once
**Rationale**: One SDK-style .NET 8 project; no project dependencies or incompatible packages.

### Execution Constraints
- Verify SDK and SDK pinning before modifying the application.
- Update framework and related package references together, then restore and build the full solution.
- Fix compilation errors and all warnings without suppressions; run tests after the upgrade builds.
- Preserve routes, application configuration, database schema, and existing architecture; do not apply database migrations.
- No Git commits or branch operations because the workspace is not a Git repository.

## Build Tool Decisions
- **MVC_App.csproj**: Prefer IDE run_build; dotnet build is the fallback for detailed warning capture (SDK-style modern .NET web project).
- User authorized PowerShell to persist the telemetry smoke-test correction after editor-tool edits did not save, then rerun validation.
