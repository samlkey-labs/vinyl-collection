// One-off: sets each album's Ranking from its current order by Rating (highest first),
// spaced by 10 (10, 20, 30...) so albums can later be slotted into the gaps by hand.
// Re-running it overwrites any manual Ranking values with the rating order.
//
// Dry run by default; pass --apply to write. Only the Ranking field of each document
// is changed (a Cosmos patch), so any other fields in the documents are left untouched.
//
// Usage (from this folder), using Cosmos__ConnectionString from the repo's .env:
//   dotnet run                 preview the rankings
//   dotnet run -- --apply      write them to Cosmos DB
// A connection string can also be passed explicitly: dotnet run -- "<connection string>" [--apply]

using FRONTEND.Data;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;

var apply = args.Contains("--apply");
var positional = args.Where(a => !a.StartsWith("--")).ToArray();
var connectionString = CosmosSettings.ConnectionString(positional.ElementAtOrDefault(0));
if (connectionString == null)
{
    Console.Error.WriteLine($"No connection string: set {CosmosSettings.ConnectionStringKey} in .env, or pass one.");
    Console.Error.WriteLine("Usage: dotnet run -- [connection string] [database name] [--apply]");
    return 1;
}

var databaseName = CosmosSettings.DatabaseName(positional.ElementAtOrDefault(1));

// Order the albums by rating as the Ranking page used to (same query, then a stable sort
// by Rating descending), so albums with equal ratings keep the order they were shown in.
var options = new DbContextOptionsBuilder<VinylDbContext>()
    .UseCosmos(connectionString, databaseName)
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
    .Options;

List<Album> ranked;
await using (var context = new VinylDbContext(options))
{
    var albums = await context.Albums.ToListAsync();
    ranked = albums.OrderByDescending(a => a.Rating).ToList();
}

var tiedRatings = ranked.GroupBy(a => a.Rating).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

const int step = 10;
Console.WriteLine($"{"Ranking",7}  {"Rating",6}  {"Current",7}  Album");
for (var i = 0; i < ranked.Count; i++)
{
    var album = ranked[i];
    var tie = tiedRatings.Contains(album.Rating) ? "  (tied rating)" : "";
    Console.WriteLine($"{(i + 1) * step,7}  {album.Rating,6}  {album.Ranking?.ToString() ?? "-",7}  {album.Artist} - {album.Name}{tie}");
}

if (!apply)
{
    Console.WriteLine();
    Console.WriteLine("Dry run: nothing written. Re-run with --apply to set these rankings.");
    return 0;
}

using var client = new CosmosClient(connectionString);
var container = client.GetContainer(databaseName, "Albums");

for (var i = 0; i < ranked.Count; i++)
{
    var album = ranked[i];
    await container.PatchItemAsync<object>(
        album.Id,
        new PartitionKey(album.Id),
        new[] { PatchOperation.Set("/Ranking", (double)((i + 1) * step)) });
}

Console.WriteLine();
Console.WriteLine($"Set Ranking on {ranked.Count} albums.");
return 0;
