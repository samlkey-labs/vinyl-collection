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
        private readonly VinylDbContext _context;

        public VinylService(VinylDbContext context)
        {
            _context = context;
        }

        // Tracks are embedded in the album document, so no Include is needed.
        public async Task<List<Album>> GetAllAlbumsAsync()
        {
            return await _context.Albums.ToListAsync();
        }

        public async Task<Album?> GetAlbumByIdAsync(string id)
        {
            return await _context.Albums.FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<Album>> SearchAlbumsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<Album>();
            var lowered = query.ToLower();
            return await _context.Albums
                .Where(a => a.Name!.ToLower().Contains(lowered) || a.Artist!.ToLower().Contains(lowered))
                .ToListAsync();
        }

        public async Task<List<Album>> GetRandomAlbumsAsync(int count = 5)
        {
            // Cosmos DB can't ORDER BY a random value, so shuffle in memory.
            // The collection is small enough for this to be cheap.
            var albums = await _context.Albums.ToListAsync();
            return albums.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
        }
    }
}
