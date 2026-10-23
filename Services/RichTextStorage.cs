using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using ValleyBaptistBulletinEditor.Models;

namespace ValleyBaptistBulletinEditor.Services;

public static class RichTextStorage
{
    public static FlowDocument Create(BulletinTextBox item, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(item.RichTextXaml))
        {
            try
            {
                if (XamlReader.Parse(item.RichTextXaml) is FlowDocument saved)
                {
                    saved.PagePadding = new Thickness(0);
                    return saved;
                }
            }
            catch { /* Old or damaged rich text falls back to plain text. */ }
        }
        var document = new FlowDocument { PagePadding = new Thickness(0),
            FontFamily = new FontFamily(item.FontFamily), FontSize = item.FontSize,
            FontWeight = item.Bold ? FontWeights.Bold : FontWeights.Normal };
        try { document.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.Color)); }
        catch { document.Foreground = Brushes.Black; }
        var paragraph = new Paragraph { Margin = new Thickness(0),
            TextAlignment = item.Alignment == "Center" ? TextAlignment.Center :
                item.Alignment == "Right" ? TextAlignment.Right : TextAlignment.Left };
        paragraph.Inlines.Add(new Run(fallback));
        document.Blocks.Add(paragraph);
        return document;
    }
    public static string Save(FlowDocument document) => XamlWriter.Save(document);
    public static string PlainText(FlowDocument document) =>
        new TextRange(document.ContentStart, document.ContentEnd).Text.TrimEnd('\r', '\n');
}
