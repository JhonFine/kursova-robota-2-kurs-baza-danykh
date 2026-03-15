-- Views and analytical SQL queries

CREATE OR REPLACE VIEW vw_rental_balance AS
SELECT
    r."Id" AS rental_id,
    r."ContractNumber" AS contract_number,
    r."TotalAmount" AS total_amount,
    COALESCE(SUM(CASE WHEN p."Direction" = 1 THEN p."Amount" ELSE -p."Amount" END), 0) AS paid_amount,
    r."TotalAmount" - COALESCE(SUM(CASE WHEN p."Direction" = 1 THEN p."Amount" ELSE -p."Amount" END), 0) AS balance
FROM "Rentals" r
LEFT JOIN "Payments" p ON p."RentalId" = r."Id"
GROUP BY r."Id", r."ContractNumber", r."TotalAmount";

CREATE OR REPLACE VIEW vw_vehicle_utilization AS
SELECT
    v."Id" AS vehicle_id,
    v."Make",
    v."Model",
    v."LicensePlate",
    COUNT(r."Id") AS rentals_count,
    COALESCE(SUM(r."TotalAmount"), 0) AS revenue
FROM "Vehicles" v
LEFT JOIN "Rentals" r ON r."VehicleId" = v."Id"
GROUP BY v."Id", v."Make", v."Model", v."LicensePlate";

-- Q1. rentals with client and manager
SELECT
    r."ContractNumber",
    c."FullName" AS client_name,
    e."FullName" AS manager_name,
    r."StartDate",
    r."EndDate",
    r."Status",
    r."TotalAmount"
FROM "Rentals" r
JOIN "Clients" c ON c."Id" = r."ClientId"
JOIN "Employees" e ON e."Id" = r."EmployeeId"
ORDER BY r."CreatedAtUtc" DESC;

-- Q2. monthly revenue
SELECT
    date_trunc('month', r."CreatedAtUtc") AS month,
    SUM(r."TotalAmount") AS revenue
FROM "Rentals" r
WHERE r."Status" = 3
GROUP BY date_trunc('month', r."CreatedAtUtc")
ORDER BY month;

-- Q3. clients with debt > 0
SELECT
    rt."ClientId" AS client_id,
    c."FullName",
    SUM(r.balance) AS total_debt
FROM vw_rental_balance r
JOIN "Rentals" rt ON rt."Id" = r.rental_id
JOIN "Clients" c ON c."Id" = rt."ClientId"
GROUP BY rt."ClientId", c."FullName"
HAVING SUM(r.balance) > 0
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

-- Q5. top vehicles by revenue (window function)
SELECT
    v."LicensePlate",
    SUM(r."TotalAmount") AS revenue,
    DENSE_RANK() OVER (ORDER BY SUM(r."TotalAmount") DESC) AS revenue_rank
FROM "Vehicles" v
JOIN "Rentals" r ON r."VehicleId" = v."Id"
GROUP BY v."LicensePlate";
