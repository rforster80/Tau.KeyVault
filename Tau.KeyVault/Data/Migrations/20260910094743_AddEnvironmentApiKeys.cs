using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tau.KeyVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvironmentApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnvironmentApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastRotatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentApiKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentApiKeys_Environment",
                table: "EnvironmentApiKeys",
                column: "Environment");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentApiKeys_KeyHash",
                table: "EnvironmentApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentApiKeys_Name",
                table: "EnvironmentApiKeys",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnvironmentApiKeys");
        }
    }
}
