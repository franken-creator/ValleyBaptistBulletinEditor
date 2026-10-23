using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ValleyBaptistBulletinEditor.Models;
using ValleyBaptistBulletinEditor.Services;

namespace ValleyBaptistBulletinEditor;

/// <summary>Prints logical folded panels onto physical sheet faces. Preview and print use identical pages.</summary>
public static class BulletinPrintBuilder
{
    public static FixedDocument Create(Bulletin bulletin, DesignSettings design, bool livePreview = false)
    {
        int panelCount = Math.Clamp(design.PrintedSides, 1, 12);
        double width = Math.Clamp(design.PaperWidthIn, 2, 40) * 96;
        double height = Math.Clamp(design.PaperHeightIn, 2, 40) * 96;
        var doc = new FixedDocument();
        doc.DocumentPaginator.PageSize = new Size(width, height);
        if (design.PageFormat == "Single-panel sheets")
        {
            for (int number = 1; number <= panelCount; number++)
                AddSingleFace(doc, bulletin, design, width, height, number, livePreview);
            return doc;
        }
        // A 4-panel half-fold bulletin: outside = [4 | 1], inside = [2 | 3].
        // Larger counts form independent 4-panel signatures (each sheet is two faces).
        for (int basePanel = 0; basePanel < panelCount; basePanel += 4)
        {
            int p1 = basePanel + 1, p2 = basePanel + 2, p3 = basePanel + 3, p4 = basePanel + 4;
            AddFace(doc, bulletin, design, width, height, p4 <= panelCount ? p4 : 0, p1, livePreview);
            AddFace(doc, bulletin, design, width, height, p2 <= panelCount ? p2 : 0, p3 <= panelCount ? p3 : 0, livePreview);
        }
        return doc;
    }
    private static void AddSingleFace(FixedDocument doc, Bulletin b, DesignSettings d, double w, double h, int number, bool livePreview)
    {
        var sheet = new Grid { Width = w, Height = h, Background = Brushes.White };
        sheet.ColumnDefinitions.Add(new ColumnDefinition());
        AddPanel(sheet, b, d, number, 0, w, h, livePreview);
        var page = new FixedPage { Width = w, Height = h, Background = Brushes.White };
        page.Children.Add(sheet);
        var content = new PageContent();
        ((System.Windows.Markup.IAddChild)content).AddChild(page);
        doc.Pages.Add(content);
    }
    private static void AddFace(FixedDocument doc, Bulletin b, DesignSettings d, double w, double h, int left, int right, bool livePreview)
    {
        var sheet = new Grid { Width = w, Height = h, Background = Brushes.White };
        sheet.ColumnDefinitions.Add(new ColumnDefinition());
        sheet.ColumnDefinitions.Add(new ColumnDefinition());
        AddPanel(sheet, b, d, left, 0, w / 2, h, livePreview);
        AddPanel(sheet, b, d, right, 1, w / 2, h, livePreview);
        var page = new FixedPage { Width = w, Height = h, Background = Brushes.White };
        page.Children.Add(sheet);
        var content = new PageContent();
        ((System.Windows.Markup.IAddChild)content).AddChild(page);
        doc.Pages.Add(content);
    }
    private static void AddPanel(Grid sheet, Bulletin b, DesignSettings d, int pageNumber, int column, double width, double height, bool livePreview)
    {
        if (pageNumber == 0) return;
        if (pageNumber == 1 && d.LockPreprintedCover)
        {
            if (livePreview && d.CoverPreviewMode == "Show cover picture" &&
                !string.IsNullOrWhiteSpace(d.CoverPreviewImageData))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(d.CoverPreviewImageData);
                    using var stream = new MemoryStream(bytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                    var picture = new Image { Source = bitmap, Stretch = Stretch.Uniform,
                        Margin = new Thickness(4) };
                    var holder = new Border { Child = picture, ClipToBounds = true };
                    Grid.SetColumn(holder, column); sheet.Children.Add(holder);
                }
                catch { /* An invalid cover image must not prevent preview. */ }
            }
            // In the LIVE editor only, distinguish "Blank page" from "Hide page".
            // Neither label nor photograph is ever sent to the printer.
            if (livePreview && d.CoverPreviewMode == "Blank page")
            {
                var label = new TextBlock
                {
                    Text = "PAGE 1 — PREPRINTED COVER\n(Blank in preview; nothing will print here)",
                    Foreground = Brushes.Gray, FontSize = 13, TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(18)
                };
                var placeholder = new Border
                {
                    Child = label, BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(10)
                };
                Grid.SetColumn(placeholder, column);
                sheet.Children.Add(placeholder);
            }
            // Blank and hidden cover modes leave this position empty. The physical sheet
            // still contains the cover panel; hiding it must never shift other panels.
            return;
        }
        foreach (string hidden in (d.HiddenPages ?? "").Split(','))
            if (int.TryParse(hidden, out int n) && n == pageNumber) return;
        if (pageNumber > b.Panels.Count) return;
        BulletinPanel data = b.Panels[pageNumber - 1];
        if (!data.Enabled || data.IntentionallyBlank) return;
        double margin = Math.Clamp(d.MarginIn * 96, 0, Math.Min(width, height) / 3);
        var panel = new StackPanel { Margin = new Thickness(margin) };
        panel.RenderTransform = new TranslateTransform(d.OffsetXIn * 96, d.OffsetYIn * 96);
        panel.SetValue(TextElement.FontFamilyProperty, new FontFamily(d.Font));
        string mode = d.GlobalAlignment == "Per section" ? data.Alignment : d.GlobalAlignment;
        TextAlignment alignment = mode == "Center" ? TextAlignment.Center : mode == "Right" ? TextAlignment.Right : TextAlignment.Left;
        Brush accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(d.Theme switch
        { "Sage & Cream" => "#526C60", "Black & White" => "#292929", "Burgundy & Gold" => "#783B4C", _ => "#243956" }));
        if ((data.TextBoxes != null && data.TextBoxes.Count > 0) || (data.Images != null && data.Images.Count > 0))
        {
            // Editor objects use a 528 x 816 logical panel. Scale them to the actual
            // physical panel while preserving their relative positions and dimensions.
            // The printable inset is determined by the physical safe margin.
            double usableW = Math.Max(1, width - 2 * margin);
            double usableH = Math.Max(1, height - 2 * margin);
            var layer = new Canvas { Width = 528, Height = 816, ClipToBounds = true };
            var fitted = new Viewbox { Width = usableW, Height = usableH,
                Stretch = Stretch.Fill, Child = layer,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top };
            // Offsets are applied to the entire composed panel, not to text individually.
            
            foreach (var item in data.TextBoxes ?? new())
            {
                var value = item.Field.Length > 0
                    ? typeof(Bulletin).GetProperty(item.Field)?.GetValue(b)?.ToString() ?? item.Text
                    : item.Text;
                Brush color;
                try { color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.Color)); }
                catch { color = Brushes.Black; }
                var tb = new RichTextBox {
                    Document = RichTextStorage.Create(item,value),
                    Width=item.Width,Height=item.Height,
                    IsReadOnly=true, IsDocumentEnabled=false,
                    IsHitTestVisible=false, Focusable=false,
                    Background=Brushes.Transparent,BorderThickness=new Thickness(0),
                    Padding=new Thickness(0),
                    HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility=ScrollBarVisibility.Disabled
                };
                tb.Document.PageWidth=Math.Max(10,item.Width-4);
                Canvas.SetLeft(tb, item.X); Canvas.SetTop(tb, item.Y); Panel.SetZIndex(tb,item.ZIndex); layer.Children.Add(tb);
            }
            foreach (var item in data.Images ?? new())
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(item.ImageData);
                    using var stream = new MemoryStream(bytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                    var picture = new Image { Source = bitmap, Width = item.Width,
                        Height = item.Height, Stretch = Stretch.Uniform };
                    Canvas.SetLeft(picture, item.X); Canvas.SetTop(picture, item.Y); Panel.SetZIndex(picture,item.ZIndex);
                    layer.Children.Add(picture);
                }
                catch { /* Invalid embedded image: skip while printing */ }
            }
            panel.Children.Add(fitted);
        }
        else if (data.UseChurchTemplate && pageNumber >= 2 && pageNumber <= 4 &&
            d.PageFormat == "Four-panel half-fold")
        {
            RenderChurchPanel(panel, b, d, pageNumber, accent);
        }
        else
        {
        if (!string.IsNullOrWhiteSpace(data.Title))
            panel.Children.Add(new TextBlock { Text = data.Title, FontWeight = FontWeights.Bold,
                FontSize = d.TitleSize, Foreground = accent, TextWrapping = TextWrapping.Wrap,
                TextAlignment = alignment, Margin = new Thickness(0, 0, 0, d.SectionSpacing) });
        if (!string.IsNullOrWhiteSpace(data.Body))
            panel.Children.Add(new TextBlock { Text = data.Body, FontSize = d.BodyFontSize,
                TextWrapping = TextWrapping.Wrap, TextAlignment = alignment,
                Margin = new Thickness(0, 0, 0, d.SectionSpacing) });
        }
        if (!string.IsNullOrWhiteSpace(data.ImageData) || !string.IsNullOrWhiteSpace(data.ImagePath))
        {
            try
            {
                BitmapImage bitmap = new();
                if (!string.IsNullOrWhiteSpace(data.ImageData))
                {
                    byte[] bytes = Convert.FromBase64String(data.ImageData);
                    using var stream = new MemoryStream(bytes);
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                }
                else
                {
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(Path.GetFullPath(data.ImagePath)); bitmap.EndInit(); bitmap.Freeze();
                }
                panel.Children.Add(new Image { Source = bitmap, Stretch = Stretch.Uniform,
                    MaxWidth = Math.Max(50, width - 2 * margin), MaxHeight = height * 0.55,
                    HorizontalAlignment = mode == "Right" ? HorizontalAlignment.Right :
                        mode == "Center" ? HorizontalAlignment.Center : HorizontalAlignment.Left });
            }
            catch { panel.Children.Add(new TextBlock { Text = "[Image unavailable]", Foreground = Brushes.DarkRed }); }
        }
        Brush background;
        try { background=d.PreprintedStock ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString(data.BackgroundColor)); }
        catch { background=Brushes.White; }
        var clip = new Border { Child = panel, ClipToBounds = true, Background=background };
        if(!d.PreprintedStock && !string.IsNullOrWhiteSpace(data.BackgroundImageData))
        {
            try
            {
                using var stream=new MemoryStream(Convert.FromBase64String(data.BackgroundImageData));
                var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;
                bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
                var image=new Image{Source=bitmap,Stretch=Stretch.UniformToFill,IsHitTestVisible=false};
                var imageLayer=new Grid{ClipToBounds=true};imageLayer.Children.Add(image);
                double brightness=Math.Clamp(data.BackgroundBrightness,0.2,1.8);
                if(brightness!=1)
                {
                    byte alpha=brightness<1?(byte)Math.Round((1-brightness)*255):(byte)Math.Round((brightness-1)/0.8*200);
                    imageLayer.Children.Add(new Border{Background=new SolidColorBrush(brightness<1?Color.FromArgb(alpha,0,0,0):Color.FromArgb(alpha,255,255,255))});
                }
                // Background goes behind all text, artwork and the optional border.
                var content=clip.Child;clip.Child=null;
                var layers=new Grid();layers.Children.Add(imageLayer);layers.Children.Add(content);
                clip.Child=layers;
            }
            catch { /* Keep the page and its text if an image is invalid. */ }
        }
        if(data.ShowBorder && !d.PreprintedStock)
        {
            clip.BorderBrush=new SolidColorBrush(Color.FromRgb(67,85,104));
            clip.BorderThickness=new Thickness(1.5);
        }
        Grid.SetColumn(clip, column);
        sheet.Children.Add(clip);
    }

    // Original purchased four-panel bulletin:
    // outside [4 | 1], inside [2 | 3]. Text is composed as separate blocks
    // so different headings, alignments, and emphasis can coexist on one panel.
    private static void RenderChurchPanel(StackPanel panel, Bulletin b, DesignSettings d, int number, Brush accent)
    {
        double fs = Math.Clamp(d.BodyFontSize - 1.5, 7.2, 11);
        Brush ink = Brushes.Black;
        TextBlock Block(string value, double size, bool bold = false,
            Brush? brush = null, TextAlignment alignment = TextAlignment.Left, double bottom = 5)
            => new()
            {
                Text = value ?? "", FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = brush ?? ink, TextAlignment = alignment, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, bottom),
                LineHeight = size * 1.16
            };
        void Put(string value, double size = 0, bool bold = false, Brush? brush = null,
            TextAlignment alignment = TextAlignment.Left, double bottom = 5)
            => panel.Children.Add(Block(value, size <= 0 ? fs : size, bold, brush, alignment, bottom));

        if (number == 2)
        {
            Put("Welcome to: " + b.Title, fs + 1, false, ink, TextAlignment.Center, 4);
            Put(b.Date + "\n" + b.Verse, fs + 0.5, false, ink, TextAlignment.Center, 12);
            Put("Interim Pastor  . . . . . . . . . .  " + b.Pastor +
                "\nSecretary  . . . . . . . . . . . . .  " + b.Secretary, fs - 0.5, true, ink, bottom: 2);
            Put(b.Staff, fs - 0.5, false, ink, bottom: 9);
            Put("Welcome/Prayer  . . .  " + b.Welcome, fs, true, ink, bottom: 10);
            Put(b.Hymns, fs, false, ink, bottom: 10);
            Put("SERMON  •  " + b.Pastor, fs + 0.4, true, accent, bottom: 3);
            Put(b.Sermon + "\n" + b.Scripture, fs + 0.2, true, accent, TextAlignment.Center, 10);
            Put(b.Offering + "\n" + b.Attendance, fs - 0.4, true, accent, bottom: 10);
            Put(b.Schedule, fs - 0.5, false, ink, bottom: 0);
        }
        else if (number == 3)
        {
            Put("OFFICE HOURS\n9AM–1PM\nMON. TUES. THURS.\n37702 W. INDIAN SCHOOL RD.\n(623) 393-0211",
                fs, false, ink, TextAlignment.Center, 18);
            Put("OFFICE STAFF AVAILABLE FOR PRAYER", fs + 0.3, true, accent, TextAlignment.Center, 12);
            Put(b.Announcements, fs, false, ink, TextAlignment.Center, 15);
            Put("SERVICE TIMES", fs + 0.2, true, accent, TextAlignment.Center, 6);
            Put("Sunday Evening Service — 6:00 PM", fs, true, ink, TextAlignment.Center, 12);
            Put("Please make tithe and offering checks payable to:\nValley Baptist Church",
                fs, false, ink, TextAlignment.Center, 10);
            Put("tonopahvalleybaptist.com", fs, true, ink, TextAlignment.Center, 0);
        }
        else if (number == 4)
        {
            Put("Email: valleybaptisttw@yahoo.com\nWebsite: tonopahvalleybaptist.com",
                fs, true, ink, TextAlignment.Center, 26);
            Put("Calendar 2026", fs + 6, true, accent, TextAlignment.Center, 3);
            Put("October", fs + 4, true, accent, TextAlignment.Center, 18);
            Put(b.Calendar, fs + 0.5, true, accent, TextAlignment.Left, 26);
            Put("Church Motto", fs, true, Brushes.DarkBlue, TextAlignment.Center, 7);
            Put(b.Footer, fs, true, Brushes.DarkBlue, TextAlignment.Center, 0);
        }
    }

}
