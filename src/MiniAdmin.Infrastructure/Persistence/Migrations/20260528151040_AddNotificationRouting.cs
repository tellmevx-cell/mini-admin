using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "mini_users",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "EmailEnabled",
                table: "mini_alert_rules",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "mini_alert_rule_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AlertRuleId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RecipientType = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecipientId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mini_alert_rule_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mini_alert_rule_recipients_mini_alert_rules_AlertRuleId",
                        column: x => x.AlertRuleId,
                        principalTable: "mini_alert_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mini_notification_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Channel = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RecipientAddress = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mini_notification_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mini_notification_deliveries_mini_users_UserId",
                        column: x => x.UserId,
                        principalTable: "mini_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_mini_alert_rule_recipients_AlertRuleId_RecipientType_Recipie~",
                table: "mini_alert_rule_recipients",
                columns: new[] { "AlertRuleId", "RecipientType", "RecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mini_notification_deliveries_Channel_SourceType_SourceId_Use~",
                table: "mini_notification_deliveries",
                columns: new[] { "Channel", "SourceType", "SourceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mini_notification_deliveries_CreatedAt",
                table: "mini_notification_deliveries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_mini_notification_deliveries_UserId",
                table: "mini_notification_deliveries",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mini_alert_rule_recipients");

            migrationBuilder.DropTable(
                name: "mini_notification_deliveries");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "mini_users");

            migrationBuilder.DropColumn(
                name: "EmailEnabled",
                table: "mini_alert_rules");
        }
    }
}
