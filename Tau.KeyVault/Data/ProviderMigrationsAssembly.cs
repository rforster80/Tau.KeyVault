using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace Tau.KeyVault.Data;

/// <summary>
/// Scopes EF Core migration discovery to the active database provider.
/// <para>
/// EF discovers every <c>[Migration]</c> type in the assembly and would otherwise try to
/// apply the SQLite migrations to Postgres (and vice versa). The two sets are not
/// interchangeable: the SQLite set uses <c>TEXT</c>/<c>INTEGER</c> column types, the
/// <c>Sqlite:Autoincrement</c> annotation, and one migration issues a raw
/// <c>PRAGMA foreign_keys</c> that Postgres rejects outright.
/// </para>
/// <para>
/// Separation is by namespace: anything under <see cref="PostgresNamespace"/> belongs to
/// Postgres, everything else to SQLite. Postgres is treated as a fresh-install target and
/// so has a single consolidated InitialCreate rather than a replay of the SQLite history.
/// </para>
/// </summary>
public class ProviderMigrationsAssembly : MigrationsAssembly
{
    /// <summary>Namespace holding the Postgres-only migration set.</summary>
    public const string PostgresNamespace = "Tau.KeyVault.Data.Migrations.Postgres";

    private readonly bool _isPostgres;
    private readonly Type _contextType;
    private readonly Lazy<IReadOnlyDictionary<string, TypeInfo>> _filtered;
    private readonly Lazy<ModelSnapshot?> _snapshot;

    public ProviderMigrationsAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions options,
        IMigrationsIdGenerator idGenerator,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger,
        IDatabaseProvider databaseProvider)
        : base(currentContext, options, idGenerator, logger)
    {
        _isPostgres = databaseProvider.Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);
        _contextType = currentContext.Context.GetType();

        _filtered = new Lazy<IReadOnlyDictionary<string, TypeInfo>>(() =>
            base.Migrations
                .Where(m => BelongsToActiveProvider(m.Value))
                .ToDictionary(m => m.Key, m => m.Value));

        _snapshot = new Lazy<ModelSnapshot?>(FindSnapshotForActiveProvider);
    }

    public override IReadOnlyDictionary<string, TypeInfo> Migrations => _filtered.Value;

    /// <summary>
    /// The snapshot for the active provider. Each provider needs its own: EF compares the
    /// live model against this at <c>MigrateAsync()</c> and throws
    /// <c>PendingModelChangesWarning</c> on a mismatch, so handing Postgres the SQLite
    /// snapshot (or vice versa) fails startup outright.
    /// </summary>
    public override ModelSnapshot? ModelSnapshot => _snapshot.Value;

    private ModelSnapshot? FindSnapshotForActiveProvider()
    {
        var match = Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false }
                        && typeof(ModelSnapshot).IsAssignableFrom(t)
                        && t.GetConstructor(Type.EmptyTypes) is not null)
            .Where(t => t.GetCustomAttribute<DbContextAttribute>()?.ContextType == _contextType)
            .Select(t => t.GetTypeInfo())
            .FirstOrDefault(BelongsToActiveProvider);

        return match is null ? null : (ModelSnapshot?)Activator.CreateInstance(match.AsType());
    }

    private bool BelongsToActiveProvider(TypeInfo migration)
    {
        var isPostgresMigration =
            migration.Namespace?.StartsWith(PostgresNamespace, StringComparison.Ordinal) == true;

        return _isPostgres ? isPostgresMigration : !isPostgresMigration;
    }
}
