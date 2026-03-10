# Session Checkpoint: Grocery Product Onboarding + Master Product Catalog

**Date:** 2026-03-09
**Feature:** Grocery Product Onboarding → Master Product Catalog
**Feature Doc:** `Clients - Projects/Famick/Road Map/Features - In Planning/Feature - Grocery Product Onboarding.md`

## Status

All code layers implemented and building successfully. `ProductTemplate` has been replaced by `MasterProduct` — a richer global catalog with universal barcodes, nutrition, and images. Tenant products link to master products via `MasterProductId` with `OverriddenFields` tracking. The ~500-item seed JSON is populated. EF migration generated.

## Architecture

### MasterProduct (BaseEntity, non-tenant)
- Replaces ProductTemplate with expanded Product-mirrored fields
- Self-referencing parent hierarchy (`ParentMasterProductId`)
- Universal barcodes (`MasterProductBarcode`), nutrition (`MasterProductNutrition`), images (`MasterProductImage`)
- Onboarding metadata (lifestyle tags, allergen/dietary conflict flags, cooking style tags, location/unit hints)
- `Brand` field: null = generic (can be master parent), set = brand-specific

### Product (BaseTenantEntity) additions
- `MasterProductId?` — FK to MasterProduct (SetNull on delete)
- `OverriddenFields` — JSON string[] of field names tenant has customized
- `Brand` — null = generic, set = brand-specific

### Auto-link (runs once on seed)
- Matches existing tenant products to master products by barcode (first) or name (case-insensitive)
- Last edited wins for conflict scope (whole-record timestamp comparison)
- Promotes tenant barcodes and nutrition to master
- Links generic parent products in master hierarchy

## Key Decisions

- **Barcodes are universal** — live on MasterProduct, not tenant-scoped
- **Self-hosted**: sharing feature NOT available now; MasterProduct table exists everywhere (replaces templates)
- **No moderation** for sharing now, may be added later
- **Updates automatic** — tenant gets master values for non-overridden fields, no notification
- **No barcode dedup** — barcodes are universal on master
- **Brand null = generic** — eligible for master parent hierarchy
- **Auto-link runs once** during initial seed

## Files Created (10)

| File | Purpose |
|------|---------|
| `src/.../Domain/Entities/MasterProduct.cs` | Global product catalog entity (replaces ProductTemplate) |
| `src/.../Domain/Entities/MasterProductBarcode.cs` | Universal barcodes |
| `src/.../Domain/Entities/MasterProductNutrition.cs` | Shared nutrition data |
| `src/.../Domain/Entities/MasterProductImage.cs` | Shared product images |
| `src/.../Infrastructure/Configuration/MasterProductConfiguration.cs` | EF config |
| `src/.../Infrastructure/Configuration/MasterProductBarcodeConfiguration.cs` | EF config |
| `src/.../Infrastructure/Configuration/MasterProductNutritionConfiguration.cs` | EF config |
| `src/.../Infrastructure/Configuration/MasterProductImageConfiguration.cs` | EF config |
| `src/.../Infrastructure/Configuration/ProductConfiguration.cs` | New FK/index config for Product |
| `src/.../Infrastructure/Data/MasterProductSeeder.cs` | Seeder with auto-link logic |

## Files Modified (7)

| File | Change |
|------|--------|
| `src/.../Domain/Entities/Product.cs` | Added Brand, MasterProductId, OverriddenFields, MasterProduct nav |
| `src/.../Infrastructure/Data/HomeManagementDbContext.cs` | Replaced ProductTemplates DbSet with MasterProducts + children |
| `src/.../Infrastructure/InfrastructureStartup.cs` | DI + seeder invocation updated |
| `src/.../Core/DTOs/ProductOnboarding/ProductOnboardingDtos.cs` | Template → MasterProduct renames |
| `src/.../Core/Validators/ProductOnboarding/ProductOnboardingCompleteRequestValidator.cs` | SelectedTemplateIds → SelectedMasterProductIds |
| `src/.../Infrastructure/Services/ProductOnboardingService.cs` | Full rewrite using MasterProduct |
| `src/.../UI/Components/Products/ProductOnboarding/ProductOnboardingWizard.razor` | Template → MasterProduct renames |

## Files Deleted (4)

| File | Reason |
|------|--------|
| `src/.../Domain/Entities/ProductTemplate.cs` | Replaced by MasterProduct |
| `src/.../Infrastructure/Configuration/ProductTemplateConfiguration.cs` | Replaced by MasterProductConfiguration |
| `src/.../Infrastructure/Data/ProductTemplateSeeder.cs` | Replaced by MasterProductSeeder |
| `src/.../Infrastructure/Migrations/20260309124321_AddProductOnboarding.*` | Replaced by new migration |

## Migration

`20260309173940_AddMasterProductsAndOnboarding` — creates master_products, master_product_barcodes, master_product_nutrition, master_product_images, tenant_product_onboarding_states tables; adds brand, master_product_id, overridden_fields to products.

## Files Modified (ProductsService merge logic)

| File | Change |
|------|--------|
| `src/.../Core/DTOs/Products/ProductDto.cs` | Added MasterProductId, MasterProductName, Brand, OverriddenFields |
| `src/.../Core/DTOs/Products/CreateProductRequest.cs` | Added MasterProductId, Brand |
| `src/.../Core/DTOs/Products/UpdateProductRequest.cs` | Added Brand |
| `src/.../Core/Mapping/ProductMappingProfile.cs` | Map MasterProduct nav, OverriddenFields deserialize, ignore new Product members |
| `src/.../Infrastructure/Services/ProductsService.cs` | Include MasterProduct in queries, BuildOverriddenFields on update |

## Remaining Work

1. **Mobile UI**: MAUI mobile app pages need onboarding integration (not started)
2. **Unit tests**: Filtering logic, product creation dedup, auto-link, state management, override tracking
3. **Sharing UI** (future): Tenant contributes products back to master catalog
4. **Moderation** (future): Approval queue for shared products
