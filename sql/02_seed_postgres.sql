-- Minimal seed data for the canonical breaking-cleanup schema

INSERT INTO "EmployeeRoles" ("Id", "DisplayName")
VALUES
    (1, 'Admin'),
    (2, 'Manager'),
    (3, 'User')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "RentalStatuses" ("Id", "DisplayName")
VALUES
    (1, 'Booked'),
    (2, 'Active'),
    (3, 'Closed'),
    (4, 'Canceled')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "PaymentMethods" ("Id", "DisplayName")
VALUES
    (1, 'Cash'),
    (2, 'Card')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "PaymentDirections" ("Id", "DisplayName")
VALUES
    (1, 'Incoming'),
    (2, 'Refund')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "PaymentStatuses" ("Id", "DisplayName")
VALUES
    (1, 'Pending'),
    (2, 'Completed'),
    (3, 'Canceled'),
    (4, 'Refunded')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "DamageStatuses" ("Id", "DisplayName")
VALUES
    (1, 'Open'),
    (2, 'Charged'),
    (3, 'Resolved')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "InspectionTypes" ("Id", "DisplayName")
VALUES
    (1, 'Pickup'),
    (2, 'Return')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "VehicleStatuses" ("Code", "DisplayName")
VALUES
    ('READY', 'Ready'),
    ('RENTED', 'Rented'),
    ('MAINTENANCE', 'Maintenance'),
    ('DAMAGED', 'Damaged'),
    ('INACTIVE', 'Inactive')
ON CONFLICT ("Code") DO NOTHING;

INSERT INTO "ClientDocumentTypes" ("Code", "DisplayName")
VALUES
    ('PASSPORT', 'Passport'),
    ('DRIVER_LICENSE', 'Driver license')
ON CONFLICT ("Code") DO NOTHING;

INSERT INTO "MaintenanceTypes" ("Code", "DisplayName")
VALUES
    ('SCHEDULED', 'Scheduled service'),
    ('REPAIR', 'Repair'),
    ('TIRES', 'Tires'),
    ('INSPECTION', 'Inspection')
ON CONFLICT ("Code") DO NOTHING;

INSERT INTO "FuelTypes" ("Code", "DisplayName", "CreatedAtUtc", "UpdatedAtUtc")
VALUES
    ('PETROL', 'Petrol', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
    ('DIESEL', 'Diesel', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
    ('EV', 'Electric', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
ON CONFLICT ("Code") DO NOTHING;

INSERT INTO "TransmissionTypes" ("Code", "DisplayName", "CreatedAtUtc", "UpdatedAtUtc")
VALUES
    ('AUTO', 'Automatic', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
    ('MANUAL', 'Manual', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
ON CONFLICT ("Code") DO NOTHING;

INSERT INTO "Accounts" ("Login", "PasswordHash", "IsActive", "FailedLoginAttempts", "PasswordChangedAtUtc", "CreatedAtUtc", "UpdatedAtUtc")
VALUES
    ('admin', 'PBKDF2$120000$demo$demo', TRUE, 0, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
    ('manager', 'PBKDF2$120000$demo$demo', TRUE, 0, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
    ('client_demo', 'PBKDF2$120000$demo$demo', TRUE, 0, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
ON CONFLICT ("Login") DO NOTHING;

INSERT INTO "Employees" ("AccountId", "FullName", "RoleId", "CreatedAtUtc", "UpdatedAtUtc")
SELECT a."Id", v.full_name, v.role_id, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
FROM (
    VALUES
        ('admin', 'System administrator', 1),
        ('manager', 'Rental manager', 2)
) AS v(login, full_name, role_id)
JOIN "Accounts" a ON a."Login" = v.login
ON CONFLICT ("AccountId") DO NOTHING;

INSERT INTO "Clients" ("AccountId", "FullName", "Phone", "IsBlacklisted", "BlacklistReason", "BlacklistedAtUtc", "BlacklistedByEmployeeId", "IsDeleted", "CreatedAtUtc", "UpdatedAtUtc")
SELECT a."Id", 'Portal client', '+380501000001', FALSE, NULL, NULL, NULL, FALSE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
FROM "Accounts" a
WHERE a."Login" = 'client_demo'
ON CONFLICT ("Phone") WHERE "IsDeleted" = FALSE DO NOTHING;

INSERT INTO "Clients" ("AccountId", "FullName", "Phone", "IsBlacklisted", "BlacklistReason", "BlacklistedAtUtc", "BlacklistedByEmployeeId", "IsDeleted", "CreatedAtUtc", "UpdatedAtUtc")
SELECT
    NULL,
    v.full_name,
    v.phone,
    v.is_blacklisted,
    v.blacklist_reason,
    CASE WHEN v.is_blacklisted THEN CURRENT_TIMESTAMP ELSE NULL END,
    CASE WHEN v.is_blacklisted THEN blacklist_actor."Id" ELSE NULL END,
    FALSE,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM (
    VALUES
        ('Walk-in client', '+380501000002', FALSE, NULL),
        ('Flagged client', '+380501000003', TRUE, 'Repeated late returns')
) AS v(full_name, phone, is_blacklisted, blacklist_reason)
LEFT JOIN LATERAL (
    SELECT e."Id"
    FROM "Employees" e
    ORDER BY CASE WHEN e."RoleId" = 1 THEN 0 ELSE 1 END, e."Id"
    LIMIT 1
) AS blacklist_actor ON TRUE
ON CONFLICT ("Phone") WHERE "IsDeleted" = FALSE DO NOTHING;

INSERT INTO "ClientDocuments" ("ClientId", "DocumentTypeCode", "DocumentNumber", "ExpirationDate", "StoredPath", "IsDeleted", "CreatedAtUtc", "UpdatedAtUtc")
SELECT c."Id", 'PASSPORT', v.document_number, v.expiration_date, NULL, FALSE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
FROM (
    VALUES
        ('Portal client', 'PA-000001', CURRENT_DATE + INTERVAL '8 years'),
        ('Walk-in client', 'PA-000002', CURRENT_DATE + INTERVAL '7 years'),
        ('Flagged client', 'PA-000003', CURRENT_DATE + INTERVAL '6 years')
) AS v(full_name, document_number, expiration_date)
JOIN "Clients" c ON c."FullName" = v.full_name
ON CONFLICT ("DocumentTypeCode", "DocumentNumber") WHERE "IsDeleted" = FALSE DO NOTHING;

INSERT INTO "ClientDocuments" ("ClientId", "DocumentTypeCode", "DocumentNumber", "ExpirationDate", "StoredPath", "IsDeleted", "CreatedAtUtc", "UpdatedAtUtc")
SELECT c."Id", 'DRIVER_LICENSE', v.document_number, v.expiration_date, NULL, FALSE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
FROM (
    VALUES
        ('Portal client', 'DL-000001', CURRENT_DATE + INTERVAL '3 years'),
        ('Walk-in client', 'DL-000002', CURRENT_DATE + INTERVAL '2 years'),
        ('Flagged client', 'DL-000003', CURRENT_DATE + INTERVAL '1 year')
) AS v(full_name, document_number, expiration_date)
JOIN "Clients" c ON c."FullName" = v.full_name
ON CONFLICT ("DocumentTypeCode", "DocumentNumber") WHERE "IsDeleted" = FALSE DO NOTHING;

INSERT INTO "VehicleMakes" ("Name", "NormalizedName", "CreatedAtUtc", "UpdatedAtUtc")
VALUES
    ('Toyota', 'TOYOTA', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
    ('Tesla', 'TESLA', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
    ('Renault', 'RENAULT', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
ON CONFLICT ("NormalizedName") DO NOTHING;

INSERT INTO "VehicleModels" ("MakeId", "Name", "NormalizedName", "CreatedAtUtc", "UpdatedAtUtc")
SELECT make_lookup."Id", model_seed."Name", model_seed."NormalizedName", CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
FROM (
    VALUES
        ('TOYOTA', 'Camry', 'CAMRY'),
        ('TESLA', 'Model 3', 'MODEL 3'),
        ('RENAULT', 'Trafic', 'TRAFIC')
) AS model_seed(make_normalized_name, "Name", "NormalizedName")
JOIN "VehicleMakes" make_lookup ON make_lookup."NormalizedName" = model_seed.make_normalized_name
ON CONFLICT ("MakeId", "NormalizedName") DO NOTHING;

INSERT INTO "Vehicles" (
    "MakeId",
    "ModelId",
    "PowertrainCapacityValue",
    "PowertrainCapacityUnit",
    "FuelTypeCode",
    "TransmissionTypeCode",
    "VehicleStatusCode",
    "DoorsCount",
    "CargoCapacityValue",
    "CargoCapacityUnit",
    "ConsumptionValue",
    "ConsumptionUnit",
    "HasAirConditioning",
    "LicensePlate",
    "Mileage",
    "DailyRate",
    "IsDeleted",
    "ServiceIntervalKm",
    "CreatedAtUtc",
    "UpdatedAtUtc")
SELECT
    make_lookup."Id",
    model_lookup."Id",
    vehicle_seed.powertrain_capacity_value,
    vehicle_seed.powertrain_capacity_unit,
    vehicle_seed.fuel_type_code,
    vehicle_seed.transmission_type_code,
    vehicle_seed.vehicle_status_code,
    vehicle_seed.doors_count,
    vehicle_seed.cargo_capacity_value,
    vehicle_seed.cargo_capacity_unit,
    vehicle_seed.consumption_value,
    vehicle_seed.consumption_unit,
    vehicle_seed.has_air_conditioning,
    vehicle_seed.license_plate,
    vehicle_seed.mileage,
    vehicle_seed.daily_rate,
    FALSE,
    vehicle_seed.service_interval_km,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM (
    VALUES
        ('TOYOTA', 'CAMRY', 2.50::numeric, 'L', 'PETROL', 'AUTO', 'READY', 4, 524.00::numeric, 'L', 7.30::numeric, 'L_PER_100KM', TRUE, 'AA1173BH', 42950, 1750.00::numeric, 10000),
        ('TESLA', 'MODEL 3', 75.00::numeric, 'KWH', 'EV', 'AUTO', 'READY', 4, 425.00::numeric, 'L', 16.00::numeric, 'KWH_PER_100KM', TRUE, 'BC1519AE', 27800, 2100.00::numeric, 15000),
        ('RENAULT', 'TRAFIC', 1.60::numeric, 'L', 'DIESEL', 'MANUAL', 'MAINTENANCE', 4, 9.00::numeric, 'SEATS', 7.70::numeric, 'L_PER_100KM', TRUE, 'BH1865AX', 36400, 1300.00::numeric, 12000)
) AS vehicle_seed(
    make_normalized_name,
    model_normalized_name,
    powertrain_capacity_value,
    powertrain_capacity_unit,
    fuel_type_code,
    transmission_type_code,
    vehicle_status_code,
    doors_count,
    cargo_capacity_value,
    cargo_capacity_unit,
    consumption_value,
    consumption_unit,
    has_air_conditioning,
    license_plate,
    mileage,
    daily_rate,
    service_interval_km)
JOIN "VehicleMakes" make_lookup ON make_lookup."NormalizedName" = vehicle_seed.make_normalized_name
JOIN "VehicleModels" model_lookup
    ON model_lookup."MakeId" = make_lookup."Id"
   AND model_lookup."NormalizedName" = vehicle_seed.model_normalized_name
ON CONFLICT ("LicensePlate") WHERE "IsDeleted" = FALSE DO NOTHING;

INSERT INTO "VehiclePhotos" ("VehicleId", "StoredPath", "SortOrder", "IsPrimary", "IsDeleted", "CreatedAtUtc", "UpdatedAtUtc")
SELECT v."Id", concat('/protected/vehicles/', lower(replace(v."LicensePlate", ' ', '')), '.jpg'), 0, TRUE, FALSE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
FROM "Vehicles" v
WHERE v."LicensePlate" IN ('AA1173BH', 'BC1519AE')
ON CONFLICT ("VehicleId", "SortOrder") WHERE "IsDeleted" = FALSE DO NOTHING;

INSERT INTO "Rentals" (
    "ClientId",
    "VehicleId",
    "CreatedByEmployeeId",
    "ContractNumber",
    "StartDate",
    "EndDate",
    "PickupLocation",
    "ReturnLocation",
    "StartMileage",
    "EndMileage",
    "OverageFee",
    "TotalAmount",
    "StatusId",
    "CreatedAtUtc",
    "ClosedAtUtc",
    "CanceledAtUtc",
    "CancellationReason")
SELECT
    c."Id",
    v."Id",
    e."Id",
    concat('CR-', EXTRACT(YEAR FROM CURRENT_DATE)::int, '-000001'),
    CURRENT_DATE + INTERVAL '1 day' + TIME '10:00',
    CURRENT_DATE + INTERVAL '3 day' + TIME '10:00',
    'Kyiv office',
    'Kyiv office',
    v."Mileage",
    NULL,
    0,
    3500.00,
    1,
    CURRENT_TIMESTAMP,
    NULL,
    NULL,
    NULL
FROM "Clients" c
JOIN "Vehicles" v ON v."LicensePlate" = 'AA1173BH'
JOIN "Employees" e ON e."FullName" = 'Rental manager'
WHERE c."FullName" = 'Portal client'
ON CONFLICT ("ContractNumber") DO NOTHING;

INSERT INTO "RentalStatusHistory" ("RentalId", "FromStatusId", "ToStatusId", "ChangedAtUtc", "ChangedByEmployeeId", "ChangedBySource")
SELECT
    r."Id",
    NULL,
    r."StatusId",
    COALESCE(r."CanceledAtUtc", r."ClosedAtUtc", r."CreatedAtUtc"),
    r."CreatedByEmployeeId",
    CASE WHEN r."CreatedByEmployeeId" IS NULL THEN 'CLIENT' ELSE 'EMPLOYEE' END
FROM "Rentals" r
WHERE r."ContractNumber" = concat('CR-', EXTRACT(YEAR FROM CURRENT_DATE)::int, '-000001')
  AND NOT EXISTS (
      SELECT 1
      FROM "RentalStatusHistory" history
      WHERE history."RentalId" = r."Id");

INSERT INTO "RentalInspections" ("RentalId", "PerformedByEmployeeId", "TypeId", "CompletedAtUtc", "FuelPercent", "Notes", "CreatedAtUtc", "UpdatedAtUtc")
SELECT r."Id", e."Id", 1, CURRENT_TIMESTAMP, 100, 'Vehicle released in clean condition', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
FROM "Rentals" r
JOIN "Employees" e ON e."FullName" = 'Rental manager'
WHERE r."ContractNumber" = concat('CR-', EXTRACT(YEAR FROM CURRENT_DATE)::int, '-000001')
ON CONFLICT ("RentalId", "TypeId") DO NOTHING;

INSERT INTO "Payments" ("RentalId", "RecordedByEmployeeId", "Amount", "MethodId", "DirectionId", "StatusId", "CreatedAtUtc", "Notes", "ExternalTransactionId")
SELECT r."Id", e."Id", 2000.00, 2, 1, 2, CURRENT_TIMESTAMP, 'Advance payment', concat('demo-', EXTRACT(YEAR FROM CURRENT_DATE)::int, '-000001')
FROM "Rentals" r
JOIN "Employees" e ON e."FullName" = 'Rental manager'
WHERE r."ContractNumber" = concat('CR-', EXTRACT(YEAR FROM CURRENT_DATE)::int, '-000001')
ON CONFLICT ("ExternalTransactionId") DO NOTHING;

INSERT INTO "Damages" ("VehicleId", "RentalId", "ReportedByEmployeeId", "Description", "DateReported", "RepairCost", "DamageActNumber", "ChargedAmount", "StatusId", "CreatedAtUtc", "UpdatedAtUtc")
SELECT
    v."Id",
    NULL,
    e."Id",
    'Windshield chip discovered during inspection',
    CURRENT_DATE - INTERVAL '2 days' + TIME '13:30',
    1800.00,
    concat('ACT-', EXTRACT(YEAR FROM CURRENT_DATE)::int, '-000001'),
    0.00,
    1,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM "Vehicles" v
JOIN "Employees" e ON e."FullName" = 'Rental manager'
WHERE v."LicensePlate" = 'BC1519AE'
ON CONFLICT ("DamageActNumber") DO NOTHING;

INSERT INTO "DamageStatusHistory" ("DamageId", "FromStatusId", "ToStatusId", "ChangedAtUtc", "ChangedByEmployeeId", "ChangedBySource")
SELECT
    d."Id",
    NULL,
    d."StatusId",
    d."UpdatedAtUtc",
    d."ReportedByEmployeeId",
    'EMPLOYEE'
FROM "Damages" d
WHERE d."DamageActNumber" = concat('ACT-', EXTRACT(YEAR FROM CURRENT_DATE)::int, '-000001')
  AND NOT EXISTS (
      SELECT 1
      FROM "DamageStatusHistory" history
      WHERE history."DamageId" = d."Id");

INSERT INTO "DamagePhotos" ("DamageId", "StoredPath", "SortOrder", "IsDeleted", "CreatedAtUtc", "UpdatedAtUtc")
SELECT d."Id", '/protected/damages/act-000001/photo-1.jpg', 0, FALSE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
FROM "Damages" d
WHERE d."DamageActNumber" = concat('ACT-', EXTRACT(YEAR FROM CURRENT_DATE)::int, '-000001')
ON CONFLICT ("DamageId", "SortOrder") WHERE "IsDeleted" = FALSE DO NOTHING;

INSERT INTO "MaintenanceRecords" (
    "VehicleId",
    "PerformedByEmployeeId",
    "ServiceDate",
    "MileageAtService",
    "Description",
    "Cost",
    "NextServiceMileage",
    "NextServiceDate",
    "MaintenanceTypeCode",
    "ServiceProviderName")
SELECT
    v."Id",
    e."Id",
    (CURRENT_DATE - INTERVAL '10 days')::date,
    42000,
    'Scheduled oil and filter service',
    3200.00,
    52000,
    (CURRENT_DATE + INTERVAL '5 months')::date,
    'SCHEDULED',
    NULL
FROM "Vehicles" v
JOIN "Employees" e ON e."FullName" = 'Rental manager'
WHERE v."LicensePlate" = 'AA1173BH'
  AND NOT EXISTS (
      SELECT 1
      FROM "MaintenanceRecords" m
      WHERE m."VehicleId" = v."Id"
        AND m."ServiceDate" = (CURRENT_DATE - INTERVAL '10 days')::date);

INSERT INTO "ContractSequences" ("Year", "LastNumber")
VALUES (EXTRACT(YEAR FROM CURRENT_DATE)::integer, 1)
ON CONFLICT ("Year") DO NOTHING;
