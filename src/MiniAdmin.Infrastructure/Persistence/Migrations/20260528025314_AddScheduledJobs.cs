using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledJobs : Migration
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
                name: "mini_scheduled_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    JobKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IntervalSeconds = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastMessage = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastRunAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    NextRunAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mini_scheduled_jobs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mini_scheduled_job_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    JobId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    JobKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JobName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TriggerType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Message = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    ElapsedMilliseconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mini_scheduled_job_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mini_scheduled_job_logs_mini_scheduled_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "mini_scheduled_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_mini_scheduled_job_logs_JobId",
                table: "mini_scheduled_job_logs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_mini_scheduled_job_logs_StartedAt",
                table: "mini_scheduled_job_logs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_mini_scheduled_jobs_JobKey",
                table: "mini_scheduled_jobs",
                column: "JobKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mini_scheduled_jobs_NextRunAt",
                table: "mini_scheduled_jobs",
                column: "NextRunAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mini_scheduled_job_logs");

            migrationBuilder.DropTable(
                name: "mini_scheduled_jobs");

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
