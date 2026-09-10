using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tau.KeyVault.Data.Migrations.Postgres
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Environment = table.Column<string>(type: "text", nullable: false),
                    BootstrapServers = table.Column<string>(type: "text", nullable: false),
                    Topic = table.Column<string>(type: "text", nullable: false),
                    LowercaseEnvironment = table.Column<bool>(type: "boolean", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: true),
                    SecurityProtocol = table.Column<int>(type: "integer", nullable: false),
                    SaslMechanism = table.Column<int>(type: "integer", nullable: false),
                    SaslUsername = table.Column<string>(type: "text", nullable: true),
                    SaslPassword = table.Column<string>(type: "text", nullable: true),
                    SslCaLocation = table.Column<string>(type: "text", nullable: true),
                    SslCertificateLocation = table.Column<string>(type: "text", nullable: true),
                    SslKeyLocation = table.Column<string>(type: "text", nullable: true),
                    SslKeyPassword = table.Column<string>(type: "text", nullable: true),
                    EnableSslCertificateVerification = table.Column<bool>(type: "boolean", nullable: false),
                    SaslKerberosServiceName = table.Column<string>(type: "text", nullable: true),
                    SaslKerberosPrincipal = table.Column<string>(type: "text", nullable: true),
                    SaslKerberosKeytab = table.Column<string>(type: "text", nullable: true),
                    OauthBearerClientId = table.Column<string>(type: "text", nullable: true),
                    OauthBearerClientSecret = table.Column<string>(type: "text", nullable: true),
                    OauthBearerTokenEndpointUrl = table.Column<string>(type: "text", nullable: true),
                    OauthBearerScope = table.Column<string>(type: "text", nullable: true),
                    OauthBearerExtensions = table.Column<string>(type: "text", nullable: true),
                    Acks = table.Column<int>(type: "integer", nullable: false),
                    MessageTimeoutMs = table.Column<int>(type: "integer", nullable: false),
                    RequestTimeoutMs = table.Column<int>(type: "integer", nullable: false),
                    EnableIdempotence = table.Column<bool>(type: "boolean", nullable: false),
                    CompressionType = table.Column<string>(type: "text", nullable: true),
                    AdditionalConfig = table.Column<string>(type: "text", nullable: true)
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
