using Microsoft.EntityFrameworkCore;

namespace FRONTEND.Data
{
    public class VinylDbContext : DbContext
    {
        public VinylDbContext(DbContextOptions<VinylDbContext> options) : base(options)
        {
        }

        public DbSet<Album> Albums { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Each album is one document in the "Albums" container, partitioned by id,
            // with its tracks embedded in the same document.
            modelBuilder.Entity<Album>(entity =>
            {
                entity.ToContainer("Albums");
                entity.HasNoDiscriminator();
                entity.HasKey(e => e.Id);
                entity.HasPartitionKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Artist).IsRequired();
                entity.OwnsMany(e => e.TrackList);
            });
        }
    }
}
