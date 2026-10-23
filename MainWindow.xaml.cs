using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ValleyBaptistBulletinEditor.Models;
using ValleyBaptistBulletinEditor.Services;

namespace ValleyBaptistBulletinEditor;
public partial class MainWindow : Window
{
    private Bulletin _bulletin = new();
    private bool _loadingPanel;
    private int _selectedPanel = 1;
    private void RefreshAppHeader()
    {
        string name = ChurchNameSettings.Load();
        if (string.IsNullOrWhiteSpace(name)) name = "Valley Baptist Church";
        AppChurchHeader.Text = name.ToUpperInvariant();
        Title = name + " | Bulletin Editor";
    }
    private int PanelCount => Math.Clamp(DesignSettings.Load().PrintedSides, 1, 12);
    private static bool PageVisible(int page, DesignSettings d)
    {
        if (page == 1 && d.LockPreprintedCover) return false;
        foreach (string hidden in (d.HiddenPages ?? "").Split(','))
            if (int.TryParse(hidden, out int n) && n == page) return false;
        return true;
    }
    private void FillPages_Click(object sender, RoutedEventArgs e)
    {
        EnsurePanels();
        if (PanelCount < 2) { MessageBox.Show("Select at least two pages in Publisher Design Studio."); return; }
        if (MessageBox.Show("Restore the original Valley Baptist layout to pages 2–4? Custom text on those pages will be replaced. Save your bulletin first if needed.",
            "Fill bulletin pages", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        PopulateFromWeeklyInformation();
        _selectedPanel = Math.Min(2, PanelCount);
        PopulatePanelSelector();
        SchedulePreview();
    }
    private void PopulateFromWeeklyInformation()
    {
        EnsurePanels();
        var b = _bulletin;
        if (PanelCount >= 2)
        {
            var p = b.Panels[1];
            p.Title = "WELCOME TO VALLEY BAPTIST CHURCH";
            p.Body = $"{b.Date}\n{b.Verse}\n\n{b.Staff}\n\nWelcome/Prayer: {b.Welcome}\n\n{b.Hymns}\n\n{b.Sermon}\n{b.Scripture}\n\n{b.Offering}\n{b.Attendance}\n\n{b.Schedule}";
            p.UseChurchTemplate = true; p.Enabled = true; p.IntentionallyBlank = false;
        }
        if (PanelCount >= 3)
        {
            var p = b.Panels[2];
            p.Title = "OFFICE HOURS & ANNOUNCEMENTS";
            p.Body = $"Office Hours: 9AM–1PM, Mon. Tues. Thurs.\n37702 W. Indian School Rd.\n(623) 393-0211\n\n{b.Announcements}";
            p.UseChurchTemplate = true; p.Enabled = true; p.IntentionallyBlank = false;
        }
        if (PanelCount >= 4)
        {
            var p = b.Panels[3];
            p.Title = "CALENDAR";
            p.Body = $"Email: valleybaptisttw@yahoo.com\nWebsite: tonopahvalleybaptist.com\n\n{b.Calendar}\n\n{b.Footer}";
            p.UseChurchTemplate = true; p.Enabled = true; p.IntentionallyBlank = false;
        }
    }
    private void EnsurePanels()
    {
        _bulletin.Panels ??= new();
        while (_bulletin.Panels.Count < PanelCount) _bulletin.Panels.Add(new BulletinPanel());
    }
    private void PopulatePanelSelector()
    {
        EnsurePanels();
        _loadingPanel = true;
        PanelSelector.Items.Clear();
        var settings = DesignSettings.Load();
        for (int i = 1; i <= PanelCount; i++)
            PanelSelector.Items.Add(i == 1 && settings.LockPreprintedCover ? $"Page {i} — Locked cover" :
                !PageVisible(i, settings) ? $"Page {i} — Hidden by Publisher" : $"Page {i}");
        _selectedPanel = Math.Clamp(_selectedPanel, 1, PanelCount);
        PanelSelector.SelectedIndex = _selectedPanel - 1;
        _loadingPanel = false;
        LoadSelectedPanel();
    }
    private void LoadSelectedPanel()
    {
        EnsurePanels();
        _loadingPanel = true;
        var panel = _bulletin.Panels[_selectedPanel - 1];
        bool locked = !PageVisible(_selectedPanel, DesignSettings.Load());
        PanelTitle.Text = panel.Title; PanelBody.Text = panel.Body;
        PanelAlignment.SelectedIndex = panel.Alignment == "Center" ? 1 : panel.Alignment == "Right" ? 2 : 0;
        PanelEnabled.IsChecked = panel.Enabled; PanelBlank.IsChecked = panel.IntentionallyBlank;
        PanelEditor.IsEnabled = !locked;
        PanelStatus.Text = locked ? "This page is locked or hidden by Publisher. It will print blank." :
            panel.UseChurchTemplate ? "Original church layout active. Weekly information fields below control the printed page. Editing the page text switches to custom layout." :
            "Custom page. Edit this panel only; an empty panel prints blank.";
        PictureStatus.Text = string.IsNullOrWhiteSpace(panel.ImageData) ? "No picture" : "Picture embedded in bulletin";
        _loadingPanel = false;
    }
    private void PanelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPanel || PanelSelector.SelectedIndex < 0) return;
        _selectedPanel = PanelSelector.SelectedIndex + 1;
        LoadSelectedPanel();
        // Keep the live viewer on the physical sheet containing the selected panel.
        SchedulePreview();
    }
    private void PanelText_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingPanel || PanelTitle == null || PanelBody == null || PanelAlignment == null ||
            PanelEnabled == null || PanelBlank == null || _selectedPanel > _bulletin.Panels.Count) return;
        var p = _bulletin.Panels[_selectedPanel - 1];
        // Editing a panel manually switches it from the fixed church layout to custom content.
        // Do not switch when loading controls or merely selecting a page.
        if (p.Title != PanelTitle.Text || p.Body != PanelBody.Text)
            p.UseChurchTemplate = false;
        p.Title = PanelTitle.Text; p.Body = PanelBody.Text;
        p.Alignment = (PanelAlignment.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Left";
        p.Enabled = PanelEnabled.IsChecked == true;
        p.IntentionallyBlank = PanelBlank.IsChecked == true;
        SchedulePreview();
    }
    private void AddPicture_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Select picture for this panel",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            byte[] bytes = File.ReadAllBytes(dialog.FileName);
            if (bytes.Length > 10 * 1024 * 1024) { MessageBox.Show("Picture must be under 10 MB."); return; }
            var p = _bulletin.Panels[_selectedPanel - 1];
            p.ImageData = Convert.ToBase64String(bytes); p.ImagePath = Path.GetFileName(dialog.FileName);
            LoadSelectedPanel(); SchedulePreview();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Picture error"); }
    }
    private void RemovePicture_Click(object sender, RoutedEventArgs e)
    {
        var p = _bulletin.Panels[_selectedPanel - 1]; p.ImageData = ""; p.ImagePath = "";
        LoadSelectedPanel(); SchedulePreview();
    }

    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    public MainWindow()
    {
        InitializeComponent();
        RefreshAppHeader();
        Activated += (_, _) => RefreshAppHeader();
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); RefreshLivePreview(); };
        DesignSettings.Apply(DesignSettings.Load());
        SetBulletin(_bulletin);
        // The initial bulletin is populated in SetBulletin. Never overwrite edited panels here.
    }
    private void SetBulletin(Bulletin bulletin)
    {
        _bulletin.PropertyChanged -= BulletinChanged;
        _bulletin = bulletin;
        _bulletin.PropertyChanged += BulletinChanged;
        DataContext = _bulletin;
        EnsurePanels();
        // Older bulletin files contain the weekly fields but no populated page panels.
        // Restore only when ALL panels are untouched, so custom layouts are never overwritten.
        if (_bulletin.Panels.TrueForAll(p =>
            string.IsNullOrWhiteSpace(p.Title) &&
            string.IsNullOrWhiteSpace(p.Body) &&
            string.IsNullOrWhiteSpace(p.ImageData) &&
            !p.IntentionallyBlank))
            PopulateFromWeeklyInformation();
        // Upgrade only the previous generated sample pages; do not touch customized bulletins.
        if (_bulletin.Panels.Count >= 4 &&
            _bulletin.Panels[1].Title == "ORDER OF WORSHIP" &&
            _bulletin.Panels[2].Title == "ANNOUNCEMENTS & EVENTS" &&
            _bulletin.Panels[3].Title == "CHURCH INFORMATION")
            PopulateFromWeeklyInformation();
        PopulatePanelSelector();
        SchedulePreview();
    }
    private void BulletinChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => SchedulePreview();
    private void SchedulePreview() { _previewTimer.Stop(); _previewTimer.Start(); }
    private void RefreshLivePreview()
    {
        try
        {
            EnsurePanels();
            var settings = DesignSettings.Load();
            LiveDocumentViewer.Document = BulletinPrintBuilder.Create(_bulletin, settings, livePreview: true);
            int sheetPage = settings.PageFormat == "Single-panel sheets"
                ? _selectedPanel
                : ((_selectedPanel - 1) / 4) * 2 +
                    ((_selectedPanel - 1) % 4 is 1 or 2 ? 2 : 1);
            // For a locked cover, open the inside face so the church information is visible.
            // Staff can still navigate to the outside face using the viewer controls.
            if (_selectedPanel == 1 && settings.LockPreprintedCover &&
                settings.PageFormat != "Single-panel sheets" && settings.PrintedSides >= 2)
                sheetPage = 2;
            LiveDocumentViewer.GoToPage(sheetPage);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Live preview: " + ex.Message); }
    }
    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Start a new week using the starter bulletin? Save your current changes first.","New week",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
        SetBulletin(new Bulletin());
        PopulateFromWeeklyInformation(); LoadSelectedPanel(); SchedulePreview();
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title="Save bulletin", Filter="Bulletin files (*.bulletin.json)|*.bulletin.json|JSON files (*.json)|*.json", InitialDirectory=BulletinFolder, FileName="ValleyBaptist_" + DateTime.Now.ToString("yyyy-MM-dd") + ".bulletin.json" };
        System.IO.Directory.CreateDirectory(BulletinFolder);
        if (dialog.ShowDialog()!=true) return;
        try { BulletinStorage.Save(dialog.FileName,_bulletin); MessageBox.Show("Bulletin saved.","Saved"); }
        catch (Exception ex) { MessageBox.Show(ex.Message,"Save failed",MessageBoxButton.OK,MessageBoxImage.Error); }
    }
    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title="Open bulletin", Filter="Bulletin files (*.bulletin.json)|*.bulletin.json|JSON files (*.json)|*.json" };
        if (dialog.ShowDialog()!=true) return;
        try { SetBulletin(BulletinStorage.Load(dialog.FileName)); }
        catch (Exception ex) { MessageBox.Show(ex.Message,"Open failed",MessageBoxButton.OK,MessageBoxImage.Error); }
    }
    private void PublisherEdit_Click(object sender, RoutedEventArgs e)
    {
        EnsurePanels();
        // Initialize movable boxes from existing weekly information only for original template pages.
        for(int n=2;n<=Math.Min(4,PanelCount);n++)
            if(_bulletin.Panels[n-1].UseChurchTemplate) PublisherLayout.Ensure(_bulletin,n);
        var editor=new PublisherPageEditor(_bulletin,DesignSettings.Load(),_selectedPanel==1?2:_selectedPanel){Owner=this};
        editor.ShowDialog();
        LoadSelectedPanel();SchedulePreview();
    }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        EnsurePanels();
        for (int n = 2; n <= Math.Min(4, PanelCount); n++)
            if (_bulletin.Panels[n - 1].UseChurchTemplate)
                PublisherLayout.Ensure(_bulletin, n);
        // Publisher/Admin uses exactly the same page workspace as the easy editor.
        var editor = new PublisherPageEditor(_bulletin, DesignSettings.Load(),
            _selectedPanel == 1 ? 2 : _selectedPanel, publisherMode: true) { Owner = this };
        editor.ShowDialog();
        PopulatePanelSelector();
        LoadSelectedPanel();
        SchedulePreview();
    }
    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var design = DesignSettings.Load();
            var dialog = new PrintDialog();
            dialog.PrintTicket.PageOrientation = design.PaperWidthIn > design.PaperHeightIn
                ? System.Printing.PageOrientation.Landscape : System.Printing.PageOrientation.Portrait;
            bool wantsDuplex = design.PrintedSides >= 2 && design.DuplexMode != "One-sided";
            if (wantsDuplex)
                dialog.PrintTicket.Duplexing = design.DuplexMode == "Short-edge (flip)"
                    ? System.Printing.Duplexing.TwoSidedShortEdge
                    : System.Printing.Duplexing.TwoSidedLongEdge;
            if (dialog.ShowDialog() != true) return;
            // The user may change printer, media, or duplex settings in the dialog.
            var caps = dialog.PrintQueue.GetPrintCapabilities(dialog.PrintTicket);
            if (wantsDuplex && (caps.DuplexingCapability == null ||
                !caps.DuplexingCapability.Contains(design.DuplexMode == "Short-edge (flip)"
                    ? System.Printing.Duplexing.TwoSidedShortEdge
                    : System.Printing.Duplexing.TwoSidedLongEdge)))
            {
                MessageBox.Show("This printer driver does not report support for the selected automatic duplex mode. " +
                    "Select a duplex-capable printer or choose One-sided and use your printer's manual duplex procedure.",
                    "Duplex not supported", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (wantsDuplex)
                dialog.PrintTicket.Duplexing = design.DuplexMode == "Short-edge (flip)"
                    ? System.Printing.Duplexing.TwoSidedShortEdge
                    : System.Printing.Duplexing.TwoSidedLongEdge;
            string details = $"Sheet: {design.PaperWidthIn} × {design.PaperHeightIn} inches\n" +
                $"Logical folded panels: {design.PrintedSides}\nPhysical sheet faces: {((design.PrintedSides + 3) / 4) * 2}\n" +
                $"Mode: {(wantsDuplex ? design.DuplexMode : "One-sided")}\n\n" +
                "Set matching paper size in printer preferences. Test on plain paper before using preprinted stock. Print now?";
            if (MessageBox.Show(details, "Confirm print setup", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            EnsurePanels();
            dialog.PrintDocument(BulletinPrintBuilder.Create(_bulletin, design).DocumentPaginator,
                "Valley Baptist Bulletin");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Print failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        var preview = new PrintPreviewWindow(BulletinPrintBuilder.Create(_bulletin, DesignSettings.Load())) { Owner = this };
        preview.ShowDialog();
    }
    private void History_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Open a saved bulletin", Filter = "Bulletin files (*.bulletin.json)|*.bulletin.json", InitialDirectory = BulletinFolder };
        if (dialog.ShowDialog() != true) return;
        try { SetBulletin(BulletinStorage.Load(dialog.FileName)); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Open failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private static string BulletinFolder => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Valley Baptist Bulletins");
}
