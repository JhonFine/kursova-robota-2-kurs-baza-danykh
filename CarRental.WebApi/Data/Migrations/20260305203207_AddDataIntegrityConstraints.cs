using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Vehicles"
                SET
                    "Mileage" = GREATEST("Mileage", 0),
                    "DailyRate" = CASE WHEN "DailyRate" <= 0 THEN 0.01 ELSE "DailyRate" END,
                    "ServiceIntervalKm" = CASE WHEN "ServiceIntervalKm" <= 0 THEN 10000 ELSE "ServiceIntervalKm" END;

                UPDATE "Employees"
                SET
                    "Role" = CASE WHEN "Role" BETWEEN 1 AND 3 THEN "Role" ELSE 3 END,
                    "FailedLoginAttempts" = GREATEST("FailedLoginAttempts", 0);

                UPDATE "Rentals"
                SET
                    "EndDate" = CASE WHEN "StartDate" <= "EndDate" THEN "EndDate" ELSE "StartDate" + INTERVAL '1 hour' END,
                    "StartMileage" = GREATEST("StartMileage", 0),
                    "EndMileage" = CASE
                        WHEN "EndMileage" IS NULL THEN NULL
                        WHEN "EndMileage" < "StartMileage" THEN "StartMileage"
                        ELSE "EndMileage"
                    END,
                    "OverageFee" = GREATEST("OverageFee", 0),
                    "TotalAmount" = GREATEST("TotalAmount", 0),
                    "Status" = CASE WHEN "Status" BETWEEN 1 AND 4 THEN "Status" ELSE 1 END;

                UPDATE "Rentals"
                SET "IsClosed" = ("Status" = 3);

                UPDATE "Payments"
                SET
                    "Amount" = CASE WHEN "Amount" > 0 THEN "Amount" ELSE 0.01 END,
                    "Method" = CASE WHEN "Method" BETWEEN 1 AND 2 THEN "Method" ELSE 1 END,
                    "Direction" = CASE WHEN "Direction" BETWEEN 1 AND 2 THEN "Direction" ELSE 1 END;

                UPDATE "MaintenanceRecords"
                SET
                    "MileageAtService" = GREATEST("MileageAtService", 0),
                    "Cost" = GREATEST("Cost", 0),
                    "NextServiceMileage" = CASE
                        WHEN "NextServiceMileage" <= 0 THEN GREATEST("MileageAtService", 0) + 1
                        WHEN "NextServiceMileage" < "MileageAtService" THEN "MileageAtService"
                        ELSE "NextServiceMileage"
                    END;

                UPDATE "Damages"
                SET
                    "RepairCost" = CASE WHEN "RepairCost" > 0 THEN "RepairCost" ELSE 0.01 END,
                    "ChargedAmount" = GREATEST("ChargedAmount", 0),
                    "Status" = CASE WHEN "Status" BETWEEN 1 AND 3 THEN "Status" ELSE 1 END;

                UPDATE "Damages"
                SET "ChargedAmount" = LEAST("ChargedAmount", "RepairCost");

                UPDATE "Damages"
                SET
                    "IsChargedToClient" = CASE WHEN "ChargedAmount" > 0 THEN TRUE ELSE FALSE END,
                    "ChargedAmount" = CASE WHEN "ChargedAmount" > 0 THEN "ChargedAmount" ELSE 0 END;

                UPDATE "Damages"
                SET "ActNumber" = CONCAT('ACT-', TO_CHAR(NOW(), 'YYYYMMDDHH24MISSMS'), '-', "Id")
                WHERE "ActNumber" IS NULL OR BTRIM("ActNumber") = '';

                WITH ranked AS (
                    SELECT
                        "Id",
                        "ActNumber",
                        ROW_NUMBER() OVER (PARTITION BY "ActNumber" ORDER BY "Id") AS rn
                    FROM "Damages"
                )
                UPDATE "Damages" AS d
                SET "ActNumber" = CONCAT(ranked."ActNumber", '-', d."Id")
                FROM ranked
                WHERE d."Id" = ranked."Id" AND ranked.rn > 1;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_DailyRate_Positive",
                table: "Vehicles",
                sql: "\"DailyRate\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_Mileage_NonNegative",
                table: "Vehicles",
                sql: "\"Mileage\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_ServiceIntervalKm_Positive",
                table: "Vehicles",
                sql: "\"ServiceIntervalKm\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_DateRange",
                table: "Rentals",
                sql: "\"StartDate\" <= \"EndDate\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_EndMileage_Valid",
                table: "Rentals",
                sql: "\"EndMileage\" IS NULL OR \"EndMileage\" >= \"StartMileage\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_IsClosed_MatchesStatus",
                table: "Rentals",
                sql: "(\"Status\" = 3) = \"IsClosed\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_OverageFee_NonNegative",
                table: "Rentals",
                sql: "\"OverageFee\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_StartMileage_NonNegative",
                table: "Rentals",
                sql: "\"StartMileage\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_Status_Range",
                table: "Rentals",
                sql: "\"Status\" BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_TotalAmount_NonNegative",
                table: "Rentals",
                sql: "\"TotalAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Amount_Positive",
                table: "Payments",
                sql: "\"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Direction_Range",
                table: "Payments",
                sql: "\"Direction\" BETWEEN 1 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Method_Range",
                table: "Payments",
                sql: "\"Method\" BETWEEN 1 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceRecords_Cost_NonNegative",
                table: "MaintenanceRecords",
                sql: "\"Cost\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceRecords_MileageAtService_NonNegative",
                table: "MaintenanceRecords",
                sql: "\"MileageAtService\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceRecords_NextServiceMileage_GteCurrent",
                table: "MaintenanceRecords",
                sql: "\"NextServiceMileage\" >= \"MileageAtService\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceRecords_NextServiceMileage_Positive",
                table: "MaintenanceRecords",
                sql: "\"NextServiceMileage\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_FailedLoginAttempts_NonNegative",
                table: "Employees",
                sql: "\"FailedLoginAttempts\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_Role_Range",
                table: "Employees",
                sql: "\"Role\" BETWEEN 1 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Damages_ChargedAmount_LteRepairCost",
                table: "Damages",
                sql: "\"ChargedAmount\" <= \"RepairCost\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Damages_ChargeFlag_Consistency",
                table: "Damages",
                sql: "(\"IsChargedToClient\" AND \"ChargedAmount\" > 0) OR (NOT \"IsChargedToClient\" AND \"ChargedAmount\" = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Damages_ChargedAmount_NonNegative",
                table: "Damages",
                sql: "\"ChargedAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Damages_RepairCost_Positive",
                table: "Damages",
                sql: "\"RepairCost\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Damages_Status_Range",
                table: "Damages",
                sql: "\"Status\" BETWEEN 1 AND 3");

            migrationBuilder.CreateIndex(
                name: "IX_Damages_ActNumber",
                table: "Damages",
                column: "ActNumber",
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION sync_vehicle_availability(p_vehicle_id integer)
                RETURNS void
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF p_vehicle_id IS NULL THEN
                        RETURN;
                    END IF;

                    UPDATE "Vehicles" AS v
                    SET "IsAvailable" = NOT EXISTS (
                        SELECT 1
                        FROM "Rentals" AS r
                        WHERE r."VehicleId" = v."Id"
                          AND r."Status" = 2
                    )
                    WHERE v."Id" = p_vehicle_id;
                END;
                $$;

                CREATE OR REPLACE FUNCTION trg_sync_vehicle_availability()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    PERFORM sync_vehicle_availability(COALESCE(NEW."VehicleId", OLD."VehicleId"));

                    IF TG_OP = 'UPDATE' AND NEW."VehicleId" IS DISTINCT FROM OLD."VehicleId" THEN
                        PERFORM sync_vehicle_availability(OLD."VehicleId");
                    END IF;

                    RETURN NULL;
                END;
                $$;

                DROP TRIGGER IF EXISTS trg_rentals_sync_vehicle_availability ON "Rentals";
                CREATE TRIGGER trg_rentals_sync_vehicle_availability
                AFTER INSERT OR UPDATE OR DELETE ON "Rentals"
                FOR EACH ROW
                EXECUTE FUNCTION trg_sync_vehicle_availability();

                UPDATE "Vehicles" AS v
                SET "IsAvailable" = NOT EXISTS (
                    SELECT 1
                    FROM "Rentals" AS r
                    WHERE r."VehicleId" = v."Id"
                      AND r."Status" = 2
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_DailyRate_Positive",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_Mileage_NonNegative",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_ServiceIntervalKm_Positive",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_DateRange",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_EndMileage_Valid",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_IsClosed_MatchesStatus",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_OverageFee_NonNegative",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_StartMileage_NonNegative",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_Status_Range",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_TotalAmount_NonNegative",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Amount_Positive",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Direction_Range",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Method_Range",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceRecords_Cost_NonNegative",
                table: "MaintenanceRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceRecords_MileageAtService_NonNegative",
                table: "MaintenanceRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceRecords_NextServiceMileage_GteCurrent",
                table: "MaintenanceRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceRecords_NextServiceMileage_Positive",
                table: "MaintenanceRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_FailedLoginAttempts_NonNegative",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_Role_Range",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Damages_ActNumber",
                table: "Damages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Damages_ChargedAmount_LteRepairCost",
                table: "Damages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Damages_ChargeFlag_Consistency",
                table: "Damages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Damages_ChargedAmount_NonNegative",
                table: "Damages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Damages_RepairCost_Positive",
                table: "Damages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Damages_Status_Range",
                table: "Damages");

            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_rentals_sync_vehicle_availability ON "Rentals";
                DROP FUNCTION IF EXISTS trg_sync_vehicle_availability();
                DROP FUNCTION IF EXISTS sync_vehicle_availability(integer);
                """);
        }
    }
}
