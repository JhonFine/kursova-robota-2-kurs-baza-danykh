using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CarRental.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeepSchemaRedesignAccountsDocumentsMediaLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Damages_Rentals_RentalId",
                table: "Damages");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Clients_PortalClientId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Rentals_Employees_EmployeeId",
                table: "Rentals");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_FuelTypes_FuelType",
                table: "Vehicles");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_TransmissionTypes_TransmissionType",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_IsBookable_Make_Model",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rentals_Status_Range",
                table: "Rentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RentalInspections_Type_Range",
                table: "RentalInspections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Direction_Range",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Method_Range",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Employees_Login",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_PortalClientId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_Role_IsActive",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_FailedLoginAttempts_NonNegative",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_Role_Range",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Damages_Status_Range",
                table: "Damages");

            migrationBuilder.DropIndex(
                name: "IX_Clients_DriverLicense",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_PassportData",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_Phone",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsBookable",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LastLoginUtc",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LockoutUntilUtc",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Login",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PortalClientId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "Damages");

            migrationBuilder.DropColumn(
                name: "DriverLicense",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DriverLicenseExpirationDate",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DriverLicensePhotoPath",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PassportData",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PassportExpirationDate",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PassportPhotoPath",
                table: "Clients");

            migrationBuilder.RenameColumn(
                name: "TransmissionType",
                table: "Vehicles",
                newName: "TransmissionTypeCode");

            migrationBuilder.RenameColumn(
                name: "FuelType",
                table: "Vehicles",
                newName: "FuelTypeCode");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_TransmissionType",
                table: "Vehicles",
                newName: "IX_Vehicles_TransmissionTypeCode");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_FuelType",
                table: "Vehicles",
                newName: "IX_Vehicles_FuelTypeCode");

            migrationBuilder.RenameColumn(
                name: "EmployeeId",
                table: "Rentals",
                newName: "CreatedByEmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_Rentals_EmployeeId_CreatedAtUtc",
                table: "Rentals",
                newName: "IX_Rentals_CreatedByEmployeeId_CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "PasswordChangedAtUtc",
                table: "Employees",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "Blacklisted",
                table: "Clients",
                newName: "IsBlacklisted");

            migrationBuilder.AddColumn<string>(
                name: "VehicleStatusCode",
                table: "Vehicles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "READY");

            migrationBuilder.AddColumn<int>(
                name: "CanceledByEmployeeId",
                table: "Rentals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosedByEmployeeId",
                table: "Rentals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerformedByEmployeeId",
                table: "RentalInspections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExternalTransactionId",
                table: "Payments",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MaintenanceTypeCode",
                table: "MaintenanceRecords",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PerformedByEmployeeId",
                table: "MaintenanceRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceProviderName",
                table: "MaintenanceRecords",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Employees",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ReportedByEmployeeId",
                table: "Damages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Clients",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlacklistReason",
                table: "Clients",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BlacklistedAtUtc",
                table: "Clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Rentals_Id_VehicleId",
                table: "Rentals",
                columns: new[] { "Id", "VehicleId" });

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Login = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    FailedLoginAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LockoutUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.CheckConstraint("CK_Accounts_FailedLoginAttempts_NonNegative", "\"FailedLoginAttempts\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ClientDocumentTypes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientDocumentTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "DamagePhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DamageId = table.Column<int>(type: "integer", nullable: false),
                    StoredPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DamagePhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DamagePhotos_Damages_DamageId",
                        column: x => x.DamageId,
                        principalTable: "Damages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DamageStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DamageStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InspectionTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceTypes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "PaymentDirections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentDirections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RentalStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VehiclePhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    StoredPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehiclePhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehiclePhotos_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleStatuses",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleStatuses", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "ClientDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    DocumentTypeCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "date", nullable: true),
                    StoredPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientDocuments_ClientDocumentTypes_DocumentTypeCode",
                        column: x => x.DocumentTypeCode,
                        principalTable: "ClientDocumentTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientDocuments_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ClientDocumentTypes",
                columns: new[] { "Code", "DisplayName" },
                values: new object[,]
                {
                    { "DRIVER_LICENSE", "Driver license" },
                    { "PASSPORT", "Passport" }
                });

            migrationBuilder.InsertData(
                table: "DamageStatuses",
                columns: new[] { "Id", "DisplayName" },
                values: new object[,]
                {
                    { 1, "Open" },
                    { 2, "Charged" },
                    { 3, "Resolved" }
                });

            migrationBuilder.InsertData(
                table: "EmployeeRoles",
                columns: new[] { "Id", "DisplayName" },
                values: new object[,]
                {
                    { 1, "Admin" },
                    { 2, "Manager" },
                    { 3, "User" }
                });

            migrationBuilder.InsertData(
                table: "InspectionTypes",
                columns: new[] { "Id", "DisplayName" },
                values: new object[,]
                {
                    { 1, "Pickup" },
                    { 2, "Return" }
                });

            migrationBuilder.InsertData(
                table: "MaintenanceTypes",
                columns: new[] { "Code", "DisplayName" },
                values: new object[,]
                {
                    { "INSPECTION", "Inspection" },
                    { "REPAIR", "Repair" },
                    { "SCHEDULED", "Scheduled service" },
                    { "TIRES", "Tires" }
                });

            migrationBuilder.InsertData(
                table: "PaymentDirections",
                columns: new[] { "Id", "DisplayName" },
                values: new object[,]
                {
                    { 1, "Incoming" },
                    { 2, "Refund" }
                });

            migrationBuilder.InsertData(
                table: "PaymentMethods",
                columns: new[] { "Id", "DisplayName" },
                values: new object[,]
                {
                    { 1, "Cash" },
                    { 2, "Card" }
                });

            migrationBuilder.InsertData(
                table: "PaymentStatuses",
                columns: new[] { "Id", "DisplayName" },
                values: new object[,]
                {
                    { 1, "Pending" },
                    { 2, "Completed" },
                    { 3, "Canceled" },
                    { 4, "Refunded" }
                });

            migrationBuilder.InsertData(
                table: "RentalStatuses",
                columns: new[] { "Id", "DisplayName" },
                values: new object[,]
                {
                    { 1, "Booked" },
                    { 2, "Active" },
                    { 3, "Closed" },
                    { 4, "Canceled" }
                });

            migrationBuilder.InsertData(
                table: "VehicleStatuses",
                columns: new[] { "Code", "DisplayName" },
                values: new object[,]
                {
                    { "DAMAGED", "Damaged" },
                    { "INACTIVE", "Inactive" },
                    { "MAINTENANCE", "Maintenance" },
                    { "READY", "Ready" },
                    { "RENTED", "Rented" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_VehicleStatusCode_Make_Model",
                table: "Vehicles",
                columns: new[] { "VehicleStatusCode", "Make", "Model" });

            migrationBuilder.CreateIndex(
                name: "IX_Rentals_CanceledByEmployeeId",
                table: "Rentals",
                column: "CanceledByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Rentals_ClosedByEmployeeId",
                table: "Rentals",
                column: "ClosedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Rentals_Status",
                table: "Rentals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RentalInspections_PerformedByEmployeeId",
                table: "RentalInspections",
                column: "PerformedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalInspections_Type",
                table: "RentalInspections",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Direction",
                table: "Payments",
                column: "Direction");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ExternalTransactionId",
                table: "Payments",
                column: "ExternalTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Method",
                table: "Payments",
                column: "Method");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status",
                table: "Payments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_MaintenanceTypeCode",
                table: "MaintenanceRecords",
                column: "MaintenanceTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_PerformedByEmployeeId",
                table: "MaintenanceRecords",
                column: "PerformedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_AccountId",
                table: "Employees",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Role_FullName",
                table: "Employees",
                columns: new[] { "Role", "FullName" });

            migrationBuilder.CreateIndex(
                name: "IX_Damages_RentalId_VehicleId",
                table: "Damages",
                columns: new[] { "RentalId", "VehicleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Damages_ReportedByEmployeeId",
                table: "Damages",
                column: "ReportedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Damages_Status",
                table: "Damages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_AccountId",
                table: "Clients",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Phone",
                table: "Clients",
                column: "Phone",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Login",
                table: "Accounts",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_ClientId_DocumentTypeCode",
                table: "ClientDocuments",
                columns: new[] { "ClientId", "DocumentTypeCode" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_DocumentTypeCode_DocumentNumber",
                table: "ClientDocuments",
                columns: new[] { "DocumentTypeCode", "DocumentNumber" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_DamagePhotos_DamageId_SortOrder",
                table: "DamagePhotos",
                columns: new[] { "DamageId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePhotos_VehicleId_SortOrder",
                table: "VehiclePhotos",
                columns: new[] { "VehicleId", "SortOrder" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Accounts_AccountId",
                table: "Clients",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Damages_DamageStatuses_Status",
                table: "Damages",
                column: "Status",
                principalTable: "DamageStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Damages_Employees_ReportedByEmployeeId",
                table: "Damages",
                column: "ReportedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Damages_Rentals_RentalId_VehicleId",
                table: "Damages",
                columns: new[] { "RentalId", "VehicleId" },
                principalTable: "Rentals",
                principalColumns: new[] { "Id", "VehicleId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Accounts_AccountId",
                table: "Employees",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_EmployeeRoles_Role",
                table: "Employees",
                column: "Role",
                principalTable: "EmployeeRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecords_Employees_PerformedByEmployeeId",
                table: "MaintenanceRecords",
                column: "PerformedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecords_MaintenanceTypes_MaintenanceTypeCode",
                table: "MaintenanceRecords",
                column: "MaintenanceTypeCode",
                principalTable: "MaintenanceTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentDirections_Direction",
                table: "Payments",
                column: "Direction",
                principalTable: "PaymentDirections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentMethods_Method",
                table: "Payments",
                column: "Method",
                principalTable: "PaymentMethods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentStatuses_Status",
                table: "Payments",
                column: "Status",
                principalTable: "PaymentStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RentalInspections_Employees_PerformedByEmployeeId",
                table: "RentalInspections",
                column: "PerformedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RentalInspections_InspectionTypes_Type",
                table: "RentalInspections",
                column: "Type",
                principalTable: "InspectionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rentals_Employees_CanceledByEmployeeId",
                table: "Rentals",
                column: "CanceledByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rentals_Employees_ClosedByEmployeeId",
                table: "Rentals",
                column: "ClosedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rentals_Employees_CreatedByEmployeeId",
                table: "Rentals",
                column: "CreatedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rentals_RentalStatuses_Status",
                table: "Rentals",
                column: "Status",
                principalTable: "RentalStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_FuelTypes_FuelTypeCode",
                table: "Vehicles",
                column: "FuelTypeCode",
                principalTable: "FuelTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_TransmissionTypes_TransmissionTypeCode",
                table: "Vehicles",
                column: "TransmissionTypeCode",
                principalTable: "TransmissionTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_VehicleStatuses_VehicleStatusCode",
                table: "Vehicles",
                column: "VehicleStatusCode",
                principalTable: "VehicleStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Accounts_AccountId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Damages_DamageStatuses_Status",
                table: "Damages");

            migrationBuilder.DropForeignKey(
                name: "FK_Damages_Employees_ReportedByEmployeeId",
                table: "Damages");

            migrationBuilder.DropForeignKey(
                name: "FK_Damages_Rentals_RentalId_VehicleId",
                table: "Damages");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Accounts_AccountId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_EmployeeRoles_Role",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecords_Employees_PerformedByEmployeeId",
                table: "MaintenanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecords_MaintenanceTypes_MaintenanceTypeCode",
                table: "MaintenanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentDirections_Direction",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentMethods_Method",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentStatuses_Status",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_RentalInspections_Employees_PerformedByEmployeeId",
                table: "RentalInspections");

            migrationBuilder.DropForeignKey(
                name: "FK_RentalInspections_InspectionTypes_Type",
                table: "RentalInspections");

            migrationBuilder.DropForeignKey(
                name: "FK_Rentals_Employees_CanceledByEmployeeId",
                table: "Rentals");

            migrationBuilder.DropForeignKey(
                name: "FK_Rentals_Employees_ClosedByEmployeeId",
                table: "Rentals");

            migrationBuilder.DropForeignKey(
                name: "FK_Rentals_Employees_CreatedByEmployeeId",
                table: "Rentals");

            migrationBuilder.DropForeignKey(
                name: "FK_Rentals_RentalStatuses_Status",
                table: "Rentals");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_FuelTypes_FuelTypeCode",
                table: "Vehicles");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_TransmissionTypes_TransmissionTypeCode",
                table: "Vehicles");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_VehicleStatuses_VehicleStatusCode",
                table: "Vehicles");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "ClientDocuments");

            migrationBuilder.DropTable(
                name: "DamagePhotos");

            migrationBuilder.DropTable(
                name: "DamageStatuses");

            migrationBuilder.DropTable(
                name: "EmployeeRoles");

            migrationBuilder.DropTable(
                name: "InspectionTypes");

            migrationBuilder.DropTable(
                name: "MaintenanceTypes");

            migrationBuilder.DropTable(
                name: "PaymentDirections");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropTable(
                name: "PaymentStatuses");

            migrationBuilder.DropTable(
                name: "RentalStatuses");

            migrationBuilder.DropTable(
                name: "VehiclePhotos");

            migrationBuilder.DropTable(
                name: "VehicleStatuses");

            migrationBuilder.DropTable(
                name: "ClientDocumentTypes");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_VehicleStatusCode_Make_Model",
                table: "Vehicles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Rentals_Id_VehicleId",
                table: "Rentals");

            migrationBuilder.DropIndex(
                name: "IX_Rentals_CanceledByEmployeeId",
                table: "Rentals");

            migrationBuilder.DropIndex(
                name: "IX_Rentals_ClosedByEmployeeId",
                table: "Rentals");

            migrationBuilder.DropIndex(
                name: "IX_Rentals_Status",
                table: "Rentals");

            migrationBuilder.DropIndex(
                name: "IX_RentalInspections_PerformedByEmployeeId",
                table: "RentalInspections");

            migrationBuilder.DropIndex(
                name: "IX_RentalInspections_Type",
                table: "RentalInspections");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Direction",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ExternalTransactionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Method",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Status",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRecords_MaintenanceTypeCode",
                table: "MaintenanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRecords_PerformedByEmployeeId",
                table: "MaintenanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_Employees_AccountId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_Role_FullName",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Damages_RentalId_VehicleId",
                table: "Damages");

            migrationBuilder.DropIndex(
                name: "IX_Damages_ReportedByEmployeeId",
                table: "Damages");

            migrationBuilder.DropIndex(
                name: "IX_Damages_Status",
                table: "Damages");

            migrationBuilder.DropIndex(
                name: "IX_Clients_AccountId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_Phone",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "VehicleStatusCode",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CanceledByEmployeeId",
                table: "Rentals");

            migrationBuilder.DropColumn(
                name: "ClosedByEmployeeId",
                table: "Rentals");

            migrationBuilder.DropColumn(
                name: "PerformedByEmployeeId",
                table: "RentalInspections");

            migrationBuilder.DropColumn(
                name: "ExternalTransactionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MaintenanceTypeCode",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "PerformedByEmployeeId",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "ServiceProviderName",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ReportedByEmployeeId",
                table: "Damages");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "BlacklistReason",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "BlacklistedAtUtc",
                table: "Clients");

            migrationBuilder.RenameColumn(
                name: "TransmissionTypeCode",
                table: "Vehicles",
                newName: "TransmissionType");

            migrationBuilder.RenameColumn(
                name: "FuelTypeCode",
                table: "Vehicles",
                newName: "FuelType");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_TransmissionTypeCode",
                table: "Vehicles",
                newName: "IX_Vehicles_TransmissionType");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_FuelTypeCode",
                table: "Vehicles",
                newName: "IX_Vehicles_FuelType");

            migrationBuilder.RenameColumn(
                name: "CreatedByEmployeeId",
                table: "Rentals",
                newName: "EmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_Rentals_CreatedByEmployeeId_CreatedAtUtc",
                table: "Rentals",
                newName: "IX_Rentals_EmployeeId_CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "Employees",
                newName: "PasswordChangedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsBlacklisted",
                table: "Clients",
                newName: "Blacklisted");

            migrationBuilder.AddColumn<bool>(
                name: "IsBookable",
                table: "Vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "Vehicles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                table: "Employees",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginUtc",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutUntilUtc",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Login",
                table: "Employees",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Employees",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PortalClientId",
                table: "Employees",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "Damages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverLicense",
                table: "Clients",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.AddColumn<string>(
                name: "PassportData",
                table: "Clients",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_IsBookable_Make_Model",
                table: "Vehicles",
                columns: new[] { "IsBookable", "Make", "Model" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rentals_Status_Range",
                table: "Rentals",
                sql: "\"Status\" BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RentalInspections_Type_Range",
                table: "RentalInspections",
                sql: "\"Type\" BETWEEN 1 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Direction_Range",
                table: "Payments",
                sql: "\"Direction\" BETWEEN 1 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Method_Range",
                table: "Payments",
                sql: "\"Method\" BETWEEN 1 AND 2");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Login",
                table: "Employees",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PortalClientId",
                table: "Employees",
                column: "PortalClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Role_IsActive",
                table: "Employees",
                columns: new[] { "Role", "IsActive" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_FailedLoginAttempts_NonNegative",
                table: "Employees",
                sql: "\"FailedLoginAttempts\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_Role_Range",
                table: "Employees",
                sql: "\"Role\" BETWEEN 1 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Damages_Status_Range",
                table: "Damages",
                sql: "\"Status\" BETWEEN 1 AND 3");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_DriverLicense",
                table: "Clients",
                column: "DriverLicense",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_PassportData",
                table: "Clients",
                column: "PassportData",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Phone",
                table: "Clients",
                column: "Phone");

            migrationBuilder.AddForeignKey(
                name: "FK_Damages_Rentals_RentalId",
                table: "Damages",
                column: "RentalId",
                principalTable: "Rentals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Clients_PortalClientId",
                table: "Employees",
                column: "PortalClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rentals_Employees_EmployeeId",
                table: "Rentals",
                column: "EmployeeId",
                principalTable: "Employees",
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
    }
}
