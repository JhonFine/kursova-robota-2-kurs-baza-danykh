using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorVehicleSpecsAndUtcSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_rentals_sync_vehicle_availability ON "Rentals";
                DROP TRIGGER IF EXISTS trg_vehicles_sync_vehicle_availability ON "Vehicles";
                DROP FUNCTION IF EXISTS trg_sync_vehicle_bookability();
                DROP FUNCTION IF EXISTS trg_sync_vehicle_availability();
                DROP FUNCTION IF EXISTS sync_vehicle_availability(integer);
                """);

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_IsAvailable_Make_Model",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_Canceled_Lifecycle",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_Closed_Lifecycle",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_IsClosed_MatchesStatus",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Damages_ChargeFlag_Consistency",
                table: "Damages");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Vehicles",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Vehicles",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AddColumn<string>(
                name: "CargoCapacityUnit",
                table: "Vehicles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CargoCapacityValue",
                table: "Vehicles",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ConsumptionUnit",
                table: "Vehicles",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ConsumptionValue",
                table: "Vehicles",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PowertrainCapacityUnit",
                table: "Vehicles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PowertrainCapacityValue",
                table: "Vehicles",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                """
                UPDATE "Vehicles"
                SET
                    "PowertrainCapacityValue" = COALESCE(NULLIF(replace(substring("EngineDisplay" from '([0-9]+([.,][0-9]+)?)'), ',', '.'), '')::numeric, 1),
                    "PowertrainCapacityUnit" = CASE
                        WHEN upper("EngineDisplay") LIKE '%KWH%' OR upper("EngineDisplay") LIKE '%КВТ%' THEN 'KWH'
                        ELSE 'L'
                    END,
                    "CargoCapacityValue" = COALESCE(NULLIF(replace(substring("CargoCapacityDisplay" from '([0-9]+([.,][0-9]+)?)'), ',', '.'), '')::numeric, 1),
                    "CargoCapacityUnit" = CASE
                        WHEN upper("CargoCapacityDisplay") LIKE '%SEAT%' OR upper("CargoCapacityDisplay") LIKE '%МІС%' THEN 'SEATS'
                        WHEN upper("CargoCapacityDisplay") LIKE '%KG%' OR upper("CargoCapacityDisplay") LIKE '%КГ%' THEN 'KG'
                        ELSE 'L'
                    END,
                    "ConsumptionValue" = COALESCE(NULLIF(replace(substring("ConsumptionDisplay" from '([0-9]+([.,][0-9]+)?)'), ',', '.'), '')::numeric, 1),
                    "ConsumptionUnit" = CASE
                        WHEN upper("ConsumptionDisplay") LIKE '%KWH%' OR upper("ConsumptionDisplay") LIKE '%КВТ%' THEN 'KWH_PER_100KM'
                        ELSE 'L_PER_100KM'
                    END;
                """);

            migrationBuilder.DropColumn(
                name: "CargoCapacityDisplay",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ConsumptionDisplay",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "EngineDisplay",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Rentals");

            migrationBuilder.DropColumn(
                name: "IsChargedToClient",
                table: "Damages");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "TransmissionTypes",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "TransmissionTypes",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Rentals",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ClosedAtUtc",
                table: "Rentals",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CanceledAtUtc",
                table: "Rentals",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "RentalInspections",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "RentalInspections",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "RentalInspections",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "FuelTypes",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "FuelTypes",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PasswordChangedAtUtc",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LockoutUntilUtc",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastLoginUtc",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Damages",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Damages",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Clients",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Clients",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_IsBookable_Make_Model",
                table: "Vehicles",
                columns: new[] { "IsBookable", "Make", "Model" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_CargoCapacity_Positive",
                table: "Vehicles",
                sql: "\"CargoCapacityValue\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_CargoCapacityUnit_Allowed",
                table: "Vehicles",
                sql: "\"CargoCapacityUnit\" IN ('L', 'KG', 'SEATS')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_Consumption_Positive",
                table: "Vehicles",
                sql: "\"ConsumptionValue\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_ConsumptionUnit_Allowed",
                table: "Vehicles",
                sql: "\"ConsumptionUnit\" IN ('L_PER_100KM', 'KWH_PER_100KM')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_PowertrainCapacity_Positive",
                table: "Vehicles",
                sql: "\"PowertrainCapacityValue\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_PowertrainCapacityUnit_Allowed",
                table: "Vehicles",
                sql: "\"PowertrainCapacityUnit\" IN ('L', 'KWH')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_Canceled_Lifecycle",
                table: "Rentals",
                sql: "\"Status\" <> 4 OR (\"ClosedAtUtc\" IS NULL AND \"CanceledAtUtc\" IS NOT NULL AND length(btrim(COALESCE(\"CancellationReason\", ''))) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_Closed_Lifecycle",
                table: "Rentals",
                sql: "\"Status\" <> 3 OR (\"ClosedAtUtc\" IS NOT NULL AND \"CanceledAtUtc\" IS NULL AND \"CancellationReason\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Damages_Status_ChargeConsistency",
                table: "Damages",
                sql: "(\"Status\" <> 1 OR \"ChargedAmount\" = 0) AND (\"Status\" <> 2 OR \"ChargedAmount\" > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_PassportData",
                table: "Clients",
                column: "PassportData",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_IsBookable_Make_Model",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_CargoCapacity_Positive",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_CargoCapacityUnit_Allowed",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_Consumption_Positive",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_ConsumptionUnit_Allowed",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_PowertrainCapacity_Positive",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_PowertrainCapacityUnit_Allowed",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_Canceled_Lifecycle",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_Closed_Lifecycle",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Damages_Status_ChargeConsistency",
                table: "Damages");

            migrationBuilder.DropIndex(
                name: "IX_Clients_PassportData",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CargoCapacityUnit",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CargoCapacityValue",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ConsumptionUnit",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ConsumptionValue",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "PowertrainCapacityUnit",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "PowertrainCapacityValue",
                table: "Vehicles");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Vehicles",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Vehicles",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "CargoCapacityDisplay",
                table: "Vehicles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConsumptionDisplay",
                table: "Vehicles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EngineDisplay",
                table: "Vehicles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "Vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "TransmissionTypes",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "TransmissionTypes",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Rentals",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ClosedAtUtc",
                table: "Rentals",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CanceledAtUtc",
                table: "Rentals",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Rentals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "RentalInspections",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "RentalInspections",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "RentalInspections",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Payments",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "FuelTypes",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "FuelTypes",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PasswordChangedAtUtc",
                table: "Employees",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LockoutUntilUtc",
                table: "Employees",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastLoginUtc",
                table: "Employees",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Damages",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Damages",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<bool>(
                name: "IsChargedToClient",
                table: "Damages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Clients",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Clients",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_IsAvailable_Make_Model",
                table: "Vehicles",
                columns: new[] { "IsAvailable", "Make", "Model" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_Canceled_Lifecycle",
                table: "Rentals",
                sql: "\"Status\" <> 4 OR (\"IsClosed\" = FALSE AND \"ClosedAtUtc\" IS NULL AND \"CanceledAtUtc\" IS NOT NULL AND length(btrim(COALESCE(\"CancellationReason\", ''))) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_Closed_Lifecycle",
                table: "Rentals",
                sql: "\"Status\" <> 3 OR (\"IsClosed\" = TRUE AND \"ClosedAtUtc\" IS NOT NULL AND \"CanceledAtUtc\" IS NULL AND \"CancellationReason\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_IsClosed_MatchesStatus",
                table: "Rentals",
                sql: "(\"Status\" = 3) = \"IsClosed\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Damages_ChargeFlag_Consistency",
                table: "Damages",
                sql: "(\"IsChargedToClient\" AND \"ChargedAmount\" > 0) OR (NOT \"IsChargedToClient\" AND \"ChargedAmount\" = 0)");
        }
    }
}
