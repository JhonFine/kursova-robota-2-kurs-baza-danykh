using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CarRental.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAdminWriteAccessGrantsAndVolan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminWriteAccessGrants");

            migrationBuilder.Sql("""
                DELETE FROM "Employees"
                WHERE lower("Login") = 'volan';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminWriteAccessGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovedByEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    RevokedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    TargetEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminWriteAccessGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminWriteAccessGrants_Employees_ApprovedByEmployeeId",
                        column: x => x.ApprovedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminWriteAccessGrants_Employees_RevokedByEmployeeId",
                        column: x => x.RevokedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminWriteAccessGrants_Employees_TargetEmployeeId",
                        column: x => x.TargetEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminWriteAccessGrants_ApprovedByEmployeeId",
                table: "AdminWriteAccessGrants",
                column: "ApprovedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminWriteAccessGrants_RevokedByEmployeeId",
                table: "AdminWriteAccessGrants",
                column: "RevokedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminWriteAccessGrants_TargetEmployeeId_ExpiresAtUtc_Revoke~",
                table: "AdminWriteAccessGrants",
                columns: new[] { "TargetEmployeeId", "ExpiresAtUtc", "RevokedAtUtc" });
        }
    }
}
