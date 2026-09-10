using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.OpenApi.Models;
using Tau.KeyVault;
using Tau.KeyVault.Components;
using Tau.KeyVault.Data;
using Tau.KeyVault.Formatters;
using Tau.KeyVault.Middleware;
using Tau.KeyVault.Models;
using Tau.KeyVault.Services;

var builder = WebApplication.CreateBuilder(args);

// EF Core — SQLite by default (zero-dependency docker setup), Postgres for enterprise use.
// Flip with "UseSqlite": false in appsettings.json and set ConnectionStrings:PostgresConnection.
builder.Services.AddDbContext<AppDbContext>(options =>
    DbProviderConfig.Configure(options, builder.Configuration));

// Services
builder.Services.AddHttpContextAccessor();
// Singleton: holds provider options and opens its own connection per write so audit rows
// survive a rollback of the operation they describe (ADR-045).
builder.Services.AddSingleton<AuditService>();
builder.Services.AddScoped<AuditActorAccessor>();
builder.Services.AddScoped<CallerScopeAccessor>();
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<KeyVaultService>();
builder.Services.AddScoped<NotificationConfigService>();
// Singleton: Kafka producers own background threads and broker metadata, so they are built
// once per configuration and reused rather than per dispatch.
builder.Services.AddSingleton<KafkaProducerFactory>();
builder.Services.AddHttpClient<NotificationDispatchService>();
builder.Services.AddScoped<AppSettingsService>();

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Blazor + Fluent UI
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();

// API controllers + Protobuf formatters + Swagger
builder.Services.AddControllers(options =>
{
    options.InputFormatters.Add(new ProtobufInputFormatter());
    options.OutputFormatters.Add(new ProtobufOutputFormatter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tau Key Vault API",
        Version = "v1",
        Description = "API for retrieving and managing key-value pairs across environments.\n\n" +
                      "Keys resolve with global fallback: if a key is not found in the requested " +
                      "environment, the global (blank environment) value is returned. That fallback " +
                      "is disabled when GlobalKeyFailover is false, and never applies to a caller " +
                      "holding an environment-bound API key. Deleting a key never falls back.\n\n" +
                      "Every read, write and delete is recorded in the access audit log, queryable " +
                      "at GET /api/audit. Values are never recorded there.\n\n" +
                      "Supports JSON (default) and Protocol Buffers (Accept: application/x-protobuf). " +
                      "Download the .proto schema at GET /api/keys/proto."
    });

    // API key auth scheme
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Api-Key",
        Description = "API key required for all /api/* endpoints. Global keys are configured in appsettings.json. When EnableAPIKeyPerEnvironment is true, per-environment keys minted via /api/apikeys are also accepted and confine the caller to their bound environment with no Global fallback."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Apply pending migrations & seed database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var salt = await DbSeeder.InitializeSaltAsync(db, app.Configuration, app.Environment);
    await DbSeeder.EncryptExistingKeysAsync(db, salt);
    await DbSeeder.SeedAsync(db);
}

// Auto-generate .proto schema file from [ProtoContract] models
ProtoSchemaGenerator.Generate(app.Environment.WebRootPath);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Swagger (available in all environments)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tau Key Vault API v1");
    options.RoutePrefix = "swagger";
});

//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Fail-closed auditing: an unrecordable operation becomes a 503 rather than happening
// unaudited. Sits outside the API key middleware so its own audit writes are covered too.
app.UseMiddleware<AuditFailureMiddleware>();

// A valid credential reaching outside its environment becomes a 403 and an audit row.
// Inside the audit middleware, so recording the violation is itself fail-closed.
app.UseMiddleware<ScopeViolationMiddleware>();

// API key middleware (only for /api/* routes, skips swagger)
app.UseMiddleware<ApiKeyMiddleware>();

// Auth endpoints (login/logout)
app.MapAuthEndpoints();

// API controllers
app.MapControllers();

// Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
