-- Repair for issue #60 — household contacts the setup wizard mis-provisioned.
--
-- Two kinds of damage, both from WizardService before the fix:
--
--   1. The household contact's name carried a stored " Household" suffix, re-applied on every
--      wizard run, so a household named "Maple Street Household" ended up as
--      "Maple Street Household Household".
--
--   2. A member saved before the household contact existed was left with no parent. Contact.IsGroup
--      is `ParentContactId == null`, so that member then renders as a household of its own with no
--      members — most often the registering user, whose contact is created at registration, before
--      the wizard runs at all.
--
-- Safe to run more than once, and against one database at a time:
--
--   psql -U homemanagement -d homemanagement        # self-hosted
--   psql -U homemanagement -d homemanagement_cloud  # cloud
--
-- The statements run inside a transaction; read the final SELECT before you COMMIT.

BEGIN;

-- 1. Names. Drop the suffix the wizard used to store, leaving the household contact named after
--    the household itself — which is what the fixed wizard writes.
UPDATE contacts c
SET "CompanyName" = t.name,
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM tenants t
WHERE c."TenantId" = t.id
  AND c."IsTenantHousehold"
  AND c."CompanyName" = t.name || ' Household';

-- 2. Orphans. A person contact (ContactType IS NULL) with no parent is unrepresentable — every
--    member hangs off a group — so parent them to their own household wherever one exists.
UPDATE contacts c
SET "ParentContactId" = h."Id",
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM contacts h
WHERE h."TenantId" = c."TenantId"
  AND h."IsTenantHousehold"
  AND c."ParentContactId" IS NULL
  AND c."ContactType" IS NULL
  AND NOT c."IsTenantHousehold"
  AND c."Id" <> h."Id";

-- 3. What is left over: orphans in a household that has no household contact at all. Creating one
--    here would need a name and a creating user, so leave them to the wizard — its household step
--    creates the household contact and, for anyone holding a sign-in, files them under it in the
--    same pass (#61). Anything listed here that has no account needs a human to say which group it
--    belongs to; everything else heals the next time that household walks the setup wizard.
SELECT t.id AS tenant_id,
       t.name AS tenant_name,
       count(*) AS unlinked_members
FROM contacts c
JOIN tenants t ON t.id = c."TenantId"
WHERE c."ParentContactId" IS NULL
  AND c."ContactType" IS NULL
  AND NOT c."IsTenantHousehold"
GROUP BY t.id, t.name
ORDER BY t.name;

COMMIT;
