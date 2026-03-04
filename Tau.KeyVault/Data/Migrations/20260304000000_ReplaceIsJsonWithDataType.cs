using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Tau.KeyVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIsJsonWithDataType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new DataType column (int, default 0 = Text)
            migrationBuilder.AddColumn<int>(
                name: "DataType",
                table: "KeyEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Migrate existing data: IsJson=true → DataType=7 (Json)
            migrationBuilder.Sql("UPDATE KeyEntries SET DataType = 7 WHERE IsJson = 1");

            // Drop old IsJson column
            migrationBuilder.DropColumn(
                name: "IsJson",
                table: "KeyEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add back IsJson column
            migrationBuilder.AddColumn<bool>(
                name: "IsJson",
                table: "KeyEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Restore data: DataType=7 (Json) → IsJson=true
            migrationBuilder.Sql("UPDATE KeyEntries SET IsJson = 1 WHERE DataType = 7");

            // Drop DataType column
            migrationBuilder.DropColumn(
                name: "DataType",
                table: "KeyEntries");
        }
    }
}
