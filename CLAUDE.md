# CLAUDE.md - Famick Home Management

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Famick Home Management** is a household management application built with .NET 10, EF Core 10, and ASP.NET Core 10. The source code is publicly available under the Elastic License 2.0. It provides inventory tracking, equipment/vehicle management, shopping lists, recipes, contacts, chores, and more.

**Repository**: `Famick-com/FamickHomeManagement` (public, Elastic License 2.0)

This repository is **standalone** — it contains the shared libraries, the self-hosted web application, the Blazor WebAssembly client, and the .NET MAUI native mobile app. Clone it, run `dotnet build`, and everything works without any external dependencies.

A separate **private** repo (`HomeManagement-Cloud`) consumes this repo as a submodule and adds the multi-tenant cloud SaaS layer (`app.famick.com` + `famick.com` marketing + Phase-5 `auth.famick.com`). If you have access to the private repo, see its own `CLAUDE.md` for the cloud-dev workflow. **You don't need cloud access to use this repo.**

**Migration Context**: This project is migrating from Grocy (PHP/SQLite) to .NET 10/PostgreSQL.

---

## Repository Structure

```
FamickHomeManagement/                  # PUBLIC repo (Elastic License 2.0)
├── .github/workflows/
│   ├── testflight.yml                # iOS TestFlight CI
│   └── play-store.yml                # Android Play Store CI
├── .vscode/
│   ├── launch.json
│   ├── settings.json
│   └── tasks.json
├── src/
│   ├── Famick.HomeManagement.Domain/           # Entities, enums, base classes
│   ├── Famick.HomeManagement.Core/             # Interfaces, DTOs, validators, mapping
│   ├── Famick.HomeManagement.Infrastructure/   # EF Core, service implementations, migrations, plugins
│   ├── Famick.HomeManagement.Web.Shared/       # Shared API controllers (v1/)
│   ├── Famick.HomeManagement.UI/               # Razor Class Library (Blazor components, pages, localization)
│   ├── Famick.HomeManagement.Shared/           # Shared utilities (canonicalization, captcha, rate-limit, etc.)
│   ├── Famick.HomeManagement.FeatureFlags/     # Microsoft.FeatureManagement wrapper + flag constants
│   ├── Famick.HomeManagement.Logging.Redaction/  # Serilog enricher + redactors (auth headers, tokens, paths)
│   ├── Famick.HomeManagement.Messaging/        # Cross-process messaging primitives
│   ├── Famick.HomeManagement.Jobs/             # IJob abstractions + runner
│   ├── Famick.HomeManagement.Web/              # Self-hosted web application (ASP.NET Core)
│   ├── Famick.HomeManagement.Web.Client/       # Blazor WebAssembly client
│   └── Famick.HomeManagement.Mobile/           # MAUI native mobile app (MVVM)
├── tests/
│   ├── Famick.HomeManagement.Shared.Tests.Unit/
│   ├── Famick.HomeManagement.Shared.Tests.Integration/
│   ├── Famick.HomeManagement.Tests.Unit/
│   ├── Famick.HomeManagement.Tests.Integration/
│   ├── Famick.HomeManagement.TestSupport/        # Testcontainers fixtures + JWT helpers
│   ├── Famick.HomeManagement.TestSupport.Tests/
│   ├── Famick.HomeManagement.FeatureFlags.Tests.Unit/
│   ├── Famick.HomeManagement.Logging.Redaction.Tests.Unit/
│   └── Famick.HomeManagement.Messaging.Tests.Unit/
├── self-hosted/                       # Self-hosted deployment strategies
│   ├── README.md                      # Strategy comparison + links
│   ├── docker-compose/                # Working strategy
│   │   ├── docker-compose.yml
│   │   ├── docker-compose.dev.yml
│   │   ├── docker-compose.libpostal.yml
│   │   ├── Dockerfile (+ Dockerfile.dev)
│   │   ├── setup.sh / start.sh / stop.sh
│   │   ├── publish-dockerhub.sh
│   │   ├── init-db.sql / scheduler-crontab / admin-cli
│   │   ├── config/                    # server-config.json overlay lives here
│   │   └── plugins/                   # Plugin DLLs + config.json (volume-mounted)
│   ├── proxmox/                       # Working LXC installer script
│   ├── kubernetes-helm/               # Planned (README stub only)
│   └── home-assistant-plugin/         # Planned (README stub only)
├── scripts/                           # Build and mobile-publish scripts
│   ├── build-testflight.sh
│   ├── build-play-store.sh
│   ├── move-to-server.sh
│   └── start-db.sh / stop-db.sh
├── docs/
│   ├── architecture.md
│   ├── author-plugins.md          # Redirect to Plugins-Abstraction repo
│   └── STORE_INTEGRATIONS.md      # Redirect to Plugins-Abstraction repo
├── Famick.sln                         # Solution file — all public projects
├── LICENSE                            # Elastic License 2.0
├── COPYRIGHT
├── CONTRIBUTING.md
├── CLAUDE.md                          # This file
├── README.md
└── GITHUB_SETUP.md
```

---

## Architecture Overview

### Layer Diagram

```
┌─────────────────────────────────────────────────────────┐
│  Presentation Layer                                      │
│  ┌──────────────────┐  ┌──────────────────────────────┐ │
│  │ Web.Shared        │  │ UI (Razor Class Library)      │ │
│  │ 23 API Controllers│  │ Blazor components & pages     │ │
│  │ + 4 Auth/Base     │  │ Localization, Theme, Services │ │
│  └──────────────────┘  └──────────────────────────────┘ │
├─────────────────────────────────────────────────────────┤
│  Application Layer (Core)                                │
│  38 service interfaces, DTOs (17 categories),            │
│  validators (13 categories), mapping profiles            │
├─────────────────────────────────────────────────────────┤
│  Infrastructure Layer                                    │
│  30+ service implementations, EF Core DbContext,         │
│  37 entity configurations, 45+ migrations,               │
│  plugin system (OpenFoodFacts, USDA); Kroger via NuGet   │
├─────────────────────────────────────────────────────────┤
│  Domain Layer                                            │
│  69+ entities, 20 enums, base classes                    │
│  (BaseEntity, BaseTenantEntity)                          │
└─────────────────────────────────────────────────────────┘
```

### Key Domain Entity Groups

| Group | Entities |
|-------|----------|
| **User & Auth** | User, UserExternalLogin, UserPasskeyCredential, UserRole, UserPermission, Permission, RefreshToken, PasswordResetToken, EmailVerificationToken, UserJwtMinIat, UserAuditLog |
| **Home & Property** | Home, HomeUtility, PropertyLink, Tenant, TenantIntegrationToken |
| **Contacts** | Contact (self-referencing group/member hierarchy via ParentContactId; ContactType: Household/Business; IsTenantHousehold flag), ContactAddress, ContactEmailAddress, ContactPhoneNumber, ContactRelationship, ContactSocialMedia, ContactTag, ContactTagLink, ContactUserShare, ContactAuditLog |
| **Products & Stock** | Product, ProductBarcode, ProductGroup, ProductImage, ProductNutrition, ProductStoreMetadata, StockEntry, StockLog, QuantityUnit, Location |
| **Equipment** | Equipment, EquipmentCategory, EquipmentDocument, EquipmentDocumentTag, EquipmentMaintenanceRecord, EquipmentUsageLog |
| **Vehicles** | Vehicle, VehicleDocument, VehicleMaintenanceRecord, VehicleMaintenanceSchedule, VehicleMileageLog |
| **Recipes** | Recipe, RecipeNesting, RecipePosition, RecipeStep, RecipeImage, RecipeShareToken |
| **Shopping** | ShoppingList, ShoppingListItem, ShoppingLocation |
| **Calendar** | CalendarEvent, CalendarEventException, CalendarEventMember, ExternalCalendarEvent, ExternalCalendarSubscription, UserCalendarIcsToken |
| **Notifications** | Notification, NotificationPreference, UserDeviceToken |
| **Other** | Chore, ChoreLog, TodoItem, StorageBin, StorageBinPhoto, Address |

### API Controllers (v1/)

AddressController, ChoresController, ConfigurationController, ContactsController, EquipmentController, HomeController, LocationsController, ProductGroupsController, ProductLookupController, ProductsController, ProfileController, QuantityUnitsController, RecipesController, ShoppingListsController, ShoppingLocationsController, StockController, StorageBinsController, StoreIntegrationsController, TenantController, TodoItemsController, UsersController, VehiclesController, WizardController

Plus base/auth controllers: ApiControllerBase, AuthApiController, ExternalAuthApiController, PasskeyApiController, SetupApiController

### UI Structure (Razor Class Library)

```
src/Famick.HomeManagement.UI/
├── Components/
│   ├── Home/
│   │   ├── HomeSetupWizard.razor              # 5-page wizard orchestrator
│   │   ├── HomeUtilityDialog.razor
│   │   └── SetupWizard/
│   │       ├── Steps/
│   │       │   ├── HouseholdInfoStep.razor    # Page 1 (required)
│   │       │   ├── HouseholdMembersStep.razor # Page 2 (skippable)
│   │       │   ├── HomeStatisticsStep.razor   # Page 3 (skippable)
│   │       │   ├── MaintenanceItemsStep.razor # Page 4 (skippable)
│   │       │   └── VehiclesStep.razor         # Page 5 (skippable)
│   │       └── Components/
│   │           ├── MemberEditor.razor
│   │           ├── DuplicateContactDialog.razor
│   │           └── VehicleEditorDialog.razor
│   ├── Settings/, Shopping/, Products/, Forms/, Layout/
│   ├── Contacts/, Shared/, Common/, Inventory/
│   ├── StorageBins/, Todos/, Authentication/, Equipment/
├── Pages/
│   ├── Home/ (MyHome.razor - supports ?rerun=true for wizard re-run)
│   ├── Settings/ (Settings.razor - includes Home Setup re-run section)
│   ├── Chores/, Tasks/, Shopping/, Products/, Contacts/
│   ├── Stores/, ShoppingLists/, Inventory/, StorageBins/
│   ├── Todos/, Authentication/, Equipment/
├── Services/         # Client-side Blazor services
├── Theme/            # MudBlazor theming
├── Localization/     # LocalizationService
└── wwwroot/
    ├── locales/en.json  # Localization strings
    ├── css/, js/, images/
```

---

## Development Workflows

### Clone and Setup

```bash
git clone git@github.com:Famick-com/FamickHomeManagement.git
cd FamickHomeManagement

# Open solution
code Famick.sln

# Build everything
dotnet build

# Run self-hosted web app
dotnet run --project src/Famick.HomeManagement.Web

# Run tests
dotnet test
```

No submodule init required — the repo is fully self-contained.

### Self-Hosted Docker Quick-Start

```bash
# Start PostgreSQL for development
./scripts/start-db.sh

# Or use docker-compose for full self-hosted stack
cd self-hosted/docker-compose
./start.sh         # runs setup.sh on first invocation, then `docker compose up -d`

# Stop
./stop.sh
```

---

## Git Workflow

Standard git — no submodule coordination needed for any change in this repo:

```bash
git add src/Famick.HomeManagement.Core/SomeFile.cs
git commit -m "feat: add new stock management feature"
git push origin main
```

### Cross-Repo Changes (Cloud Maintainers Only)

If a feature requires changes here AND in the private cloud repo, work happens in the cloud repo's working dir (which has this repo as a submodule at `famick-home-management/`). Make changes here in the submodule first, push them, then update the submodule pointer in cloud. See the cloud repo's `CLAUDE.md` for the full workflow.

---

## Solution File

**`Famick.sln`** at the repo root contains all 22 projects (13 src + 9 tests). Builds standalone with no external dependencies.

**Source Projects (src/, 13 projects)**:
- Famick.HomeManagement.Domain
- Famick.HomeManagement.Core
- Famick.HomeManagement.Infrastructure
- Famick.HomeManagement.Web.Shared
- Famick.HomeManagement.UI
- Famick.HomeManagement.Shared
- Famick.HomeManagement.FeatureFlags
- Famick.HomeManagement.Logging.Redaction
- Famick.HomeManagement.Messaging
- Famick.HomeManagement.Jobs
- Famick.HomeManagement.Web (self-hosted)
- Famick.HomeManagement.Web.Client (Blazor WebAssembly)
- Famick.HomeManagement.Mobile (MAUI native)

**Test Projects (tests/, 9 projects)**:
- Famick.HomeManagement.Shared.Tests.Unit
- Famick.HomeManagement.Shared.Tests.Integration
- Famick.HomeManagement.Tests.Unit
- Famick.HomeManagement.Tests.Integration
- Famick.HomeManagement.TestSupport
- Famick.HomeManagement.TestSupport.Tests
- Famick.HomeManagement.FeatureFlags.Tests.Unit
- Famick.HomeManagement.Logging.Redaction.Tests.Unit
- Famick.HomeManagement.Messaging.Tests.Unit

---

## Mobile App

The mobile app is a **.NET MAUI Native** application using the **MVVM pattern** (NOT Blazor Hybrid).

- **Location**: `src/Famick.HomeManagement.Mobile/`
- **Pattern**: MVVM with `CommunityToolkit.Mvvm` (v8.4.0)
- **Messaging**: Use `WeakReferenceMessenger` from `CommunityToolkit.Mvvm.Messaging` (NOT `MessagingCenter`, which is internal in .NET 10 MAUI)
- **Message types**: Use `ValueChangedMessage<T>` from `CommunityToolkit.Mvvm.Messaging.Messages`
- **CI/CD**: TestFlight via `.github/workflows/testflight.yml`, Play Store via `.github/workflows/play-store.yml`
- **Build scripts**: `scripts/build-testflight.sh`, `scripts/build-play-store.sh`

---

## Multi-Tenancy Architecture

### Shared Code is Configurable

The shared libraries support both deployment models through configuration:

**Self-Hosted (Single-Tenant)**:
```csharp
builder.Services.AddSingleton<IMultiTenancyOptions>(new MultiTenancyOptions
{
    IsMultiTenantEnabled = false,
    FixedTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001")
});
builder.Services.AddSingleton<ITenantProvider, FixedTenantProvider>();
```

**Cloud (Multi-Tenant)** — handled in the private repo:
```csharp
builder.Services.AddSingleton<IMultiTenancyOptions>(new MultiTenancyOptions
{
    IsMultiTenantEnabled = true,
    FixedTenantId = null  // Dynamic tenant resolution
});
builder.Services.AddScoped<ITenantProvider, HttpContextTenantProvider>();
app.UseMiddleware<TenantResolutionMiddleware>();
```

### Key Principle

**Same codebase, different configuration.** The shared libraries here contain no cloud-specific features (billing, subscriptions, Stripe, SendGrid, S3 storage, push notifications, etc.). Those live exclusively in the private cloud repo.

---

## Authentication Architecture

Each deployment model handles authentication differently:

**Self-Hosted**:
- Email/Password + Passkeys only
- Optional OpenID Connect (configurable per deployment)
- No social login (Google/Apple) by default — no OAuth proxy needed

**Cloud** (private repo):
- Email/Password + Passkeys
- Google Sign-In via native iOS/Android SDKs (no server proxy)
- Apple Sign-In via native iOS/Android SDKs (no server proxy)
- Mobile OAuth flows route through `app.famick.com`

**Shared Auth Components** (in `src/Famick.HomeManagement.Web.Shared/`):
- JWT with refresh tokens (`AuthApiController`)
- Passkey/WebAuthn (`PasskeyApiController`)
- External auth provider integration (`ExternalAuthApiController`)

---

## Localization

### How It Works

Localization strings live in `src/Famick.HomeManagement.UI/wwwroot/locales/en.json`. The `LocalizationService` flattens the nested JSON into dot-notation keys at load time. Access strings in Razor components via `@L["key.path"]`.

### Key Rules

1. **Nested JSON objects become dot-separated keys.** A structure like:
   ```json
   {
     "settings": {
       "homeSetup": {
         "title": "Home Setup",
         "rerun": "Re-run Home Setup Wizard"
       }
     }
   }
   ```
   Produces keys: `settings.homeSetup.title`, `settings.homeSetup.rerun`.

2. **Object nodes are NOT string values.** `L["settings.homeSetup"]` will NOT resolve if `homeSetup` is an object — you must reference a leaf string like `L["settings.homeSetup.title"]`.

3. **Use `.title` for section headings** when a key has children. This matches the existing pattern (e.g., `settings.mobileAppSetup.title`, `home.setupWizard.title`).

4. **Always add keys when adding UI text.** Any `L["..."]` reference in a Razor file must have a corresponding entry in `en.json`. Missing keys render as the raw key string in the UI.

5. **Follow existing naming conventions:**
   - `common.*` — Shared labels (Save, Cancel, Edit, etc.)
   - `settings.*` — Settings page sections
   - `home.*` — Home/property related
   - `contact.*` — Contact fields and labels
   - `*.title` — Section/page titles
   - `*.description` / `*Desc` — Descriptive text

---

## Testing Strategy

### Test Projects

- `Famick.HomeManagement.Shared.Tests.Unit` — Unit tests for shared services
- `Famick.HomeManagement.Shared.Tests.Integration` — Integration tests with Docker/Testcontainers
- `Famick.HomeManagement.Tests.Unit` — Unit tests with fixed tenant configuration
- `Famick.HomeManagement.Tests.Integration` — Integration tests with Docker/Testcontainers
- `Famick.HomeManagement.TestSupport` — Testcontainers fixtures + JWT helpers shared across test projects
- Plus per-library tests: FeatureFlags, Logging.Redaction, Messaging

### Test Frameworks

- xUnit, FluentAssertions, Moq
- Microsoft.AspNetCore.Mvc.Testing
- Testcontainers.PostgreSql

### Test Both Modes

```csharp
[Fact]
public async Task StockService_WorksInSingleTenantMode()
{
    var options = new MultiTenancyOptions
    {
        IsMultiTenantEnabled = false,
        FixedTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001")
    };
    // Test...
}

[Fact]
public async Task StockService_WorksInMultiTenantMode()
{
    var options = new MultiTenancyOptions { IsMultiTenantEnabled = true };
    // Test...
}
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Famick.HomeManagement.Shared.Tests.Unit
```

---

## Current Project Status

**Source**: Grocy (PHP/SQLite household management system)
**Target**: .NET 10 / PostgreSQL with multi-tenancy

### What's Built

- 69+ domain entities across all major feature areas
- 38 service interfaces with 30+ implementations
- 27 API controllers (23 resource + 4 auth/base)
- Full Blazor Razor Class Library with components and pages
- Blazor WebAssembly client project
- 45+ EF Core migrations (PostgreSQL)
- Plugin system (OpenFoodFacts, USDA built-in; Kroger via `Famick-com/Plugin-Kroger` NuGet)
- Plugin interfaces extracted to `Famick-com/Plugins-Abstraction` (public, Apache-2.0)
- Authentication: JWT with refresh tokens, passkeys, native mobile OAuth (Google/Apple Sign-In via app.famick.com)
- Multi-tenant query filters and tenant resolution middleware
- 5-page onboarding wizard with skip/exit/re-run support
- .NET MAUI native mobile app with MVVM
- Self-hosted Docker deployment
- CI/CD for TestFlight and Play Store

---

## Best Practices

### Development

1. **Test in Both Modes** — When working on shared libraries, ensure changes work in BOTH self-hosted and cloud configurations. Use feature flags for optional functionality.

2. **Keep Public Code Cloud-Agnostic** — No cloud-specific features (Stripe, SendGrid, S3, push notifications, subscription billing) in this repo. Cloud features belong exclusively in the private cloud repo.

3. **Maintain Backwards Compatibility** — Avoid breaking changes to interfaces consumed by the cloud project.

4. **File Formatting** — NEVER use Windows line endings (CRLF / `\r\n`). Always use Unix line endings (LF / `\n`).

### .NET 10 MAUI Notes

- `MessagingCenter` is inaccessible (made internal) in .NET 10 MAUI. Use `WeakReferenceMessenger` from `CommunityToolkit.Mvvm.Messaging` instead.
- Define message types using `ValueChangedMessage<T>` from `CommunityToolkit.Mvvm.Messaging.Messages`.

### Mobile Logging — Don't Log HTTP Response Bodies on Auth Paths

`Console.WriteLine` / `Debug.WriteLine` output routes to the device console (Xcode / Android Studio / log-dump tools). When debugging an API call in `ShoppingApiClient`, log the status code, not the response body — auth/registration/refresh responses embed `AccessToken` and `RefreshToken` in plain text. The server-side redaction pipeline (`Famick.HomeManagement.Logging.Redaction`) doesn't reach the mobile app; redaction here is by convention. Other categories (status codes, error message strings, push-token first-8-chars) are fine.

---

## Quick Reference Commands

### Build and Run
```bash
# Self-hosted web app
dotnet run --project src/Famick.HomeManagement.Web

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Famick.HomeManagement.Tests.Unit
```

### Docker (Self-Hosted)
```bash
# Dev database
./scripts/start-db.sh
./scripts/stop-db.sh

# Full production stack
cd self-hosted/docker-compose
./start.sh
./stop.sh

# Publish to Docker Hub
./self-hosted/docker-compose/publish-dockerhub.sh <version>
```

### Mobile Builds
```bash
./scripts/build-testflight.sh
./scripts/build-play-store.sh
```

---

## Release tag scheme (CI triggers)

The repo has three deploy/publish workflows that historically all listened
on `v*` tags. A single tag would silently fire TestFlight + Play Store +
Docker image builds at once, even when the change only touched one. Tags
are now namespaced so each release train is independent:

| Tag prefix    | Fires                                            |
|---------------|--------------------------------------------------|
| `mobile-v*`   | `testflight.yml` + `play-store.yml`              |
| `image-v*`    | `docker-image.yml` (canonical Docker Hub image)  |

Examples:
- `git tag mobile-v1.0.0-beta50 && git push origin mobile-v1.0.0-beta50` → mobile-only deploy
- `git tag image-v1.0.0-beta50 && git push origin image-v1.0.0-beta50` → image-only publish

Historical `v*` tags remain valid as past releases; nothing rewrites them.
New work that needs a CI fire must use the namespaced form.

---

## Using Gemini CLI for Large Codebase Analysis

When analyzing large codebases or multiple files that might exceed context limits, use the Gemini CLI with its massive context window. Use `gemini -p` to leverage Google Gemini's large context capacity.

### File and Directory Inclusion Syntax

Use the `@` syntax to include files and directories in your Gemini prompts. The paths should be relative to WHERE you run the gemini command:

```bash
# Single file analysis
gemini -p "@src/main.py Explain this file's purpose and structure"

# Multiple files
gemini -p "@package.json @src/index.js Analyze the dependencies used in the code"

# Entire directory
gemini -p "@src/ Summarize the architecture of this codebase"

# Multiple directories
gemini -p "@src/ @tests/ Analyze test coverage for the source code"

# Current directory and subdirectories
gemini -p "@./ Give me an overview of this entire project"

# Or use --all_files flag
gemini --all_files -p "Analyze the project structure and dependencies"
```

### Implementation Verification Examples

```bash
# Check if a feature is implemented
gemini -p "@src/ Has dark mode been implemented? Show the relevant files"

# Verify authentication implementation
gemini -p "@src/ Is JWT authentication implemented? List all auth-related endpoints"

# Verify test coverage
gemini -p "@src/Famick.HomeManagement.Core/ @tests/ Is the service layer fully tested?"
```

### When to Use Gemini CLI

Use `gemini -p` when:
- Analyzing entire codebases or large directories
- Comparing multiple large files
- Need to understand project-wide patterns or architecture
- Current context window is insufficient for the task
- Working with files totaling more than 100KB
- Verifying if specific features, patterns, or security measures are implemented

---

## Related Documentation

- **Architecture Document**: `docs/architecture.md`
- **Plugin Authoring Guide**: [Plugins-Abstraction/docs/author-plugins.md](https://github.com/Famick-com/Plugins-Abstraction/blob/main/docs/author-plugins.md)
- **Store Integrations**: [Plugins-Abstraction/docs/STORE_INTEGRATIONS.md](https://github.com/Famick-com/Plugins-Abstraction/blob/main/docs/STORE_INTEGRATIONS.md)
- **Plugin Interfaces**: [Famick-com/Plugins-Abstraction](https://github.com/Famick-com/Plugins-Abstraction) (public, Apache-2.0)
- **Kroger Plugin**: [Famick-com/Plugin-Kroger](https://github.com/Famick-com/Plugin-Kroger) (private, ELv2)
- **GitHub Setup / CI/CD**: `GITHUB_SETUP.md`
- **Contributing Guide**: `CONTRIBUTING.md`

---

## Maintaining This File

**This file is NOT automatically updated.** It is loaded at the start of every Claude Code session as context. When making architectural changes (adding/removing projects, changing auth flows, modifying infrastructure, etc.), update this file as part of the same commit to prevent drift. Key sections to keep current:
- Repository structure diagram
- Solution file structure
- Authentication/Infrastructure architecture
- Current project status

---

## License

- **FamickHomeManagement** (this repository): **Elastic License 2.0 (ELv2)**

---

Last Updated: 2026-05-24 (repo-restructure: standalone refactor, cloud is now a separate private repo)
