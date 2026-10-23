using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ValleyBaptistBulletinEditor.Models;

namespace ValleyBaptistBulletinEditor.Services;

// Local, page-only recovery drafts. No settings, credentials, or top-level bulletin fields.
public static class PageDraftStorage
{
    static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ValleyBaptistBulletinEditor", "PageDrafts");
    static readonly JsonSerializerOptions Options = new() { WriteIndented = false };
    public static string KeyForFile(string file)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(file).ToUpperInvariant()));
        return Convert.ToHexString(hash);
    }
    static string PathFor(string key) => Path.Combine(Folder, key + ".pages.json");
    public static void Save(string key, List<BulletinPanel> pages)
    {
        Directory.CreateDirectory(Folder);
        string target = PathFor(key);
        string temp = target + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(pages, Options));
        File.Move(temp, target, true);
        // Reopen the last edited pages on next launch, without touching the full saved bulletin.
        File.WriteAllText(Path.Combine(Folder, "last-draft.txt"), key);
    }
    public static List<BulletinPanel>? Load(string key)
    {
        string path = PathFor(key);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<List<BulletinPanel>>(File.ReadAllText(path), Options);
    }
    public static List<BulletinPanel>? LoadLast()
    {
        string pointer = Path.Combine(Folder, "last-draft.txt");
        if (!File.Exists(pointer)) return null;
        string key = File.ReadAllText(pointer).Trim();
        return key.Length == 0 ? null : Load(key);
    }
}
