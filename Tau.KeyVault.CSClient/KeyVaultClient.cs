using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProtoBuf;
using Tau.KeyVault.Client.Models;

namespace Tau.KeyVault.Client;

/// <summary>
/// Client for the Tau Key Vault REST API. Supports JSON and Protocol Buffers
/// transport with typed helper methods for all key-value data types.
/// </summary>
public class KeyVaultClient
{
    private readonly HttpClient _http;
    private readonly KeyVaultClientOptions _options;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ─────────────────────────────────────────────────────────
    //  Constructors
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new client with the given options and a shared HttpClient
    /// (typically injected via IHttpClientFactory).
    /// </summary>
    public KeyVaultClient(HttpClient httpClient, IOptions<KeyVaultClientOptions> options)
    {
        _options = options.Value;
        _http = httpClient;
        ConfigureClient();
    }

    /// <summary>
    /// Creates a new client with explicit options (useful without DI).
    /// </summary>
    public KeyVaultClient(KeyVaultClientOptions options)
    {
        _options = options;
        _http = new HttpClient();
        ConfigureClient();
    }

    private void ConfigureClient()
    {
        if (!string.IsNullOrEmpty(_options.BaseUrl))
            _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        _http.DefaultRequestHeaders.Add("X-Api-Key", _options.ApiKey);
        _http.Timeout = _options.Timeout;
    }

    // ═══════════════════════════════════════════════════════════
    //  CORE API METHODS (Swagger endpoints)
    // ═══════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────
    //  Keys
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// List all keys for an environment with global fallback.
    /// When no environment is specified, uses the default environment (global if not configured).
    /// </summary>
    public async Task<KeyEntryListResponse> GetAllKeysAsync(
        string? environment = null, bool raw = false, CancellationToken ct = default)
    {
        var env = environment ?? _options.DefaultEnvironment;
        var url = $"api/keys?environment={Uri.EscapeDataString(env)}&raw={raw}";
        return await SendGetAsync<KeyEntryListResponse>(url, ct);
    }

    /// <summary>
    /// List all keys across all environments (no filtering).
    /// </summary>
    public async Task<KeyEntryListResponse> GetAllKeysAllEnvironmentsAsync(CancellationToken ct = default)
    {
        return await SendGetAsync<KeyEntryListResponse>("api/keys/all", ct);
    }

    /// <summary>
    /// Get a single key by name with global fallback.
    /// </summary>
    public async Task<KeyEntryResponse> GetKeyAsync(
        string key, string? environment = null, CancellationToken ct = default)
    {
        var env = environment ?? _options.DefaultEnvironment;
        var url = $"api/keys/{Uri.EscapeDataString(key)}?environment={Uri.EscapeDataString(env)}";
        return await SendGetAsync<KeyEntryResponse>(url, ct);
    }

    /// <summary>
    /// Create or update a key-value pair.
    /// </summary>
    public async Task<KeyEntryResponse> UpsertKeyAsync(
        string key, string value, string? environment = null,
        KeyVaultDataType dataType = KeyVaultDataType.Text,
        bool isSensitive = false, CancellationToken ct = default)
    {
        var env = environment ?? _options.DefaultEnvironment;
        var request = new UpsertRequest
        {
            Key = key,
            Value = value,
            Environment = env,
            DataType = dataType.ToString(),
            IsSensitive = isSensitive
        };
        return await SendPutAsync<UpsertRequest, KeyEntryResponse>("api/keys", request, ct);
    }

    // ─────────────────────────────────────────────────────────
    //  Environments
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// List all known environments.
    /// </summary>
    public async Task<EnvironmentListResponse> GetEnvironmentsAsync(CancellationToken ct = default)
        => await SendGetAsync<EnvironmentListResponse>("api/keys/environments", ct);

    /// <summary>
    /// Delete an environment and all its keys.
    /// </summary>
    public async Task<DeleteEnvironmentResponse> DeleteEnvironmentAsync(
        string environment, CancellationToken ct = default)
    {
        var url = $"api/keys/environments/{Uri.EscapeDataString(environment)}";
        return await SendDeleteAsync<DeleteEnvironmentResponse>(url, ct);
    }

    /// <summary>
    /// Rename an environment.
    /// </summary>
    public async Task<RenameEnvironmentResponse> RenameEnvironmentAsync(
        string environment, string newName, CancellationToken ct = default)
    {
        var url = $"api/keys/environments/{Uri.EscapeDataString(environment)}/rename";
        var request = new RenameRequest { NewName = newName };
        return await SendPutAsync<RenameRequest, RenameEnvironmentResponse>(url, request, ct);
    }

    // ─────────────────────────────────────────────────────────
    //  Export / Import
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Export all keys for an environment.
    /// </summary>
    public async Task<ExportPayloadResponse> ExportAsync(
        string? environment = null, CancellationToken ct = default)
    {
        var env = environment ?? _options.DefaultEnvironment;
        return await SendGetAsync<ExportPayloadResponse>(
            $"api/keys/export?environment={Uri.EscapeDataString(env)}", ct);
    }

    /// <summary>
    /// Import keys into an environment.
    /// </summary>
    public async Task<ImportResultResponse> ImportAsync(
        ImportRequest request, CancellationToken ct = default)
        => await SendPostAsync<ImportRequest, ImportResultResponse>("api/keys/import", request, ct);

    /// <summary>
    /// Download the auto-generated .proto schema file as a string.
    /// </summary>
    public async Task<string> GetProtoSchemaAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/keys/proto", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════
    //  KEY EXISTS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Checks if a key exists in the specified environment (with global fallback).
    /// </summary>
    public async Task<bool> KeyExistsAsync(
        string key, string? environment = null, CancellationToken ct = default)
    {
        try
        {
            await GetKeyAsync(key, environment, ct);
            return true;
        }
        catch (KeyVaultApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  GET OR CREATE (typed, creates with default if missing)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Gets a Text value, creating the key with the default if it does not exist.
    /// </summary>
    public async Task<string> GetOrCreateTextAsync(
        string key, string defaultValue, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
        => await GetOrCreateRawAsync(key, defaultValue, KeyVaultDataType.Text, environment, isSensitive, ct);

    /// <summary>
    /// Gets a Code value (always uppercase), creating the key with the default if it does not exist.
    /// </summary>
    public async Task<string> GetOrCreateCodeAsync(
        string key, string defaultValue, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
        => await GetOrCreateRawAsync(key, defaultValue.ToUpperInvariant(), KeyVaultDataType.Code, environment, isSensitive, ct);

    /// <summary>
    /// Gets a Numeric (decimal) value, creating the key with the default if it does not exist.
    /// </summary>
    public async Task<decimal> GetOrCreateNumericAsync(
        string key, decimal defaultValue, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
    {
        var raw = await GetOrCreateRawAsync(key,
            defaultValue.ToString(CultureInfo.InvariantCulture),
            KeyVaultDataType.Numeric, environment, isSensitive, ct);
        return decimal.Parse(raw, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets a Boolean value, creating the key with the default if it does not exist.
    /// </summary>
    public async Task<bool> GetOrCreateBooleanAsync(
        string key, bool defaultValue, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
    {
        var raw = await GetOrCreateRawAsync(key,
            defaultValue.ToString().ToLowerInvariant(),
            KeyVaultDataType.Boolean, environment, isSensitive, ct);
        return raw.Trim().ToLowerInvariant() is "true" or "1" or "yes";
    }

    /// <summary>
    /// Gets a Date value, creating the key with the default if it does not exist.
    /// </summary>
    public async Task<DateOnly> GetOrCreateDateAsync(
        string key, DateOnly defaultValue, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
    {
        var raw = await GetOrCreateRawAsync(key,
            defaultValue.ToString("yyyy-MM-dd"),
            KeyVaultDataType.Date, environment, isSensitive, ct);
        return DateOnly.Parse(raw, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets a Time value, creating the key with the default if it does not exist.
    /// </summary>
    public async Task<TimeOnly> GetOrCreateTimeAsync(
        string key, TimeOnly defaultValue, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
    {
        var raw = await GetOrCreateRawAsync(key,
            defaultValue.ToString("HH:mm:ss"),
            KeyVaultDataType.Time, environment, isSensitive, ct);
        return TimeOnly.Parse(raw, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets a DateTime value, creating the key with the default if it does not exist.
    /// </summary>
    public async Task<DateTime> GetOrCreateDateTimeAsync(
        string key, DateTime defaultValue, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
    {
        var raw = await GetOrCreateRawAsync(key,
            defaultValue.ToString("yyyy-MM-ddTHH:mm:ss"),
            KeyVaultDataType.DateTime, environment, isSensitive, ct);
        return DateTime.Parse(raw, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets a JSON value as a deserialized object, creating the key with the
    /// serialized default if it does not exist.
    /// </summary>
    public async Task<T> GetOrCreateJsonAsync<T>(
        string key, T defaultValue, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default) where T : class
    {
        var defaultJson = JsonSerializer.Serialize(defaultValue, JsonOpts);
        var raw = await GetOrCreateRawAsync(key, defaultJson, KeyVaultDataType.Json, environment, isSensitive, ct);
        return JsonSerializer.Deserialize<T>(raw, JsonOpts)
               ?? throw new InvalidOperationException($"Failed to deserialize JSON value for key '{key}'.");
    }

    /// <summary>
    /// Gets a CSV value as a list of strings, creating the key with the
    /// default list if it does not exist.
    /// </summary>
    public async Task<List<string>> GetOrCreateCsvAsync(
        string key, IEnumerable<string> defaultValues, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
    {
        var csvString = string.Join(",", defaultValues);
        var raw = await GetOrCreateRawAsync(key, csvString, KeyVaultDataType.Csv, environment, isSensitive, ct);
        return ParseCsv(raw);
    }

    // ═══════════════════════════════════════════════════════════
    //  TYPED GET HELPERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>Gets a Text value.</summary>
    public async Task<string> GetTextAsync(string key, string? environment = null, CancellationToken ct = default)
        => (await GetKeyAsync(key, environment, ct)).Value;

    /// <summary>Gets a Code value (uppercase text).</summary>
    public async Task<string> GetCodeAsync(string key, string? environment = null, CancellationToken ct = default)
        => (await GetKeyAsync(key, environment, ct)).Value;

    /// <summary>Gets a Numeric value as decimal.</summary>
    public async Task<decimal> GetNumericAsync(string key, string? environment = null, CancellationToken ct = default)
        => decimal.Parse((await GetKeyAsync(key, environment, ct)).Value, CultureInfo.InvariantCulture);

    /// <summary>Gets a Boolean value.</summary>
    public async Task<bool> GetBooleanAsync(string key, string? environment = null, CancellationToken ct = default)
    {
        var val = (await GetKeyAsync(key, environment, ct)).Value.Trim().ToLowerInvariant();
        return val is "true" or "1" or "yes";
    }

    /// <summary>Gets a Date value.</summary>
    public async Task<DateOnly> GetDateAsync(string key, string? environment = null, CancellationToken ct = default)
        => DateOnly.Parse((await GetKeyAsync(key, environment, ct)).Value, CultureInfo.InvariantCulture);

    /// <summary>Gets a Time value.</summary>
    public async Task<TimeOnly> GetTimeAsync(string key, string? environment = null, CancellationToken ct = default)
        => TimeOnly.Parse((await GetKeyAsync(key, environment, ct)).Value, CultureInfo.InvariantCulture);

    /// <summary>Gets a DateTime value.</summary>
    public async Task<DateTime> GetDateTimeAsync(string key, string? environment = null, CancellationToken ct = default)
        => DateTime.Parse((await GetKeyAsync(key, environment, ct)).Value, CultureInfo.InvariantCulture);

    /// <summary>Gets a JSON value deserialized to the specified type.</summary>
    public async Task<T> GetJsonAsync<T>(string key, string? environment = null, CancellationToken ct = default) where T : class
    {
        var raw = (await GetKeyAsync(key, environment, ct)).Value;
        return JsonSerializer.Deserialize<T>(raw, JsonOpts)
               ?? throw new InvalidOperationException($"Failed to deserialize JSON value for key '{key}'.");
    }

    /// <summary>Gets a CSV value as a list of strings.</summary>
    public async Task<List<string>> GetCsvAsync(string key, string? environment = null, CancellationToken ct = default)
        => ParseCsv((await GetKeyAsync(key, environment, ct)).Value);

    // ═══════════════════════════════════════════════════════════
    //  TYPED UPDATE HELPERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>Updates a Text value.</summary>
    public Task<KeyEntryResponse> UpdateTextAsync(string key, string value, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
        => UpsertKeyAsync(key, value, environment, KeyVaultDataType.Text, isSensitive, ct);

    /// <summary>Updates a Code value (stored uppercase).</summary>
    public Task<KeyEntryResponse> UpdateCodeAsync(string key, string value, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
        => UpsertKeyAsync(key, value.ToUpperInvariant(), environment, KeyVaultDataType.Code, isSensitive, ct);

    /// <summary>Updates a Numeric value.</summary>
    public Task<KeyEntryResponse> UpdateNumericAsync(string key, decimal value, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
        => UpsertKeyAsync(key, value.ToString(CultureInfo.InvariantCulture), environment, KeyVaultDataType.Numeric, isSensitive, ct);

    /// <summary>Updates a Boolean value.</summary>
    public Task<KeyEntryResponse> UpdateBooleanAsync(string key, bool value, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
        => UpsertKeyAsync(key, value.ToString().ToLowerInvariant(), environment, KeyVaultDataType.Boolean, isSensitive, ct);

    /// <summary>Updates a Date value.</summary>
    public Task<KeyEntryResponse> UpdateDateAsync(string key, DateOnly value, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
        => UpsertKeyAsync(key, value.ToString("yyyy-MM-dd"), environment, KeyVaultDataType.Date, isSensitive, ct);

    /// <summary>Updates a Time value.</summary>
    public Task<KeyEntryResponse> UpdateTimeAsync(string key, TimeOnly value, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
        => UpsertKeyAsync(key, value.ToString("HH:mm:ss"), environment, KeyVaultDataType.Time, isSensitive, ct);

    /// <summary>Updates a DateTime value.</summary>
    public Task<KeyEntryResponse> UpdateDateTimeAsync(string key, DateTime value, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
        => UpsertKeyAsync(key, value.ToString("yyyy-MM-ddTHH:mm:ss"), environment, KeyVaultDataType.DateTime, isSensitive, ct);

    /// <summary>Updates a JSON value by serializing the object.</summary>
    public Task<KeyEntryResponse> UpdateJsonAsync<T>(string key, T value, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default) where T : class
        => UpsertKeyAsync(key, JsonSerializer.Serialize(value, JsonOpts), environment, KeyVaultDataType.Json, isSensitive, ct);

    /// <summary>Updates a CSV value from a list of strings.</summary>
    public Task<KeyEntryResponse> UpdateCsvAsync(string key, IEnumerable<string> values, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
        => UpsertKeyAsync(key, string.Join(",", values), environment, KeyVaultDataType.Csv, isSensitive, ct);

    // ═══════════════════════════════════════════════════════════
    //  CSV LIST MANAGEMENT HELPERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Adds an item to a CSV list key. If the key does not exist, it is created.
    /// Returns the updated list.
    /// </summary>
    public async Task<List<string>> CsvAddAsync(
        string key, string item, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
    {
        var list = await GetOrCreateCsvAsync(key, Array.Empty<string>(), environment, isSensitive, ct);
        list.Add(item);
        await UpdateCsvAsync(key, list, environment, isSensitive, ct);
        return list;
    }

    /// <summary>
    /// Removes the first occurrence of an item from a CSV list key.
    /// Returns the updated list.
    /// </summary>
    public async Task<List<string>> CsvRemoveAsync(
        string key, string item, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
    {
        var list = await GetCsvAsync(key, environment, ct);
        list.Remove(item);
        await UpdateCsvAsync(key, list, environment, isSensitive, ct);
        return list;
    }

    /// <summary>
    /// Checks if a CSV list key contains an item.
    /// </summary>
    public async Task<bool> CsvContainsAsync(
        string key, string item, string? environment = null, CancellationToken ct = default)
    {
        var list = await GetCsvAsync(key, environment, ct);
        return list.Contains(item);
    }

    /// <summary>
    /// Replaces all items in a CSV list key that match oldItem with newItem.
    /// Returns the updated list.
    /// </summary>
    public async Task<List<string>> CsvReplaceAsync(
        string key, string oldItem, string newItem, string? environment = null,
        bool isSensitive = false, CancellationToken ct = default)
    {
        var list = await GetCsvAsync(key, environment, ct);
        for (var i = 0; i < list.Count; i++)
            if (list[i] == oldItem)
                list[i] = newItem;
        await UpdateCsvAsync(key, list, environment, isSensitive, ct);
        return list;
    }

    // ═══════════════════════════════════════════════════════════
    //  INTERNAL: Transport (JSON / Protobuf / Failover)
    // ═══════════════════════════════════════════════════════════

    private async Task<string> GetOrCreateRawAsync(
        string key, string defaultValue, KeyVaultDataType dataType,
        string? environment, bool isSensitive, CancellationToken ct)
    {
        try
        {
            var entry = await GetKeyAsync(key, environment, ct);
            return entry.Value;
        }
        catch (KeyVaultApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            var created = await UpsertKeyAsync(key, defaultValue, environment, dataType, isSensitive, ct);
            return created.Value;
        }
    }

    private async Task<TResponse> SendGetAsync<TResponse>(string url, CancellationToken ct)
        where TResponse : class, new()
    {
        return _options.Transport switch
        {
            KeyVaultTransport.Protobuf => await ExecuteGetAsync<TResponse>(url, useProtobuf: true, ct),
            KeyVaultTransport.ProtobufWithApiFallback => await ExecuteWithFallbackGetAsync<TResponse>(url, ct),
            _ => await ExecuteGetAsync<TResponse>(url, useProtobuf: false, ct)
        };
    }

    private async Task<TResponse> SendPutAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct)
        where TRequest : class where TResponse : class, new()
    {
        return _options.Transport switch
        {
            KeyVaultTransport.Protobuf => await ExecutePutAsync<TRequest, TResponse>(url, body, useProtobuf: true, ct),
            KeyVaultTransport.ProtobufWithApiFallback => await ExecuteWithFallbackPutAsync<TRequest, TResponse>(url, body, ct),
            _ => await ExecutePutAsync<TRequest, TResponse>(url, body, useProtobuf: false, ct)
        };
    }

    private async Task<TResponse> SendPostAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct)
        where TRequest : class where TResponse : class, new()
    {
        return _options.Transport switch
        {
            KeyVaultTransport.Protobuf => await ExecutePostAsync<TRequest, TResponse>(url, body, useProtobuf: true, ct),
            KeyVaultTransport.ProtobufWithApiFallback => await ExecuteWithFallbackPostAsync<TRequest, TResponse>(url, body, ct),
            _ => await ExecutePostAsync<TRequest, TResponse>(url, body, useProtobuf: false, ct)
        };
    }

    private async Task<TResponse> SendDeleteAsync<TResponse>(string url, CancellationToken ct)
        where TResponse : class, new()
    {
        return _options.Transport switch
        {
            KeyVaultTransport.Protobuf => await ExecuteDeleteAsync<TResponse>(url, useProtobuf: true, ct),
            KeyVaultTransport.ProtobufWithApiFallback => await ExecuteWithFallbackDeleteAsync<TResponse>(url, ct),
            _ => await ExecuteDeleteAsync<TResponse>(url, useProtobuf: false, ct)
        };
    }

    // ── Failover wrappers ─────────────────────────────────────

    private async Task<TResponse> ExecuteWithFallbackGetAsync<TResponse>(string url, CancellationToken ct)
        where TResponse : class, new()
    {
        try { return await ExecuteGetAsync<TResponse>(url, useProtobuf: true, ct); }
        catch { return await ExecuteGetAsync<TResponse>(url, useProtobuf: false, ct); }
    }

    private async Task<TResponse> ExecuteWithFallbackPutAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct)
        where TRequest : class where TResponse : class, new()
    {
        try { return await ExecutePutAsync<TRequest, TResponse>(url, body, useProtobuf: true, ct); }
        catch { return await ExecutePutAsync<TRequest, TResponse>(url, body, useProtobuf: false, ct); }
    }

    private async Task<TResponse> ExecuteWithFallbackPostAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct)
        where TRequest : class where TResponse : class, new()
    {
        try { return await ExecutePostAsync<TRequest, TResponse>(url, body, useProtobuf: true, ct); }
        catch { return await ExecutePostAsync<TRequest, TResponse>(url, body, useProtobuf: false, ct); }
    }

    private async Task<TResponse> ExecuteWithFallbackDeleteAsync<TResponse>(string url, CancellationToken ct)
        where TResponse : class, new()
    {
        try { return await ExecuteDeleteAsync<TResponse>(url, useProtobuf: true, ct); }
        catch { return await ExecuteDeleteAsync<TResponse>(url, useProtobuf: false, ct); }
    }

    // ── Core HTTP methods ─────────────────────────────────────

    private async Task<TResponse> ExecuteGetAsync<TResponse>(string url, bool useProtobuf, CancellationToken ct)
        where TResponse : class, new()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        SetAcceptHeader(request, useProtobuf);
        return await SendAndDeserializeAsync<TResponse>(request, useProtobuf, ct);
    }

    private async Task<TResponse> ExecutePutAsync<TRequest, TResponse>(string url, TRequest body, bool useProtobuf, CancellationToken ct)
        where TRequest : class where TResponse : class, new()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        SetAcceptHeader(request, useProtobuf);
        request.Content = CreateContent(body, useProtobuf);
        return await SendAndDeserializeAsync<TResponse>(request, useProtobuf, ct);
    }

    private async Task<TResponse> ExecutePostAsync<TRequest, TResponse>(string url, TRequest body, bool useProtobuf, CancellationToken ct)
        where TRequest : class where TResponse : class, new()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        SetAcceptHeader(request, useProtobuf);
        request.Content = CreateContent(body, useProtobuf);
        return await SendAndDeserializeAsync<TResponse>(request, useProtobuf, ct);
    }

    private async Task<TResponse> ExecuteDeleteAsync<TResponse>(string url, bool useProtobuf, CancellationToken ct)
        where TResponse : class, new()
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        SetAcceptHeader(request, useProtobuf);
        return await SendAndDeserializeAsync<TResponse>(request, useProtobuf, ct);
    }

    // ── Serialization helpers ─────────────────────────────────

    private static void SetAcceptHeader(HttpRequestMessage request, bool useProtobuf)
    {
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(
            useProtobuf ? "application/x-protobuf" : "application/json"));
    }

    private static HttpContent CreateContent<T>(T body, bool useProtobuf) where T : class
    {
        if (useProtobuf)
        {
            using var ms = new MemoryStream();
            Serializer.Serialize(ms, body);
            var content = new ByteArrayContent(ms.ToArray());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
            return content;
        }
        else
        {
            var json = JsonSerializer.Serialize(body, JsonOpts);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }
    }

    private async Task<TResponse> SendAndDeserializeAsync<TResponse>(
        HttpRequestMessage request, bool useProtobuf, CancellationToken ct)
        where TResponse : class, new()
    {
        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            string? apiError = null;
            try
            {
                var errorJson = await response.Content.ReadAsStringAsync(ct);
                var err = JsonSerializer.Deserialize<ErrorResponse>(errorJson, JsonOpts);
                apiError = err?.Error;
            }
            catch { /* swallow deserialization errors */ }

            throw new KeyVaultApiException(
                response.StatusCode,
                apiError,
                apiError ?? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        if (useProtobuf)
        {
            var stream = await response.Content.ReadAsStreamAsync(ct);
            return Serializer.Deserialize<TResponse>(stream);
        }
        else
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<TResponse>(json, JsonOpts)
                   ?? new TResponse();
        }
    }

    // ── CSV helpers ───────────────────────────────────────────

    private static List<string> ParseCsv(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        return value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }
}
