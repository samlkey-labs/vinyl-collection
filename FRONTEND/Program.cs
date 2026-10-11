using Azure.Identity;
using FRONTEND.Components;
using FRONTEND.Data;
using FRONTEND.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Locally, also read the repo's .env (the file docker-compose and the tools use), so the Cosmos
// connection string lives in one place. It's added as the lowest-priority source: user-secrets
// and environment variables still override it. Azure doesn't run as Development, so this never
// applies there.
if (builder.Environment.IsDevelopment())
{
    var dotEnv = LoadDotEnv(builder.Environment.ContentRootPath);
    if (dotEnv.Count > 0)
        builder.Configuration.Sources.Insert(0, new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource { InitialData = dotEnv });
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database Configuration
// In Azure, Cosmos:Endpoint is set and the app authenticates with its managed identity.
// Locally, set Cosmos__ConnectionString in the repo's .env (or Cosmos:ConnectionString in user-secrets).
var cosmosConnectionString = builder.Configuration["Cosmos:ConnectionString"];
var cosmosEndpoint = builder.Configuration["Cosmos:Endpoint"];
var cosmosDatabase = builder.Configuration["Cosmos:DatabaseName"] ?? "VinylCollection";

// Create the credential once and share it. A new credential instance per DbContext makes EF
// build a new internal service provider each time, which fails after 20
// (ManyServiceProvidersCreatedWarning).
var cosmosCredential = new DefaultAzureCredential();

// A factory rather than AddDbContext: in Blazor Server a scoped DbContext lives for the whole
// circuit, so overlapping queries (e.g. typing quickly in search) would share one instance and
// fail with "A second operation was started on this context". VinylService creates a context per call.
builder.Services.AddDbContextFactory<VinylDbContext>(options =>
{
    // The app only reads data, so skip change tracking
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    if (!string.IsNullOrEmpty(cosmosConnectionString))
        options.UseCosmos(cosmosConnectionString, cosmosDatabase);
    else if (!string.IsNullOrEmpty(cosmosEndpoint))
        options.UseCosmos(cosmosEndpoint, cosmosCredential, cosmosDatabase);
    else
        throw new InvalidOperationException("Set Cosmos:ConnectionString or Cosmos:Endpoint.");
});

// Services
builder.Services.AddScoped<IVinylService, VinylService>();
builder.Services.AddHttpClient<IColorAnalysisService, ColorAnalysisService>();
builder.Services.AddScoped<ICurrentAlbumColorService, CurrentAlbumColorService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Reads KEY=VALUE lines from the nearest .env at or above startDir. Keys use the environment
// variable form (Cosmos__ConnectionString), so "__" becomes the ":" configuration separator.
static Dictionary<string, string?> LoadDotEnv(string startDir)
{
    var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var dir = new DirectoryInfo(startDir); dir != null; dir = dir.Parent)
    {
        var path = Path.Combine(dir.FullName, ".env");
        if (!File.Exists(path))
            continue;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOf('=');
            if (line.StartsWith('#') || separator <= 0)
                continue;

            // Split on the first '=' only: connection strings contain '=' themselves
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
                value = value[1..^1];
            values[line[..separator].Trim().Replace("__", ":")] = value;
        }
        break;
    }
    return values;
}
