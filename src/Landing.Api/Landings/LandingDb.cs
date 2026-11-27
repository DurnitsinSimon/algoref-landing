using Microsoft.EntityFrameworkCore;

namespace Landing.Api.Landings;

internal sealed class LandingDb(DbContextOptions<LandingDb> options) : DbContext(options)
{
    public DbSet<LandingPage> Landings => Set<LandingPage>();
    public DbSet<Publication> Publications => Set<Publication>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var pages = model.Entity<LandingPage>();
        pages.ToTable("landings");
        pages.HasIndex(p => p.Slug).IsUnique().HasDatabaseName("ux_landings_slug");
        pages.HasIndex(p => p.PartnerId);
        pages.Property(p => p.Draft).HasColumnType("jsonb");
        pages.Property(p => p.Revision).IsConcurrencyToken();
        pages.Property(p => p.PublishedRevision).IsConcurrencyToken();

        var publications = model.Entity<Publication>();
        publications.ToTable("landing_publications");
        publications.HasKey(p => new { p.LandingId, p.Revision });
        publications.Property(p => p.Content).HasColumnType("jsonb");
        publications.HasOne<LandingPage>().WithMany().HasForeignKey(p => p.LandingId);
    }
}

internal sealed class LandingPage
{
    public Guid Id { get; set; }
    public required string Slug { get; init; }
    public required string PartnerId { get; init; }
    public required string Draft { get; set; }
    public int Revision { get; set; } = 1;
    public int? PublishedRevision { get; set; }
}

internal sealed class Publication
{
    public Guid LandingId { get; init; }
    public int Revision { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset PublishedAt { get; init; }
}
