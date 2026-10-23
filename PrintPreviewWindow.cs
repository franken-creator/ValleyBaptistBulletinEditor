using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
namespace ValleyBaptistBulletinEditor;
public sealed class PrintPreviewWindow : Window
{
    public PrintPreviewWindow(FixedDocument document)
    {
        Title = "Bulletin Print Preview — use Print / PDF on main window to print";
        Width = 1100; Height = 800; MinWidth = 700; MinHeight = 500;
        Content = new DocumentViewer { Document = document };
    }
}
