using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniAdmin.Infrastructure.Persistence;

#nullable disable

namespace MiniAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MiniAdminDbContext))]
    [Migration("20260529024500_AddOnlineUserSessions")]
    public partial class AddOnlineUserSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "mini_online_users",
                type: "char(36)",
                maxLength: 36,
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.Sql("""
                UPDATE `mini_online_users`
                SET `SessionId` = `UserId`
                WHERE `SessionId` IS NULL OR `SessionId` = ''
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "SessionId",
                table: "mini_online_users",
                type: "char(36)",
                maxLength: 36,
                nullable: false,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldMaxLength: 36,
                oldNullable: true,
                oldCollation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "BrowserName",
                table: "mini_online_users",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeviceName",
                table: "mini_online_users",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.DropPrimaryKey(
                name: "PK_mini_online_users",
                table: "mini_online_users");

            migrationBuilder.AddPrimaryKey(
                name: "PK_mini_online_users",
                table: "mini_online_users",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_mini_online_users_UserId",
                table: "mini_online_users",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_mini_online_users_UserId",
                table: "mini_online_users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_mini_online_users",
                table: "mini_online_users");

            migrationBuilder.DropColumn(
                name: "BrowserName",
                table: "mini_online_users");

            migrationBuilder.DropColumn(
                name: "DeviceName",
                table: "mini_online_users");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "mini_online_users");

            migrationBuilder.AddPrimaryKey(
                name: "PK_mini_online_users",
                table: "mini_online_users",
                column: "UserId");
        }
    }
}
