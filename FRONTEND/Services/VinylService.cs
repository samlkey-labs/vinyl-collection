using FRONTEND.Data;
using Microsoft.EntityFrameworkCore;

namespace FRONTEND.Services
{
    public interface IVinylService
    {
        Task<List<Album>> GetAllAlbumsAsync();
        Task<Album?> GetAlbumByIdAsync(string id);
        Task<List<Album>> SearchAlbumsAsync(string query);
        Task<List<Album>> GetRandomAlbumsAsync(int count = 5);
    }

    // Read-only: album data is managed directly in Cosmos DB, not through the app.
    public class VinylService : IVinylService
    {
        private readonly IDbContextFactory<VinylDbContext> _contextFactory;

        public VinylService(IDbContextFactory<VinylDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // Tracks are embedded in the album document, so no Include is needed.
        public async Task<List<Album>> GetAllAlbumsAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Albums.ToListAsync();
        }

        public async Task<Album?> GetAlbumByIdAsync(string id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Albums.FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<Album>> SearchAlbumsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<Album>();
            var lowered = query.ToLower();
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Albums
                .Where(a => a.Name!.ToLower().Contains(lowered) || a.Artist!.ToLower().Contains(lowered))
                .ToListAsync();
        }

        public async Task<List<Album>> GetRandomAlbumsAsync(int count = 5)
        {
            // Cosmos DB can't ORDER BY a random value, so shuffle in memory.
            // The collection is small enough for this to be cheap.
            await using var context = await _contextFactory.CreateDbContextAsync();
            var albums = await context.Albums.ToListAsync();
            return albums.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
        }
    }
}
