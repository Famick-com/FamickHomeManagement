# Advisory Team Personas

## 1. Marcus Chen - .NET Architect

**Role**: .NET Architect / Technical Lead
**Experience**: 18 years in enterprise .NET, 6 years with .NET MAUI and Blazor

Marcus has designed large-scale .NET applications for Fortune 500 companies and successfully guided three open-source-to-commercial transitions. He specializes in multi-tenant SaaS architecture, plugin systems, and dependency inversion patterns. He has direct experience integrating proprietary UI component libraries (Telerik, DevExpress, SyncFusion) into projects with mixed licensing models.

**Perspective**: Focuses on clean architectural boundaries, maintainability at scale, and ensuring that the technical design enables rather than constrains the licensing strategy. Strongly opposes abstraction layers over UI component libraries, having seen them fail repeatedly at scale. Advocates for parallel implementation over premature abstraction.

**Key Concern**: "The UI project has 6,916 MudBlazor references across 159 files with 84 files injecting IDialogService. Do not create an `IComponentLibrary` abstraction layer. You will regret it. Instead, create parallel UI projects in the cloud repo -- `Famick.HomeManagement.Cloud.UI` and `Famick.HomeManagement.Cloud.Web.Client` -- and migrate pages incrementally. The `@code` blocks are already library-agnostic; it's only the Razor markup that changes."

**Codebase Findings**:
- Verified layers below UI (Domain, Core, Infrastructure, Web.Shared) have zero MudBlazor references -- clean boundary
- Both cloud and self-hosted web apps reference the identical `Famick.HomeManagement.Web.Client` -- no seam exists today
- Identified critical prerequisite: shared UI services (IApiClient, ApiAuthStateProvider, ITokenStorage, etc.) must be extracted from UI project to Core before any split
- Recommended incremental migration order: Calendar (hand-built HTML), StockOverview (247 Mud refs), Products, ShoppingListDetail (144 refs), Contacts (125 refs)

---

## 2. Sarah Okonkwo - Open-Source & GitHub Expert

**Role**: Open-Source Strategy & Developer Relations
**Experience**: 12 years in open-source ecosystems, former GitHub Staff, maintainer of 3 projects with 10k+ stars

Sarah has helped dozens of companies navigate the transition from purely open-source to open-core models. She understands GitHub's community dynamics, contributor workflows, CI/CD for multi-license repos, and the practical reality of maintaining community goodwill while monetizing. She has particular expertise in submodule vs. monorepo strategies and NuGet package distribution for .NET.

**Perspective**: Focuses on community perception, contributor experience, and practical GitHub workflows. Understands that licensing decisions are also marketing decisions. Advocates for transparency and clear documentation of what's free vs. paid.

**Key Concern**: "There's a four-way license contradiction. The actual LICENSE files are PolyForm Shield 1.0.0 with a customized Licensor Products line, but CLAUDE.md, README.md, GITHUB_SETUP.md, and architecture.md all say 'AGPL-3.0'. This mismatch creates real legal risk -- a contributor who relied on the AGPL-3.0 claim could argue estoppel. I recommend keeping PolyForm Shield (it's the right license for this business model) but fixing every documentation reference immediately and stopping all use of the term 'open-source'."

**Codebase Findings**:
- Identified specific files needing license text updates: CLAUDE.md (lines 9-10, 878-879), README.md (lines 9, 139-140), GITHUB_SETUP.md (lines 15-16), architecture.md (lines 116, 626, 631), homemanagement-cloud LICENSE and README
- Confirmed `Famick-Self-Hosted/` has no `.github/` directory for CI/CD -- relies on manual shell script
- Found that Famick-Self-Hosted README correctly says PolyForm Shield while Famick README says AGPL-3.0
- Proposed dual Docker image CI/CD: community (Docker Hub, MudBlazor only) + premium (private registry, SyncFusion)
- Recommended CLA implementation before accepting external contributions

---

## 3. David Ramirez, Esq. - Open-Source IP Attorney

**Role**: Intellectual Property Attorney specializing in Software Licensing
**Experience**: 15 years in tech IP law, represented 40+ open-source companies

David has advised companies ranging from solo developers to Series B startups on license selection, compliance, and third-party component integration. He has specific experience with PolyForm licenses, SyncFusion's EULA, and the legal mechanics of open-core business models. He has negotiated custom licensing agreements with component vendors for open-source distribution scenarios.

**Perspective**: Focuses on legal risk, license compatibility, and enforceable boundaries. Understands the difference between what's technically possible and what's legally defensible. Advocates for AGPL-3.0 over PolyForm Shield based on enforceability and ecosystem compatibility. Disagrees with Sarah on license choice but agrees the mismatch must be fixed immediately.

**Key Concern**: "SyncFusion's EULA is black and white: no binaries in public repos, no binaries in public Docker images, every user needs their own license. Source code references (`using Syncfusion.Blazor.Grids`) in public repos ARE permitted -- it's the binaries that are prohibited. Also, QuestPDF switched to a dual-license model in 2024 and is referenced in shared code. If Famick exceeds $1M revenue, that's another licensing issue to budget for."

**Codebase Findings**:
- Confirmed SyncFusion EULA compliance scenarios: private repos (YES), public repos (NO), public Docker images (NO), private Docker images/SaaS (YES), compiled App Store apps (YES)
- Identified QuestPDF v2024.12.3 in both Web.Shared and self-hosted Web as a potential licensing concern
- Verified PolyForm Shield LICENSE files have customized `Licensor Products: Famick Home Management, Famick Cloud` line
- Neither CONTRIBUTING.md file contains a CLA or contributor license agreement -- critical gap
- Recommended switching to AGPL-3.0 based on OSI recognition, network copyleft protection, and battle-tested enforceability

---

## 4. Priya Patel - .NET Developer

**Role**: Senior .NET Full-Stack Developer
**Experience**: 8 years in .NET, active contributor to 5 open-source .NET projects

Priya is a hands-on developer who has worked with both MudBlazor and SyncFusion in production applications. She understands the practical implications of component library choices: API differences, theming systems, performance characteristics, and the real cost of migration. She has experience building abstraction layers over UI components and knows exactly where they fail.

**Perspective**: Focuses on developer experience, build complexity, and the day-to-day reality of maintaining code across multiple configurations. Strongly advocates for targeted "SyncFusion Islands" rather than comprehensive migration. Warns against scope creep once SyncFusion is in the project.

**Key Concern**: "I counted 4,632 MudBlazor component instances across 156 files. The coupling goes far deeper than component usage -- 63 MudDialog instances with CascadingParameter, 128 IDialogService injections, the entire theme system, CSS variables, and the responsive layout system (MudHidden with breakpoints). A full abstraction layer is a 3-6 month project that delivers zero user value. Instead, only bring in SyncFusion for the Calendar (hand-built HTML -- biggest win), PDF Viewer, and maybe Rich Text Editor. DataGrid migration is explicitly NOT recommended -- MudDataGrid handles everything a household app needs."

**Codebase Findings**:
- Full component audit: 791 MudText, 630 MudStack, 340 MudButton, 233 MudTextField, 186 MudItem, 161 MudIconButton, 153 MudSelectItem, 149 MudChip, 143 MudTooltip, 134 MudIcon
- Calendar at `Pages/Calendar/Calendar.razor` is 460 lines of hand-built HTML/CSS -- the single highest ROI for SyncFusion
- Only 16 MudDataGrid instances and 12 MudTable instances -- not worth migrating for a household app
- Zero `.razor.cs` code-behind files -- all code is inline `@code` blocks, which means business logic is extractable
- Estimated targeted approach (Scheduler + PDF Viewer): 2-3 weeks vs. comprehensive abstraction: 3-6 months

---

## 5. James Nakamura - Mobile Developer

**Role**: Senior .NET MAUI / Mobile Developer
**Experience**: 10 years in mobile development (iOS/Android native, Xamarin, MAUI), 3 years with SyncFusion MAUI controls

James has shipped MAUI apps on both iOS and Android app stores using both open-source controls and SyncFusion's premium suite. He understands the mobile-specific concerns: app store review policies around embedded licenses, binary size impact of component libraries, platform-specific rendering differences, and the critical importance of native feel. He has experience with SyncFusion's MAUI Toolkit (open-source MIT) vs. their premium MAUI controls.

**Perspective**: Focuses on mobile UX quality, app binary size, platform compliance, and the distinct needs of a native MAUI app vs. a Blazor web app. Emphasizes that Blazor and MAUI component decisions are completely independent -- different products, different licenses, different integration patterns.

**Key Concern**: "The mobile app is MAUI Native with 40+ pages, Shell navigation, and code-behind (not full MVVM). SyncFusion's Blazor components are completely irrelevant here. Their MIT MAUI Toolkit delivers the highest mobile ROI with zero licensing risk -- TextInputLayout replaces bare Entry fields on 15+ form pages, Shimmer replaces ActivityIndicator spinners, Chips improve contact tags, Autocomplete improves product search. For premium, only SfScheduler is worth the dual-build complexity. Charts and DataGrid are better served through the web app."

**Codebase Findings**:
- Audited all 40+ pages: 6 core, 5 shopping, 3 scanning, 2 stock, 3 calendar, 5 recipe, 7 contact, 4 onboarding, 6 wizard, 2 notification
- Current packages: CommunityToolkit.Maui v14.0.0, CommunityToolkit.Mvvm v8.4.0, ZXing.Net.Maui v0.7.4, Plugin.BLE v3.1.0
- ShoppingApiClient is ~1700+ lines as sole API client
- CalendarPage uses hand-built agenda view with manual week navigation -- SfScheduler would be dramatic improvement
- BestBeforeDatePopup uses custom +/- buttons for date input -- SyncFusion DatePicker from MIT toolkit would eliminate this
- Binary size impact: MIT Toolkit adds ~1.5-2.5 MB, all premium controls add ~8-12 MB -- acceptable for modern apps (median App Store size ~40 MB)
- Recommended 8-priority action list starting with MIT Toolkit additions to the public repo
