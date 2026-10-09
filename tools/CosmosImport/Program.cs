// One-off import of albums.json into the Cosmos DB "Albums" container.
// The web app is read-only; after this initial load, manage albums in the
// Azure Portal's Data Explorer.
//
// Usage (from this folder):
//   dotnet run -- "<connection string>"   key auth, also creates the database/container if missing
//   dotnet run -- "<account endpoint>"    Entra ID auth via `az login` (needs a Cosmos data-plane role)

using System.Text.Json;
using Azure.Identity;
using FRONTEND.Data;
using FRONTEND.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotnet run -- <connection string | account endpoint> [database name]");
    return 1;
}

var target = args[0];
var databaseName = args.Length > 1 ? args[1] : "VinylCollection";
var useKey = target.StartsWith("AccountEndpoint=", StringComparison.OrdinalIgnoreCase);

var optionsBuilder = new DbContextOptionsBuilder<VinylDbContext>();
if (useKey)
    optionsBuilder.UseCosmos(target, databaseName);
else
    optionsBuilder.UseCosmos(target, new DefaultAzureCredential(), databaseName);

await using var context = new VinylDbContext(optionsBuilder.Options);

// Entra ID identities can't create databases/containers; Bicep creates them in Azure
if (useKey)
    await context.Database.EnsureCreatedAsync();

if (await context.Albums.FirstOrDefaultAsync() != null)
{
    Console.WriteLine("Albums container already has data; nothing imported.");
    return 0;
}

var jsonPath = Path.Combine(AppContext.BaseDirectory, "albums.json");
var albums = JsonSerializer.Deserialize<List<Album>>(File.ReadAllText(jsonPath)) ?? new List<Album>();

using var httpClient = new HttpClient();
var colorService = new ColorAnalysisService(httpClient, NullLogger<ColorAnalysisService>.Instance);

foreach (var album in albums)
{
    if (string.IsNullOrEmpty(album.Name))
        album.Name = "Unknown Album";
    if (string.IsNullOrEmpty(album.Artist))
        album.Artist = "Unknown Artist";

    // Store the cover's primary colour so the app doesn't recalculate it
    await album.CalculatePrimaryColourAsync(colorService);

    if (album.TrackList != null)
    {
        foreach (var track in album.TrackList)
        {
            if (string.IsNullOrEmpty(track.Name))
                track.Name = "Unknown Track";
        }
    }

    Console.WriteLine($"  {album.Artist} - {album.Name} ({album.PrimaryColour})");
}

context.Albums.AddRange(albums);
await context.SaveChangesAsync();
Console.WriteLine($"Imported {albums.Count} albums.");
return 0;
