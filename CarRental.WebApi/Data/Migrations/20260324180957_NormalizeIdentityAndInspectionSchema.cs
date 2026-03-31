using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CarRental.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeIdentityAndInspectionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Clients_ClientId",
                table: "Employees");

            migrationBuilder.RenameColumn(
                name: "ClientId",
                table: "Employees",
                newName: "PortalClientId");

            migrationBuilder.RenameIndex(
                name: "IX_Employees_ClientId",
                table: "Employees",
                newName: "IX_Employees_PortalClientId");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Vehicles",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Vehicles",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Damages",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Damages",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Clients",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DriverLicenseExpirationDate",
                table: "Clients",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverLicensePhotoPath",
                table: "Clients",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PassportExpirationDate",
                table: "Clients",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassportPhotoPath",
                table: "Clients",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Clients",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "FuelTypes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "RentalInspections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RentalId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FuelPercent = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalInspections", x => x.Id);
                    table.CheckConstraint("CK_RentalInspections_FuelPercent_Range", "\"FuelPercent\" IS NULL OR \"FuelPercent\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_RentalInspections_Type_Range", "\"Type\" BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_RentalInspections_Rentals_RentalId",
                        column: x => x.RentalId,
                        principalTable: "Rentals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransmissionTypes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransmissionTypes", x => x.Code);
                });

            migrationBuilder.Sql(
                """
                UPDATE "Clients"
                SET "CreatedAtUtc" = CURRENT_TIMESTAMP,
                    "UpdatedAtUtc" = CURRENT_TIMESTAMP
                WHERE "CreatedAtUtc" = TIMESTAMP '0001-01-01 00:00:00'
                   OR "UpdatedAtUtc" = TIMESTAMP '0001-01-01 00:00:00';

                UPDATE "Vehicles"
                SET "CreatedAtUtc" = CURRENT_TIMESTAMP,
                    "UpdatedAtUtc" = CURRENT_TIMESTAMP
                WHERE "CreatedAtUtc" = TIMESTAMP '0001-01-01 00:00:00'
                   OR "UpdatedAtUtc" = TIMESTAMP '0001-01-01 00:00:00';

                UPDATE "Damages"
                SET "CreatedAtUtc" = CURRENT_TIMESTAMP,
                    "UpdatedAtUtc" = CURRENT_TIMESTAMP
                WHERE "CreatedAtUtc" = TIMESTAMP '0001-01-01 00:00:00'
                   OR "UpdatedAtUtc" = TIMESTAMP '0001-01-01 00:00:00';
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "FuelTypes" ("Code", "DisplayName", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT DISTINCT
                    "FuelType",
                    "FuelType",
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP
                FROM "Vehicles"
                WHERE COALESCE(TRIM("FuelType"), '') <> ''
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "TransmissionTypes" ("Code", "DisplayName", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT DISTINCT
                    "TransmissionType",
                    "TransmissionType",
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP
                FROM "Vehicles"
                WHERE COALESCE(TRIM("TransmissionType"), '') <> ''
                ON CONFLICT ("Code") DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "RentalInspections" ("RentalId", "Type", "CompletedAtUtc", "FuelPercent", "Notes", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT
                    "Id",
                    1,
                    COALESCE("PickupInspectionCompletedAtUtc", CURRENT_TIMESTAMP),
                    "PickupFuelPercent",
                    NULLIF(TRIM(COALESCE("PickupInspectionNotes", '')), ''),
                    COALESCE("PickupInspectionCompletedAtUtc", CURRENT_TIMESTAMP),
                    COALESCE("PickupInspectionCompletedAtUtc", CURRENT_TIMESTAMP)
                FROM "Rentals"
                WHERE "PickupInspectionCompletedAtUtc" IS NOT NULL
                   OR "PickupFuelPercent" IS NOT NULL
                   OR COALESCE(TRIM("PickupInspectionNotes"), '') <> '';

                INSERT INTO "RentalInspections" ("RentalId", "Type", "CompletedAtUtc", "FuelPercent", "Notes", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT
                    "Id",
                    2,
                    COALESCE("ReturnInspectionCompletedAtUtc", CURRENT_TIMESTAMP),
                    "ReturnFuelPercent",
                    NULLIF(TRIM(COALESCE("ReturnInspectionNotes", '')), ''),
                    COALESCE("ReturnInspectionCompletedAtUtc", CURRENT_TIMESTAMP),
                    COALESCE("ReturnInspectionCompletedAtUtc", CURRENT_TIMESTAMP)
                FROM "Rentals"
                WHERE "ReturnInspectionCompletedAtUtc" IS NOT NULL
                   OR "ReturnFuelPercent" IS NOT NULL
                   OR COALESCE(TRIM("ReturnInspectionNotes"), '') <> '';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_PickupFuelPercent_Range",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_ReturnFuelPercent_Range",
                table: "Rentals");

            migrationBuilder.DropColumn(
                name: "PickupFuelPercent",
                table: "Rentals");

            migrationBuilder.DropColumn(
                name: "PickupInspectionCompletedAtUtc",
                table: "Rentals");

            migrationBuilder.DropColumn(
                name: "PickupInspectionNotes",
                table: "Rentals");

            migrationBuilder.DropColumn(
                name: "ReturnFuelPercent",
                table: "Rentals");

            migrationBuilder.DropColumn(
                name: "ReturnInspectionCompletedAtUtc",
                table: "Rentals");

            migrationBuilder.DropColumn(
                name: "ReturnInspectionNotes",
                table: "Rentals");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_FuelType",
                table: "Vehicles",
                column: "FuelType");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_IsDeleted",
                table: "Vehicles",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TransmissionType",
                table: "Vehicles",
                column: "TransmissionType");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_IsDeleted",
                table: "Clients",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_RentalInspections_RentalId_Type",
                table: "RentalInspections",
                columns: new[] { "RentalId", "Type" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Clients_PortalClientId",
                table: "Employees",
                column: "PortalClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_FuelTypes_FuelType",
                table: "Vehicles",
                column: "FuelType",
                principalTable: "FuelTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_TransmissionTypes_TransmissionType",
                table: "Vehicles",
                column: "TransmissionType",
                principalTable: "TransmissionTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Clients_PortalClientId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_FuelTypes_FuelType",
                table: "Vehicles");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_TransmissionTypes_TransmissionType",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_FuelType",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_IsDeleted",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_TransmissionType",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Clients_IsDeleted",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Damages");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Damages");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DriverLicenseExpirationDate",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DriverLicensePhotoPath",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PassportExpirationDate",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PassportPhotoPath",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Clients");

            migrationBuilder.RenameColumn(
                name: "PortalClientId",
                table: "Employees",
                newName: "ClientId");

            migrationBuilder.RenameIndex(
                name: "IX_Employees_PortalClientId",
                table: "Employees",
                newName: "IX_Employees_ClientId");

            migrationBuilder.AddColumn<int>(
                name: "PickupFuelPercent",
                table: "Rentals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupInspectionCompletedAtUtc",
                table: "Rentals",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupInspectionNotes",
                table: "Rentals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnFuelPercent",
                table: "Rentals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnInspectionCompletedAtUtc",
                table: "Rentals",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnInspectionNotes",
                table: "Rentals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Rentals" AS r
                SET
                    "PickupInspectionCompletedAtUtc" = inspection."CompletedAtUtc",
                    "PickupFuelPercent" = inspection."FuelPercent",
                    "PickupInspectionNotes" = inspection."Notes"
                FROM "RentalInspections" AS inspection
                WHERE inspection."RentalId" = r."Id"
                  AND inspection."Type" = 1;

                UPDATE "Rentals" AS r
                SET
                    "ReturnInspectionCompletedAtUtc" = inspection."CompletedAtUtc",
                    "ReturnFuelPercent" = inspection."FuelPercent",
                    "ReturnInspectionNotes" = inspection."Notes"
                FROM "RentalInspections" AS inspection
                WHERE inspection."RentalId" = r."Id"
                  AND inspection."Type" = 2;
                """);

            migrationBuilder.DropTable(
                name: "FuelTypes");

            migrationBuilder.DropTable(
                name: "RentalInspections");

            migrationBuilder.DropTable(
                name: "TransmissionTypes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_PickupFuelPercent_Range",
                table: "Rentals",
                sql: "\"PickupFuelPercent\" IS NULL OR \"PickupFuelPercent\" BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_ReturnFuelPercent_Range",
                table: "Rentals",
                sql: "\"ReturnFuelPercent\" IS NULL OR \"ReturnFuelPercent\" BETWEEN 0 AND 100");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Clients_ClientId",
                table: "Employees",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
