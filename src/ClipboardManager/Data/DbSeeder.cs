using ClipboardManager.Models;

namespace ClipboardManager.Data;

/// <summary>
/// Ilk acilista varsayilan gruplari olusturur. Idempotent'tir.
/// </summary>
public static class DbSeeder
{
    public static void SeedDefaults(AppDbContext db)
    {
        if (!db.Groups.Any())
        {
            db.Groups.AddRange(
                new Group { Name = "Tumu", Icon = "🗂️", SortOrder = 0, IsSystem = true },
                new Group { Name = "Is", Icon = "💼", SortOrder = 1 },
                new Group { Name = "Kisisel", Icon = "🏠", SortOrder = 2 },
                new Group { Name = "Kod", Icon = "💻", SortOrder = 3 },
                new Group { Name = "Resimler", Icon = "🖼️", SortOrder = 4 }
            );
            db.SaveChanges();
        }
    }
}
