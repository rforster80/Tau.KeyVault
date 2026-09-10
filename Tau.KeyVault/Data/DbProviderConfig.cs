using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Tau.KeyVault.Data;

/// <summary>
/// Single place that decides which database provider the app talks to, shared by the
/// request-scoped <see cref="AppDbContext"/> registration and by the audit writer, which
/// needs an independent connection of its own (see <c>AuditService</c>).
/// </summary>
public static class DbProviderConfig
{
    public const string UseSqliteKey = "UseSqlite";
    public const string SqliteConnectionName = "DefaultConnection";
    public const string PostgresConnectionName = "PostgresConnection";

    /// <summary>SQLite unless <c>UseSqlite</c> is explicitly false.</summary>
    public static bool UseSqlite(IConfiguration config) =>
        config.GetValue<bool?>(UseSqliteKey) ?? true;

    public static void Configure(DbContextOptionsBuilder options, IConfiguration config)
    {
        if (UseSqlite(config))
        {
            options.UseSqlite(config.GetConnectionString(SqliteConnectionName)
                              ?? "Data Source=keyvault.db");
        }
        else
        {
            var postgres = config.GetConnectionString(PostgresConnectionName);
            if (string.IsNullOrWhiteSpace(postgres))
                throw new InvalidOperationException(
                    $"{UseSqliteKey} is false but ConnectionStrings:{PostgresConnectionName} is not set in appsettings.json.");

            options.UseNpgsql(postgres);
        }

        // Each provider has its own migration set and snapshot; see ProviderMigrationsAssembly.
        options.ReplaceService<IMigrationsAssembly, ProviderMigrationsAssembly>();
    }

    /// <summary>Builds standalone options for a context that must not share the request's connection.</summary>
    public static DbContextOptions<AppDbContext> BuildStandaloneOptions(IConfiguration config)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        Configure(builder, config);
        return builder.Options;
    }
}
