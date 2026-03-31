# Web Production Architecture

## 1. Overview

Implemented runtime architecture:

- `CarRental.Desktop` for internal desktop workflows and desktop self-service mode
- `CarRental.WebApi` as ASP.NET Core 8 backend
- `CarRental.WebApp` as React frontend
- shared PostgreSQL database for desktop and web

Desktop and Web API write to the same PostgreSQL schema. Web API applies EF Core migrations on startup; desktop expects the schema to already exist.

## 2. PostgreSQL Model and Integrity

`RentalDbContext` in `CarRental.WebApi` defines the runtime model for:

- `Accounts`
- `Employees`
- `EmployeeRoles`
- `Clients`
- `ClientDocuments`
- `ClientDocumentTypes`
- `Vehicles`
- `VehiclePhotos`
- `VehicleStatuses`
- `Rentals`
- `RentalStatuses`
- `RentalInspections`
- `InspectionTypes`
- `Payments`
- `PaymentMethods`
- `PaymentDirections`
- `PaymentStatuses`
- `Damages`
- `DamagePhotos`
- `DamageStatuses`
- `MaintenanceRecords`
- `MaintenanceTypes`
- `ContractSequences`

Implemented integrity mechanisms:

- unique keys for `Accounts.Login`, active `ClientDocuments`, active `Clients.Phone`, `Vehicles.LicensePlate`, `Rentals.ContractNumber`, `Damages.ActNumber`, `ContractSequences.Year`
- FK constraints with explicit delete behavior
- money precision via `decimal(10,2)`
- integer enums replaced by lookup tables or code-based reference tables
- computed vehicle availability based on `VehicleStatusCode`, soft delete, and conflicting rentals
- query indexes for rentals, payments, damages, maintenance, vehicles, clients, employees

## 3. Security

`CarRental.WebApi` includes:

- JWT Bearer authentication
- policy-based authorization for rentals, payments, clients, fleet, pricing, employees, maintenance, damages, reports, deletes
- PBKDF2 password hashing
- login lockout logic in auth services

## 4. API Coverage

Implemented API groups:

- `api/auth`
- `api/profile`
- `api/clients`
- `api/vehicles`
- `api/rentals`
- `api/payments`
- `api/damages`
- `api/maintenance`
- `api/admin`
- `api/reports`
- `api/system/health`

## 5. Operations

Desktop PostgreSQL connection is provided through:

- `CAR_RENTAL_POSTGRES_CONNECTION`

Database backups are handled externally through PostgreSQL tooling such as `pg_dump`; there is no built-in backup UI.

Generated EF migrations live in:

- `CarRental.WebApi/Data/Migrations`

For local reset/apply flow use:

```powershell
.\FactoryReset.ps1
```

## 6. Deployment Baseline

Recommended production setup:

- run `CarRental.WebApi` behind a reverse proxy with TLS termination
- store DB credentials and JWT signing key in environment variables or a secret vault
- restrict CORS to actual frontend origins
- configure PostgreSQL backup and recovery procedures (`pg_dump`, snapshots, PITR/WAL)
- enable centralized logs and alerting
