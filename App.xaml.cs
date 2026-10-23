using System.Windows;
using ValleyBaptistBulletinEditor.Models;
namespace ValleyBaptistBulletinEditor;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var bulletin = new Bulletin();
        for (int i = 0; i < 4; i++) bulletin.Panels.Add(new BulletinPanel());
        for (int page = 2; page <= 4; page++)
        {
            bulletin.Panels[page - 1].UseChurchTemplate = true;
            PublisherLayout.Ensure(bulletin, page);
        }
        try
        {
            var pages = Services.PageDraftStorage.LoadLast();
            if (pages != null && pages.Count > 0) bulletin.Panels = pages;
        }
        catch (System.Exception) { /* A damaged draft must not prevent startup. */ }
        var window = new PublisherPageEditor(bulletin, DesignSettings.Load(), 2);
        MainWindow = window;
        window.Show();
    }
}
