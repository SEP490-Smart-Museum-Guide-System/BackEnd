-- Retired: history-only inserts do not create or upgrade schema.
-- Fresh database: use ../project-docs/smgs_current_schema.sql or EF database update.
-- Existing database: follow ../project-docs/DATABASE_SETUP.md.
DO $$ BEGIN
    RAISE EXCEPTION 'Legacy history-only repair disabled. Follow project-docs/DATABASE_SETUP.md.';
END $$;
