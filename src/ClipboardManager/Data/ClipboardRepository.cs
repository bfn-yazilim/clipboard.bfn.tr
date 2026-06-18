using System.IO;
using ClipboardManager.Models;
using Microsoft.EntityFrameworkCore;

namespace ClipboardManager.Data;

/// <summary>
/// Veritabani islemleri icin ince bir repository katmani.
/// ViewModel dogrudan DbContext ile didismek yerine bu katmani kullanir;
/// boylece test edilebilirlik ve sorgu merkeziyeti saglanir.
/// </summary>
public class ClipboardRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public ClipboardRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<ClipboardItem>> GetItemsAsync(int? tagId, string? search)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.Items
            .Include(i => i.Group)
            .Include(i => i.ItemTags).ThenInclude(it => it.Tag)
            .AsQueryable();

        if (tagId is > 0)
            q = q.Where(i => i.ItemTags.Any(it => it.TagId == tagId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(i =>
                (i.Content != null && i.Content.Contains(s)) ||
                (i.Title != null && i.Title.Contains(s)) ||
                i.ItemTags.Any(it => it.Tag.Name.Contains(s)));
        }

        return await q.OrderByDescending(i => i.IsPinned)
                      .ThenBy(i => i.OrderIndex)
                      .ThenByDescending(i => i.CreatedAt)
                      .ToListAsync();
    }

    public async Task<List<Group>> GetGroupsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Groups.OrderBy(g => g.SortOrder).ToListAsync();
    }

    public async Task<List<Tag>> GetTagsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Tags.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<ClipboardItem> AddItemAsync(ClipboardItem item)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task UpdateItemAsync(ClipboardItem item)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Items.Update(item);
        await db.SaveChangesAsync();
    }

    public async Task UpdateItemsOrderAsync(List<ClipboardItem> items)
    {
        await using var db = await _factory.CreateDbContextAsync();
        foreach (var item in items)
        {
            var dbItem = await db.Items.FindAsync(item.Id);
            if (dbItem != null)
            {
                dbItem.OrderIndex = item.OrderIndex;
                db.Items.Update(dbItem);
            }
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteItemAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var item = await db.Items.FindAsync(id);
        if (item != null)
        {
            // Ilgili resim dosyasini da sil
            if (!string.IsNullOrEmpty(item.ImageFilePath) && File.Exists(item.ImageFilePath))
            {
                try { File.Delete(item.ImageFilePath); } catch { /* ignore */ }
            }
            db.Items.Remove(item);
            await db.SaveChangesAsync();
        }
    }

    public async Task<Group> AddGroupAsync(string name, string? icon = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var g = new Group { Name = name, Icon = icon ?? "📁", SortOrder = (int)(DateTime.UtcNow.Ticks % int.MaxValue) };
        db.Groups.Add(g);
        await db.SaveChangesAsync();
        return g;
    }

    public async Task<Tag> AddTagAsync(string name, string? color = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var t = new Tag { Name = name, Color = color ?? "#3B82F6" };
        db.Tags.Add(t);
        await db.SaveChangesAsync();
        return t;
    }

    public async Task UpdateTagAsync(int tagId, string newName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var t = await db.Tags.FindAsync(tagId);
        if (t != null)
        {
            t.Name = newName;
            await db.SaveChangesAsync();
        }
    }

    public async Task DeleteTagAsync(int tagId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var t = await db.Tags.FindAsync(tagId);
        if (t != null)
        {
            db.Tags.Remove(t);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Bir ogeye etiket atar (yoksa etiketi olusturur).</summary>
    public async Task AssignTagAsync(int itemId, string tagName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var tag = db.Tags.FirstOrDefault(t => t.Name == tagName)
                  ?? db.Tags.Add(new Tag { Name = tagName, Color = "#3B82F6" }).Entity;

        if (!db.ItemTags.Any(it => it.ItemId == itemId && it.TagId == tag.Id))
        {
            db.ItemTags.Add(new ItemTag { ItemId = itemId, TagId = tag.Id });
            await db.SaveChangesAsync();
        }
    }

    public async Task ToggleTagAsync(int itemId, int tagId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var it = await db.ItemTags.FirstOrDefaultAsync(x => x.ItemId == itemId && x.TagId == tagId);
        if (it != null)
        {
            db.ItemTags.Remove(it);
        }
        else
        {
            db.ItemTags.Add(new ItemTag { ItemId = itemId, TagId = tagId });
        }
        await db.SaveChangesAsync();
    }

    public async Task RemoveTagAsync(int itemId, int tagId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var it = await db.ItemTags.FirstOrDefaultAsync(x => x.ItemId == itemId && x.TagId == tagId);
        if (it != null)
        {
            db.ItemTags.Remove(it);
            await db.SaveChangesAsync();
        }
    }

    public async Task AssignGroupAsync(int itemId, int groupId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var item = await db.Items.FindAsync(itemId);
        if (item != null)
        {
            item.GroupId = groupId;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Son N kaydi birakip eskileri temizler.</summary>
    public async Task TrimToAsync(int maxItems)
    {
        if (maxItems <= 0) return;
        await using var db = await _factory.CreateDbContextAsync();
        var total = await db.Items.CountAsync();
        if (total <= maxItems) return;

        var oldIds = await db.Items
            .OrderByDescending(i => i.CreatedAt)
            .Skip(maxItems)
            .Select(i => i.Id)
            .ToListAsync();

        var olds = await db.Items.Where(i => oldIds.Contains(i.Id)).ToListAsync();
        db.Items.RemoveRange(olds);
        await db.SaveChangesAsync();
    }
}
