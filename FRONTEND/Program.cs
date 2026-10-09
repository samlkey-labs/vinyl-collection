using Azure.Identity;
using FRONTEND.Components;
using FRONTEND.Data;
using FRONTEND.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database Configuration
// In Azure, Cosmos:Endpoint is set and the app authenticates with its managed identity.
// Locally, set Cosmos:ConnectionString (user-secrets) to use a key or the emulator instead.
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
