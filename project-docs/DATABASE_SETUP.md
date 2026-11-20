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

## Museum arrival and room positioning — 20/11/2026

`AddMuseumPresence` adds only `museum_presence`, its indexes and foreign keys. Its Up operation does not delete existing tables or rows. The physical model now has 53 tables. The separate `museum_presence_upgrade.sql` script includes only this incremental migration, not the full initial schema. It requires the existing backend parent tables and `__EFMigrationsHistory`. On an existing database, inspect/backup it and run this incremental script after verifying those prerequisites; do not replay InitialCurrentSchema over existing tables. This change was tested on isolated schemas, not applied automatically to the user's application database.

Staff workflow: open the indoor-map editor, link an **entrance** marker or **gallery** marker to a route node, publish the map, then choose **Tải QR vị trí**. Print/place it at the corresponding entrance/room. The QR encodes map ID, published revision and marker ID. Reprint QR codes after publishing a new map revision. Artifact QR generation stays separate.

Visitor workflow: the museum detail/directions screens show GPS proximity and a QR confirmation action. GPS requires a fresh sample (<=30 seconds), reported accuracy <=50 m, and the distance plus error circle inside the 150 m proximity radius to show `nearby`. Last-known/mock/poor-accuracy samples cannot confirm arrival; GPS never writes confirmed presence. Checks run only in the foreground, with explicit permission and a manual retry, and refresh after returning from Maps.

Scanning a valid entrance/room QR records a private server-timestamped observation, restores the room/floor/map after reopening, and supports routing from its node. The general artifact scanner also accepts location QR. Artifact QR recognition remains usable as a positioning anchor. QR room scans do not award artifact progress, points or quiz eligibility. Only the most recent applicable QR observation is used; after five minutes routes require another scan. Changing floors selects the corresponding published map; cross-floor paths are not fabricated. Manual start selection is labelled as manual and does not record confirmation.

This is checkpoint-based positioning, not continuous GPS movement inside rooms. Coordinates/map nodes/room links and physical QR placement must be configured by the museum. Real-device GPS/camera accuracy inside the actual museum has not been measured. Static QR scanning is an observation, not anti-spoofing attendance evidence or a payment entitlement.

Run backend regression from Smart_Museum_Guide_System with SMGS_TEST_CONNECTION set to a disposable test database:

```powershell
dotnet run --project tests/SMGS.PresenceChecks
```

Frontend regression: `flutter test --no-pub test/museum_presence_test.dart` from FrontEnd/smgs_mobile. Tests include GPS metadata/denial, nearby-versus-confirmed UI, saved room/floor, stale observations and authenticated route requests. Existing web and Flutter suites should also pass. No new positioning hardware, background-location permission or model service is required.
