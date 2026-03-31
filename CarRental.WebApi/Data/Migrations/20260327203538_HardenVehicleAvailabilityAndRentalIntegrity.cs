using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenVehicleAvailabilityAndRentalIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_LicensePlate",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Clients_DriverLicense",
                table: "Clients");

            migrationBuilder.AddColumn<bool>(
                name: "IsBookable",
                table: "Vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                """
                CREATE EXTENSION IF NOT EXISTS btree_gist;

                UPDATE "Vehicles" AS v
                SET "IsBookable" = CASE
                    WHEN v."IsDeleted" THEN FALSE
                    WHEN EXISTS (
                        SELECT 1
                        FROM "Rentals" AS r
                        WHERE r."VehicleId" = v."Id"
                          AND r."Status" = 2
                    ) THEN TRUE
                    ELSE v."IsAvailable"
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_LicensePlate",
                table: "Vehicles",
                column: "LicensePlate",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Rentals_CreatedAtUtc",
                table: "Rentals",
                column: "CreatedAtUtc");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_Canceled_Lifecycle",
                table: "Rentals",
                sql: "\"Status\" <> 4 OR (\"IsClosed\" = FALSE AND \"ClosedAtUtc\" IS NULL AND \"CanceledAtUtc\" IS NOT NULL AND length(btrim(COALESCE(\"CancellationReason\", ''))) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_Closed_Lifecycle",
                table: "Rentals",
                sql: "\"Status\" <> 3 OR (\"IsClosed\" = TRUE AND \"ClosedAtUtc\" IS NOT NULL AND \"CanceledAtUtc\" IS NULL AND \"CancellationReason\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_Open_Lifecycle",
                table: "Rentals",
                sql: "\"Status\" IN (3, 4) OR (\"ClosedAtUtc\" IS NULL AND \"CanceledAtUtc\" IS NULL AND \"CancellationReason\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_DriverLicense",
                table: "Clients",
                column: "DriverLicense",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.Sql(
                """
                ALTER TABLE "Rentals"
                DROP CONSTRAINT IF EXISTS "EX_Rentals_NoOverlappingActiveOrBookedPeriods";

                ALTER TABLE "Rentals"
                ADD CONSTRAINT "EX_Rentals_NoOverlappingActiveOrBookedPeriods"
                EXCLUDE USING gist (
                    "VehicleId" WITH =,
                    tsrange("StartDate", "EndDate", '[]') WITH &&
                )
                WHERE ("Status" IN (1, 2));

                CREATE OR REPLACE FUNCTION sync_vehicle_availability(p_vehicle_id integer)
                RETURNS void
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF p_vehicle_id IS NULL THEN
                        RETURN;
                    END IF;

                    UPDATE "Vehicles" AS v
                    SET "IsAvailable" = NOT v."IsDeleted"
                        AND v."IsBookable"
                        AND NOT EXISTS (
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

                CREATE OR REPLACE FUNCTION trg_sync_vehicle_bookability()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    PERFORM sync_vehicle_availability(NEW."Id");
                    RETURN NULL;
                END;
                $$;

                DROP TRIGGER IF EXISTS trg_rentals_sync_vehicle_availability ON "Rentals";
                CREATE TRIGGER trg_rentals_sync_vehicle_availability
                AFTER INSERT OR UPDATE OR DELETE ON "Rentals"
                FOR EACH ROW
                EXECUTE FUNCTION trg_sync_vehicle_availability();

                DROP TRIGGER IF EXISTS trg_vehicles_sync_vehicle_availability ON "Vehicles";
                CREATE TRIGGER trg_vehicles_sync_vehicle_availability
                AFTER INSERT OR UPDATE OF "IsBookable", "IsDeleted" ON "Vehicles"
                FOR EACH ROW
                EXECUTE FUNCTION trg_sync_vehicle_bookability();

                UPDATE "Vehicles" AS v
                SET "IsAvailable" = NOT v."IsDeleted"
                    AND v."IsBookable"
                    AND NOT EXISTS (
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
            migrationBuilder.Sql(
                """
                ALTER TABLE "Rentals"
                DROP CONSTRAINT IF EXISTS "EX_Rentals_NoOverlappingActiveOrBookedPeriods";

                DROP TRIGGER IF EXISTS trg_vehicles_sync_vehicle_availability ON "Vehicles";
                DROP FUNCTION IF EXISTS trg_sync_vehicle_bookability();

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

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_LicensePlate",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Rentals_CreatedAtUtc",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_Canceled_Lifecycle",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_Closed_Lifecycle",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_Open_Lifecycle",
                table: "Rentals");

            migrationBuilder.DropIndex(
                name: "IX_Clients_DriverLicense",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsBookable",
                table: "Vehicles");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_LicensePlate",
                table: "Vehicles",
                column: "LicensePlate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_DriverLicense",
                table: "Clients",
                column: "DriverLicense",
                unique: true);
        }
    }
}
