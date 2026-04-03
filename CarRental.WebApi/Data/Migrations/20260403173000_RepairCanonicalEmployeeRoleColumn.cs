using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.WebApi.Data.Migrations
{
    [DbContext(typeof(RentalDbContext))]
    [Migration("20260403173000_RepairCanonicalEmployeeRoleColumn")]
    public partial class RepairCanonicalEmployeeRoleColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'Employees' AND column_name = 'Role')
                       AND NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'Employees' AND column_name = 'RoleId') THEN
                        ALTER TABLE "Employees" RENAME COLUMN "Role" TO "RoleId";
                    END IF;
                END $$;

                ALTER INDEX IF EXISTS "IX_Employees_Role_FullName" RENAME TO "IX_Employees_RoleId_FullName";

                ALTER TABLE "Employees" DROP CONSTRAINT IF EXISTS "FK_Employees_EmployeeRoles_Role";

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_Employees_EmployeeRoles_RoleId') THEN
                        ALTER TABLE "Employees"
                            ADD CONSTRAINT "FK_Employees_EmployeeRoles_RoleId"
                            FOREIGN KEY ("RoleId") REFERENCES "EmployeeRoles" ("Id") ON DELETE RESTRICT;
                    END IF;
                END $$;

                CREATE INDEX IF NOT EXISTS "IX_Employees_RoleId_FullName"
                    ON "Employees" ("RoleId", "FullName");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
