using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tau.KeyVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    ActorType = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: true),
                    ItemCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_Action",
                table: "AccessAuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_ActorId",
                table: "AccessAuditLogs",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_Key_Environment",
                table: "AccessAuditLogs",
                columns: new[] { "Key", "Environment" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_Timestamp",
                table: "AccessAuditLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessAuditLogs");
        }
    }
}
