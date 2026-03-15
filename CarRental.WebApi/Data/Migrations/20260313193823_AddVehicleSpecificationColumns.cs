using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleSpecificationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<int>(
                name: "DoorsCount",
                table: "Vehicles",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<string>(
                name: "EngineDisplay",
                table: "Vehicles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FuelType",
                table: "Vehicles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasAirConditioning",
                table: "Vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "TransmissionType",
                table: "Vehicles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_DoorsCount_Range",
                table: "Vehicles",
                sql: "\"DoorsCount\" BETWEEN 1 AND 8");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_DoorsCount_Range",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CargoCapacityDisplay",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ConsumptionDisplay",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "DoorsCount",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "EngineDisplay",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FuelType",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "HasAirConditioning",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "TransmissionType",
                table: "Vehicles");
        }
    }
}
