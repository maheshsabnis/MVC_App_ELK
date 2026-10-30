# Projects and dependencies analysis

This document provides a comprehensive overview of the projects and their dependencies in the context of upgrading to .NETCoreApp,Version=v10.0.

## Table of Contents

- [Executive Summary](#executive-Summary)
  - [Highlevel Metrics](#highlevel-metrics)
  - [Projects Compatibility](#projects-compatibility)
  - [Package Compatibility](#package-compatibility)
  - [API Compatibility](#api-compatibility)
  - [Binding Redirect Configuration](#binding-redirect-configuration)
- [Aggregate NuGet packages details](#aggregate-nuget-packages-details)
- [Top API Migration Challenges](#top-api-migration-challenges)
  - [Technologies and Features](#technologies-and-features)
  - [Most Frequent API Issues](#most-frequent-api-issues)
- [Projects Relationship Graph](#projects-relationship-graph)
- [Project Details](#project-details)

  - [MVC_App\MVC_App.csproj](#mvc_appmvc_appcsproj)


## Executive Summary

### Highlevel Metrics

| Metric | Count | Status |
| :--- | :---: | :--- |
| Total Projects | 1 | All require upgrade |
| Total NuGet Packages | 75 | 4 need upgrade |
| Total Code Files | 30 |  |
| Total Code Files with Incidents | 2 |  |
| Total Lines of Code | 1133 |  |
| Total Number of Issues | 6 |  |
| Estimated LOC to modify | 1+ | at least 0.1% of codebase |

### Projects Compatibility

| Project | Target Framework | Difficulty | Package Issues | API Issues | Binding Issues | Est. LOC Impact | Description |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| [MVC_App\MVC_App.csproj](#mvc_appmvc_appcsproj) | net8.0 | 🟢 Low | 4 | 1 | 0 | 1+ | AspNetCore, Sdk Style = True |

### Package Compatibility

| Status | Count | Percentage |
| :--- | :---: | :---: |
| ✅ Compatible | 71 | 94.7% |
| ⚠️ Incompatible | 0 | 0.0% |
| 🔄 Upgrade Recommended | 4 | 5.3% |
| ***Total NuGet Packages*** | ***75*** | ***100%*** |

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 1 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 8926 |  |
| ***Total APIs Analyzed*** | ***8927*** |  |

## Aggregate NuGet packages details

| Package | Current Version | Suggested Version | Projects | Description |
| :--- | :---: | :---: | :--- | :--- |
| Azure.Core | 1.25.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Azure.Identity | 1.7.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Humanizer.Core | 2.14.1 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Bcl.AsyncInterfaces | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.CodeAnalysis.Analyzers | 3.3.3 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.CodeAnalysis.Common | 4.5.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.CodeAnalysis.CSharp | 4.5.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.CodeAnalysis.CSharp.Workspaces | 4.5.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.CodeAnalysis.Workspaces.Common | 4.5.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.CSharp | 4.5.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Data.SqlClient | 5.1.1 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Data.SqlClient.SNI.runtime | 5.1.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.EntityFrameworkCore | 8.0.0 | 10.0.12 | [MVC_App.csproj](#mvc_appmvc_appcsproj) | NuGet package upgrade is recommended |
| Microsoft.EntityFrameworkCore.Abstractions | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.EntityFrameworkCore.Analyzers | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.EntityFrameworkCore.Design | 8.0.0 | 10.0.12 | [MVC_App.csproj](#mvc_appmvc_appcsproj) | NuGet package upgrade is recommended |
| Microsoft.EntityFrameworkCore.Relational | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.0 | 10.0.12 | [MVC_App.csproj](#mvc_appmvc_appcsproj) | NuGet package upgrade is recommended |
| Microsoft.EntityFrameworkCore.Tools | 8.0.0 | 10.0.12 | [MVC_App.csproj](#mvc_appmvc_appcsproj) | NuGet package upgrade is recommended |
| Microsoft.Extensions.Caching.Abstractions | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Extensions.Caching.Memory | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Extensions.Configuration.Abstractions | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Extensions.DependencyInjection | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Extensions.DependencyModel | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Extensions.Logging | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Extensions.Logging.Abstractions | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Extensions.Options | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Extensions.Primitives | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Identity.Client | 4.47.2 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Identity.Client.Extensions.Msal | 2.19.3 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.IdentityModel.Abstractions | 6.24.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.IdentityModel.JsonWebTokens | 6.24.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.IdentityModel.Logging | 6.24.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.IdentityModel.Protocols | 6.24.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.IdentityModel.Protocols.OpenIdConnect | 6.24.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.IdentityModel.Tokens | 6.24.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.NETCore.Platforms | 1.1.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.NETCore.Targets | 1.1.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.SqlServer.Server | 1.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Microsoft.Win32.SystemEvents | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| Mono.TextTemplating | 2.2.1 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.CodeDom | 4.4.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Collections.Immutable | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Composition | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Composition.AttributedModel | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Composition.Convention | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Composition.Hosting | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Composition.Runtime | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Composition.TypedParts | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Configuration.ConfigurationManager | 6.0.1 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Diagnostics.DiagnosticSource | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Drawing.Common | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Formats.Asn1 | 5.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.IdentityModel.Tokens.Jwt | 6.24.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.IO.Pipelines | 6.0.3 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Memory | 4.5.4 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Memory.Data | 1.0.2 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Numerics.Vectors | 4.5.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Reflection.Metadata | 6.0.1 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Runtime | 4.3.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Runtime.Caching | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Security.AccessControl | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Security.Cryptography.Cng | 5.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Security.Cryptography.ProtectedData | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Security.Permissions | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Security.Principal.Windows | 5.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Text.Encoding | 4.3.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Text.Encoding.CodePages | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Text.Encodings.Web | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Text.Json | 8.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Threading.Channels | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Threading.Tasks.Extensions | 4.5.4 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |
| System.Windows.Extensions | 6.0.0 |  | [MVC_App.csproj](#mvc_appmvc_appcsproj) | ✅Compatible |

## Top API Migration Challenges

### Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |

### Most Frequent API Issues

| API | Count | Percentage | Category |
| :--- | :---: | :---: | :--- |
| M:Microsoft.AspNetCore.Builder.ExceptionHandlerExtensions.UseExceptionHandler(Microsoft.AspNetCore.Builder.IApplicationBuilder,System.String) | 1 | 100.0% | Behavioral Change |

## Projects Relationship Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart LR
    P1["<b>📦&nbsp;MVC_App.csproj</b><br/><small>net8.0</small>"]
    click P1 "#mvc_appmvc_appcsproj"

```

## Project Details

<a id="mvc_appmvc_appcsproj"></a>
### MVC_App\MVC_App.csproj

#### Project Info

- **Current Target Framework:** net8.0
- **Proposed Target Framework:** net10.0
- **SDK-style**: True
- **Project Kind:** AspNetCore
- **Dependencies**: 0
- **Dependants**: 0
- **Number of Files**: 35
- **Number of Files with Incidents**: 2
- **Lines of Code**: 1133
- **Estimated LOC to modify**: 1+ (at least 0.1% of the project)

#### Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph current["MVC_App.csproj"]
        MAIN["<b>📦&nbsp;MVC_App.csproj</b><br/><small>net8.0</small>"]
        click MAIN "#mvc_appmvc_appcsproj"
    end

```

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 1 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 8926 |  |
| ***Total APIs Analyzed*** | ***8927*** |  |

