# SQL Package

Execution order:
1. `01_schema_postgres.sql`
2. `02_seed_postgres.sql`
3. `03_views_and_reports.sql`
4. `04_integrity_checks.sql`

Notes:

- scripts target PostgreSQL 14+;
- the package mirrors the current runtime schema managed by EF Core migrations;
- `...Utc` columns use `timestamp with time zone`;
- operational local-time facts such as `Rentals.StartDate`, `Rentals.EndDate`, `Damages.DateReported`, and `MaintenanceRecords.ServiceDate` stay `timestamp without time zone`;
- document expiration is stored as `date` in `ClientDocuments.ExpirationDate`;
- vehicle availability is computed from `VehicleStatusCode`, `IsDeleted`, and conflicting active or booked rentals, so there is no persisted `Vehicles.IsAvailable` or `Vehicles.IsBookable` column;
- authentication lives in `Accounts`, while `Employees` and `Clients` keep business profiles;
- `ContractSequences` remains a technical helper table for contract-number generation and should be excluded from the defense ER diagram.
