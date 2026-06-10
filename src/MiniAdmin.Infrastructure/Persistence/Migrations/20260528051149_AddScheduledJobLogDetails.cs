using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledJobLogDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "mini_login_logs",
                type: "char(64)",
                maxLength: 64,
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(string),
                oldType: "char(64)",
                oldMaxLength: 64,
                oldNullable: true)
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mini_scheduled_job_log_details",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LogId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    JobId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    JobKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DetailType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StorageProvider = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoragePath = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Message = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mini_scheduled_job_log_details", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mini_scheduled_job_log_details_mini_scheduled_job_logs_LogId",
                        column: x => x.LogId,
                        principalTable: "mini_scheduled_job_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_mini_scheduled_job_log_details_JobId",
                table: "mini_scheduled_job_log_details",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_mini_scheduled_job_log_details_LogId",
                table: "mini_scheduled_job_log_details",
                column: "LogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mini_scheduled_job_log_details");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "mini_login_logs",
                type: "char(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "char(64)",
                oldMaxLength: 64,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");
        }
    }
}
