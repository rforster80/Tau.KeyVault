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

// EF Core + SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? "Data Source=keyvault.db"));

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<KeyVaultService>();
builder.Services.AddScoped<NotificationConfigService>();
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
    options.InputFormatters.Insert(0, new ProtobufInputFormatter());
    options.OutputFormatters.Insert(0, new ProtobufOutputFormatter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tau Key Vault API",
        Version = "v1",
        Description = "API for retrieving and managing key-value pairs across environments. " +
                      "Keys resolve with global fallback: if a key is not found in the requested " +
                      "environment, the global (blank environment) value is returned. " +
                      "Supports JSON (default) and Protocol Buffers (Accept: application/x-protobuf). " +
                      "Download the .proto schema at GET /api/keys/proto."
    });

    // API key auth scheme
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Api-Key",
        Description = "API key required to access all /api/* endpoints. Configure keys in appsettings.json."
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
