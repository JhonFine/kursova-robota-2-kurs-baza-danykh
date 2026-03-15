# Car Rental DB Coursework Package

## 1. Scope

This package documents the database design and SQL artifacts for the car rental information system.

Artifacts included:

- ER model and relationship map
- normalization proof to 3NF
- PostgreSQL DDL script
- seed DML script
- analytical SQL queries and views
- integrity trigger and stored function examples

## 2. ER Diagram

```mermaid
erDiagram
    EMPLOYEES ||--o{ RENTALS : creates
    EMPLOYEES ||--o{ PAYMENTS : records
    CLIENTS ||--o{ RENTALS : has
    VEHICLES ||--o{ RENTALS : assigned
    RENTALS ||--o{ PAYMENTS : contains
    VEHICLES ||--o{ DAMAGES : has
    RENTALS o|--o{ DAMAGES : may-link
    VEHICLES ||--o{ MAINTENANCE_RECORDS : has

    EMPLOYEES {
        int Id PK
        string Login UK
        string FullName
        string PasswordHash
        int Role
        bool IsActive
        int FailedLoginAttempts
    }

    CLIENTS {
        int Id PK
        string DriverLicense UK
        string FullName
        string PassportData
        string Phone
        bool Blacklisted
    }

    VEHICLES {
        int Id PK
        string LicensePlate UK
        string Make
        string Model
        int Mileage
        decimal DailyRate
        bool IsAvailable
        int ServiceIntervalKm
    }

    RENTALS {
        int Id PK
        string ContractNumber UK
        int ClientId FK
        int VehicleId FK
        int EmployeeId FK
        datetime StartDate
        datetime EndDate
        int StartMileage
        int EndMileage
        decimal TotalAmount
        int Status
        bool IsClosed
    }

    PAYMENTS {
        int Id PK
        int RentalId FK
        int EmployeeId FK
        decimal Amount
        int Method
        int Direction
        datetime CreatedAtUtc
    }

    DAMAGES {
        int Id PK
        int VehicleId FK
        int RentalId FK NULL
        string ActNumber UK
        decimal RepairCost
        decimal ChargedAmount
        bool IsChargedToClient
        int Status
    }

    MAINTENANCE_RECORDS {
        int Id PK
        int VehicleId FK
        datetime ServiceDate
        int MileageAtService
        int NextServiceMileage
        decimal Cost
    }

    CONTRACT_SEQUENCES {
        int Id PK
        int Year UK
        int LastNumber
    }
```

## 3. Normalization

### 3.1 1NF

All persisted attributes are atomic:

- no array/list columns are stored in base tables;
- each row represents one business fact;
- one-to-many relationships are modeled through separate tables and FKs.

### 3.2 2NF

All base tables use a single-column surrogate primary key (`Id`), so partial dependency on composite keys does not occur.

### 3.3 3NF

| Relation | Key | Non-key attributes | Main FDs | 3NF result |
| --- | --- | --- | --- | --- |
| `Employees` | `Id` | login, profile, role, activity, auth timestamps | `Id -> ...`, `Login -> Id` | no transitive dependency between non-key attributes |
| `Clients` | `Id` | identity and contact fields | `Id -> ...`, `DriverLicense -> Id` | no transitive dependency inside relation |
| `Vehicles` | `Id` | plate, make, model, mileage, rate, availability | `Id -> ...`, `LicensePlate -> Id` | no transitive dependency inside relation |
| `Rentals` | `Id` | FK refs, contract/date/mileage/status/amount fields | `Id -> ...`, `ContractNumber -> Id` | no non-key attribute determines another non-key attribute |
| `Payments` | `Id` | rental, employee, amount, method, direction, time, notes | `Id -> ...` | referenced entities are not duplicated |
| `Damages` | `Id` | vehicle, rental, act, repair/charge fields, status | `Id -> ...`, `ActNumber -> Id` | no transitive dependency inside relation |
| `MaintenanceRecords` | `Id` | vehicle, service date, mileage, cost, next mileage, description | `Id -> ...` | no transitive dependency inside relation |
| `ContractSequences` | `Id` | year, last number | `Id -> ...`, `Year -> Id` | no transitive dependency inside relation |

Decomposition notes:

- payments are separated from rentals to avoid repeating payment groups;
- damages are separated from rentals to avoid nullable repeating damage columns;
- maintenance history is separated from vehicles to avoid repeating service groups;
- balance and debt are computed in queries/views instead of stored as mutable duplicates.

Note: `Vehicles.IsAvailable` is a controlled denormalized cache used for faster filtering and kept in sync by service logic and DB triggers.

## 4. Integrity Rules

Implemented through CHECK, FK, UNIQUE constraints and indexes:

- `Rentals.StartDate <= Rentals.EndDate`
- non-negative money and mileage
- enum range validation for roles, statuses, methods, directions
- `Damages.ChargedAmount` consistency with `IsChargedToClient`
- unique keys for login, license plate, contract number and damage act number

See executable SQL scripts in [sql](../sql) and runtime migrations in `CarRental.WebApi`.

## 5. Transaction and Concurrency Design

- rental creation runs in a serializable transaction
- PostgreSQL path uses advisory lock per `VehicleId`
- transient serialization and lock conflicts are retried in service logic

## 6. SQL Package

- `sql/01_schema_postgres.sql` — DDL, constraints, functions, triggers
- `sql/02_seed_postgres.sql` — minimal seed data
- `sql/03_views_and_reports.sql` — views and analytical queries
- `sql/04_integrity_checks.sql` — verification queries

## 7. Defense Checklist

- [x] ER model
- [x] normalization to 3NF
- [x] DDL script
- [x] DML script
- [x] complex SELECT/report queries
- [x] trigger/function examples
- [x] constraints and indexes
- [x] migration-based reproducibility
