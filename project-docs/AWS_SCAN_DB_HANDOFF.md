# Scan PostgreSQL connection — 2026-11-20

AWS-side setup is complete. The user requested EC2 be stopped again after checks. It must be running before Azure can connect.

## Connection settings

```dotenv
AI_SYSTEM_DB_HOST=smgs-52-62-165-39.sslip.io
AI_SYSTEM_DB_PORT=5432
AI_SYSTEM_DB_NAME=smgs_demo
AI_SYSTEM_DB_USER=smgs_scan
SCAN_DB_SCHEMA=ai_3d_scan
PGSSLMODE=verify-full
PGSSLROOTCERT=/etc/ssl/certs/ca-certificates.crt
```

The unique generated password was saved privately in the git-ignored local file:
`D:\Semester9\SEP490\Project\Smart-Museum-Guide-System\AI-System\deploy\azure\.env.azure`.
It is restricted to the Windows user and SYSTEM. No password is included in this handoff. The same password is held in AWS SSM SecureString `/smgs/scan/password` for recovery. The temporary plaintext file on EC2 and its setup-only IAM permission were removed.

The local `.env.azure` is a database-only fragment. Merge its database/TLS settings into the existing private env on Azure; do not replace Azure's TTS/Scan service keys. No files were transferred to Azure and no Azure service was restarted in this task.

## Network and TLS

- AWS instance: `i-01915e4c9035c7542`, Region `ap-southeast-2`, CLI profile `smart-museum-guide-system`.
- Elastic public IP: `52.62.165.39`; use the hostname above for certificate validation.
- PostgreSQL ingress is restricted to Azure outbound `172.198.161.72/32` by security group rule `sgr-01f2e3b40f062daa4`.
- PostgreSQL listens on loopback and the EC2 private IP. `pg_hba.conf` requires SCRAM and TLS for the exact Scan user/database/source; other users from this Azure IP are rejected. Scan plaintext connections are rejected.
- PostgreSQL uses the publicly trusted Let's Encrypt certificate for the existing hostname. The Ubuntu Scan image already includes the system CA bundle; no custom CA or disabled verification is needed.
- `smgs-postgres-tls-sync.timer` checks every six hours and five minutes after boot, copies renewed certificates from Caddy into the PostgreSQL-only TLS directory, and reloads PostgreSQL. Certificate/private-key copies are root:postgres mode 640, directory 750. No private key is sent to Azure.

## Database permissions and verification

Applied the actual AI-System `deploy/azure/scan_database.sql` to the existing `smgs_demo`. `smgs_scan` owns only the dedicated `ai_3d_scan` schema and `scan_jobs` table because current Scan startup performs table/index DDL. It has no elevated role flags, role memberships, or CREATE on the database. Existing BE tables and data were not modified.

Verified on EC2 with the Scan password and `sslmode=verify-full`: hostname and CA validation, negotiated TLS 1.3, insert/update/select/delete in a rolled-back transaction, rejection of BE table access, rejection of other-schema creation, and rejection of plaintext connections. HBA parse errors: zero. TLS renewal timer enabled and active. Core public BE read returned 200; anonymous admin request returned 401. Independent setup review found no important issues.

Actual Azure-to-AWS connectivity and Scan startup have **not** been verified from the VM. Do not interpret the loopback checks as an end-to-end Azure test.

## Azure handoff

`AI-System/deploy/azure/compose.yaml` was updated locally to pass:

```yaml
PGSSLROOTCERT: ${PGSSLROOTCERT:-/etc/ssl/certs/ca-certificates.crt}
```

Keep `PGSSLMODE=verify-full` in the private Azure env; the existing Compose default `require` alone does not authenticate the server certificate. Deploy the one-line Compose update and merge the database env variables before recreating Scan. From the Azure deployment directory, with its complete existing private `.env.azure`:

```bash
docker compose --env-file .env.azure --profile scan up -d --no-deps --force-recreate scan
```

Start AWS first:

```powershell
aws ec2 start-instances --instance-ids i-01915e4c9035c7542 --region ap-southeast-2 --profile smart-museum-guide-system
```

If the Azure outbound IP changes, update both the security group and HBA allowlist. Do not add `0.0.0.0/0`. PostgreSQL certificate renewal depends on Caddy renewing its certificate; after a long stop with an expired certificate, let Caddy renew and run `sudo systemctl start smgs-postgres-tls-sync.service` before retrying Scan. EC2/EBS/IP costs remain as documented; stopping EC2 retains the database but disk and public IP charges continue.
