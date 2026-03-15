using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientProfilesAndRentalLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<string>(
                name: "PickupLocation",
                table: "Rentals",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.AddColumn<string>(
                name: "ReturnLocation",
                table: "Rentals",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "Employees",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Rentals"
                SET "PickupLocation" = 'Київ'
                WHERE "PickupLocation" = '';

                UPDATE "Rentals"
                SET "ReturnLocation" = COALESCE(NULLIF("ReturnLocation", ''), "PickupLocation", 'Київ');
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Employees" AS e
                SET "ClientId" = c."Id"
                FROM "Clients" AS c
                WHERE e."ClientId" IS NULL
                  AND (
                      c."PassportData" = 'EMP-' || lpad(e."Id"::text, 6, '0')
                      OR c."DriverLicense" = 'USR-' || lpad(e."Id"::text, 6, '0')
                  );
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_PickupFuelPercent_Range",
                table: "Rentals",
                sql: "\"PickupFuelPercent\" IS NULL OR \"PickupFuelPercent\" BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_ReturnFuelPercent_Range",
                table: "Rentals",
                sql: "\"ReturnFuelPercent\" IS NULL OR \"ReturnFuelPercent\" BETWEEN 0 AND 100");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ClientId",
                table: "Employees",
                column: "ClientId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Clients_ClientId",
                table: "Employees",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Clients_ClientId",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_PickupFuelPercent_Range",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_ReturnFuelPercent_Range",
                table: "Rentals");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ClientId",
                table: "Employees");

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
                name: "PickupLocation",
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

            migrationBuilder.DropColumn(
                name: "ReturnLocation",
                table: "Rentals");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Employees");
        }
    }
}
