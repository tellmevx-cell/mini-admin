using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniAdmin.Infrastructure.Persistence.Migrations
{
    public partial class AddProductionReliability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastHeartbeatAt",
                table: "mini_scheduled_jobs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                table: "mini_scheduled_jobs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                table: "mini_scheduled_jobs",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseToken",
                table: "mini_scheduled_jobs",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "mini_inbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MessageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConsumerName = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mini_inbox_messages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mini_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EventType = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Payload = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CorrelationId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LeaseToken = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LeaseOwner = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    LastError = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mini_outbox_messages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_mini_scheduled_jobs_LeaseExpiresAt",
                table: "mini_scheduled_jobs",
                column: "LeaseExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_mini_inbox_messages_MessageId_ConsumerName",
                table: "mini_inbox_messages",
                columns: new[] { "MessageId", "ConsumerName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mini_inbox_messages_ProcessedAt",
                table: "mini_inbox_messages",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_mini_outbox_messages_CreatedAt",
                table: "mini_outbox_messages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_mini_outbox_messages_LeaseExpiresAt",
                table: "mini_outbox_messages",
                column: "LeaseExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_mini_outbox_messages_Status_NextAttemptAt",
                table: "mini_outbox_messages",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "mini_inbox_messages");
            migrationBuilder.DropTable(name: "mini_outbox_messages");
            migrationBuilder.DropIndex(
                name: "IX_mini_scheduled_jobs_LeaseExpiresAt",
                table: "mini_scheduled_jobs");
            migrationBuilder.DropColumn(name: "LastHeartbeatAt", table: "mini_scheduled_jobs");
            migrationBuilder.DropColumn(name: "LeaseExpiresAt", table: "mini_scheduled_jobs");
            migrationBuilder.DropColumn(name: "LeaseOwner", table: "mini_scheduled_jobs");
            migrationBuilder.DropColumn(name: "LeaseToken", table: "mini_scheduled_jobs");
        }
    }
}
