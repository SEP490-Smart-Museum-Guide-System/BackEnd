# Reproducible backend schema

Previously there was a snapshot but no executable migration. `InitialCurrentSchema` now creates the current EF model on a **new, empty database**, including Identity, constraints, indexes and role seeds. AI Vision / Scan own separate schemas.

Run from `BackEnd/Smart_Museum_Guide_System` with .NET 8 SDK and an EF 8 tool:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.0
$env:ConnectionStrings__DefaultConnection = '<your connection string>'
dotnet ef database update --project SMGS.Infrastructure --startup-project SMGS.API
```

If a global EF tool already exists, use a local EF 8 installation rather than overwriting another project's tool. The design-time factory reads the environment variable above without starting the API or development seeders. Without the variable its fallback is localhost / smgs_db / postgres without a password; set the variable before commands that connect.

Alternatively execute `smgs_current_schema.sql` with psql on an empty database. It records the migration actually applied. Re-running an applied baseline is a no-op. Start the API afterward; use `--seed-data` only if demo data is wanted. Normal development startup can seed development accounts according to configuration.

## Existing databases

Do not apply this initial migration to a database that already contains application tables, even if migration history is missing or lists the removed older migrations. Back up that database and restore into a disposable database. Compare schema, constraints and data with the current model/SQL, then prepare an incremental upgrade for that actual schema. Matching table names alone is insufficient. Do not insert a baseline history row without independently verifying a complete schema reconciliation. The audit has not altered or certified the existing application database.

`fix_migrations.sql` now deliberately stops instead of inventing migration-history rows.

## Regenerate and verify

```powershell
dotnet run --project tools/SMGS.SchemaExport -- ../project-docs
$env:SMGS_TEST_CONNECTION = '<connection to a disposable development/test database>'
dotnet run --project tools/SMGS.SchemaExport -- ../project-docs --verify
```

Verification creates a unique schema, applies migrations twice, compares every model table/column and drops only that schema. The account needs CREATE SCHEMA permission. Generated `smgs_backend.dbml` is the physical backend ERD. `smgs_schema.dbml` is a conceptual target, not proof of implemented tables. SQL/migrations are authoritative for defaults, checks and filtered indexes.
