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
                new Group { Name = "Tümü", Icon = "🗂️", SortOrder = 0, IsSystem = true },
                new Group { Name = "Şifre", Icon = "🔒", SortOrder = 1 }, 
                new Group { Name = "Kod", Icon = "💻", SortOrder = 2 },
                new Group { Name = "Resimler", Icon = "🖼️", SortOrder = 3 },
                new Group { Name = "İş", Icon = "💼", SortOrder = 4 },
                new Group { Name = "Kişisel", Icon = "🏠", SortOrder = 5 },
                new Group { Name = "SQL", Icon = "🗄️", SortOrder = 6 }
            );
            db.SaveChanges();
        }
    }
}
