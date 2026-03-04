using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Tau.KeyVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSensitive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSensitive",
                table: "KeyEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSensitive",
                table: "KeyEntries");
        }
    }
}
