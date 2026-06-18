using ClipboardManager.Models;
using Microsoft.EntityFrameworkCore;

namespace ClipboardManager.Data;

/// <summary>
/// EF Core / SQLite DbContext. Fluent API ile iliskileri ve indeksleri tanimlar.
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<ClipboardItem> Items => Set<ClipboardItem>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ItemTag> ItemTags => Set<ItemTag>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---- Group ----
        b.Entity<Group>(e =>
        {
            e.HasIndex(g => g.Name).IsUnique();
            e.Property(g => g.Name).IsRequired();
            e.HasMany(g => g.Items)
             .WithOne(i => i.Group!)
             .HasForeignKey(i => i.GroupId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ---- Tag ----
        b.Entity<Tag>(e =>
        {
            e.HasIndex(t => t.Name).IsUnique();
            e.Property(t => t.Name).IsRequired();
        });

        // ---- ItemTag (join) ----
        b.Entity<ItemTag>(e =>
        {
            e.HasKey(it => new { it.ItemId, it.TagId });
            e.HasOne(it => it.Item)
             .WithMany(i => i.ItemTags)
             .HasForeignKey(it => it.ItemId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(it => it.Tag)
             .WithMany(t => t.ItemTags)
             .HasForeignKey(it => it.TagId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- ClipboardItem ----
        b.Entity<ClipboardItem>(e =>
        {
            e.HasIndex(i => i.CreatedAt);
            e.HasIndex(i => i.GroupId);
            e.Property(i => i.Kind).HasConversion<int>();
        });
    }
}
