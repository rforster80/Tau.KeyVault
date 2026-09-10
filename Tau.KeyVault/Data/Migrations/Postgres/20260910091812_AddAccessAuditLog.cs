using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tau.KeyVault.Data.Migrations.Postgres
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Environment = table.Column<string>(type: "text", nullable: false),
                    ActorType = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    ItemCount = table.Column<int>(type: "integer", nullable: false)
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
