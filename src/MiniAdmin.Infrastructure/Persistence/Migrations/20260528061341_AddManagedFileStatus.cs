using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedFileStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "mini_files",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Normal")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "mini_files");
        }
    }
}
