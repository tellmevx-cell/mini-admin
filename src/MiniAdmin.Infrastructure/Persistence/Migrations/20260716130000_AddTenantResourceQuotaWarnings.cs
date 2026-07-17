using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniAdmin.Infrastructure.Persistence;

#nullable disable

namespace MiniAdmin.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MiniAdminDbContext))]
    [Migration("20260716130000_AddTenantResourceQuotaWarnings")]
    public partial class AddTenantResourceQuotaWarnings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mini_tenant_resource_quota_warnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ResourceType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UsedValue = table.Column<long>(type: "bigint", nullable: false),
                    LimitValue = table.Column<long>(type: "bigint", nullable: false),
                    NotificationSequence = table.Column<int>(type: "int", nullable: false),
                    LastNotifiedStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastNotifiedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mini_tenant_resource_quota_warnings", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_mini_tenant_resource_quota_warnings_LastCheckedAt",
                table: "mini_tenant_resource_quota_warnings",
                column: "LastCheckedAt");

            migrationBuilder.CreateIndex(
                name: "IX_mini_tenant_resource_quota_warnings_TenantId_ResourceType",
                table: "mini_tenant_resource_quota_warnings",
                columns: new[] { "TenantId", "ResourceType" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mini_tenant_resource_quota_warnings");
        }
    }
}
