using System;
using System.Collections.Generic;
using ValleyBaptistBulletinEditor.Models;
namespace ValleyBaptistBulletinEditor;
public static class PublisherLayout
{
    public static void Ensure(Bulletin b, int page)
    {
        if (page < 2 || page > 4 || b.Panels.Count < page) return;
        var p=b.Panels[page-1]; p.TextBoxes ??= new();
        if (p.TextBoxes.Count > 0) return;
        void Add(string field,double x,double y,double w,double h,double size=11,bool bold=false,string align="Left",string color="#222222")
        {
            var property=typeof(Bulletin).GetProperty(field);
            p.TextBoxes.Add(new BulletinTextBox{Field=field,Text=property?.GetValue(b)?.ToString()??"",X=x,Y=y,Width=w,Height=h,FontSize=size,Bold=bold,Alignment=align,Color=color});
        }
        if(page==2)
        {
            // One editable header text box instead of three separate boxes.
            p.TextBoxes.Add(new BulletinTextBox {
                Text = string.Join("\n", new[] { b.Title, b.Date, b.Verse }),
                X=24, Y=12, Width=480, Height=88, FontSize=14, Bold=false,
                Alignment="Center", Color="#222222"
            });
            Add("Pastor",24,104,235,24);
            Add("Secretary",268,104,230,24); Add("Staff",24,135,480,172,10);
            Add("Welcome",24,315,480,28,11,true); Add("Hymns",24,352,480,162,11);
            Add("Sermon",24,526,480,45,13,true,"Center","#243956");
            Add("Scripture",24,574,480,26,11,false,"Center");
            Add("Offering",24,609,480,40,10); Add("Attendance",24,651,480,26,10);
            Add("Schedule",24,685,480,110,9);
        }
        else if(page==3)
        {
            p.TextBoxes.Add(new BulletinTextBox{Text="OFFICE HOURS\n9 AM–1 PM · MON / TUES / THURS\n37702 W. Indian School Rd.\n(623) 393-0211",X=28,Y=22,Width=470,Height=116,FontSize=12,Alignment="Center"});
            Add("Announcements",28,145,470,455,12,false,"Center");
            Add("Schedule",28,610,470,166,11,false,"Center");
        }
        else
        {
            p.TextBoxes.Add(new BulletinTextBox{Text="Email: valleybaptisttw@yahoo.com\nWebsite: tonopahvalleybaptist.com",X=26,Y=24,Width=475,Height=72,FontSize=11,Alignment="Center"});
            Add("Calendar",26,132,475,415,13,true,"Left","#243956");
            Add("Footer",26,565,475,175,13,true,"Center","#243956");
        }
        p.UseChurchTemplate=true;
    }
}
