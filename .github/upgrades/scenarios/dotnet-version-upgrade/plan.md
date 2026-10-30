# .NET 10 Upgrade Plan

## Overview

**Target**: MVC_App.sln to .NET 10 LTS
**Scope**: One SDK-style ASP.NET Core project, approximately 1,133 lines of code.

### Selected Strategy
**All-At-Once** — The project and related packages are upgraded in a single operation.
**Rationale**: One project on .NET 8 with no project dependencies, no incompatible packages, and one behavioral API finding.

Projects: `MVC_App/MVC_App.csproj`.

## Tasks

### 01-upgrade-mvc-app: Upgrade and validate MVC_App on .NET 10

Verify the installed SDK and any SDK pinning, capture the current build/test baseline, and upgrade `MVC_App/MVC_App.csproj` to net10.0 with compatible Entity Framework Core packages. The assessment recommends four EF Core package updates and flags exception-handler behavior; research startup, data access, runtime configuration, and the flagged API before changing code. Preserve the existing application architecture, routes, configuration, and database schema.

Resolve compilation errors and all warnings in the modified project. Perform a full solution build, run available tests, audit packages for vulnerabilities, and automate non-destructive runtime smoke checks where feasible. Record unavailable database or deployment validation explicitly rather than claiming coverage.

**Done when**: The project targets net10.0; related packages restore successfully; the solution builds with zero warnings and errors; available tests pass; package audit findings are resolved or explicitly blocked; runtime checks and any environmental limitations are documented.
