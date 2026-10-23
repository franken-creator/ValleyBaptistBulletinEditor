using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace ValleyBaptistBulletinEditor;

public sealed class DesignSettings
{
    // Typography and layout controls. "Per section" uses each section's own alignment.
    public string GlobalAlignment { get; set; } = "Per section";
    public string TitleAlignment { get; set; } = "Center";
    public string WorshipAlignment { get; set; } = "Left";
    public string SermonAlignment { get; set; } = "Left";
    public string LeadershipAlignment { get; set; } = "Left";
    public string ReportsAlignment { get; set; } = "Left";
    public string ScheduleAlignment { get; set; } = "Left";
    public string AnnouncementsAlignment { get; set; } = "Left";
    public string CalendarAlignment { get; set; } = "Left";
    public string FooterAlignment { get; set; } = "Center";
    public double BodyFontSize { get; set; } = 11;
    public double SectionSpacing { get; set; } = 7;
    public double ColumnGap { get; set; } = 24;
    public string Theme { get; set; } = "Classic Navy";
    public string Font { get; set; } = "Georgia";
    public double TitleSize { get; set; } = 26;
    public string Template { get; set; } = "Valley Baptist Original";
    public string PageFormat { get; set; } = "Four-panel half-fold";
    public string HiddenPages { get; set; } = ""; // comma-delimited logical panels hidden by Publisher
    public bool ShowLeadership { get; set; } = true;
    public bool ShowReports { get; set; } = true;
    public bool ShowCalendar { get; set; } = true;
    // Physical sheet size, separate from number of printed faces.
    public double PaperWidthIn { get; set; } = 11;
    public double PaperHeightIn { get; set; } = 8.5;
    public int PrintedSides { get; set; } = 4; // Logical panels, NOT physical sheets
    // Page 1 is an existing purchased cover: preserve its position but output no ink.
    public bool LockPreprintedCover { get; set; } = true;
    // Cover display affects editor preview only; never print purchased cover artwork.
    public string CoverPreviewMode { get; set; } = "Blank page";
    public string CoverPreviewImageData { get; set; } = "";
    public bool PreprintedStock { get; set; } = true;
    public string DuplexMode { get; set; } = "Long-edge (book)";
    public double MarginIn { get; set; } = 0.25;
    public double OffsetXIn { get; set; } = 0;
    public double OffsetYIn { get; set; } = 0;
    public string PaperPresetName { get; set; } = "Valley Baptist Preprinted";
    private static string PathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ValleyBaptistBulletinEditor", "design.json");
    public static DesignSettings Load()
    {
        try { return File.Exists(PathName) ? JsonSerializer.Deserialize<DesignSettings>(File.ReadAllText(PathName)) ?? new() : new(); }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        File.WriteAllText(PathName, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static void Apply(DesignSettings d)
    {
        string hex = d.Theme switch { "Sage & Cream" => "#526C60", "Black & White" => "#292929", "Burgundy & Gold" => "#783B4C", _ => "#243956" };
        Application.Current.Resources["BulletinAccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        Application.Current.Resources["BulletinHeadingFont"] = new FontFamily(d.Font);
        Application.Current.Resources["BulletinTitleSize"] = Math.Clamp(d.TitleSize, 18, 38);
        Application.Current.Resources["LeadershipVisibility"] = d.ShowLeadership ? Visibility.Visible : Visibility.Collapsed;
        Application.Current.Resources["ReportsVisibility"] = d.ShowReports ? Visibility.Visible : Visibility.Collapsed;
        Application.Current.Resources["CalendarVisibility"] = d.ShowCalendar ? Visibility.Visible : Visibility.Collapsed;
        Application.Current.Resources["BulletinPageWidth"] = Math.Clamp(d.PaperWidthIn * 80, 350, 1200);
    }
}
