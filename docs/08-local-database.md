# Local database setup

Verified on 2026-09-27 with SQL Server default instance `localhost`, Windows Authentication, and a new database named `DillustrationLocal`. Existing databases were not modified.

## Configure and run

From the backend repository root:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=DillustrationLocal;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true" --project src/ArtCommission.API
dotnet ef database update --project src/ArtCommission.Infrastructure --startup-project src/ArtCommission.API
dotnet run --project src/ArtCommission.API --launch-profile http
```

User Secrets apply in Development. Do not commit credentials. `TrustServerCertificate=True` is for local development, not a production TLS policy.

## Schema source for this checkout

Use the checked-in EF migration chain for a fresh database. Do not combine it with `docs/database.sql`, `docs/Database_Schema_MSSQL.sql`, or `scripts/Dillustration_Database_v01.sql`: those are divergent designs, including different Identity table names.

`SyncLocalRuntimeSchema` adds the five missing creator workstation tables and synchronizes the model snapshot. Marketplace tables/columns already created by `AddMarketplaceDiscoveryInteractions` are not recreated. This has been tested on a fresh database only; back up and review schema drift before updating an existing database.

The generated `scripts/Dillustration_Runtime_Migrations.sql` is an alternative to `database update`. Select the intended database explicitly in SSMS before running it; the script does not create/select a database and uses `__EFMigrationsHistory` for idempotency. It does not reconcile arbitrary schemas or insert the application's demo data.

Regenerate after migration changes:

```powershell
dotnet ef migrations script --idempotent --project src/ArtCommission.Infrastructure --startup-project src/ArtCommission.API --output scripts/Dillustration_Runtime_Migrations.sql
```

## Verification

- 15 migrations applied; 55 tables including migration history.
- Backend startup logged `Database initialized successfully` and seeded development demo data.
- Registration and login succeeded for `dbcheck.1790515499@dillustration.test` (smoke-test account; generated password not retained).
- Marketplace feed returned four artworks; frontend returned HTTP 200.
- `dotnet ef migrations has-pending-model-changes` reported no pending model changes.

External email, payment, storage and AI integrations are not validated by these database checks. Existing EF precision/value-comparer warnings remain. Features absent from backend code (such as Events) are not implemented by setting up the database.
