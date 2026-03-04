using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Tau.KeyVault.Data;
using Tau.KeyVault.Models;

namespace Tau.KeyVault.Services;

/// <summary>
/// Manages application-level settings with DB override over appsettings.json defaults.
/// </summary>
public class AppSettingsService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    // Well-known setting keys
    public const string KeyAppTitle = "AppTitle";
    public const string KeyThemeMode = "ThemeMode";
    public const string KeyOfficeColor = "OfficeColor";

    public AppSettingsService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // ── Generic get/set ──────────────────────────────────────────

    /// <summary>
    /// Gets a setting value. DB value takes priority, then appsettings.json, then the provided default.
    /// </summary>
    public async Task<string> GetAsync(string key, string? configPath = null, string defaultValue = "")
    {
        var dbSetting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (dbSetting != null)
            return dbSetting.Value;

        if (configPath != null)
        {
            var configValue = _config[configPath];
            if (!string.IsNullOrEmpty(configValue))
                return configValue;
        }

        return defaultValue;
    }

    /// <summary>
    /// Upserts a setting value to the database.
    /// </summary>
    public async Task SetAsync(string key, string value)
    {
        var existing = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    // ── Typed helpers ────────────────────────────────────────────

    public async Task<string> GetAppTitleAsync()
    {
        return await GetAsync(KeyAppTitle, "AppTitle", "Tau Key Vault");
    }

    public async Task<DesignThemeModes> GetThemeModeAsync()
    {
        var value = await GetAsync(KeyThemeMode, "Theme:Mode", "Light");
        return Enum.TryParse<DesignThemeModes>(value, ignoreCase: true, out var mode)
            ? mode
            : DesignThemeModes.Light;
    }

    public async Task<OfficeColor?> GetOfficeColorAsync()
    {
        var value = await GetAsync(KeyOfficeColor, "Theme:OfficeColor", "Default");
        return Enum.TryParse<OfficeColor>(value, ignoreCase: true, out var color)
            ? color
            : Microsoft.FluentUI.AspNetCore.Components.OfficeColor.Default;
    }

    public async Task SetThemeModeAsync(DesignThemeModes mode)
    {
        await SetAsync(KeyThemeMode, mode.ToString());
    }

    public async Task SetOfficeColorAsync(OfficeColor? color)
    {
        await SetAsync(KeyOfficeColor, (color ?? Microsoft.FluentUI.AspNetCore.Components.OfficeColor.Default).ToString());
    }

    public async Task SetAppTitleAsync(string title)
    {
        await SetAsync(KeyAppTitle, title);
    }
}
