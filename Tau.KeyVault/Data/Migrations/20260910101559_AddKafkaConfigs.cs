using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tau.KeyVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKafkaConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KafkaConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    BootstrapServers = table.Column<string>(type: "TEXT", nullable: false),
                    Topic = table.Column<string>(type: "TEXT", nullable: false),
                    LowercaseEnvironment = table.Column<bool>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityProtocol = table.Column<int>(type: "INTEGER", nullable: false),
                    SaslMechanism = table.Column<int>(type: "INTEGER", nullable: false),
                    SaslUsername = table.Column<string>(type: "TEXT", nullable: true),
                    SaslPassword = table.Column<string>(type: "TEXT", nullable: true),
                    SslCaLocation = table.Column<string>(type: "TEXT", nullable: true),
                    SslCertificateLocation = table.Column<string>(type: "TEXT", nullable: true),
                    SslKeyLocation = table.Column<string>(type: "TEXT", nullable: true),
                    SslKeyPassword = table.Column<string>(type: "TEXT", nullable: true),
                    EnableSslCertificateVerification = table.Column<bool>(type: "INTEGER", nullable: false),
                    SaslKerberosServiceName = table.Column<string>(type: "TEXT", nullable: true),
                    SaslKerberosPrincipal = table.Column<string>(type: "TEXT", nullable: true),
                    SaslKerberosKeytab = table.Column<string>(type: "TEXT", nullable: true),
                    OauthBearerClientId = table.Column<string>(type: "TEXT", nullable: true),
                    OauthBearerClientSecret = table.Column<string>(type: "TEXT", nullable: true),
                    OauthBearerTokenEndpointUrl = table.Column<string>(type: "TEXT", nullable: true),
                    OauthBearerScope = table.Column<string>(type: "TEXT", nullable: true),
                    OauthBearerExtensions = table.Column<string>(type: "TEXT", nullable: true),
                    Acks = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageTimeoutMs = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestTimeoutMs = table.Column<int>(type: "INTEGER", nullable: false),
                    EnableIdempotence = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompressionType = table.Column<string>(type: "TEXT", nullable: true),
                    AdditionalConfig = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KafkaConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KafkaConfigs_Environment",
                table: "KafkaConfigs",
                column: "Environment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KafkaConfigs");
        }
    }
}
