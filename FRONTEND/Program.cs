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

builder.Services.AddDbContext<VinylDbContext>(options =>
{
    // The app only reads data, so skip change tracking
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    if (!string.IsNullOrEmpty(cosmosConnectionString))
        options.UseCosmos(cosmosConnectionString, cosmosDatabase);
    else if (!string.IsNullOrEmpty(cosmosEndpoint))
        options.UseCosmos(cosmosEndpoint, new DefaultAzureCredential(), cosmosDatabase);
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
