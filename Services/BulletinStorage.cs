using System.IO;
using System.Text.Json;
using ValleyBaptistBulletinEditor.Models;
namespace ValleyBaptistBulletinEditor.Services;
public static class BulletinStorage
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static void Save(string path, Bulletin bulletin) => File.WriteAllText(path, JsonSerializer.Serialize(bulletin, Options));
    public static Bulletin Load(string path) => JsonSerializer.Deserialize<Bulletin>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("Bulletin file was empty.");
}
