# CLAUDE.md - Famick Home Management

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Famick Home Management** is an open-source household management application built with .NET 10, EF Core 10, and ASP.NET Core 10. It provides inventory tracking, equipment/vehicle management, shopping lists, recipes, contacts, chores, and more.

**Repository**: `Famick-com/FamickHomeManagement` (public, AGPL-3.0)

The repository contains all shared libraries, the self-hosted web application, a Blazor WebAssembly client, and a .NET MAUI native mobile app. A single private git submodule (`homemanagement-cloud`) adds the multi-tenant cloud SaaS layer.

**Migration Context**: This project is migrating from Grocy (PHP/SQLite) to .NET 10/PostgreSQL.

---

## Repository Structure

```
FamickHomeManagement/                  # PUBLIC repo (AGPL-3.0)
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
│   ├── Famick.HomeManagement.Shared/           # Shared utilities
│   ├── Famick.HomeManagement.Web/              # Self-hosted web application (ASP.NET Core)
│   ├── Famick.HomeManagement.Web.Client/       # Blazor WebAssembly client
│   └── Famick.HomeManagement.Mobile/           # MAUI native mobile app (MVVM)
├── tests/
│   ├── Famick.HomeManagement.Shared.Tests.Unit/
│   ├── Famick.HomeManagement.Shared.Tests.Integration/
│   ├── Famick.HomeManagement.Tests.Unit/
│   └── Famick.HomeManagement.Tests.Integration/
├── docker/                            # Self-hosted Docker files (dev + production)
│   ├── docker-compose.yml
│   ├── docker-compose.dev.yml
│   ├── Dockerfile
│   ├── setup.sh
│   ├── init-db.sql
│   ├── admin-cli
│   └── config/
├── scripts/                           # Build, publish, and maintenance scripts
│   ├── build-testflight.sh
│   ├── build-play-store.sh
│   ├── publish-selfhosted-dockerhub.sh
│   ├── move-to-server.sh
│   ├── start-db.sh / stop-db.sh
│   └── start-production.sh / stop-production.sh
├── docs/
│   ├── architecture.md
│   ├── author-plugins.md
│   └── STORE_INTEGRATIONS.md
├── homemanagement-cloud/              # PRIVATE submodule (proprietary)
│   ├── homemanagement-cloud.sln       # Cloud-only solution file
│   ├── docker/
│   │   └── docker-compose.dev.yml
│   ├── scripts/
│   │   ├── start-db.sh
│   │   └── stop-db.sh
│   ├── src/
│   │   ├── Famick.HomeManagement.Cloud/                  # Cloud domain, services, plugins
│   │   ├── Famick.HomeManagement.Cloud.Infrastructure/   # HttpContextTenantProvider, S3, KMS
│   │   └── Famick.HomeManagement.Web/                    # Cloud web app (app.famick.com)
│   └── tests/
│       ├── Famick.HomeManagement.Cloud.Tests.Unit/
│       └── Famick.HomeManagement.Cloud.Tests.Integration/
├── Famick.sln                         # Master solution (all projects)
├── docker-compose.yml                 # Self-hosted quick-start
├── Dockerfile                         # Production self-hosted image
├── LICENSE                            # AGPL-3.0
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
│  37 entity configurations, 45 migrations,                │
│  plugin system (OpenFoodFacts, USDA, Kroger)             │
├─────────────────────────────────────────────────────────┤
│  Domain Layer                                            │
│  56 entities, 14 enums, base classes                     │
│  (BaseEntity, BaseTenantEntity)                          │
└─────────────────────────────────────────────────────────┘
```

### Key Domain Entity Groups

| Group | Entities |
|-------|----------|
| **User & Auth** | User, UserExternalLogin, UserPasskeyCredential, UserRole, UserPermission, Permission, RefreshToken, PasswordResetToken, EmailVerificationToken |
| **Home & Property** | Home, HomeUtility, PropertyLink, Tenant, TenantIntegrationToken |
| **Contacts** | Contact, ContactAddress, ContactEmailAddress, ContactPhoneNumber, ContactRelationship, ContactSocialMedia, ContactTag, ContactTagLink, ContactUserShare, ContactAuditLog |
| **Products & Stock** | Product, ProductBarcode, ProductGroup, ProductImage, ProductNutrition, ProductStoreMetadata, StockEntry, StockLog, QuantityUnit, Location |
| **Equipment** | Equipment, EquipmentCategory, EquipmentDocument, EquipmentDocumentTag, EquipmentMaintenanceRecord, EquipmentUsageLog |
| **Vehicles** | Vehicle, VehicleDocument, VehicleMaintenanceRecord, VehicleMaintenanceSchedule, VehicleMileageLog |
| **Recipes** | Recipe, RecipeNesting, RecipePosition |
| **Shopping** | ShoppingList, ShoppingListItem, ShoppingLocation |
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

## Git Submodule Configuration

This repository has a single submodule for the private cloud SaaS layer:

```ini
[submodule "homemanagement-cloud"]
    path = homemanagement-cloud
    url = git@github.com:Famick-com/HomeManagement-Cloud.git
```

All shared libraries, the self-hosted web app, mobile app, and test projects live directly in `src/` and `tests/` -- they are NOT submodules.

---

## Development Workflows

### Standard Development (Most Work)

Most code lives directly in this repository. No submodule coordination is needed for changes to shared libraries, the self-hosted web app, the mobile app, or tests.

```bash
# Clone with submodule
git clone --recursive git@github.com:Famick-com/FamickHomeManagement.git
cd FamickHomeManagement

# Open master solution
code Famick.sln

# Build everything
dotnet build

# Run self-hosted web app
dotnet run --project src/Famick.HomeManagement.Web

# Run tests
dotnet test
```

### Cloud Development (Requires Private Submodule Access)

Only needed when working on cloud-specific features (billing, subscriptions, multi-tenant middleware, push notifications, S3 storage, etc.).

```bash
# If submodule was not initialized
git submodule update --init --recursive

# Cloud projects are in the master solution (Famick.sln)
# Changes to cloud code require the submodule-first commit workflow (see below)
```

### Self-Hosted Docker Quick-Start

```bash
# Start PostgreSQL for development
./scripts/start-db.sh

# Or use docker-compose for full self-hosted stack
docker-compose up

# Stop
./scripts/stop-db.sh
```

---

## Git Workflow

### Changes to Public Code (Most Changes)

Shared libraries, self-hosted web app, mobile app, and tests all live directly in this repo. Standard git workflow applies:

```bash
git add src/Famick.HomeManagement.Core/SomeFile.cs
git commit -m "feat: add new stock management feature"
git push origin main
```

### Changes to Cloud Code (Submodule)

When modifying files inside `homemanagement-cloud/`, always commit the submodule BEFORE the parent:

```bash
# 1. Commit inside the submodule
cd homemanagement-cloud
git add .
git commit -m "feat(cloud): add subscription webhook"
git push origin main

# 2. Update parent to track the new submodule commit
cd ..
git add homemanagement-cloud
git commit -m "chore: update homemanagement-cloud submodule"
git push origin main
```

### Changes Spanning Both Repos

When a feature requires changes to both public code and cloud code:

```bash
# 1. Make all changes
# 2. Commit and push the submodule first
cd homemanagement-cloud
git add . && git commit -m "feat(cloud): cloud-side changes" && git push

# 3. Then commit public code + submodule pointer
cd ..
git add src/ tests/ homemanagement-cloud
git commit -m "feat: add feature spanning public and cloud code"
git push
```

### Updating the Cloud Submodule

```bash
# Pull latest cloud changes
cd homemanagement-cloud
git pull origin main
cd ..
git add homemanagement-cloud
git commit -m "chore: update homemanagement-cloud to latest"
```

---

## Solution File Structure

### Master Solution (Famick.sln)

Located at the repository root. Contains all projects from both the public repo and the cloud submodule:

**Source Projects (src/ folder, 9 projects)**:
- Famick.HomeManagement.Domain
- Famick.HomeManagement.Core
- Famick.HomeManagement.Infrastructure
- Famick.HomeManagement.Web.Shared
- Famick.HomeManagement.UI
- Famick.HomeManagement.Shared
- Famick.HomeManagement.Web (self-hosted)
- Famick.HomeManagement.Web.Client (Blazor WebAssembly)
- Famick.HomeManagement.Mobile (MAUI native)

**Cloud Projects (homemanagement-cloud/ folder, 3 projects)**:
- Famick.HomeManagement.Cloud (domain, services, plugins)
- Famick.HomeManagement.Cloud.Infrastructure (tenant provider, S3, KMS)
- Famick.HomeManagement.Web (cloud web app -- distinct from self-hosted Web)

**Test Projects (8 projects)**:
- Famick.HomeManagement.Shared.Tests.Unit
- Famick.HomeManagement.Shared.Tests.Integration
- Famick.HomeManagement.Tests.Unit (self-hosted)
- Famick.HomeManagement.Tests.Integration (self-hosted)
- Famick.HomeManagement.Cloud.Tests.Unit
- Famick.HomeManagement.Cloud.Tests.Integration

### Cloud Solution (homemanagement-cloud/homemanagement-cloud.sln)

Standalone solution for cloud-only development:
- 3 cloud source projects + 2 cloud test projects
- References shared projects from the parent `src/` directory via relative paths

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

**Cloud (Multi-Tenant)**:
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

**Same codebase, different configuration.** The shared libraries (in `src/`) contain no cloud-specific features (billing, subscriptions, Stripe, SendGrid, S3 storage, push notifications, etc.). Those live exclusively in `homemanagement-cloud/`.

---

## Authentication Architecture

Each deployment model handles authentication differently:

**Self-Hosted**:
- Email/Password + Passkeys only
- Optional OpenID Connect (configurable per deployment)
- No social login (Google/Apple) -- no OAuth proxy needed

**Cloud**:
- Email/Password + Passkeys
- Google Sign-In via native iOS/Android SDKs (no server proxy)
- Apple Sign-In via native iOS/Android SDKs (no server proxy)
- Mobile OAuth flows route through `app.famick.com`

**Shared Auth Components** (in `src/Famick.HomeManagement.Web.Shared/`):
- JWT with refresh tokens (`AuthApiController`)
- Passkey/WebAuthn (`PasskeyApiController`)
- External auth provider integration (`ExternalAuthApiController`)

---

## Cloud Infrastructure

All cloud infrastructure configuration lives inside the `homemanagement-cloud/` submodule.

**AWS Services** (2 App Runner services):
- **Marketing**: `famick.com` -- Marketing website (Famick.Marketing.Web, uses LandingController)
- **Cloud App**: `app.famick.com` -- Cloud SaaS application (HomeManagement.Web)
- **Database**: RDS PostgreSQL
- **Cache**: ElastiCache Redis
- **DNS**: DNSimple (NOT Route 53)
- **Container Registry**: ECR
- **Storage**: S3 (per-tenant encrypted with KMS)

**Terraform Commands** (always use the wrapper script):
```bash
# NEVER run terraform directly -- always use the wrapper
./homemanagement-cloud/infrastructure/scripts/tf.sh <env> <command>
# Example: ./homemanagement-cloud/infrastructure/scripts/tf.sh prod plan
```

**Deployment Commands**:
```bash
# Build and push container images
./homemanagement-cloud/infrastructure/scripts/build-and-push.sh <env> <service>
# service: marketing, cloud-app, all

# Deploy via App Runner
aws apprunner start-deployment --service-arn <arn>
```

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

2. **Object nodes are NOT string values.** `L["settings.homeSetup"]` will NOT resolve if `homeSetup` is an object -- you must reference a leaf string like `L["settings.homeSetup.title"]`.

3. **Use `.title` for section headings** when a key has children. This matches the existing pattern (e.g., `settings.mobileAppSetup.title`, `home.setupWizard.title`).

4. **Always add keys when adding UI text.** Any `L["..."]` reference in a Razor file must have a corresponding entry in `en.json`. Missing keys render as the raw key string in the UI.

5. **Follow existing naming conventions:**
   - `common.*` -- Shared labels (Save, Cancel, Edit, etc.)
   - `settings.*` -- Settings page sections
   - `home.*` -- Home/property related
   - `contact.*` -- Contact fields and labels
   - `*.title` -- Section/page titles
   - `*.description` / `*Desc` -- Descriptive text

---

## Testing Strategy

### Test Projects

**Shared Library Tests** (in `tests/`):
- `Famick.HomeManagement.Shared.Tests.Unit` -- Unit tests for shared services
- `Famick.HomeManagement.Shared.Tests.Integration` -- Integration tests with Docker/Testcontainers

**Self-Hosted App Tests** (in `tests/`):
- `Famick.HomeManagement.Tests.Unit` -- Unit tests with fixed tenant configuration
- `Famick.HomeManagement.Tests.Integration` -- Integration tests with Docker/Testcontainers

**Cloud Tests** (in `homemanagement-cloud/tests/`):
- `Famick.HomeManagement.Cloud.Tests.Unit` -- Tenant isolation, multi-tenancy, cloud services
- `Famick.HomeManagement.Cloud.Tests.Integration` -- Integration tests with cloud services

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

# Run cloud tests (requires submodule)
dotnet test homemanagement-cloud/tests/Famick.HomeManagement.Cloud.Tests.Unit
```

---

## Current Project Status

**Source**: Grocy (PHP/SQLite household management system)
**Target**: .NET 10 / PostgreSQL with multi-tenancy

### What's Built

- 56 domain entities across all major feature areas
- 38 service interfaces with 30+ implementations
- 27 API controllers (23 resource + 4 auth/base)
- Full Blazor Razor Class Library with components and pages
- Blazor WebAssembly client project
- 45 EF Core migrations (PostgreSQL)
- Plugin system (OpenFoodFacts, USDA FoodData, Kroger)
- Authentication: JWT with refresh tokens, passkeys, native mobile OAuth (Google/Apple Sign-In via app.famick.com)
- Multi-tenant query filters and tenant resolution middleware
- 5-page onboarding wizard with skip/exit/re-run support
- .NET MAUI native mobile app with MVVM
- Self-hosted Docker deployment
- CI/CD for TestFlight and Play Store

---

## Feature Documentation Standards

Feature documents live in Obsidian at `Clients - Projects/Famick/Road Map/` and follow this standard format:

### Required Sections

1. **Title & Metadata** -- Feature name, status (Not Started / In Progress / Complete), last updated date
2. **Overview** -- 2-3 sentence description of what the feature does and why
3. **User Stories** -- As a [role], I want [goal] so that [benefit]
4. **Flows** -- Step-by-step user journeys for each scenario, noting web vs mobile differences
5. **Web UI** -- Pages, components, and dialogs involved
6. **Mobile UI** -- MAUI pages, layouts, and navigation
7. **API Endpoints** -- Relevant endpoints (reference, not full spec)
8. **Business Rules** -- Validation, calculations, constraints
9. **Updates** -- Changelog with dates, descriptions, and commit references

### Updates Section Format

```markdown
## Updates

### YYYY-MM-DD - Brief Description
- **Changed**: What changed
- **Added**: What was added
- **Removed**: What was removed
- **Commits**: abc1234, def5678
- **PRs**: #123, #456
```

---

## Best Practices

### Development

1. **Test in Both Modes** -- Always test changes in BOTH self-hosted and cloud configurations. Use feature flags for optional functionality.

2. **Keep Public Code Cloud-Agnostic** -- No cloud-specific features (Stripe, SendGrid, S3, push notifications, subscription billing) in `src/`. Cloud features belong exclusively in `homemanagement-cloud/`.

3. **Maintain Backwards Compatibility** -- In shared libraries, avoid breaking changes to interfaces consumed by the cloud project.

4. **File Formatting** -- NEVER use Windows line endings (CRLF / `\r\n`). Always use Unix line endings (LF / `\n`).

### Git Submodule

1. **Always commit submodule changes before parent.**
2. **Use `--recursive` when cloning** to initialize the cloud submodule.
3. **Check submodule status before committing**: `git submodule status`

### .NET 10 MAUI Notes

- `MessagingCenter` is inaccessible (made internal) in .NET 10 MAUI. Use `WeakReferenceMessenger` from `CommunityToolkit.Mvvm.Messaging` instead.
- Define message types using `ValueChangedMessage<T>` from `CommunityToolkit.Mvvm.Messaging.Messages`.

---

## Troubleshooting

### Problem: Cloud Submodule Not Found

```bash
# Error: fatal: 'homemanagement-cloud' does not appear to be a git repository
# Solution: Initialize the submodule
git submodule update --init --recursive
```

### Problem: Submodule Detached HEAD

```bash
cd homemanagement-cloud
git checkout main
```

### Problem: Build Fails With Missing Projects

If cloud projects fail to load, that is normal when the submodule is not initialized. The self-hosted projects in `src/` will still build independently.

```bash
# Build just the self-hosted web app
dotnet build src/Famick.HomeManagement.Web
```

### Problem: Changes Not Reflected After Editing Shared Code

```bash
# Clean and rebuild
dotnet clean
dotnet build
```

### Problem: Submodule Merge Conflicts

```bash
cd homemanagement-cloud
git pull origin main
# Resolve any code conflicts
cd ..
git add homemanagement-cloud
git commit -m "resolve: merge homemanagement-cloud conflicts"
```

---

## Quick Reference Commands

### Clone and Setup
```bash
git clone --recursive git@github.com:Famick-com/FamickHomeManagement.git
cd FamickHomeManagement
dotnet build
```

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
docker-compose up
# or
./scripts/start-production.sh
./scripts/stop-production.sh

# Publish to Docker Hub
./scripts/publish-selfhosted-dockerhub.sh
```

### Mobile Builds
```bash
./scripts/build-testflight.sh
./scripts/build-play-store.sh
```

### Submodule Management
```bash
# Check status
git submodule status

# Update to latest
cd homemanagement-cloud && git pull origin main && cd ..
git add homemanagement-cloud
git commit -m "chore: update homemanagement-cloud to latest"

# See submodule diff
git diff --submodule
```

### Cloud Infrastructure (requires submodule access)
```bash
# Terraform (always use wrapper)
./homemanagement-cloud/infrastructure/scripts/tf.sh <env> <command>

# Build and deploy
./homemanagement-cloud/infrastructure/scripts/build-and-push.sh <env> <service>
```

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
- **Plugin Authoring Guide**: `docs/author-plugins.md`
- **Store Integrations**: `docs/STORE_INTEGRATIONS.md`
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

- **FamickHomeManagement** (this repository): **AGPL-3.0**
- **homemanagement-cloud** (submodule): **Proprietary** (all rights reserved)

---

Last Updated: 2026-02-24
