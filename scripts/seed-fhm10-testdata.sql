-- ============================================================================
-- FHM-10 test data
--
-- The dev database has 501 master products but zero barcodes anywhere, no
-- child products and no store metadata — so most of FHM-10 cannot be exercised
-- against it: every scan reports "not found" regardless of the code, and the
-- parent aisle roll-up has nothing to roll up.
--
-- This adds the missing pieces. Idempotent: re-running replaces the seeded
-- rows rather than duplicating them. Everything it creates is tagged with the
-- note 'FHM10-SEED' or a name prefix of 'Seed ', so it is easy to find and
-- remove (see the teardown block at the bottom).
--
--   docker exec -i homemanagement-db-dev psql -U homemanagement -d homemanagement \
--     < scripts/seed-fhm10-testdata.sql
-- ============================================================================

BEGIN;

-- Reused from the existing dev tenant/household.
\set tenant       '00000000-0000-0000-0000-000000000001'
\set location     'cfe8328a-2a15-4cb4-9ddd-222bddc87268'
\set unit         '15dcf07f-aaa8-4512-b065-a5702fbb9de6'
\set store        '276fab2d-0df7-4bf3-a9a5-5866fdd7c581'
\set mtn_dew      'e8a96491-a81f-47a6-9724-bbf92e78660c'
\set whole_milk   '7faf6fcf-2c50-4b3e-bd7c-e402b51675ac'

-- ---------------------------------------------------------------------------
-- Clean up any previous run first, children before parents.
-- ---------------------------------------------------------------------------
DELETE FROM product_store_metadata
 WHERE product_id IN (SELECT "Id" FROM products WHERE "Name" LIKE 'Seed %');
DELETE FROM product_barcodes WHERE "Note" = 'FHM10-SEED';
DELETE FROM products WHERE "ParentProductId" IN (SELECT "Id" FROM products WHERE "Name" LIKE 'Seed %');
DELETE FROM products WHERE "Name" LIKE 'Seed %';
DELETE FROM master_product_barcodes WHERE barcode LIKE 'SEED%' OR barcode IN ('0001111041700','0002200000021','4011');

-- ---------------------------------------------------------------------------
-- 1. Barcodes on the products that already exist.
--    Without these every scan returns "not found" no matter what the code does.
-- ---------------------------------------------------------------------------
INSERT INTO product_barcodes ("Id","TenantId","ProductId","Barcode","Note") VALUES
  (gen_random_uuid(), :'tenant', :'mtn_dew',    '012000001291', 'FHM10-SEED'),
  (gen_random_uuid(), :'tenant', :'whole_milk', '011110416001', 'FHM10-SEED');

-- ---------------------------------------------------------------------------
-- 2. By-weight products with Type-2 (price/weight-embedded) barcodes.
--
--    The stored barcode row holds the 5-DIGIT ITEM NUMBER, not the full scanned
--    barcode — the scanner sends a full 12-digit Type-2 code and the lookup
--    extracts the item number from it.
--
--    'Seed Deli Turkey' deliberately has NO Type2Prefix. That is the exact case
--    FHM-10 bullet 2 is about: before the fix the lookup required
--    Type2Prefix IS NOT NULL, so this row could never match and the scan
--    reported "not found".
--
--    'Seed Rotisserie Chicken' DOES have a Type2Prefix, so the two together
--    also exercise the ordering that prefers a genuine Type-2 row on collision.
-- ---------------------------------------------------------------------------
INSERT INTO products ("Id","TenantId","Name","LocationId","QuantityUnitIdPurchase","QuantityUnitIdStock","SaleType","TracksBestBeforeDate","DefaultBestBeforeDays")
VALUES
  ('a1000000-0000-4000-8000-000000000001', :'tenant', 'Seed Deli Turkey Breast',   :'location', :'unit', :'unit', 1, true, 7),
  ('a1000000-0000-4000-8000-000000000002', :'tenant', 'Seed Rotisserie Chicken',   :'location', :'unit', :'unit', 1, true, 3),
  ('a1000000-0000-4000-8000-000000000003', :'tenant', 'Seed Bananas',              :'location', :'unit', :'unit', 1, true, 5);

INSERT INTO product_barcodes ("Id","TenantId","ProductId","Barcode","Type2Prefix","Note") VALUES
  -- scan 281234002507 -> item number 81234, weight 2.50 lb. No Type2Prefix on purpose.
  (gen_random_uuid(), :'tenant', 'a1000000-0000-4000-8000-000000000001', '81234', NULL, 'FHM10-SEED'),
  -- scan 297788001750 -> item number 97788, weight 1.75 lb. Genuine Type-2 row.
  (gen_random_uuid(), :'tenant', 'a1000000-0000-4000-8000-000000000002', '97788', '29',  'FHM10-SEED'),
  -- bare produce PLU: 4 digits, treated as by-weight
  (gen_random_uuid(), :'tenant', 'a1000000-0000-4000-8000-000000000003', '4011',  NULL, 'FHM10-SEED');

-- ---------------------------------------------------------------------------
-- 3. A parent with child variants, each with its own barcode.
--    Covers: scanning a child's barcode records directly instead of opening the
--    picker, and re-scanning prompts to add another purchase.
-- ---------------------------------------------------------------------------
INSERT INTO products ("Id","TenantId","Name","LocationId","QuantityUnitIdPurchase","QuantityUnitIdStock")
VALUES ('b1000000-0000-4000-8000-000000000000', :'tenant', 'Seed Soda (generic)', :'location', :'unit', :'unit');

INSERT INTO products ("Id","TenantId","Name","LocationId","QuantityUnitIdPurchase","QuantityUnitIdStock","ParentProductId")
VALUES
  ('b1000000-0000-4000-8000-000000000001', :'tenant', 'Seed Soda - Cola 12pk',      :'location', :'unit', :'unit', 'b1000000-0000-4000-8000-000000000000'),
  ('b1000000-0000-4000-8000-000000000002', :'tenant', 'Seed Soda - Diet Cola 12pk', :'location', :'unit', :'unit', 'b1000000-0000-4000-8000-000000000000'),
  ('b1000000-0000-4000-8000-000000000003', :'tenant', 'Seed Soda - Lemon Lime 12pk',:'location', :'unit', :'unit', 'b1000000-0000-4000-8000-000000000000');

INSERT INTO product_barcodes ("Id","TenantId","ProductId","Barcode","Note") VALUES
  (gen_random_uuid(), :'tenant', 'b1000000-0000-4000-8000-000000000000', '099999900000', 'FHM10-SEED'), -- the parent's own barcode
  (gen_random_uuid(), :'tenant', 'b1000000-0000-4000-8000-000000000001', '099999900011', 'FHM10-SEED'),
  (gen_random_uuid(), :'tenant', 'b1000000-0000-4000-8000-000000000002', '099999900028', 'FHM10-SEED'),
  (gen_random_uuid(), :'tenant', 'b1000000-0000-4000-8000-000000000003', '099999900035', 'FHM10-SEED');

-- ---------------------------------------------------------------------------
-- 4. Store metadata on the CHILDREN only, never the parent.
--    That is the shape FHM-10 bullet 4 describes: the parent has no aisle of
--    its own and must inherit one from a child. Two children share aisle 12 and
--    one sits in aisle 3, so the "most common aisle wins" tie-break is
--    exercised rather than just "first row found".
-- ---------------------------------------------------------------------------
INSERT INTO product_store_metadata (id, tenant_id, product_id, shopping_location_id, aisle, shelf, department, external_product_id) VALUES
  (gen_random_uuid(), :'tenant', 'b1000000-0000-4000-8000-000000000001', :'store', '12', 'B', 'Beverages', 'EXT-COLA-001'),
  (gen_random_uuid(), :'tenant', 'b1000000-0000-4000-8000-000000000002', :'store', '12', 'C', 'Beverages', 'EXT-COLA-002'),
  (gen_random_uuid(), :'tenant', 'b1000000-0000-4000-8000-000000000003', :'store',  '3', 'A', 'Beverages', 'EXT-LEMON-003');

-- ---------------------------------------------------------------------------
-- 5. Enough tenant products matching "milk" to contend for the 10-result
--    window, so master-catalogue results would be starved without the quota
--    fix. With the fix, master rows still get a share.
-- ---------------------------------------------------------------------------
INSERT INTO products ("Id","TenantId","Name","LocationId","QuantityUnitIdPurchase","QuantityUnitIdStock")
SELECT gen_random_uuid(), :'tenant', 'Seed Milk Variant ' || g, :'location', :'unit', :'unit'
FROM generate_series(1, 12) g;

-- ---------------------------------------------------------------------------
-- 6. Master-catalogue barcodes, so master lookup by barcode can match at all.
--    (master_product_barcodes was completely empty.)
-- ---------------------------------------------------------------------------
INSERT INTO master_product_barcodes (id, master_product_id, barcode)
SELECT gen_random_uuid(), id, '0001111041700'
FROM master_products WHERE name = 'Whole Milk' LIMIT 1;

INSERT INTO master_product_barcodes (id, master_product_id, barcode)
SELECT gen_random_uuid(), id, '4011'
FROM master_products WHERE name ILIKE 'banana%' LIMIT 1;

-- ---------------------------------------------------------------------------
-- 7. Dedup case: a tenant product whose name exactly matches a master product.
--    Searching that term should return the household's product and suppress the
--    master duplicate — and it must work even when the tenant row ranks below
--    the result cut, which is why the DB-backed dedup replaced the page-based one.
-- ---------------------------------------------------------------------------
INSERT INTO products ("Id","TenantId","Name","LocationId","QuantityUnitIdPurchase","QuantityUnitIdStock")
SELECT gen_random_uuid(), :'tenant', 'Seed ' || name, :'location', :'unit', :'unit'
FROM master_products WHERE name = 'All-Purpose Flour' LIMIT 1;

COMMIT;

-- ---------------------------------------------------------------------------
-- Summary
-- ---------------------------------------------------------------------------
SELECT 'products'               AS table, count(*) FROM products
UNION ALL SELECT 'product_barcodes',        count(*) FROM product_barcodes
UNION ALL SELECT 'product_store_metadata',  count(*) FROM product_store_metadata
UNION ALL SELECT 'master_product_barcodes', count(*) FROM master_product_barcodes
UNION ALL SELECT 'by-weight products',      count(*) FROM products WHERE "SaleType" = 1
UNION ALL SELECT 'child products',          count(*) FROM products WHERE "ParentProductId" IS NOT NULL;

-- ============================================================================
-- Teardown — removes everything this script created:
--
--   DELETE FROM product_store_metadata WHERE product_id IN (SELECT "Id" FROM products WHERE "Name" LIKE 'Seed %');
--   DELETE FROM product_barcodes WHERE "Note" = 'FHM10-SEED';
--   DELETE FROM products WHERE "ParentProductId" IN (SELECT "Id" FROM products WHERE "Name" LIKE 'Seed %');
--   DELETE FROM products WHERE "Name" LIKE 'Seed %';
--   DELETE FROM master_product_barcodes WHERE barcode IN ('0001111041700','4011');
-- ============================================================================
