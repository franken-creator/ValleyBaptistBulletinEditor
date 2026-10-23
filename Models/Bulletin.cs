using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace ValleyBaptistBulletinEditor.Models;
public sealed class Bulletin : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    // Each logical folded panel is editable independently. Empty panels print blank.
    public List<BulletinPanel> Panels { get; set; } = new();

    private void Set(ref string field, string value, [CallerMemberName] string? name = null)
    { if (field == value) return; field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
    private string _date="October 4, 2026", _verse="John 3:16", _pastor="Terry Wong", _secretary="Beverly Trice", _staff="Associate Pastors: Mike Goodrich (Counselor); James White (Outreach)\nYouth: Eric & Sasha Lanzty\nChildren's Ministry: Judy Bingham\nPantry: Carmen Jimenez\nUshers: Ricky Hardage\nMusic: Mike G., Tim A., Jackie A., Lisa L., David D.\nNursery: Brianna", _welcome="Associate Pastor Mike Goodrich", _hymns="I Will Sing The Wondrous Story — 633\nAs The Deer (Offertory) — 554\nIn Christ Alone — 506\nThis Is Amazing Grace\nWithout Him (Invitation) — 470\nI've Got Peace Like a River (Closing) — 618", _sermon="Peaceful Living in an Uptight World", _scripture="Galatians 5:19–20", _offering="9/25 Offering $1,332.00 / Online $0.00 / Total $0.00", _attendance="Morning (00) / Evening (0)", _schedule="Sunday School — 9:00 AM\nSunday Morning Worship — 10:30 AM\nChildren's Church — 10:30 AM\nSunday Evening Service — 6:00 PM\nWednesday AWANA — 6:00–8:00 PM\nSaturday Bible Study — 9:00 AM", _announcements="Monday morning prayer, coffee and fellowship: on hold until December 7.\nWednesday AWANA: bring your kids and neighbors' kids.\nPantry donations: specify Pantry on your envelope or check.\nPlease keep Pastor's family in prayer. Celebration of Life: October 17, 2026 at 10 AM.", _calendar="October 10 — Women's Brunch / Meeting, 11 AM–12:30 PM\nOctober 16 — Ladies Friday Evening, 6–9 PM\nOctober 17 — Celebration of Life, 10 AM; Mexican food potluck following\nOctober 17 — Youth Night, 6–8 PM", _footer="We love children and appreciate their parents.\nPray for the Peace of Jerusalem — Psalm 122:6", _title="Valley Baptist Church";
    public string Title { get=>_title; set=>Set(ref _title,value); }
    public string Date { get=>_date; set=>Set(ref _date,value); }
    public string Verse { get=>_verse; set=>Set(ref _verse,value); }
    public string Pastor { get=>_pastor; set=>Set(ref _pastor,value); }
    public string Secretary { get=>_secretary; set=>Set(ref _secretary,value); }
    public string Staff { get=>_staff; set=>Set(ref _staff,value); }
    public string Welcome { get=>_welcome; set=>Set(ref _welcome,value); }
    public string Hymns { get=>_hymns; set=>Set(ref _hymns,value); }
    public string Sermon { get=>_sermon; set=>Set(ref _sermon,value); }
    public string Scripture { get=>_scripture; set=>Set(ref _scripture,value); }
    public string Offering { get=>_offering; set=>Set(ref _offering,value); }
    public string Attendance { get=>_attendance; set=>Set(ref _attendance,value); }
    public string Schedule { get=>_schedule; set=>Set(ref _schedule,value); }
    public string Announcements { get=>_announcements; set=>Set(ref _announcements,value); }
    public string Calendar { get=>_calendar; set=>Set(ref _calendar,value); }
    public string Footer { get=>_footer; set=>Set(ref _footer,value); }
}

public sealed class BulletinPanel
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public string ImageData { get; set; } = ""; // Embedded image for portable saved bulletins
    public string Alignment { get; set; } = "Left";
    public bool IntentionallyBlank { get; set; } = false;
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public string BackgroundImageData { get; set; } = "";
    public double BackgroundBrightness { get; set; } = 1.0;
    public bool ShowBorder { get; set; } = false;
    public bool Enabled { get; set; } = true;
    public List<BulletinTextBox> TextBoxes { get; set; } = new();
    public List<BulletinImageBox> Images { get; set; } = new();
    public bool UseChurchTemplate { get; set; } = false; // Render structured weekly fields in original four-panel arrangement
}

public sealed class BulletinTextBox
{
    public int ZIndex { get; set; } = 0;
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public string RichTextXaml { get; set; } = ""; // Per-character formatting; blank for legacy text
    public double X { get; set; } = 20;
    public double Y { get; set; } = 20;
    public double Width { get; set; } = 475;
    public double Height { get; set; } = 80;
    public double FontSize { get; set; } = 12;
    public string FontFamily { get; set; } = "Segoe UI";
    public bool Bold { get; set; }
    public string Alignment { get; set; } = "Left";
    public string Color { get; set; } = "#222222";
    public string Field { get; set; } = ""; // Optional weekly field binding
}


public sealed class BulletinImageBox
{
    public int ZIndex { get; set; } = 0;
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
    public string ImageData { get; set; } = "";
    public double X { get; set; } = 40;
    public double Y { get; set; } = 80;
    public double Width { get; set; } = 240;
    public double Height { get; set; } = 180;
}
