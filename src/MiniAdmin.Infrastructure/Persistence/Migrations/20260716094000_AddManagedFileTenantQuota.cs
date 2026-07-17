using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniAdmin.Infrastructure.Persistence;

#nullable disable

namespace MiniAdmin.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MiniAdminDbContext))]
    [Migration("20260716094000_AddManagedFileTenantQuota")]
    public partial class AddManagedFileTenantQuota : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "mini_files",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_mini_files_TenantId",
                table: "mini_files",
                column: "TenantId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_mini_files_TenantId",
                table: "mini_files");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "mini_files");
        }
    }
}
