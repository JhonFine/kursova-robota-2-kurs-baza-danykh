# SQL Package

Execution order:
1. `01_schema_postgres.sql`
2. `02_seed_postgres.sql`
3. `03_views_and_reports.sql`
4. `04_integrity_checks.sql`

Notes:
- scripts target PostgreSQL 14+;
- they are intended for coursework defense and reproducible demos;
- production app schema is still managed by EF Core migrations in source code.
- seed users in `02_seed_postgres.sql` are compatible with app auth:
  - `admin` / `admin123`
  - `manager` / `manager123`
