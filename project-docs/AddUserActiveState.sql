-- Align existing development databases with ApplicationUser.IsActive.
-- Keep existing account records and use the same default as the EF model.
BEGIN;
SET LOCAL lock_timeout = '5s';
ALTER TABLE public."Users"
    ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT TRUE;
COMMIT;
