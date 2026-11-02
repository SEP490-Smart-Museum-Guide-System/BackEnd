INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20261019043314_kj', '9.0.0') ON CONFLICT DO NOTHING;
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20261020103942_AddArtifactContentReview', '9.0.0') ON CONFLICT DO NOTHING;
SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
