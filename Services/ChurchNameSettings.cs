using System;
using System.IO;
using System.Text.Json;

namespace ValleyBaptistBulletinEditor.Services;

public static class ChurchNameSettings
{
    private sealed class Record { public string ChurchName { get; set; } = "Valley Baptist Church"; }
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ValleyBaptistBulletinEditor", "church-name.json");

    public static string Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var saved = JsonSerializer.Deserialize<Record>(File.ReadAllText(FilePath));
                if (!string.IsNullOrWhiteSpace(saved?.ChurchName)) return saved.ChurchName.Trim();
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
        return "Valley Baptist Church";
    }

    public static void Save(string name)
    {
        if (!AdminSecurity.IsPublisher)
            throw new UnauthorizedAccessException("Only a signed-in Publisher can change the church name.");
        name = (name ?? "").Trim();
        if (name.Length < 2 || name.Length > 100)
            throw new ArgumentException("Church name must contain 2 to 100 characters.");
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new Record { ChurchName = name }));
    }
}
