-- Views and analytical SQL queries for the redesigned schema

-- View акумулює signed платежі, щоб борг/баланс можна було перевикористовувати у звітах без копіювання CASE-логіки.
CREATE OR REPLACE VIEW vw_rental_balance AS
SELECT
    r."Id" AS rental_id,
    r."ContractNumber" AS contract_number,
    r."TotalAmount" AS total_amount,
    COALESCE(
        SUM(
            CASE
                WHEN p."Status" = 2 AND p."Direction" = 1 THEN p."Amount"
                WHEN p."Status" = 2 AND p."Direction" = 2 THEN -p."Amount"
                ELSE 0
            END
        ),
        0
    ) AS paid_amount,
    r."TotalAmount" - COALESCE(
        SUM(
            CASE
                WHEN p."Status" = 2 AND p."Direction" = 1 THEN p."Amount"
                WHEN p."Status" = 2 AND p."Direction" = 2 THEN -p."Amount"
                ELSE 0
            END
        ),
        0
    ) AS balance
FROM "Rentals" r
LEFT JOIN "Payments" p ON p."RentalId" = r."Id"
GROUP BY r."Id", r."ContractNumber", r."TotalAmount";

CREATE OR REPLACE VIEW vw_vehicle_utilization AS
SELECT
    v."Id" AS vehicle_id,
    v."Make",
    v."Model",
    v."LicensePlate",
    v."VehicleStatusCode",
    COUNT(r."Id") AS rentals_count,
    COALESCE(SUM(r."TotalAmount"), 0) AS revenue
FROM "Vehicles" v
LEFT JOIN "Rentals" r ON r."VehicleId" = v."Id"
GROUP BY v."Id", v."Make", v."Model", v."LicensePlate", v."VehicleStatusCode";

CREATE OR REPLACE VIEW vw_rental_inspection_summary AS
SELECT
    r."Id" AS rental_id,
    MAX(i."CompletedAtUtc") FILTER (WHERE i."Type" = 1) AS pickup_completed_at_utc,
    MAX(i."FuelPercent") FILTER (WHERE i."Type" = 1) AS pickup_fuel_percent,
    MAX(i."Notes") FILTER (WHERE i."Type" = 1) AS pickup_notes,
    MAX(i."CompletedAtUtc") FILTER (WHERE i."Type" = 2) AS return_completed_at_utc,
    MAX(i."FuelPercent") FILTER (WHERE i."Type" = 2) AS return_fuel_percent,
    MAX(i."Notes") FILTER (WHERE i."Type" = 2) AS return_notes
FROM "Rentals" r
LEFT JOIN "RentalInspections" i ON i."RentalId" = r."Id"
GROUP BY r."Id";

-- Q1. rentals with client and responsible employees
SELECT
    r."ContractNumber",
    c."FullName" AS client_name,
    creator."FullName" AS created_by_employee,
    closer."FullName" AS closed_by_employee,
    canceler."FullName" AS canceled_by_employee,
    r."StartDate",
    r."EndDate",
    ins.pickup_completed_at_utc,
    ins.return_completed_at_utc,
    r."Status",
    r."TotalAmount"
FROM "Rentals" r
JOIN "Clients" c ON c."Id" = r."ClientId"
JOIN "Employees" creator ON creator."Id" = r."CreatedByEmployeeId"
LEFT JOIN "Employees" closer ON closer."Id" = r."ClosedByEmployeeId"
LEFT JOIN "Employees" canceler ON canceler."Id" = r."CanceledByEmployeeId"
LEFT JOIN vw_rental_inspection_summary ins ON ins.rental_id = r."Id"
ORDER BY r."CreatedAtUtc" DESC;

-- Q2. monthly closed-rental revenue
SELECT
    date_trunc('month', r."ClosedAtUtc") AS month,
    SUM(r."TotalAmount") AS revenue
FROM "Rentals" r
WHERE r."Status" = 3
GROUP BY date_trunc('month', r."ClosedAtUtc")
ORDER BY month;

-- Q3. clients with debt > 0
SELECT
    rt."ClientId" AS client_id,
    c."FullName",
    SUM(b.balance) AS total_debt
FROM vw_rental_balance b
JOIN "Rentals" rt ON rt."Id" = b.rental_id
JOIN "Clients" c ON c."Id" = rt."ClientId"
GROUP BY rt."ClientId", c."FullName"
HAVING SUM(b.balance) > 0
ORDER BY total_debt DESC;

-- Q4. overdue maintenance candidates
SELECT
    v."Id",
    v."Make",
    v."Model",
    v."Mileage",
    m.next_service_mileage,
    (v."Mileage" - m.next_service_mileage) AS overdue_km
FROM "Vehicles" v
JOIN LATERAL (
    SELECT COALESCE(MAX("NextServiceMileage"), v."Mileage" + v."ServiceIntervalKm") AS next_service_mileage
    FROM "MaintenanceRecords"
    WHERE "VehicleId" = v."Id"
) m ON TRUE
WHERE v."Mileage" >= m.next_service_mileage
ORDER BY overdue_km DESC;

-- Q5. top vehicles by revenue
SELECT
    v."LicensePlate",
    SUM(r."TotalAmount") AS revenue,
    DENSE_RANK() OVER (ORDER BY SUM(r."TotalAmount") DESC) AS revenue_rank
FROM "Vehicles" v
JOIN "Rentals" r ON r."VehicleId" = v."Id"
GROUP BY v."LicensePlate";

-- Q6. damage summary with reporter and rental linkage
SELECT
    d."ActNumber",
    v."LicensePlate",
    e."FullName" AS reported_by_employee,
    r."ContractNumber",
    d."RepairCost",
    d."ChargedAmount",
    d."Status"
FROM "Damages" d
JOIN "Vehicles" v ON v."Id" = d."VehicleId"
JOIN "Employees" e ON e."Id" = d."ReportedByEmployeeId"
LEFT JOIN "Rentals" r ON r."Id" = d."RentalId"
ORDER BY d."CreatedAtUtc" DESC;
