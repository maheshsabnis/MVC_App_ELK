# 01-upgrade-mvc-app: Upgrade and validate MVC_App on .NET 10

Verify the installed SDK and any SDK pinning, capture the current build/test baseline, and upgrade `MVC_App/MVC_App.csproj` to net10.0 with compatible Entity Framework Core packages. The assessment recommends four EF Core package updates and flags exception-handler behavior; research startup, data access, runtime configuration, and the flagged API before changing code. Preserve the existing application architecture, routes, configuration, and database schema.

Resolve compilation errors and all warnings in the modified project. Perform a full solution build, run available tests, audit packages for vulnerabilities, and automate non-destructive runtime smoke checks where feasible. Record unavailable database or deployment validation explicitly rather than claiming coverage.

**Done when**: The project targets net10.0; related packages restore successfully; the solution builds with zero warnings and errors; available tests pass; package audit findings are resolved or explicitly blocked; runtime checks and any environmental limitations are documented.

## Research and Scope Inventory
- Affected project: MVC_App/MVC_App.csproj; SDK-style web application, net8.0, nullable enabled, implicit usings enabled. No local MSBuild imports, central package management, or global.json were found. TargetFramework and all four direct EF Core package versions are defined in the project file.
- SDK validation passed; installed SDK is 10.0.401. Baseline IDE build succeeded; dotnet test completed successfully but no test projects exist, so no test cases executed.
- Queried global and project assessment summary and list_issues: Project.0002 requires net10.0; NuGet.0002 recommends EF Core, SqlServer, Design, and Tools from 8.0.0 to 10.0.12. Supported-version tool independently confirmed EF Core 10.0.12.
- Program.cs registers both Razor Pages and MVC and maps both. Keep these registrations and routes unchanged. CompanyContext has Department and Employee entities, simple relational mappings, no seed data or migration calls.
- Api.0003 flags UseExceptionHandler("/Error"). Official .NET 10 guidance describes telemetry suppression only when IExceptionHandler.TryHandleAsync returns true. No custom IExceptionHandler is registered here, so preserve the existing handler without an unnecessary override.
- Reviewed official EF Core 9 and 10 breaking-change documentation. No migration execution, multi-targeting, JSON/complex-type mapping, custom provider internals, or compiled-query usage requires source changes. Database integration remains a runtime validation concern; no schema changes will be applied.
- Baseline public NuGet audit found seven vulnerable transitive packages: Azure.Identity 1.7.0, Microsoft.Data.SqlClient 5.1.1, Microsoft.Extensions.Caching.Memory 8.0.0, Microsoft.IdentityModel.JsonWebTokens 6.24.0, System.Formats.Asn1 5.0.0, System.IdentityModel.Tokens.Jwt 6.24.0, System.Text.Json 8.0.0. Re-audit after EF upgrades.
- Default vulnerability audit failed with HTTP 401 from a configured feed; public NuGet source audit succeeded. Do not change user feed configuration.
- Source scan found no STUB markers or applicable C# 13/14 transformation candidates. Preserve existing syntax and let net10.0 select C# 14 implicitly.

## Execution Checklist
1. Update net10.0 and all four EF Core package references atomically, preserving private asset metadata.
2. Restore; build; resolve warnings and errors; audit direct and transitive dependencies.
3. Run available test discovery/execution and non-destructive HTTP smoke checks for Razor Pages and static assets. Do not execute CRUD writes or database migrations.
4. Record results and limitations in progress-details.md before completing the task.
