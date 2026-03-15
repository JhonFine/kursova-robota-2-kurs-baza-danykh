-- Post-migration integrity checks

-- 1) no invalid rental ranges
SELECT COUNT(*) AS invalid_rental_ranges
FROM "Rentals"
WHERE "StartDate" > "EndDate";

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

-- 3) check IsClosed/status consistency
SELECT COUNT(*) AS inconsistent_closed_status
FROM "Rentals"
WHERE ("Status" = 3 AND "IsClosed" = FALSE)
   OR ("Status" <> 3 AND "IsClosed" = TRUE);

-- 4) duplicate act numbers
SELECT "ActNumber", COUNT(*)
FROM "Damages"
GROUP BY "ActNumber"
HAVING COUNT(*) > 1;

-- 5) IsAvailable should match active rentals
SELECT v."Id", v."LicensePlate", v."IsAvailable"
FROM "Vehicles" v
WHERE v."IsAvailable" = TRUE
  AND EXISTS (
      SELECT 1
      FROM "Rentals" r
      WHERE r."VehicleId" = v."Id"
        AND r."Status" = 2
  );

-- 6) charged amount and charge-flag consistency
SELECT COUNT(*) AS inconsistent_damage_charge_flag
FROM "Damages"
WHERE ("IsChargedToClient" = TRUE AND "ChargedAmount" <= 0)
   OR ("IsChargedToClient" = FALSE AND "ChargedAmount" <> 0);
