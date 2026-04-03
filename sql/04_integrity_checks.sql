-- Post-migration integrity checks for the redesigned schema

-- РќР°Р±С–СЂ РїРµСЂРµРІС–СЂРѕРє Р·Р°РґСѓРјР°РЅРёР№ СЏРє after-migration audit: РІС–РЅ РЅРµ Р·РјС–РЅСЋС” РґР°РЅС–, Р° Р»РёС€Рµ РїРѕРєР°Р·СѓС”, РґРµ СЃС…РµРјР° Р°Р±Рѕ seed СЂРѕР·С–Р№С€Р»РёСЃСЏ Р· С–РЅРІР°СЂС–Р°РЅС‚Р°РјРё.
-- 1) no invalid rental ranges
SELECT COUNT(*) AS invalid_rental_ranges
FROM "Rentals"
WHERE "StartDate" >= "EndDate";

-- 2) no negative money values
SELECT COUNT(*) AS negative_money_values
FROM (
    SELECT "TotalAmount" AS amount FROM "Rentals"
    UNION ALL
    SELECT "OverageFee" AS amount FROM "Rentals"
    UNION ALL
    SELECT "Amount" AS amount FROM "Payments"
    UNION ALL
    SELECT "RepairCost" AS amount FROM "Damages"
    UNION ALL
    SELECT "ChargedAmount" AS amount FROM "Damages"
    UNION ALL
    SELECT "Cost" AS amount FROM "MaintenanceRecords"
) q
WHERE q.amount < 0;

-- 3) rental lifecycle consistency without duplicated IsClosed flag
SELECT COUNT(*) AS inconsistent_rental_lifecycle
FROM "Rentals"
WHERE ("StatusId" = 3 AND ("ClosedAtUtc" IS NULL OR "CanceledAtUtc" IS NOT NULL OR "CancellationReason" IS NOT NULL))
   OR ("StatusId" = 4 AND ("ClosedAtUtc" IS NOT NULL OR "CanceledAtUtc" IS NULL OR length(btrim(COALESCE("CancellationReason", ''))) = 0))
   OR ("StatusId" IN (1, 2) AND ("ClosedAtUtc" IS NOT NULL OR "CanceledAtUtc" IS NOT NULL OR "CancellationReason" IS NOT NULL));

-- 4) duplicate active document numbers
SELECT "DocumentTypeCode", "DocumentNumber", COUNT(*) AS duplicate_count
FROM "ClientDocuments"
WHERE NOT "IsDeleted"
GROUP BY "DocumentTypeCode", "DocumentNumber"
HAVING COUNT(*) > 1;

-- 5) duplicate client phones among active clients
SELECT "Phone", COUNT(*) AS duplicate_count
FROM "Clients"
WHERE NOT "IsDeleted"
GROUP BY "Phone"
HAVING COUNT(*) > 1;

-- 6) duplicate license plates among active vehicles
SELECT "LicensePlate", COUNT(*) AS duplicate_count
FROM "Vehicles"
WHERE NOT "IsDeleted"
GROUP BY "LicensePlate"
HAVING COUNT(*) > 1;

-- 7) snapshot of computed availability
SELECT
    v."Id",
    v."LicensePlate",
    v."VehicleStatusCode",
    v."IsDeleted",
    (
        NOT v."IsDeleted"
        AND v."VehicleStatusCode" = 'READY'
        AND NOT EXISTS (
            SELECT 1
            FROM "Rentals" r
            WHERE r."VehicleId" = v."Id"
              AND r."StatusId" IN (1, 2)
        )
    ) AS "ComputedIsAvailable"
FROM "Vehicles" v
ORDER BY v."Id";

-- 8) damage status must stay consistent with charged amount
SELECT COUNT(*) AS inconsistent_damage_charge_state
FROM "Damages"
WHERE ("StatusId" = 1 AND "ChargedAmount" <> 0)
   OR ("StatusId" = 2 AND "ChargedAmount" <= 0);

-- 9) no duplicate inspection types per rental
SELECT "RentalId", "TypeId", COUNT(*) AS duplicate_count
FROM "RentalInspections"
GROUP BY "RentalId", "TypeId"
HAVING COUNT(*) > 1;

-- 10) rentals must point to clients with valid driver licenses
SELECT COUNT(*) AS rentals_with_invalid_license
FROM "Rentals" r
LEFT JOIN "ClientDocuments" d
  ON d."ClientId" = r."ClientId"
 AND d."DocumentTypeCode" = 'DRIVER_LICENSE'
 AND NOT d."IsDeleted"
WHERE d."ExpirationDate" IS NULL
   OR d."ExpirationDate" < r."StartDate"::date;

-- 11) damages must reference the same vehicle as the linked rental
SELECT d."Id", d."RentalId", d."VehicleId", r."VehicleId" AS rental_vehicle_id
FROM "Damages" d
JOIN "Rentals" r ON r."Id" = d."RentalId"
WHERE d."VehicleId" <> r."VehicleId";

-- 12) no overlapping booked/active rentals for the same vehicle
SELECT left_rental."Id" AS left_rental_id, right_rental."Id" AS right_rental_id, left_rental."VehicleId"
FROM "Rentals" AS left_rental
JOIN "Rentals" AS right_rental
  ON left_rental."VehicleId" = right_rental."VehicleId"
 AND left_rental."Id" < right_rental."Id"
 AND left_rental."StatusId" IN (1, 2)
 AND right_rental."StatusId" IN (1, 2)
 AND left_rental."StartDate" <= right_rental."EndDate"
 AND right_rental."StartDate" <= left_rental."EndDate";
