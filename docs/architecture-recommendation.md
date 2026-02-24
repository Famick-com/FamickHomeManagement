# Famick Architecture Recommendation: SyncFusion Integration & Open-Source Strategy

**Date**: 2026-02-22
**Advisory Team**: Marcus Chen (.NET Architect), Sarah Okonkwo (Open-Source Expert), David Ramirez, Esq. (IP Attorney), Priya Patel (.NET Developer), James Nakamura (Mobile Developer)
**Status**: Recommendation for Review

---

## Executive Summary

The team was asked to evaluate how Famick can integrate paid SyncFusion components while keeping the project source-available and maintainable. After independently auditing the codebase -- reading project files, Razor components, license files, dependency chains, and build configurations -- the team presents a unified recommendation with noted areas of disagreement.

**Core findings**:

1. **The UI layer is deeply coupled to MudBlazor (6,916 references, 159 files) but the layers below are clean.** Domain, Core, Infrastructure, and Web.Shared have zero MudBlazor references. This is the good news.

2. **A comprehensive abstraction layer is infeasible and counterproductive.** The team unanimously rejects wrapping MudBlazor in interfaces. Instead, create parallel UI projects in the private cloud repo and migrate only high-value pages incrementally.

3. **Only 2-3 SyncFusion components justify the complexity.** The Calendar (hand-built HTML), PDF Viewer, and possibly Rich Text Editor are the only components where SyncFusion delivers meaningful improvement. DataGrid migration is explicitly not recommended.

4. **There is a critical license documentation mismatch** that must be fixed before anything else. The actual LICENSE files are PolyForm Shield 1.0.0, but documentation says AGPL-3.0 in at least 8 locations.

5. **SyncFusion components must live exclusively in the private `homemanagement-cloud` repo** to comply with SyncFusion's EULA.

6. **The MIT-licensed SyncFusion MAUI Toolkit provides high-value mobile improvements at zero licensing cost** and should be adopted immediately in the public repo.

---

## 1. Current State Assessment

### 1.1 Repository Structure (As-Is)

```
Famick/                          (Private parent, no LICENSE file)
├── homemanagement-shared/       (Submodule, PolyForm Shield 1.0.0)
├── homemanagement/              (Submodule, PolyForm Shield 1.0.0)
├── homemanagement-cloud/        (Submodule, Proprietary)
├── src/Famick.Marketing.Web/
└── infrastructure/terraform/

Famick-Self-Hosted/              (Separate public parent, PolyForm Shield 1.0.0)
├── homemanagement-shared/       (Same submodule, same remote)
└── homemanagement/              (Same submodule, same remote)
```

### 1.2 Key Findings

| Finding | Details | Discovered By |
|---------|---------|---------------|
| **License mismatch** | LICENSE files contain PolyForm Shield 1.0.0 with customized `Licensor Products` line. CLAUDE.md, README.md, GITHUB_SETUP.md, architecture.md, and cloud repo docs all say "AGPL-3.0". | Sarah, David |
| **Deep MudBlazor coupling** | 4,632 component instances, 156 Razor files, 63 MudDialog instances, 128 IDialogService injections, MudBlazor-specific theme/CSS variables throughout. | Priya, Marcus |
| **Clean lower layers** | Domain, Core, Infrastructure, and Web.Shared have zero MudBlazor references. The contamination boundary is precisely at `Famick.HomeManagement.UI` and `Web.Client`. | Marcus |
| **Shared Web.Client** | Both cloud and self-hosted reference the identical `Famick.HomeManagement.Web.Client` project. No UI seam exists today between deployment modes. | Marcus |
| **Calendar is hand-built** | `Pages/Calendar/Calendar.razor` is 460 lines of custom HTML/CSS with no calendar component. This is the single highest-value SyncFusion opportunity. | Priya |
| **Mobile is MAUI Native** | 40+ pages, Shell navigation, code-behind pattern, CommunityToolkit.Maui. SyncFusion Blazor is irrelevant; only MAUI controls apply. | James |
| **Two parent repos** | `Famick` and `Famick-Self-Hosted` both reference the same submodules, creating submodule pointer drift and documentation divergence. | Sarah |
| **No CLA** | Neither CONTRIBUTING.md contains a Contributor License Agreement, creating risk for the open-core model. | David |
| **QuestPDF license risk** | QuestPDF v2024.12.3 in shared code switched to dual-license in 2024. Community license requires <$1M revenue. | David |

### 1.3 Verified Dependency Map

```
Cloud Web ──► Web.Client ──► UI (MudBlazor RCL, in shared/public repo)
                              │
Self-Hosted Web ──► Web.Client ──┘

UI ──► Core ──► Domain   (UI only depends on Core, not Infrastructure)

Cloud Web ──► Web.Shared ──► Infrastructure ──► Core ──► Domain
              │
Self-Hosted Web ──► Web.Shared ──┘

Cloud Web ──► Cloud ──► Cloud.Infrastructure (proprietary domain services)

Mobile ──► Core (via HTTP API client, no Blazor at all)
```

**Key architectural fact**: The `Famick.HomeManagement.UI` project references only `Famick.HomeManagement.Core`. It does NOT reference Infrastructure, Web.Shared, or any cloud project. This means a parallel UI project only needs to reference Core to access the same DTOs, interfaces, and services.

---

## 2. SyncFusion Licensing Analysis

### 2.1 SyncFusion EULA Compliance Matrix

**David Ramirez (Legal)**:

| Scenario | Permitted? | Basis |
|----------|-----------|-------|
| SyncFusion binaries in private GitHub repo | **YES** | Not public distribution |
| SyncFusion binaries in public GitHub repo | **NO** | Unauthorized redistribution |
| SyncFusion in public Docker image (Docker Hub) | **NO** | Public redistribution of compiled assemblies |
| SyncFusion in private Docker image (ECR for SaaS) | **YES** | Not distributed; runs on your infrastructure |
| SyncFusion in compiled App Store / Play Store app | **YES** | Standard redistribution of compiled application |
| Source code referencing SyncFusion namespaces in public repo | **YES** | Source code is yours; binaries are SyncFusion's |
| SyncFusion license key in public source/CI logs | **NO** | EULA violation; key can be revoked |

### 2.2 SyncFusion's MIT MAUI Toolkit

**James Nakamura (Mobile)**:

SyncFusion's .NET MAUI Toolkit is released under the **MIT license** with 24+ controls:
- TextInputLayout, MaskedEntry, Autocomplete, Segmented Control
- Shimmer, Chips, Carousel View, Accordion, Tab View
- Navigation Drawer, Date/Time Pickers, Progress Bars

These **can** be freely used in any public repository. MIT is compatible with both PolyForm Shield and AGPL-3.0. No isolation required.

### 2.3 Community License

SyncFusion offers a Community License (free for <$1M revenue, <=5 developers, <=10 employees). It carries the **same EULA restrictions** as commercial licenses regarding redistribution. The "free" part is the price, not the redistribution rights.

---

## 3. Licensing Strategy

### 3.1 The License Mismatch (URGENT)

**Full team consensus: This must be fixed before anything else.**

The team identified a four-way contradiction:

| Location | License Stated |
|----------|----------------|
| `homemanagement-shared/LICENSE` | PolyForm Shield 1.0.0 |
| `homemanagement/LICENSE` | PolyForm Shield 1.0.0 |
| `Famick-Self-Hosted/LICENSE` | PolyForm Shield 1.0.0 |
| `Famick-Self-Hosted/COPYRIGHT` | PolyForm Shield 1.0.0 |
| `Famick-Self-Hosted/README.md` | PolyForm Shield 1.0.0 |
| `Famick/CLAUDE.md` (lines 9-10, 878-879) | **AGPL-3.0** |
| `Famick/README.md` (lines 9, 139-140) | **AGPL-3.0** |
| `Famick/GITHUB_SETUP.md` (lines 15-16) | **AGPL-3.0** |
| `Famick/docs/architecture.md` (lines 116, 626) | **AGPL-3.0** |
| `homemanagement-cloud/LICENSE` (line 36) | References **AGPL-3.0** for public repos |
| `homemanagement-cloud/README.md` (line 362) | References **AGPL-3.0** for public repos |

### 3.2 License Choice (Team Disagreement)

The team is split on the preferred license. Both options are viable.

#### Option A: Keep PolyForm Shield, Fix Documentation (Sarah's Recommendation)

**Advocate**: Sarah Okonkwo

PolyForm Shield is the right license for this business model. It:
- Lets users self-host, inspect, and modify freely
- Directly prevents competitors from forking (non-compete clause)
- Is the intentional choice (the `Licensor Products` line is customized)
- Follows the pattern of HashiCorp (BSL), Elastic (SSPL/Elastic License), and Confluent

**Required actions**:
- Update ALL documentation references from "AGPL-3.0" to "PolyForm Shield 1.0.0"
- Stop calling the project "open-source" -- use "source-available" or "open code"
- Add a plain-language LICENSING.md explaining what users can and cannot do
- Update GitHub repository settings (GitHub does not recognize PolyForm Shield natively)

#### Option B: Switch to AGPL-3.0 (David's Recommendation)

**Advocate**: David Ramirez

AGPL-3.0 is the better choice for an open-core model. It:
- Is OSI-approved -- community recognition and trust
- Has network copyleft (Section 13) that prevents SaaS competitors from forking without sharing modifications
- Is battle-tested in court with extensive enforcement history
- Is used by successful open-core companies (GitLab, Nextcloud, Grafana, Mattermost)
- Does not prevent competitors as directly as PolyForm Shield's non-compete, but the copyleft mechanism achieves a similar result

**Required actions**:
- Replace LICENSE files with AGPL-3.0 text
- As sole copyright holder, Famick can relicense freely
- Audit git history for any external contributors (their consent may be needed)
- Still requires a CLA going forward

**Both agree**: Whichever license is chosen, the documentation mismatch must be resolved and a CLA must be implemented before accepting external contributions.

### 3.3 Per-Repository License Assignment

| Repository | License | Rationale |
|-----------|---------|-----------|
| `homemanagement-shared` | PolyForm Shield 1.0.0 OR AGPL-3.0 | Core shared code |
| `homemanagement` | PolyForm Shield 1.0.0 OR AGPL-3.0 | Self-hosted app |
| `homemanagement-cloud` | Proprietary | Cloud SaaS, not distributed |
| `Famick` (parent) | MIT | Development workspace config only |

---

## 4. Proposed Architecture Changes

### 4.1 Repository Consolidation (Team Consensus)

**Full team evaluated**: Merge `homemanagement` and `homemanagement-shared` directly into the Famick repository. Keep `homemanagement-cloud` as the sole remaining submodule.

#### Current Pain Points (5 repos, 3 submodules)

- Cross-cutting features require 4 commits across 4 repos (shared, self-hosted, cloud, parent pointer update)
- Submodule pointer drift between two parents (Famick + Famick-Self-Hosted)
- 4 solution files to maintain
- No CI/CD in Famick-Self-Hosted (only a manual shell script)
- Documentation drift across repos

#### Approved Structure (3 repos, 1 submodule)

Make `Famick/` the single public repository. Move `infrastructure/` and `Famick.Marketing.Web` into the private `homemanagement-cloud/` repo. Archive all other repos.

```
Famick/                                    # PUBLIC (source-available)
├── src/
│   ├── Famick.HomeManagement.Domain/            # was homemanagement-shared
│   ├── Famick.HomeManagement.Core/              # was homemanagement-shared
│   ├── Famick.HomeManagement.Infrastructure/    # was homemanagement-shared
│   ├── Famick.HomeManagement.Web.Shared/        # was homemanagement-shared
│   ├── Famick.HomeManagement.UI/                # was homemanagement-shared
│   ├── Famick.HomeManagement.Shared/            # was homemanagement-shared
│   ├── Famick.HomeManagement.Web/               # was homemanagement (self-hosted)
│   ├── Famick.HomeManagement.Web.Client/        # was homemanagement (WASM host)
│   └── Famick.HomeManagement.Mobile/            # was homemanagement (MAUI)
├── tests/
│   ├── Famick.HomeManagement.Shared.Tests.Unit/
│   ├── Famick.HomeManagement.Shared.Tests.Integration/
│   ├── Famick.HomeManagement.Tests.Unit/
│   └── Famick.HomeManagement.Tests.Integration/
├── docker/                                # was homemanagement/docker
├── docs/
├── .github/workflows/                     # testflight.yml, play-store.yml from homemanagement
├── homemanagement-cloud/                  # PRIVATE submodule (grayed-out link for public)
│   ├── src/
│   │   ├── Famick.Marketing.Web/                # moved from Famick/src/
│   │   ├── Famick.HomeManagement.Web/           # cloud web app
│   │   ├── Famick.HomeManagement.Cloud/
│   │   └── Famick.HomeManagement.Cloud.Infrastructure/
│   ├── infrastructure/                    # moved from Famick root
│   │   ├── scripts/
│   │   └── terraform/
│   ├── tests/
│   └── deployment/
├── Famick.sln
├── LICENSE
└── README.md
```

**Private submodule visibility**: A private submodule in a public GitHub repo is **not broken** — GitHub shows it as a grayed-out folder that outsiders cannot click into. The public repo builds and works without it. Only in-house developers with access clone with `--recursive`. This is a standard open-core pattern (GitLab, Sentry, etc.).

**Why Marketing.Web moves to cloud repo**: The marketing website contains landing pages, pricing, and branding that should not be public. It has zero project references (only NuGet packages), so it moves cleanly with no code changes. It's already deployed to the same AWS infrastructure.

See **Section 12** for the complete migration plan with exact file changes.

### 4.2 Reject the Abstraction Layer (Team Consensus)

**Marcus Chen + Priya Patel** (independently reached the same conclusion):

**Do NOT build an `IComponentLibrary` or `IDataGridProvider` interface abstraction over MudBlazor and SyncFusion.** The team unanimously rejects this approach after auditing the codebase.

**Why it fails**:

| Factor | Reality |
|--------|---------|
| Scale | 4,632 MudBlazor component instances across 156 files |
| Dialog system | 63 `MudDialog` instances with `MudDialogInstance` as `CascadingParameter`, 128 `IDialogService` injections, `DialogParameters<T>` generic type -- completely MudBlazor-specific |
| Layout system | 630 `MudStack`, 186 `MudItem`, 59 `MudGrid`, 40 `MudHidden` with breakpoints |
| Theme integration | `FamickTheme.cs` uses `MudTheme`, `PaletteLight`, `PaletteDark`; CSS uses `var(--mud-palette-*)` variables |
| API surface mismatch | MudDataGrid uses `<PropertyColumn>`, `<TemplateColumn>`, `<HierarchyColumn>`; SyncFusion SfGrid uses `<GridColumns>`, `<GridColumn>` -- fundamentally different compositional models |
| Estimated effort | 3-6 months of refactoring with zero user-facing value |

### 4.3 Adopt Parallel UI Projects (Recommended)

**Marcus Chen (Architect)**:

Instead of abstracting MudBlazor away, treat the UI RCL as the concrete implementation it is, and create a parallel SyncFusion implementation for cloud only.

**Key insight**: The business logic in `@code` blocks is already library-agnostic. Pages call `IApiClient.GetAsync<T>()` and work with DTOs from Core. Only the Razor markup changes between implementations.

#### New Projects in `homemanagement-cloud/`

```
homemanagement-cloud/
  src/
    Famick.HomeManagement.Cloud.UI/              ← NEW (SyncFusion-based RCL)
      Famick.HomeManagement.Cloud.UI.csproj
      _Imports.razor
      SyncfusionLicenseInitializer.cs
      Theme/FamickSyncfusionTheme.cs
      Components/Layout/ (MainLayout, NavMenu)
      Pages/ (mirrors shared UI structure as needed)
      wwwroot/css/

    Famick.HomeManagement.Cloud.Web.Client/       ← NEW (Cloud WASM host)
      Famick.HomeManagement.Cloud.Web.Client.csproj
      Program.cs
      App.razor
      _Imports.razor
      wwwroot/index.html
```

#### Update Cloud Web to Reference Cloud UI

```xml
<!-- In homemanagement-cloud/src/Famick.HomeManagement.Web/Famick.HomeManagement.Web.csproj -->

<!-- BEFORE -->
<ProjectReference Include="..\..\..\homemanagement\src\Famick.HomeManagement.Web.Client\Famick.HomeManagement.Web.Client.csproj" />

<!-- AFTER -->
<ProjectReference Include="..\Famick.HomeManagement.Cloud.Web.Client\Famick.HomeManagement.Cloud.Web.Client.csproj" />
```

### 4.4 Extract Shared UI Services (Prerequisite)

**Marcus Chen (Architect)**:

Before creating parallel UI projects, UI-agnostic services must be extracted from `Famick.HomeManagement.UI/Services/` to `Famick.HomeManagement.Core/` so both UI implementations can use them.

**Services to move to Core**:

| Service | Current Location | Purpose |
|---------|-----------------|---------|
| `IApiClient` / `HttpApiClient` | UI/Services/ | HTTP API communication |
| `ApiAuthStateProvider` | UI/Services/ | Authentication state |
| `ITokenStorage` | UI/Services/ | JWT token persistence |
| `IMobileDetectionService` | UI/Services/ | Mobile/desktop detection |
| `IUserPermissions` / `UserPermissions` | UI/Services/ | Role-based access checks |
| `ILocalizer` / `LocalizationService` | UI/Localization/ | Localization |
| `INavMenuPreferenceStorage` | UI/Services/ | Navigation preferences |
| `IShoppingListPreferenceStorage` | UI/Services/ | Shopping preferences |
| `IInventorySessionService` | UI/Services/ | Inventory session state |
| `IBarcodeScannerService` | UI/Services/ | Barcode scanning interface |
| `IServerSettings` | UI/Services/ | Server configuration |

**Services to keep in Famick.HomeManagement.UI** (MudBlazor-specific):
- `FamickMudLocalizer` (MudBlazor localizer adapter)
- `WebBarcodeScannerService` (web-specific implementation)

This extraction improves the architecture regardless of SyncFusion -- moving `IApiClient` and auth services to Core eliminates a dependency that currently forces both UIs to reference each other.

### 4.5 Updated Project Structure (Post-Consolidation)

```
Famick.sln                                    # PUBLIC repo
  src/
    Famick.HomeManagement.Domain              # shared domain entities
    Famick.HomeManagement.Core                # shared interfaces, DTOs (+ extracted UI services)
    Famick.HomeManagement.Infrastructure      # shared EF Core, service implementations
    Famick.HomeManagement.Web.Shared          # shared API controllers
    Famick.HomeManagement.UI                  # MudBlazor RCL (source-available)
    Famick.HomeManagement.Shared              # shared utilities
    Famick.HomeManagement.Web                 # self-hosted server
    Famick.HomeManagement.Web.Client          # MudBlazor WASM host
    Famick.HomeManagement.Mobile              # MAUI Native app
  tests/
    Famick.HomeManagement.Shared.Tests.Unit
    Famick.HomeManagement.Shared.Tests.Integration
    Famick.HomeManagement.Tests.Unit
    Famick.HomeManagement.Tests.Integration
  docker/
    Dockerfile                                # self-hosted Docker build

  homemanagement-cloud/                       # PRIVATE submodule
    src/
      Famick.Marketing.Web                    # marketing website
      Famick.HomeManagement.Cloud             # cloud domain services
      Famick.HomeManagement.Cloud.Infrastructure
      Famick.HomeManagement.Cloud.UI          # SyncFusion RCL (proprietary)    ← FUTURE
      Famick.HomeManagement.Cloud.Web.Client  # SyncFusion WASM host            ← FUTURE
      Famick.HomeManagement.Web               # cloud server
    infrastructure/
      scripts/                                # build-and-push, tf.sh, etc.
      terraform/                              # IaC modules + environments
    tests/
      Famick.HomeManagement.Cloud.Tests.Unit
      Famick.HomeManagement.Cloud.Tests.Integration
```

### 4.6 Updated Dependency Diagram (Post-Consolidation)

```
┌──────────────────────────────────────────────────────────┐
│    homemanagement-cloud/ (PRIVATE submodule)              │
│                                                           │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ Cloud Web App                                        │ │
│  │ homemanagement-cloud/src/Famick.HM.Web              │ │
│  └──────┬────────────────────────┬─────────────────────┘ │
│         │                        │                        │
│         │                        ▼                        │
│         │              ┌───────────────────────┐         │
│         │              │ Cloud.Web.Client  NEW │         │
│         │              └───────────┬───────────┘         │
│         │                          │                      │
│         │                          ▼                      │
│         │              ┌───────────────────────┐         │
│         │              │ Cloud.UI         NEW  │         │
│         │              │  - SfScheduler        │         │
│         │              │  - SfPdfViewer        │         │
│         │              └───────────┬───────────┘         │
│         │                          │                      │
│         │  ┌─────────────────┐     │                      │
│         │  │ Marketing.Web   │     │  (no project refs)   │
│         │  └─────────────────┘     │                      │
│         │                          │                      │
│         └──────────┬───────────────┘                      │
│                    │ references via ../../../src/          │
└────────────────────┼──────────────────────────────────────┘
                     ▼
┌──────────────────────────────────────────────────────────┐
│  Famick/ (PUBLIC repo)                                    │
│                                                           │
│  ┌────────────────────────────────────────────────────┐  │
│  │  src/                                               │  │
│  │  ┌────────────┐  ┌────────┐  ┌────────────┐       │  │
│  │  │ Web.Shared │  │   UI   │  │ Infra-     │       │  │
│  │  │ Controllers│  │MudBlzr │  │ structure  │       │  │
│  │  └─────┬──────┘  └───┬────┘  └──────┬─────┘       │  │
│  │        │             │              │              │  │
│  │        └──────┬──────┘              │              │  │
│  │               ▼                     │              │  │
│  │        ┌──────────┐                 │              │  │
│  │        │   Core   │◄────────────────┘              │  │
│  │        │ + shared │                                │  │
│  │        │ services │                                │  │
│  │        └────┬─────┘                                │  │
│  │             ▼                                      │  │
│  │        ┌──────────┐                                │  │
│  │        │  Domain  │                                │  │
│  │        └──────────┘                                │  │
│  │                                                    │  │
│  │  Self-Hosted: Web + Web.Client ──► UI (MudBlazor)  │  │
│  │  Mobile: MAUI Native (no project refs to above)    │  │
│  └────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
```

---

## 5. Which SyncFusion Components to Use

### 5.1 Blazor Components (Cloud Web Only)

**Priya Patel + Marcus Chen** (independently reached the same conclusions):

#### Tier 1: Genuinely Better Than MudBlazor (worth the complexity)

| Component | Current State | SyncFusion Value | Estimated Effort |
|-----------|--------------|-----------------|-----------------|
| **SfScheduler** | Calendar.razor is 460 lines of hand-built HTML/CSS with raw `<div>` grid. No drag-to-reschedule, no recurring events, no time slot visualization. | Day/week/month/agenda/timeline views, drag-and-drop, recurring events, multi-day spanning, per-household-member resource views. **Single largest UX gap in the application.** | 2-3 days basic, +1 week for full feature parity |
| **SfPdfViewer** | Equipment/vehicle documents use basic preview dialog. No zoom, annotation, or search. | Full PDF viewer with zoom, annotation, search, download. Useful for equipment docs, vehicle docs, recipe printouts. | 1-2 days |

#### Tier 2: Nice to Have (consider later)

| Component | Value | Notes |
|-----------|-------|-------|
| **SfRichTextEditor** | Medium | Current MarkdownEditor uses Markdig. WYSIWYG would be nicer for recipe instructions. Markdown may actually be preferred by self-hosted users. Cloud-only enhancement at best. |
| **SfChart** | Low-Medium | Only 1 chart reference exists today. Would add value for analytics features that don't exist yet. Build new features, not replace existing ones. |

#### Tier 3: NOT Recommended (MudBlazor is sufficient)

| Component | Why Not | Details |
|-----------|---------|---------|
| **SfGrid (DataGrid)** | **No user-visible improvement** | Only 16 MudDataGrid + 12 MudTable instances. A household app with 200-500 products does not need Excel-level pivoting, frozen columns, or batch editing. Migration would take 2-3 weeks for zero UX benefit. |
| **SfDialog** | **Actively harmful** | 63 MudDialog instances with CascadingParameter pattern. Rewriting every dialog is the single most time-consuming and bug-introducing task possible. |
| **SfDropDownList / SfAutoComplete** | **No improvement** | 76 MudSelects and 14 MudAutocompletes work fine with simple SearchFunc delegates. |
| **SfDatePicker / SfTimePicker** | **No improvement** | 20 MudDatePickers work fine. |

### 5.2 MAUI Controls (Mobile App)

**James Nakamura**:

#### MIT Toolkit (Add to Public Repo Immediately)

| Control | Target Pages | Improvement |
|---------|-------------|-------------|
| **TextInputLayout** | All 15+ form pages (wizard, contacts, recipes, events) | Material Design floating labels, helper text, error text replacing bare Entry |
| **Shimmer** | DashboardPage, RecipeListPage, ContactGroupsPage, StockOverviewPage | Shimmer placeholders instead of ActivityIndicator spinners |
| **Chips** | ContactDetailPage (tags), ShoppingSessionPage (filters) | Proper chip selection/deletion replacing FlexLayout of labels |
| **Autocomplete** | AddItemPage (product search), AddIngredientPage | Typeahead dropdown replacing Entry + manual CollectionView |
| **Segmented Control** | StockOverviewPage (filters), CalendarPage (view modes) | Intuitive toggle replacing Picker/manual button groups |
| **Tab View** | ContactDetailPage (Phone/Email/Address/Social), RecipeDetailPage | Tabbed sections replacing long vertical scroll |
| **Date Picker** | BestBeforeDatePopup, CreateEditEventPage | Proper picker replacing custom +/- month/day/year buttons |

#### Premium MAUI Controls (Cloud Build Only, Evaluate Later)

| Control | Value | Recommendation |
|---------|-------|----------------|
| **SfScheduler** | Very High | Only premium control worth dual-build complexity for mobile |
| **SfCartesianChart** | High | Better delivered through the responsive web app |
| **SfDataGrid** | High | Better delivered through the responsive web app |
| **SfListView** | Medium | Improved swipe behavior over standard CollectionView, but consider Community License |

### 5.3 Page Migration Priority (Cloud Web)

**Marcus Chen's recommended migration order** for the parallel Cloud.UI project:

| Priority | Page | Why |
|----------|------|-----|
| 1 | Calendar + event dialogs | Hand-built HTML to SfScheduler -- biggest UX leap |
| 2 | Equipment/Vehicle document views | SfPdfViewer for document management |
| 3 | StockOverview (247 Mud refs) | Most complex page, benefits from SfGrid |
| 4 | Products + ProductDetail | Data-heavy with images |
| 5 | ShoppingListDetail (144 refs) | Drag-and-drop reordering |
| 6 | Contacts (125 refs) | Grouping, search, virtual scrolling |
| 7+ | Remaining pages | Incremental |

**Critical note from Priya**: For any page NOT yet migrated to SyncFusion, the Cloud.UI project can temporarily reference and re-export the MudBlazor page from the shared UI project. This enables incremental rollout rather than a big-bang rewrite.

---

## 6. Mobile Architecture

**James Nakamura**:

### 6.1 Why Mobile Needs a Different Strategy

MAUI Native and Blazor are fundamentally different:
- XAML pages are compiled, not interpreted at runtime
- MAUI has no equivalent to Blazor's `DynamicComponent` for runtime control swapping
- The overhead of a mobile abstraction layer produces over-engineered code without runtime flexibility
- SyncFusion Blazor and SyncFusion MAUI are completely different product lines with different APIs

### 6.2 Recommended Mobile Approach

1. **MIT Toolkit controls**: Add directly to existing pages in the public repo. Straightforward substitutions in XAML -- no abstraction needed.

2. **Premium controls (if pursuing dual-build)**: Use build-time conditional compilation:

```xml
<!-- In .csproj -->
<PropertyGroup Condition="'$(PremiumBuild)' == 'true'">
    <DefineConstants>$(DefineConstants);PREMIUM</DefineConstants>
</PropertyGroup>
```

```csharp
// In MauiProgram.cs
#if PREMIUM
    Routing.RegisterRoute("CalendarPage", typeof(PremiumCalendarPage));
#else
    Routing.RegisterRoute("CalendarPage", typeof(CalendarPage));
#endif
```

3. **Feature gating for cloud users**: The server returns feature entitlements at login. The open-source app shows the base feature set. The cloud-distributed app (built with `PREMIUM` flag) shows additional tabs/pages based on the tenant's plan.

### 6.3 Binary Size Impact

| Package | Size Impact (compressed) |
|---------|------------------------|
| Syncfusion.Maui.Toolkit (MIT, 24+ controls) | ~1.5-2.5 MB |
| Syncfusion.Maui.Scheduler (premium) | ~2-3 MB |
| All premium controls combined | ~8-12 MB |

Current estimated app size without SyncFusion: 30-50 MB. With MIT Toolkit: 35-55 MB. With all premium: 40-65 MB. All acceptable (App Store median ~40 MB, Play Store limit 150 MB without expansion files).

---

## 7. CI/CD Architecture

### 7.1 SyncFusion-Specific Constraints

**Sarah Okonkwo + David Ramirez**:

1. **License key must be secret** -- injected at build/runtime via environment variable, never in source
2. **NuGet packages are freely downloadable from nuget.org** but require a license to use legally
3. **Build outputs containing SyncFusion cannot be publicly distributed** -- affects Docker images

### 7.2 Recommended CI/CD Structure (Post-Consolidation)

```
Famick/.github/workflows/                # PUBLIC repo
  ci.yml                    # Build + test shared libraries + self-hosted app
  testflight.yml            # iOS build (moved from homemanagement)
  play-store.yml            # Android build (moved from homemanagement)
  docker-community.yml      # Docker image WITHOUT SyncFusion → Docker Hub
  docker-premium.yml        # Docker image WITH SyncFusion → private registry

homemanagement-cloud/.github/workflows/  # PRIVATE repo
  ci.yml                    # Build + test cloud version (includes SyncFusion)
  deploy-staging.yml        # Deploy to staging (cloud-app + marketing)
  deploy-prod.yml           # Deploy to production
```

**Note**: Cloud CI must check out the parent `Famick` repo (public) to resolve project references. Use `actions/checkout` with submodules or a separate step to clone the public parent. Since Famick is public, no authentication tokens are needed for the checkout.

### 7.3 SyncFusion License Key Handling

```csharp
// In Cloud.Web.Client/Program.cs
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
    builder.Configuration["SyncFusion:LicenseKey"]
    ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")
    ?? throw new InvalidOperationException("SyncFusion license key not configured"));
```

- **Development**: `dotnet user-secrets set "SyncFusion:LicenseKey" "..."`
- **Production**: AWS Secrets Manager / SSM Parameter Store
- **CI/CD**: GitHub Actions secret `SYNCFUSION_LICENSE_KEY`
- **Never** commit the key to any repository, including the private cloud repo

---

## 8. Risk Analysis

### 8.1 Legal & Security Risks

| Risk | Severity | Probability | Mitigation |
|------|----------|-------------|------------|
| **JWT private key in git history** | Critical | Certain (exists today) | Rotate key before making Famick public. File: `infrastructure/terraform/environments/prod/jwt-prod-private.pem` |
| **SyncFusion binaries in public repo/Docker image** | Critical | Low (if architecture followed) | SyncFusion exclusively in homemanagement-cloud; architecture review before any integration |
| **License documentation mismatch** | Critical | Certain (exists today) | Fix immediately -- every day increases exposure |
| **Secrets exposed when Famick goes public** | Critical | Medium | Audit git history with `git log --diff-filter=A -- "*.pem" "*.key" "*.env"`. Use BFG Repo Cleaner if needed |
| **SyncFusion license key exposure** | High | Medium | Environment variables, AWS Secrets Manager, `.gitignore` patterns, secret scanning |
| **Contributor copyright in cloud version** | High | Medium (when contributors arrive) | Implement CLA before accepting external contributions |
| **QuestPDF Community license threshold** | Medium | Low (currently) | Monitor revenue; budget $699/year for Professional license |

### 8.2 Technical Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Code duplication in parallel UI `@code` blocks | Medium | Extract complex logic into shared services in Core |
| UI drift between MudBlazor and SyncFusion versions | Medium | Feature parity checklist; shared localization (single `en.json`) |
| Bug fixed in one UI but not the other | Medium | Test both in CI; shared API contract tests |
| SyncFusion scope creep ("let's also replace the DataGrid...") | Medium | Hard rule: SyncFusion only for components MudBlazor doesn't offer (Scheduler, PDF Viewer) |
| WASM bundle size increase | Medium | Use individual SyncFusion NuGet packages, not the meta-package |

### 8.3 Community Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Calling project "open-source" with PolyForm Shield | High | Use "source-available"; transparent documentation |
| Community perceives "open-core bait" | Medium | Ensure self-hosted version is fully functional; premium enhances, never gates |
| Contributors confused by dual UI projects | Low | Clear CONTRIBUTING.md; free tier contributors never encounter SyncFusion |
| SyncFusion creates two-class contributor system | Low | Link to SyncFusion Community License in CONTRIBUTING.md |

---

## 9. Implementation Roadmap

### Phase 1: Foundation (Repo Consolidation & Licensing)

| Task | Effort | Impact |
|------|--------|--------|
| **Rotate JWT production private key** (in git history) | Immediate | Critical -- security prerequisite |
| Execute repository consolidation (see Section 12) | 2-4 hours | High -- simplifies all future work |
| Move `infrastructure/` and `Marketing.Web` to cloud repo | 1 hour | High -- keeps secrets private |
| Make Famick repo public | 30 min | Required -- public visibility for open code |
| Fix license mismatch in all documentation files | 1 day | Critical -- resolves legal risk |
| Create LICENSING.md with plain-language explanation | 1 day | High -- user clarity |
| Add CLA to Famick repo | 1 day | High -- protects open-core model |
| Archive old repos with redirect notices | 1 hour | Medium -- clean GitHub presence |
| Create THIRD-PARTY-NOTICES.md | 1 day | Medium -- compliance best practice |

### Phase 2: Service Extraction (Prerequisite for Phase 3)

| Task | Effort | Impact |
|------|--------|--------|
| Move UI-agnostic services from UI/Services/ to Core | 2-3 days | High -- enables parallel UI projects |
| Update references in existing UI project | 1 day | Required |
| Verify self-hosted and cloud apps still work unchanged | 1 day | Required |

### Phase 3: Cloud UI Projects

| Task | Effort | Impact |
|------|--------|--------|
| Create `Famick.HomeManagement.Cloud.UI` project with SyncFusion packages | 1 day | Foundation |
| Create `Famick.HomeManagement.Cloud.Web.Client` WASM host | 1 day | Foundation |
| Implement SfScheduler-based Calendar (highest value) | 2-3 days + 1 week | Very High |
| Implement SfPdfViewer for documents | 1-2 days | High |
| Configure SyncFusion license key management | 1 day | Required |
| Update cloud solution file and CI/CD | 1 day | Required |
| Wire Cloud Web to reference Cloud.Web.Client instead of homemanagement's | 1 day | Required |

### Phase 4: Mobile Enhancement

| Task | Effort | Impact |
|------|--------|--------|
| Add SyncFusion MAUI Toolkit (MIT) to mobile project | 1 day | Immediate |
| Replace ActivityIndicator with SfShimmer on loading pages | 1 day | Medium |
| Replace bare Entry with SfTextInputLayout on form pages | 2-3 days | High |
| Replace product search Entry with SfAutocomplete | 1-2 days | Medium |
| Add SfChips for contact tags and filter chips | 1 day | Medium |
| Replace custom date picker popup with SfDatePicker | 1 day | Low |
| Evaluate SfScheduler for cloud mobile calendar (premium) | Ongoing | Plan |

---

## 10. Key Decisions Required

### Decision 1: License Model

| Option | Advocate | Pros | Cons |
|--------|----------|------|------|
| **A: Keep PolyForm Shield, fix docs** | Sarah | Direct non-compete, honest positioning | Not OSI-approved, limited enforcement history |
| **B: Switch to AGPL-3.0** | David | OSI-approved, battle-tested, network copyleft | Doesn't directly prevent competitors; copyleft creates obligations |

### Decision 2: Repository Consolidation -- DECIDED

- **Approved**: Merge `homemanagement` and `homemanagement-shared` into `Famick`. Keep `homemanagement-cloud` as sole submodule. Move `infrastructure/` and `Marketing.Web` to cloud repo. Make `Famick` public. Archive `Famick-Self-Hosted`, `homemanagement`, and `homemanagement-shared` repos.
- See **Section 12** for the complete migration plan.

### Decision 3: SyncFusion License Tier

- **Community License**: Free if qualifying (<$1M revenue, <=5 devs, <=10 employees)
- **Commercial License**: Required if above thresholds
- Same EULA restrictions apply to both tiers

### Decision 4: SyncFusion Scope (Team Consensus)

- **SfScheduler**: Yes -- highest value, hand-built calendar replacement
- **SfPdfViewer**: Yes -- genuine improvement for document management
- **SfRichTextEditor**: Maybe -- cloud-only enhancement, evaluate later
- **SfDataGrid**: No -- MudDataGrid is sufficient for a household app
- **SfDialog / SfDropdown / SfDatePicker**: No -- not worth the migration cost

---

## 11. What NOT To Do

The team unanimously warns against the following:

1. **Do not create an `IComponentLibrary` abstraction layer.** (Marcus, Priya) With 6,916 MudBlazor references across 159 files, this would be months of refactoring with zero user value.

2. **Do not add SyncFusion packages to the shared submodule**, even with conditional compilation or `PrivateAssets`. NuGet restore would still download proprietary binaries into the public repo's build artifacts. (Marcus, David)

3. **Do not try to make MudBlazor and SyncFusion share Razor files with different `@using` directives.** Razor components are not designed for this level of polymorphism. (Marcus)

4. **Do not conflate Blazor UI and MAUI UI decisions.** They are separate platforms, separate SyncFusion products, separate license types, and separate integration patterns. (James, Marcus)

5. **Do not migrate the DataGrid.** MudDataGrid handles everything a household management app needs. SyncFusion's grid is more powerful, but that power is irrelevant for 200-500 products. (Priya)

6. **Do not ship the product until the license mismatch is resolved.** The documentation claiming AGPL-3.0 while LICENSE files say PolyForm Shield is a material legal liability that grows every day. (David, Sarah)

---

## Team Agreement Matrix

| Topic | Marcus | Sarah | David | Priya | James |
|-------|--------|-------|-------|-------|-------|
| Fix license mismatch immediately | Agree | Agree | Agree | Agree | Agree |
| Reject abstraction layer | **Strongly agree** | -- | -- | **Strongly agree** | Agree |
| Parallel UI projects in cloud repo | **Strongly agree** | Agree | Agree | Agree | -- |
| SyncFusion only for Scheduler + PDF Viewer | Agree | -- | -- | **Strongly agree** | Agree (web) |
| Keep PolyForm Shield | -- | **Strongly agree** | Disagree | -- | -- |
| Switch to AGPL-3.0 | -- | Disagree | **Strongly agree** | -- | -- |
| Implement CLA | Agree | Agree | **Strongly agree** | Agree | -- |
| Consolidate to one parent repo | **Strongly agree** | **Strongly agree** | Agree | **Strongly agree** | Agree |
| Move Marketing.Web to cloud repo | Agree | Agree | Agree | Agree | -- |
| Move infrastructure/ to cloud repo | Agree | **Strongly agree** | **Strongly agree** | Agree | -- |
| MIT MAUI Toolkit immediately | -- | -- | -- | -- | **Strongly agree** |
| SfScheduler for cloud mobile | -- | -- | -- | -- | **Strongly agree** |
| Do NOT migrate DataGrid | Agree | -- | -- | **Strongly agree** | Agree (for mobile) |
| Extract UI services to Core first | **Strongly agree** | -- | -- | Agree | -- |

---

## 12. Repository Consolidation Migration Plan

### 12.1 Overview

| Metric | Before | After |
|--------|--------|-------|
| Total repositories | 5 (Famick, Famick-Self-Hosted, homemanagement-shared, homemanagement, homemanagement-cloud) | 2 (Famick, homemanagement-cloud) |
| Submodule references | 5 (3 in Famick + 2 in Famick-Self-Hosted) | 1 (homemanagement-cloud in Famick) |
| Solution files | 4 | 2 (Famick.sln + homemanagement-cloud.sln) |
| Commits for cross-cutting feature | 4 (shared + self-hosted + cloud + parent pointer) | 2 (Famick + cloud) |
| Commits for shared-only change | 2 (shared + parent pointer) | 1 (Famick) |

### 12.2 Prerequisites (Do First)

#### 12.2.1 Rotate JWT Production Private Key

**David Ramirez (Legal/Security)**: `infrastructure/terraform/environments/prod/jwt-prod-private.pem` is committed to git history. Before making Famick public:

1. Generate a new JWT signing key
2. Deploy the new key to production (AWS Secrets Manager or SSM Parameter Store)
3. Update the application to use the new key
4. The old key in git history will be exposed but already rotated

#### 12.2.2 Audit Git History for Other Secrets

Check for any other sensitive files in Famick's git history that would be exposed when going public:
- AWS credentials or access keys
- Database connection strings with passwords
- API keys (SyncFusion, Stripe, etc.)
- `.env` files accidentally committed

Use `git log --all --diff-filter=A -- "*.pem" "*.key" "*.env" "*secret*" "*credential*"` to scan.

### 12.3 Migration Steps

#### Step 1: Move `infrastructure/` to `homemanagement-cloud`

Move terraform, scripts, and deployment configs to the private cloud repo **before** making Famick public.

```bash
# In homemanagement-cloud repo
cd /path/to/homemanagement-cloud

# Copy infrastructure from Famick
cp -r /path/to/Famick/infrastructure ./infrastructure

# Commit to cloud repo
git add infrastructure/
git commit -m "chore: move infrastructure from parent repo (pre-consolidation)"
git push
```

Then delete from Famick:
```bash
cd /path/to/Famick
git rm -r infrastructure/
git commit -m "chore: move infrastructure to cloud repo (pre-consolidation)"
```

**Script path update**: `build-and-push.sh` uses `PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"`. After moving to `homemanagement-cloud/infrastructure/scripts/`, `PROJECT_ROOT` resolves to `homemanagement-cloud/` -- but Docker builds need the Famick workspace root. Update to:
```bash
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
```

**Dockerfile path updates in build-and-push.sh**:
```bash
# BEFORE
build_and_push "marketing" "src/Famick.Marketing.Web/Dockerfile" "src/Famick.Marketing.Web"
build_and_push "cloud-app" "homemanagement-cloud/src/Famick.HomeManagement.Web/Dockerfile" "."

# AFTER
build_and_push "marketing" "homemanagement-cloud/src/Famick.Marketing.Web/Dockerfile" "homemanagement-cloud/src/Famick.Marketing.Web"
build_and_push "cloud-app" "homemanagement-cloud/src/Famick.HomeManagement.Web/Dockerfile" "."
```

**tf.sh**: No path changes needed (uses `SCRIPT_DIR` relative paths to find terraform configs).

#### Step 2: Move `Famick.Marketing.Web` to `homemanagement-cloud`

```bash
# In homemanagement-cloud repo
cp -r /path/to/Famick/src/Famick.Marketing.Web ./src/Famick.Marketing.Web

# Commit
git add src/Famick.Marketing.Web/
git commit -m "chore: move Marketing.Web from parent repo (pre-consolidation)"
git push
```

Then delete from Famick:
```bash
cd /path/to/Famick
git rm -r src/Famick.Marketing.Web/
git commit -m "chore: move Marketing.Web to cloud repo"
```

**No code changes needed** -- Marketing.Web has zero project references. The Dockerfile is self-contained.

#### Step 3: Merge `homemanagement-shared` into Famick

Use `git subtree add` to preserve full commit history:

```bash
cd /path/to/Famick

# Remove the submodule first
git submodule deinit homemanagement-shared
git rm homemanagement-shared
rm -rf .git/modules/homemanagement-shared

# Add as subtree (preserves history)
git subtree add --prefix=_temp_shared \
    git@github.com:Famick-com/HomeManagement-Shared.git main

# Move files to final locations
mkdir -p src tests
mv _temp_shared/src/* src/
mv _temp_shared/tests/* tests/

# Clean up temp directory and non-src files
# (CLAUDE.md, .sln, .github/ etc. from homemanagement-shared)
rm -rf _temp_shared

git add .
git commit -m "chore: merge homemanagement-shared into Famick repo"
```

**Alternative (simpler, no history)**: If preserving homemanagement-shared's commit history is not important:
```bash
git submodule deinit homemanagement-shared
git rm homemanagement-shared
cp -r /path/to/homemanagement-shared/src/* src/
cp -r /path/to/homemanagement-shared/tests/* tests/
git add .
git commit -m "chore: merge homemanagement-shared into Famick repo"
```

#### Step 4: Merge `homemanagement` into Famick

Same approach:

```bash
cd /path/to/Famick

# Remove the submodule
git submodule deinit homemanagement
git rm homemanagement
rm -rf .git/modules/homemanagement

# Add as subtree (preserves history)
git subtree add --prefix=_temp_hm \
    git@github.com:Famick-com/HomeManagement.git main

# Move files to final locations
mv _temp_hm/src/* src/
mv _temp_hm/tests/* tests/
mv _temp_hm/docker docker/
mv _temp_hm/.github .github/

# Clean up
rm -rf _temp_hm

git add .
git commit -m "chore: merge homemanagement into Famick repo"
```

#### Step 5: Update `.gitmodules`

After removing homemanagement and homemanagement-shared, `.gitmodules` should only contain:

```ini
[submodule "homemanagement-cloud"]
    path = homemanagement-cloud
    url = git@github.com:Famick-com/HomeManagement-Cloud.git
```

#### Step 6: Update Project References

##### Shared Library .csproj Files (NO CHANGES)

These projects reference siblings via `..\..\ProjectName\...` and remain siblings under `src/`:

| Project | References | Change? |
|---------|-----------|---------|
| Domain | (none) | No |
| Shared | (none) | No |
| Core | Domain, Shared | **No** (still siblings) |
| Infrastructure | Domain, Core, Shared | **No** (still siblings) |
| UI | Core | **No** (still siblings) |
| Web.Shared | Domain, Core, Infrastructure | **No** (still siblings) |

##### Self-Hosted .csproj Files (CHANGES NEEDED)

**`src/Famick.HomeManagement.Web/Famick.HomeManagement.Web.csproj`**:

```xml
<!-- BEFORE (cross-submodule paths) -->
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Domain\Famick.HomeManagement.Domain.csproj" />
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Core\Famick.HomeManagement.Core.csproj" />
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Infrastructure\Famick.HomeManagement.Infrastructure.csproj" />
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Shared\Famick.HomeManagement.Shared.csproj" />
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Web.Shared\Famick.HomeManagement.Web.Shared.csproj" />
<ProjectReference Include="..\Famick.HomeManagement.Web.Client\Famick.HomeManagement.Web.Client.csproj" />

<!-- AFTER (siblings in src/) -->
<ProjectReference Include="..\Famick.HomeManagement.Domain\Famick.HomeManagement.Domain.csproj" />
<ProjectReference Include="..\Famick.HomeManagement.Core\Famick.HomeManagement.Core.csproj" />
<ProjectReference Include="..\Famick.HomeManagement.Infrastructure\Famick.HomeManagement.Infrastructure.csproj" />
<ProjectReference Include="..\Famick.HomeManagement.Shared\Famick.HomeManagement.Shared.csproj" />
<ProjectReference Include="..\Famick.HomeManagement.Web.Shared\Famick.HomeManagement.Web.Shared.csproj" />
<ProjectReference Include="..\Famick.HomeManagement.Web.Client\Famick.HomeManagement.Web.Client.csproj" />
```

**`src/Famick.HomeManagement.Web.Client/Famick.HomeManagement.Web.Client.csproj`**:

```xml
<!-- BEFORE -->
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.UI\Famick.HomeManagement.UI.csproj" />

<!-- AFTER -->
<ProjectReference Include="..\Famick.HomeManagement.UI\Famick.HomeManagement.UI.csproj" />
```

**`src/Famick.HomeManagement.Mobile/Famick.HomeManagement.Mobile.csproj`**: **NO CHANGES** (zero project references).

##### Self-Hosted Test .csproj Files (PARTIAL CHANGES)

**`tests/Famick.HomeManagement.Tests.Unit/Famick.HomeManagement.Tests.Unit.csproj`**:

```xml
<!-- BEFORE -->
<ProjectReference Include="..\..\src\Famick.HomeManagement.Web\Famick.HomeManagement.Web.csproj" />
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Web.Shared\Famick.HomeManagement.Web.Shared.csproj" />

<!-- AFTER -->
<ProjectReference Include="..\..\src\Famick.HomeManagement.Web\Famick.HomeManagement.Web.csproj" />
<ProjectReference Include="..\..\src\Famick.HomeManagement.Web.Shared\Famick.HomeManagement.Web.Shared.csproj" />
```

**`tests/Famick.HomeManagement.Tests.Integration/Famick.HomeManagement.Tests.Integration.csproj`**: **NO CHANGES** (only references `..\..\src\Famick.HomeManagement.Web\...` which stays the same).

##### Shared Test .csproj Files (NO CHANGES)

Both `Shared.Tests.Unit` and `Shared.Tests.Integration` reference siblings via `..\..\src\...` — same pattern after move.

##### Cloud .csproj Files (PATH SIMPLIFICATION)

**`homemanagement-cloud/src/Famick.HomeManagement.Web/Famick.HomeManagement.Web.csproj`**:

```xml
<!-- BEFORE (3 levels up, then into submodule paths) -->
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Domain\Famick.HomeManagement.Domain.csproj" />
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Core\Famick.HomeManagement.Core.csproj" />
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Infrastructure\Famick.HomeManagement.Infrastructure.csproj" />
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Shared\Famick.HomeManagement.Shared.csproj" />
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Web.Shared\Famick.HomeManagement.Web.Shared.csproj" />
<ProjectReference Include="..\..\..\homemanagement\src\Famick.HomeManagement.Web.Client\Famick.HomeManagement.Web.Client.csproj" />

<!-- AFTER (3 levels up, then into src/) -->
<ProjectReference Include="..\..\..\src\Famick.HomeManagement.Domain\Famick.HomeManagement.Domain.csproj" />
<ProjectReference Include="..\..\..\src\Famick.HomeManagement.Core\Famick.HomeManagement.Core.csproj" />
<ProjectReference Include="..\..\..\src\Famick.HomeManagement.Infrastructure\Famick.HomeManagement.Infrastructure.csproj" />
<ProjectReference Include="..\..\..\src\Famick.HomeManagement.Shared\Famick.HomeManagement.Shared.csproj" />
<ProjectReference Include="..\..\..\src\Famick.HomeManagement.Web.Shared\Famick.HomeManagement.Web.Shared.csproj" />
<ProjectReference Include="..\..\..\src\Famick.HomeManagement.Web.Client\Famick.HomeManagement.Web.Client.csproj" />
```

**`homemanagement-cloud/src/Famick.HomeManagement.Cloud/Famick.HomeManagement.Cloud.csproj`**:

```xml
<!-- BEFORE -->
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Domain\Famick.HomeManagement.Domain.csproj" />
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Infrastructure\Famick.HomeManagement.Infrastructure.csproj" />

<!-- AFTER -->
<ProjectReference Include="..\..\..\src\Famick.HomeManagement.Domain\Famick.HomeManagement.Domain.csproj" />
<ProjectReference Include="..\..\..\src\Famick.HomeManagement.Infrastructure\Famick.HomeManagement.Infrastructure.csproj" />
```

**`homemanagement-cloud/src/Famick.HomeManagement.Cloud.Infrastructure/Famick.HomeManagement.Cloud.Infrastructure.csproj`**:

```xml
<!-- BEFORE -->
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Core\Famick.HomeManagement.Core.csproj" />
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Infrastructure\Famick.HomeManagement.Infrastructure.csproj" />
<ProjectReference Include="..\..\..\homemanagement-shared\src\Famick.HomeManagement.Domain\Famick.HomeManagement.Domain.csproj" />

<!-- AFTER -->
<ProjectReference Include="..\..\..\src\Famick.HomeManagement.Core\Famick.HomeManagement.Core.csproj" />
<ProjectReference Include="..\..\..\src\Famick.HomeManagement.Infrastructure\Famick.HomeManagement.Infrastructure.csproj" />
<ProjectReference Include="..\..\..\src\Famick.HomeManagement.Domain\Famick.HomeManagement.Domain.csproj" />
```

##### Cloud Test .csproj Files (NO CHANGES)

Both `Cloud.Tests.Unit` and `Cloud.Tests.Integration` only reference cloud-internal projects via `..\..\src\...` — no cross-repo paths.

#### Step 7: Update Dockerfiles

##### Self-Hosted Dockerfile (`docker/Dockerfile`)

```dockerfile
# BEFORE (paths reference submodule directories)
COPY homemanagement-shared/src/Famick.HomeManagement.Shared/... homemanagement-shared/src/.../
COPY homemanagement-shared/src/Famick.HomeManagement.Domain/... homemanagement-shared/src/.../
COPY homemanagement/src/Famick.HomeManagement.Web/... homemanagement/src/.../
RUN dotnet restore homemanagement/src/Famick.HomeManagement.Web/...
COPY homemanagement-shared/src/ homemanagement-shared/src/
COPY homemanagement/src/ homemanagement/src/
WORKDIR /src/homemanagement/src/Famick.HomeManagement.Web

# AFTER (everything under src/)
COPY src/Famick.HomeManagement.Shared/... src/Famick.HomeManagement.Shared/
COPY src/Famick.HomeManagement.Domain/... src/Famick.HomeManagement.Domain/
COPY src/Famick.HomeManagement.Web/... src/Famick.HomeManagement.Web/
RUN dotnet restore src/Famick.HomeManagement.Web/...
COPY src/ src/
WORKDIR /src/src/Famick.HomeManagement.Web
```

Build context changes from workspace root to repo root (same thing now):
```bash
docker build -f docker/Dockerfile .
```

##### Cloud Dockerfile (`homemanagement-cloud/src/Famick.HomeManagement.Web/Dockerfile`)

```dockerfile
# BEFORE
COPY homemanagement-shared/src/Famick.HomeManagement.Domain/... homemanagement-shared/src/.../
COPY homemanagement/src/Famick.HomeManagement.Web.Client/... homemanagement/src/.../
COPY homemanagement-cloud/src/... homemanagement-cloud/src/.../
COPY homemanagement-shared/src/ homemanagement-shared/src/
COPY homemanagement/src/Famick.HomeManagement.Web.Client/ homemanagement/src/.../

# AFTER
COPY src/Famick.HomeManagement.Domain/... src/Famick.HomeManagement.Domain/
COPY src/Famick.HomeManagement.Web.Client/... src/Famick.HomeManagement.Web.Client/
COPY homemanagement-cloud/src/... homemanagement-cloud/src/.../
COPY src/ src/
```

Build context is still the Famick workspace root:
```bash
docker build -f homemanagement-cloud/src/Famick.HomeManagement.Web/Dockerfile .
```

#### Step 8: Update Solution Files

**`Famick.sln`** (public repo): Remove all submodule-relative project paths and replace with direct `src/` and `tests/` paths. Remove Marketing.Web. Remove infrastructure references.

**`homemanagement-cloud/homemanagement-cloud.sln`**: Update shared project references from `../homemanagement-shared/src/...` to `../src/...`.

#### Step 9: Update GitHub Workflows

Move `homemanagement/.github/workflows/testflight.yml` and `play-store.yml` to `Famick/.github/workflows/`. Update any path references:

```yaml
# BEFORE (in testflight.yml / play-store.yml)
working-directory: src/Famick.HomeManagement.Mobile
# (Verify actual paths in workflow files -- mobile project is now at src/)

# AFTER
working-directory: src/Famick.HomeManagement.Mobile
# (Same relative path from repo root -- likely no change needed)
```

#### Step 10: Archive Old Repos

After migration is complete and verified:

1. **homemanagement-shared**: Archive on GitHub. Update README to say "This repository has been merged into [Famick](link). All development continues there."
2. **homemanagement**: Archive on GitHub. Same redirect notice.
3. **Famick-Self-Hosted**: Archive on GitHub. Same redirect notice. The `docker-compose.yml`, `Dockerfile`, and self-hosting docs should be migrated to Famick's `docker/` directory first.

#### Step 11: Make Famick Public

After:
- JWT key is rotated
- Secrets are scrubbed from history (or use `git filter-repo` / BFG Repo Cleaner)
- `infrastructure/` is removed from Famick
- `Marketing.Web` is removed from Famick
- LICENSE file is correct
- All tests pass

Set the Famick repository to public on GitHub.

### 12.4 Complete .csproj Change Inventory

| File | Change Type | What Changes |
|------|-------------|-------------|
| **Shared libraries (6 projects)** | | |
| `src/Famick.HomeManagement.Domain/*.csproj` | **No change** | No project references |
| `src/Famick.HomeManagement.Shared/*.csproj` | **No change** | No project references |
| `src/Famick.HomeManagement.Core/*.csproj` | **No change** | Siblings stay siblings |
| `src/Famick.HomeManagement.Infrastructure/*.csproj` | **No change** | Siblings stay siblings |
| `src/Famick.HomeManagement.UI/*.csproj` | **No change** | Siblings stay siblings |
| `src/Famick.HomeManagement.Web.Shared/*.csproj` | **No change** | Siblings stay siblings |
| **Self-hosted app (3 projects)** | | |
| `src/Famick.HomeManagement.Web/*.csproj` | **5 refs change** | `../../../homemanagement-shared/src/X` → `../X` |
| `src/Famick.HomeManagement.Web.Client/*.csproj` | **1 ref changes** | `../../../homemanagement-shared/src/X` → `../X` |
| `src/Famick.HomeManagement.Mobile/*.csproj` | **No change** | Zero project references |
| **Self-hosted tests (2 projects)** | | |
| `tests/Famick.HomeManagement.Tests.Unit/*.csproj` | **1 ref changes** | `../../../homemanagement-shared/src/X` → `../../src/X` |
| `tests/Famick.HomeManagement.Tests.Integration/*.csproj` | **No change** | Only refs self-hosted Web |
| **Shared tests (2 projects)** | | |
| `tests/Famick.HomeManagement.Shared.Tests.Unit/*.csproj` | **No change** | `../../src/X` pattern preserved |
| `tests/Famick.HomeManagement.Shared.Tests.Integration/*.csproj` | **No change** | `../../src/X` pattern preserved |
| **Cloud projects (3 projects)** | | |
| `homemanagement-cloud/src/Famick.HomeManagement.Web/*.csproj` | **6 refs change** | `../../../homemanagement-shared/src/X` → `../../../src/X` and `../../../homemanagement/src/X` → `../../../src/X` |
| `homemanagement-cloud/src/Famick.HomeManagement.Cloud/*.csproj` | **2 refs change** | `../../../homemanagement-shared/src/X` → `../../../src/X` |
| `homemanagement-cloud/src/Famick.HomeManagement.Cloud.Infrastructure/*.csproj` | **3 refs change** | `../../../homemanagement-shared/src/X` → `../../../src/X` |
| **Cloud tests (2 projects)** | | |
| `homemanagement-cloud/tests/*.csproj` (both) | **No change** | Only ref cloud-internal projects |
| **Marketing (1 project)** | | |
| `homemanagement-cloud/src/Famick.Marketing.Web/*.csproj` | **No change** | Zero project references |
| | | |
| **Total** | **18 refs across 7 files** | All other 14 projects unchanged |

### 12.5 Git Workflow Comparison

#### Before (Cross-Cutting Feature)

```bash
# 1. Change shared code
cd homemanagement-shared && git add . && git commit && git push

# 2. Change self-hosted code
cd ../homemanagement && git add . && git commit && git push

# 3. Change cloud code
cd ../homemanagement-cloud && git add . && git commit && git push

# 4. Update parent submodule pointers
cd .. && git add homemanagement-shared homemanagement homemanagement-cloud
git commit -m "chore: update submodules" && git push

# 5. ALSO update Famick-Self-Hosted parent
cd ../Famick-Self-Hosted
git submodule update --remote && git add . && git commit && git push
```

**5 commits, 5 pushes, 3+ repos**

#### After (Cross-Cutting Feature)

```bash
# 1. Change shared + self-hosted code (same repo now)
cd Famick && git add . && git commit && git push

# 2. Change cloud code (if needed)
cd homemanagement-cloud && git add . && git commit && git push

# 3. Update submodule pointer (if cloud changed)
cd .. && git add homemanagement-cloud && git commit && git push
```

**2-3 commits, 2-3 pushes, 1-2 repos**

#### After (Shared-Only Change)

```bash
# Just one commit!
cd Famick && git add . && git commit && git push
```

**1 commit, 1 push, 1 repo** (was 3 commits, 3 pushes, 2 repos)

### 12.6 Cloud CI/CD Strategy

Since the cloud repo is a submodule of the public Famick repo, cloud CI needs access to the parent:

```yaml
# homemanagement-cloud/.github/workflows/ci.yml
jobs:
  build:
    steps:
      - name: Checkout cloud repo
        uses: actions/checkout@v4

      - name: Checkout parent (public, no auth needed)
        uses: actions/checkout@v4
        with:
          repository: Famick-com/Famick
          path: _parent

      - name: Symlink cloud into parent
        run: |
          rm -rf _parent/homemanagement-cloud
          ln -s $GITHUB_WORKSPACE _parent/homemanagement-cloud

      - name: Build
        working-directory: _parent/homemanagement-cloud/src/Famick.HomeManagement.Web
        run: dotnet build
```

Since `Famick` is public, no authentication token is needed to check it out.

### 12.7 Estimated Effort

| Step | Time | Notes |
|------|------|-------|
| Rotate JWT key | 30 min | Must happen first |
| Move infrastructure/ to cloud | 30 min | Simple copy + commit |
| Move Marketing.Web to cloud | 15 min | Simple copy + commit |
| Merge homemanagement-shared (subtree) | 30 min | Preserves history |
| Merge homemanagement (subtree) | 30 min | Preserves history |
| Update .gitmodules | 5 min | Remove 2 entries |
| Update 7 .csproj files (18 refs) | 30 min | Mechanical find-replace |
| Update 2 Dockerfiles | 30 min | Path changes |
| Update build-and-push.sh | 15 min | Path changes |
| Update solution files | 30 min | Re-add projects with new paths |
| Update GitHub workflows | 15 min | Move + verify paths |
| Update CLAUDE.md | 30 min | New structure documentation |
| Verify `dotnet build` for all solutions | 15 min | Build check |
| Run all tests | 15 min | Regression check |
| Archive old repos | 15 min | GitHub settings |
| Make Famick public | 5 min | GitHub settings |
| **Total** | **~4-5 hours** | Can be done in one session |

---

## Sources

- [SyncFusion Community License](https://www.syncfusion.com/products/communitylicense)
- [SyncFusion Essential Studio EULA](https://www.syncfusion.com/eula/es)
- [SyncFusion License Key Overview](https://help.syncfusion.com/common/essential-studio/licensing/overview)
- [SyncFusion .NET MAUI Toolkit (MIT)](https://www.syncfusion.com/net-maui-toolkit)
- [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/)
- [AGPL-3.0 License Text](https://www.gnu.org/licenses/agpl-3.0.en.html)
- [CLA Assistant (GitHub integration)](https://cla-assistant.io/)
- [QuestPDF License Changes (2024)](https://www.questpdf.com/license.html)
