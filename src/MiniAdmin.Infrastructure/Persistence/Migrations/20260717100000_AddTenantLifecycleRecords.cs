using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniAdmin.Infrastructure.Persistence;

#nullable disable

namespace MiniAdmin.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MiniAdminDbContext))]
    [Migration("20260717100000_AddTenantLifecycleRecords")]
    public partial class AddTenantLifecycleRecords : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mini_tenant_lifecycle_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EventType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Source = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OperatorUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    OperatorUserName = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FromStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ToStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreviousExpireAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    NewExpireAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    PreviousPackageId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    NewPackageId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ReminderDays = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeduplicationKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mini_tenant_lifecycle_records", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_mini_tenant_lifecycle_records_DeduplicationKey",
                table: "mini_tenant_lifecycle_records",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mini_tenant_lifecycle_records_TenantId_CreatedAt",
                table: "mini_tenant_lifecycle_records",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "mini_tenant_lifecycle_records");
        }
    }
}
