using System;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.ComponentModel;

namespace ValleyBaptistBulletinEditor;
public partial class DesignStudioWindow : Window
{
    private readonly DesignSettings _original;
    public bool SettingsSaved { get; private set; }
    private bool _explicitCancel;
    public event Action<string>? PresetNameChanged;
    private bool _ready;
    private string _coverPictureData = "";
    private readonly Dictionary<int, CheckBox> _pageChecks = new();
    public DesignStudioWindow()
    {
        InitializeComponent();
        _original = DesignSettings.Load();
        SetSelection(TemplatePicker, _original.Template);
        SetSelection(PagePicker, _original.PageFormat);

        SetSelection(ThemePicker, _original.Theme);
        SetSelection(FontPicker, _original.Font);
        SizePicker.Value = _original.TitleSize;
        PresetNameBox.Text = _original.PaperPresetName;
        PresetNameBox.TextChanged += (_, _) => PresetNameChanged?.Invoke(PresetNameBox.Text.Trim());
        WidthBox.Text = _original.PaperWidthIn.ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = _original.PaperHeightIn.ToString(CultureInfo.InvariantCulture);
        MarginBox.Text = _original.MarginIn.ToString(CultureInfo.InvariantCulture);
        OffsetXBox.Text = _original.OffsetXIn.ToString(CultureInfo.InvariantCulture);
        OffsetYBox.Text = _original.OffsetYIn.ToString(CultureInfo.InvariantCulture);
        PreprintedCheck.IsChecked = _original.PreprintedStock;
        LockCoverCheck.IsChecked = _original.LockPreprintedCover;
        _coverPictureData = _original.CoverPreviewImageData ?? "";
        SetSelection(CoverModeBox, _original.CoverPreviewMode);
        UpdateCoverPictureStatus();
        SetSelection(SidesBox, _original.PrintedSides.ToString());
        SetSelection(DuplexBox, _original.DuplexMode);
        BuildPageChecks(_original.PrintedSides, _original.HiddenPages);
        foreach (ComboBox box in AlignmentBoxes())
        {
            box.Items.Add(new ComboBoxItem { Content = "Left" });
            box.Items.Add(new ComboBoxItem { Content = "Center" });
            box.Items.Add(new ComboBoxItem { Content = "Right" });
        }
        LoadAlignment(_original);
        _ready = true;
    }
    private void BuildPageChecks(int count, string hidden)
    {
        VisiblePagesPanel.Children.Clear(); _pageChecks.Clear();
        var hiddenSet = new HashSet<int>();
        foreach (string part in (hidden ?? "").Split(',')) if (int.TryParse(part, out int n)) hiddenSet.Add(n);
        for (int i = 1; i <= Math.Clamp(count, 1, 12); i++)
        {
            var check = new CheckBox { Content = $"Page {i}", IsChecked = !hiddenSet.Contains(i),
                Margin = new Thickness(0, 5, 18, 7), MinWidth = 75 };
            _pageChecks[i] = check; VisiblePagesPanel.Children.Add(check);
        }
    }
    private void PageCount_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        string hidden = string.Join(",", _pageChecks.Where(x => x.Value.IsChecked != true).Select(x => x.Key));
        int count = int.TryParse((SidesBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out int n) ? n : 4;
        BuildPageChecks(count, hidden);
    }
    private void Format_Changed(object sender, SelectionChangedEventArgs e) { }
    private ComboBox[] AlignmentBoxes() => new[] { TitleAlignBox, WorshipAlignBox, SermonAlignBox,
        LeadershipAlignBox, ReportsAlignBox, ScheduleAlignBox, AnnouncementsAlignBox, CalendarAlignBox, FooterAlignBox };
    private void LoadAlignment(DesignSettings d)
    {
        SetSelection(GlobalAlignBox, d.GlobalAlignment);
        string[] values = { d.TitleAlignment, d.WorshipAlignment, d.SermonAlignment,
            d.LeadershipAlignment, d.ReportsAlignment, d.ScheduleAlignment, d.AnnouncementsAlignment,
            d.CalendarAlignment, d.FooterAlignment };
        var boxes = AlignmentBoxes();
        for (int i = 0; i < boxes.Length; i++) SetSelection(boxes[i], values[i]);
        BodySizeBox.Text = d.BodyFontSize.ToString(CultureInfo.InvariantCulture);
        SectionSpacingBox.Text = d.SectionSpacing.ToString(CultureInfo.InvariantCulture);
        ColumnGapBox.Text = d.ColumnGap.ToString(CultureInfo.InvariantCulture);
    }
    private static string Selected(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Left";
    private void ResetSectionAlignment_Click(object sender, RoutedEventArgs e) => LoadAlignment(new DesignSettings());
    private static void SetSelection(ComboBox box, string value)
    {
        foreach (ComboBoxItem item in box.Items)
            if (string.Equals(item.Content?.ToString(), value, StringComparison.Ordinal)) { box.SelectedItem = item; return; }
        box.SelectedIndex = 0;
    }
    private DesignSettings Current() => new()
    {
        GlobalAlignment = _original.GlobalAlignment,
        TitleAlignment = _original.TitleAlignment, WorshipAlignment = _original.WorshipAlignment,
        SermonAlignment = _original.SermonAlignment, LeadershipAlignment = _original.LeadershipAlignment,
        ReportsAlignment = _original.ReportsAlignment, ScheduleAlignment = _original.ScheduleAlignment,
        AnnouncementsAlignment = _original.AnnouncementsAlignment, CalendarAlignment = _original.CalendarAlignment,
        FooterAlignment = _original.FooterAlignment,
        BodyFontSize = _original.BodyFontSize, SectionSpacing = _original.SectionSpacing,
        ColumnGap = _original.ColumnGap,
        Theme = _original.Theme,
        Font = _original.Font,
        TitleSize = _original.TitleSize,
        Template = _original.Template,
        PageFormat = (PagePicker.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Two-page landscape",
        ShowLeadership = _original.ShowLeadership,
        ShowReports = _original.ShowReports,
        ShowCalendar = _original.ShowCalendar,
        HiddenPages = string.Join(",", _pageChecks.Where(x => x.Value.IsChecked != true).Select(x => x.Key)),
        PaperPresetName = PresetNameBox.Text.Trim(),
        PaperWidthIn = Read(WidthBox, 11), PaperHeightIn = Read(HeightBox, 8.5),
        PrintedSides = int.TryParse((SidesBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out int count) ? count : 2,
        PreprintedStock = PreprintedCheck.IsChecked == true,
        LockPreprintedCover = LockCoverCheck.IsChecked == true,
        CoverPreviewMode = Selected(CoverModeBox),
        CoverPreviewImageData = _coverPictureData,
        DuplexMode = (DuplexBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Long-edge (book)",
        MarginIn = Read(MarginBox, 0.25), OffsetXIn = Read(OffsetXBox, 0), OffsetYIn = Read(OffsetYBox, 0)
    };
    private static double Read(TextBox box, double fallback) =>
        double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : fallback;
    private static string PresetFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ValleyBaptistBulletinEditor", "PaperPresets");
    private static void Validate(DesignSettings d)
    {
        if (d.BodyFontSize < 6 || d.BodyFontSize > 28) throw new ArgumentException("Body text size must be between 6 and 28.");
        if (d.SectionSpacing < 0 || d.SectionSpacing > 50) throw new ArgumentException("Section spacing must be between 0 and 50.");
        if (d.ColumnGap < 0 || d.ColumnGap > 150) throw new ArgumentException("Column gap must be between 0 and 150.");
        if (d.PaperWidthIn < 2 || d.PaperHeightIn < 2 || d.PaperWidthIn > 40 || d.PaperHeightIn > 40)
            throw new ArgumentException("Paper width and height must each be between 2 and 40 inches.");
        if (d.MarginIn < 0 || d.MarginIn * 2 >= Math.Min(d.PaperWidthIn, d.PaperHeightIn))
            throw new ArgumentException("Safe margin is outside the printable sheet.");
        if (Math.Abs(d.OffsetXIn) > 2 || Math.Abs(d.OffsetYIn) > 2)
            throw new ArgumentException("Alignment offsets must be within ±2 inches.");
        if (d.PrintedSides is < 1 or > 12) throw new ArgumentException("Printed faces must be between 1 and 12.");
    }
    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        try { var d = Current(); Validate(d); Directory.CreateDirectory(PresetFolder);
            if (string.IsNullOrWhiteSpace(d.PaperPresetName)) throw new ArgumentException("Enter a preset name.");
            string file = Path.Combine(PresetFolder, string.Concat(d.PaperPresetName.Split(Path.GetInvalidFileNameChars())) + ".json");
            File.WriteAllText(file, JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }));
            PresetNameChanged?.Invoke(d.PaperPresetName);
            MessageBox.Show("Paper preset saved. To retain this name as the active page setup, also click Save Settings.", "Saved"); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Preset error"); }
    }
    private void LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title="Load paper preset", InitialDirectory=PresetFolder, Filter="Paper presets (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;
        try { var d = JsonSerializer.Deserialize<DesignSettings>(File.ReadAllText(dialog.FileName)) ?? throw new Exception("Invalid preset.");
            Validate(d); WidthBox.Text=d.PaperWidthIn.ToString(CultureInfo.InvariantCulture); HeightBox.Text=d.PaperHeightIn.ToString(CultureInfo.InvariantCulture);
            MarginBox.Text=d.MarginIn.ToString(CultureInfo.InvariantCulture); OffsetXBox.Text=d.OffsetXIn.ToString(CultureInfo.InvariantCulture);
            OffsetYBox.Text=d.OffsetYIn.ToString(CultureInfo.InvariantCulture); PreprintedCheck.IsChecked=d.PreprintedStock; LockCoverCheck.IsChecked=d.LockPreprintedCover;
            _coverPictureData=d.CoverPreviewImageData ?? ""; SetSelection(CoverModeBox,d.CoverPreviewMode); UpdateCoverPictureStatus();
            PresetNameBox.Text=d.PaperPresetName; SetSelection(SidesBox,d.PrintedSides.ToString()); SetSelection(PagePicker,d.PageFormat); BuildPageChecks(d.PrintedSides,d.HiddenPages); SetSelection(DuplexBox,d.DuplexMode); LoadAlignment(d); }
        catch(Exception ex) { MessageBox.Show(ex.Message,"Load failed"); }
    }
    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        string name=string.Concat(PresetNameBox.Text.Trim().Split(Path.GetInvalidFileNameChars()));
        string path=Path.Combine(PresetFolder,name+".json");
        if (File.Exists(path) && MessageBox.Show("Delete named preset?", "Confirm",MessageBoxButton.YesNo)==MessageBoxResult.Yes) File.Delete(path);
    }
    private void UpdateCoverPictureStatus() => CoverPictureStatus.Text =
        string.IsNullOrEmpty(_coverPictureData) ? "No cover picture selected" : "Cover picture saved in Publisher settings (preview only; never printed).";
    private void ChooseCoverPicture_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Choose preprinted cover reference picture",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            byte[] bytes = File.ReadAllBytes(dialog.FileName);
            if (bytes.Length > 8 * 1024 * 1024) throw new ArgumentException("Cover picture must be smaller than 8 MB.");
            _coverPictureData = Convert.ToBase64String(bytes);
            SetSelection(CoverModeBox, "Show cover picture");
            UpdateCoverPictureStatus();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Cover picture"); }
    }
    private void RemoveCoverPicture_Click(object sender, RoutedEventArgs e)
    {
        _coverPictureData = "";
        SetSelection(CoverModeBox, "Blank page");
        UpdateCoverPictureStatus();
    }
    private void LetterLandscape_Click(object sender, RoutedEventArgs e) { WidthBox.Text="11";HeightBox.Text="8.5"; }
    private void LetterPortrait_Click(object sender, RoutedEventArgs e) { WidthBox.Text="8.5";HeightBox.Text="11"; }
    private void ResetAlignment_Click(object sender, RoutedEventArgs e) { OffsetXBox.Text="0";OffsetYBox.Text="0"; }
    private void CheckPrinter_Click(object sender, RoutedEventArgs e)
    {
        try { var d=Current(); Validate(d);
            var dialog=new PrintDialog(); if(dialog.ShowDialog()!=true) return;
            var caps=dialog.PrintQueue.GetPrintCapabilities(dialog.PrintTicket);
            double w=d.PaperWidthIn*96, h=d.PaperHeightIn*96;
            bool fits=caps.OrientedPageMediaWidth is double pw && caps.OrientedPageMediaHeight is double ph &&
                ((pw>=w-2 && ph>=h-2) || (pw>=h-2 && ph>=w-2));
            MessageBox.Show(fits ? "Driver reports a printable media size at least as large as the requested sheet. Check the paper selection and test alignment." :
                "Printer driver has not confirmed this custom paper size. Select matching paper in printer preferences and run a plain-paper test. The application cannot guarantee compatibility.","Printer compatibility"); }
        catch(Exception ex) { MessageBox.Show(ex.Message,"Printer check"); }
    }
    private void Preset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        string preset = (TemplatePicker.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        var settings = preset switch
        {
            "Classic Elegance" => ("Classic Navy", "Georgia", 28d),
            "Modern Minimalist" => ("Black & White", "Segoe UI", 30d),
            "Botanical Traditional" => ("Sage & Cream", "Cambria", 28d),
            "Contemporary Newsletter" => ("Burgundy & Gold", "Segoe UI", 26d),
            _ => ("Classic Navy", "Georgia", 26d)
        };
        _ready = false;
        SetSelection(ThemePicker, settings.Item1);
        SetSelection(FontPicker, settings.Item2);
        SizePicker.Value = settings.Item3;
        _ready = true;
    }
    private void Choice_Changed(object sender, RoutedEventArgs e)
    {
        // Typography is edited in the Design Inspector, not Page Setup.
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { var d=Current(); Validate(d); d.Save(); SettingsSaved = true; DialogResult = true; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Could not save design", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { _explicitCancel = true; DesignSettings.Apply(_original); DialogResult = false; }
    private void Defaults_Click(object sender, RoutedEventArgs e)
    {
        _ready = false;
        SetSelection(TemplatePicker, "Valley Baptist Original");
        SetSelection(PagePicker, "Four-panel half-fold");
        SetSelection(SidesBox, "4"); BuildPageChecks(4, "");
        SetSelection(ThemePicker, "Classic Navy"); SetSelection(FontPicker, "Georgia"); SizePicker.Value = 26;
        LoadAlignment(new DesignSettings());
        _ready = true;
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        // The window's X button and application shutdown persist the current
        // page setup without requiring the user to click Save Settings.
        // Cancel remains an explicit discard action.
        if (!_explicitCancel && !SettingsSaved)
        {
            try
            {
                var current = Current();
                Validate(current);
                current.Save();
                SettingsSaved = true;
            }
            catch (Exception ex)
            {
                // On invalid settings, keep the window open so the user can fix them.
                e.Cancel = true;
                MessageBox.Show(this, ex.Message, "Page Setup could not be saved", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        base.OnClosing(e);
    }
    protected override void OnClosed(EventArgs e)
    {
        if (_explicitCancel) DesignSettings.Apply(_original);
        base.OnClosed(e);
    }
}
