using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Tau.KeyVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NatsConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    ServerUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Queue = table.Column<string>(type: "TEXT", nullable: false),
                    LowercaseEnvironment = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NatsConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    LowercaseEnvironment = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NatsConfigs_Environment",
                table: "NatsConfigs",
                column: "Environment");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookConfigs_Environment",
                table: "WebhookConfigs",
                column: "Environment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "NatsConfigs");
            migrationBuilder.DropTable(name: "WebhookConfigs");
        }
    }
}
