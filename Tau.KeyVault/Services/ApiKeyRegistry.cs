namespace Tau.KeyVault.Services;

/// <summary>A configured API key and the name the audit log records for it.</summary>
public readonly record struct ApiKeyEntry(string Name, string Key);

/// <summary>
/// Reads the <c>ApiKeys</c> configuration section, which accepts two interchangeable shapes:
/// <code>
/// "ApiKeys": [
///   "PLAIN-STRING-KEY",                              // legacy — audited as "#0"
///   { "Name": "adapter-prod", "Key": "SECRET" }      // named — audited as "adapter-prod"
/// ]
/// </code>
/// The name exists so the audit trail can say <em>which</em> credential acted without ever
/// storing the credential itself.
/// </summary>
public static class ApiKeyRegistry
{
    public const string ConfigSection = "ApiKeys";

    public static IReadOnlyList<ApiKeyEntry> Load(IConfiguration config)
    {
        var entries = new List<ApiKeyEntry>();
        var index = 0;

        foreach (var child in config.GetSection(ConfigSection).GetChildren())
        {
            // A plain string element surfaces as a value; an object element has children instead.
            if (child.Value is not null)
            {
                if (!string.IsNullOrWhiteSpace(child.Value))
                    entries.Add(new ApiKeyEntry($"#{index}", child.Value));
            }
            else
            {
                var key = child["Key"];
                if (!string.IsNullOrWhiteSpace(key))
                {
                    var name = child["Name"];
                    entries.Add(new ApiKeyEntry(
                        string.IsNullOrWhiteSpace(name) ? $"#{index}" : name.Trim(), key));
                }
            }

            index++;
        }

        return entries;
    }

    /// <summary>
    /// Resolves a presented key to its configured name, or null when it matches nothing.
    /// Comparison is constant-time per candidate so a rejected key does not leak its prefix.
    /// </summary>
    public static string? ResolveName(IReadOnlyList<ApiKeyEntry> entries, string presented)
    {
        string? matched = null;

        foreach (var entry in entries)
        {
            if (FixedTimeEquals(entry.Key, presented))
                matched ??= entry.Name;
        }

        return matched;
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var x = System.Text.Encoding.UTF8.GetBytes(a);
        var y = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(x, y);
    }
}
