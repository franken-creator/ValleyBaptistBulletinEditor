using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using ValleyBaptistBulletinEditor.Models;
using ValleyBaptistBulletinEditor.Services;

namespace ValleyBaptistBulletinEditor;
public sealed class PublisherPageEditor : Window
{
    Bulletin bulletin;
    DesignSettings settings;
    readonly bool publisherMode;
    readonly TextBlock appHeader = new();
    void RefreshAppHeader()
    {
        string name = ChurchNameSettings.Load();
        appHeader.Text = name.ToUpperInvariant() + "  /  BULLETIN EDITOR";
        Title = name + " | Bulletin Editor";
    }
    void EditChurchName()
    {
        if (!AdminSecurity.IsPublisher)
        {
            MessageBox.Show(this, "Sign in with a Publisher account to change the church name.",
                "Publisher Only", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new Window { Title = "Publisher Settings — Church Name", Owner = this,
            Width = 470, Height = 230, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = "Church name displayed in the application header",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10) });
        var entry = new TextBox { Text = ChurchNameSettings.Load(), MaxLength = 100,
            Padding = new Thickness(8), FontSize = 15 };
        body.Children.Add(entry);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,18,0,0) };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(15,7,15,7), Margin = new Thickness(5) };
        cancel.Click += (_,_) => dialog.DialogResult = false;
        var save = new Button { Content = "Save Church Name", Padding = new Thickness(15,7,15,7), Margin = new Thickness(5) };
        save.Click += (_,_) =>
        {
            try
            {
                ChurchNameSettings.Save(entry.Text);
                dialog.DialogResult = true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            { MessageBox.Show(dialog, ex.Message, "Unable to Save", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
        buttons.Children.Add(cancel); buttons.Children.Add(save); body.Children.Add(buttons);
        dialog.Content = body;
        if (dialog.ShowDialog() == true) RefreshAppHeader();
    }
    readonly Canvas surface = new() { Width=528, Height=816, Background=Brushes.White };
    readonly ComboBox pagePicker=new(){Width=115,Margin=new Thickness(5)};
    readonly CheckBox admin=new(){Content="Admin: move and resize",Margin=new Thickness(12,0,10,0)};
    readonly TextBox sizeBox=new(){Width=45,Text="12",Margin=new Thickness(3)};
    readonly TextBox widthBox=new(){Width=55,Margin=new Thickness(3)};
    readonly TextBox heightBox=new(){Width=55,Margin=new Thickness(3)};
    readonly ComboBox fontBox=new(){Width=170,Margin=new Thickness(3)};
    readonly ComboBox colorBox=new(){Width=170,Margin=new Thickness(3),IsEditable=false};
    readonly Dictionary<string,string> fontColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Black"]="#000000", ["Charcoal"]="#222222", ["Dark Gray"]="#555555", ["Gray"]="#808080",
        ["Light Gray"]="#C0C0C0", ["White"]="#FFFFFF", ["Navy Blue"]="#243956", ["Midnight Blue"]="#191970",
        ["Royal Blue"]="#4169E1", ["Blue"]="#0000FF", ["Sky Blue"]="#146C94", ["Light Blue"]="#87CEFA",
        ["Teal"]="#008080", ["Turquoise"]="#40E0D0", ["Forest Green"]="#228B22", ["Sage Green"]="#526C60",
        ["Green"]="#008000", ["Mint Green"]="#3EB489", ["Olive"]="#808000", ["Burgundy"]="#783B4C",
        ["Maroon"]="#800000", ["Red"]="#B22222", ["Crimson"]="#DC143C", ["Coral"]="#FF7F50",
        ["Orange"]="#FF8C00", ["Gold"]="#986D22", ["Yellow"]="#FFD700", ["Brown"]="#8B4513",
        ["Purple"]="#6A4C93", ["Violet"]="#8A2BE2", ["Lavender"]="#9370DB", ["Pink"]="#FF69B4",
        ["Snow White"]="#FFFAFA", ["Ivory"]="#FFFFF0", ["Cream"]="#FFFDD0", ["Beige"]="#F5F5DC",
        ["Silver"]="#C0C0C0", ["Slate Gray"]="#708090", ["Steel Blue"]="#4682B4", ["Powder Blue"]="#B0E0E6",
        ["Cornflower Blue"]="#6495ED", ["Dodger Blue"]="#1E90FF", ["Deep Sky Blue"]="#00BFFF", ["Cobalt Blue"]="#0047AB",
        ["Indigo"]="#4B0082", ["Denim Blue"]="#1560BD", ["Aqua"]="#00FFFF", ["Cyan"]="#00CED1",
        ["Dark Teal"]="#006666", ["Sea Green"]="#2E8B57", ["Emerald Green"]="#50C878", ["Kelly Green"]="#4CBB17",
        ["Lime Green"]="#32CD32", ["Chartreuse"]="#7FFF00", ["Dark Olive Green"]="#556B2F", ["Pine Green"]="#01796F",
        ["Pale Green"]="#98FB98", ["Seafoam"]="#9FE2BF", ["Pistachio"]="#93C572", ["Mustard Yellow"]="#D4A017",
        ["Goldenrod"]="#DAA520", ["Amber"]="#FFBF00", ["Lemon Yellow"]="#FFF44F", ["Canary Yellow"]="#FFFF99",
        ["Tangerine"]="#F28500", ["Burnt Orange"]="#CC5500", ["Peach"]="#FFDAB9", ["Apricot"]="#FBCEB1",
        ["Salmon"]="#FA8072", ["Tomato Red"]="#FF6347", ["Scarlet"]="#FF2400", ["Firebrick"]="#B22222",
        ["Brick Red"]="#CB4154", ["Ruby Red"]="#9B111E", ["Wine Red"]="#722F37", ["Rose"]="#FF007F",
        ["Rose Pink"]="#FF66CC", ["Blush Pink"]="#DE5D83", ["Hot Pink"]="#FF69B4", ["Magenta"]="#FF00FF",
        ["Fuchsia"]="#FF00FF", ["Orchid"]="#DA70D6", ["Plum"]="#8E4585", ["Lilac"]="#C8A2C8",
        ["Periwinkle"]="#CCCCFF", ["Mauve"]="#E0B0FF", ["Amethyst"]="#9966CC", ["Eggplant"]="#614051",
        ["Tan"]="#D2B48C", ["Khaki"]="#C3B091", ["Camel"]="#C19A6B", ["Caramel"]="#AF6F09",
        ["Chocolate"]="#7B3F00", ["Coffee Brown"]="#6F4E37", ["Rust"]="#B7410E", ["Copper"]="#B87333"
    };
    bool syncingFontColor;
    bool syncingInspector;
    readonly Border colorPreview=new(){Width=34,Height=24,CornerRadius=new CornerRadius(4),BorderThickness=new Thickness(1),BorderBrush=Brushes.Gray,Margin=new Thickness(6,2,2,2)};
    readonly ComboBox templateBox=new(){Width=180,Margin=new Thickness(4)};
    readonly ComboBox alignBox=new(){Width=83,Margin=new Thickness(3)};
    readonly CheckBox boldBox=new(){Content="Bold",Margin=new Thickness(5)};
    readonly TextBlock status=new(){Foreground=Brushes.DarkSlateGray,Margin=new Thickness(10)};
    readonly System.Collections.Generic.Dictionary<string,RichTextBox> views=new();
    BulletinTextBox? selected;
    RichTextBox? selectedView;
    int page=2;
    Point dragStart;
    double startX,startY;
    bool dragging;
    bool adminUnlocked;
    bool updatingAdminToggle;
    // Complete bulletin snapshots support cross-object undo/redo, including text and layout.
    readonly Stack<string> undoHistory = new();
    readonly Stack<string> redoHistory = new();
    // Page-only drafts live in AppData and never overwrite manually saved bulletin files.
    readonly DispatcherTimer pageAutosaveTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    string draftKey = "startup";
    string lastDraftSnapshot = "";
    void SavePageDraft(bool force = false)
    {
        try
        {
            string snapshot = JsonSerializer.Serialize(bulletin.Panels);
            if (!force && snapshot == lastDraftSnapshot) return;
            Services.PageDraftStorage.Save(draftKey, bulletin.Panels);
            lastDraftSnapshot = snapshot;
        }
        catch (Exception ex) { status.Text = "Page autosave failed: " + ex.Message; }
    }
    void RestorePageDraft()
    {
        var saved = Services.PageDraftStorage.Load(draftKey);
        if (saved != null && saved.Count > 0)
        {
            bulletin.Panels = saved;
            while (bulletin.Panels.Count < Math.Max(4, settings.PrintedSides))
                bulletin.Panels.Add(new BulletinPanel());
        }
        lastDraftSnapshot = JsonSerializer.Serialize(bulletin.Panels);
    }
    bool restoringHistory;
    const int MaxHistory = 100;
    string Snapshot() => JsonSerializer.Serialize(bulletin);
    void Checkpoint()
    {
        if (restoringHistory) return;
        string snapshot = Snapshot();
        if (undoHistory.Count == 0 || undoHistory.Peek() != snapshot)
        {
            undoHistory.Push(snapshot);
            if (undoHistory.Count > MaxHistory)
            {
                var keep = undoHistory.Take(MaxHistory).Reverse().ToArray();
                undoHistory.Clear();
                foreach (var item in keep) undoHistory.Push(item);
            }
        }
        redoHistory.Clear();
    }
    void Undo()
    {
        if (undoHistory.Count == 0) { status.Text = "Nothing to undo."; return; }
        redoHistory.Push(Snapshot());
        RestoreSnapshot(undoHistory.Pop(), "Undo completed.");
    }
    void Redo()
    {
        if (redoHistory.Count == 0) { status.Text = "Nothing to redo."; return; }
        undoHistory.Push(Snapshot());
        RestoreSnapshot(redoHistory.Pop(), "Redo completed.");
    }
    void RestoreSnapshot(string snapshot, string message)
    {
        restoringHistory = true;
        try
        {
            bulletin = JsonSerializer.Deserialize<Bulletin>(snapshot) ?? bulletin;
            Render();
            status.Text = message;
        }
        finally { restoringHistory = false; }
    }
    void ResetHistory() { undoHistory.Clear(); redoHistory.Clear(); }

    bool showGuides=true;
    bool showGrid=false;
    readonly ScaleTransform pageZoom=new(1,1);
    readonly TextBlock zoomLabel=new(){Text="100%",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(7,0,5,0)};
    readonly Slider zoomSlider=new(){Minimum=20,Maximum=175,Value=100,Width=145,Margin=new Thickness(6,0,6,0),VerticalAlignment=VerticalAlignment.Center};
    readonly System.Collections.Generic.List<UIElement> guideOverlays=new();
    readonly System.Collections.Generic.List<Button> layoutActions=new();
    Button? pageSetupButton;
    Button? addTextButton;
    Button? deleteTextButton;
    Button? addImageButton;
    Button? deleteImageButton;
    Button? imageSizeButton;
    BulletinImageBox? selectedImage;
    Image? selectedImageView;
    readonly Canvas selectionOverlay=new(){Width=528,Height=816,IsHitTestVisible=true};
    readonly System.Collections.Generic.List<FrameworkElement> selectionHandles=new();
    readonly System.Collections.Generic.List<Button> pageButtons=new();
    bool drawingText;
    Point drawingStart;
    Rectangle? drawingRectangle;
    Button? accountsButton;
    Button? formattingButton;
    Button? templateApplyButton;
    StackPanel? formattingPanel;
    StackPanel? adminSettingsPanel;
    ScrollViewer? formattingScroll;
    public PublisherPageEditor(Bulletin b, DesignSettings d,int initialPage, bool publisherMode = false)
    {
        bulletin=b;settings=d;this.publisherMode=publisherMode;
        // Restore the latest edited pages on launch; leave other bulletin fields unchanged.
        lastDraftSnapshot = JsonSerializer.Serialize(bulletin.Panels);
        Title="Valley Baptist Bulletin Editor | Publisher-style Design Workspace"; Width=1500;Height=950;
        MinWidth=1040;MinHeight=690;WindowState=WindowState.Maximized;Background=new SolidColorBrush(Color.FromRgb(241,245,249));
        var root=new DockPanel(); Content=root;
        var header=new StackPanel{Background=new SolidColorBrush(Color.FromRgb(24,43,65))};
        DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        appHeader.FontSize=20;appHeader.FontWeight=FontWeights.Bold;appHeader.Foreground=Brushes.White;appHeader.Margin=new Thickness(16,12,12,4);
        header.Children.Add(appHeader);RefreshAppHeader();
        header.Children.Add(new TextBlock{Text="WEEKLY EDITION   •   Four-panel print layout   •   Publisher-controlled design",Foreground=Brushes.White,Margin=new Thickness(16,0,12,10)});
        var tools=new WrapPanel{Background=Brushes.White,Margin=new Thickness(8,8,8,4)};
        DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);
        Button Action(string label,Action callback){var btn=new Button{Content=label,Padding=new Thickness(12,7,12,7),Margin=new Thickness(4)};btn.Click+=(_,_)=>callback();tools.Children.Add(btn);return btn;}
        Action("+ New",NewBulletin);
        Action("Open",OpenBulletin);
        Action("Save",Save);
        Action("Print Preview",Preview);
        Action("Publisher / Admin Sign In",()=>
        {
            if (AdminSecurity.SignIn(this))
            {
                adminUnlocked = AdminSecurity.CanEditLayout;

                updatingAdminToggle = true;
                admin.IsChecked = adminUnlocked;
                updatingAdminToggle = false;

                UpdateAdminControls();
                ShowSelectionHandles();

                status.Text = adminUnlocked
                    ? "Publisher design unlocked."
                    : "Signed in. Layout editing is not permitted.";

                AdminSecurity.ShowAccountSettings(this);
            }
        });
        Action("Sign Out",()=>
        {
            AdminSecurity.SignOut();admin.IsChecked=false;
            UpdateAdminControls();status.Text="Signed out. Layout locked.";
        });
        // Publisher-inspired contextual ribbon. Actions are real controls, not mockups.
        var ribbon=new TabControl{Margin=new Thickness(8,2,8,3),Height=116,Background=Brushes.White};
        DockPanel.SetDock(ribbon,Dock.Top);root.Children.Add(ribbon);
        TabItem RibbonTab(string name, out WrapPanel band)
        {
            band=new WrapPanel{Margin=new Thickness(9,8,9,5),VerticalAlignment=VerticalAlignment.Top};
            var tab=new TabItem{Header="  "+name+"  ",Content=band};ribbon.Items.Add(tab);return tab;
        }
        Button RibbonButton(WrapPanel band,string caption,Action callback,bool design=false)
        {
            var button=new Button{Content=caption,Padding=new Thickness(12,11,12,11),Margin=new Thickness(3),
                Background=new SolidColorBrush(Color.FromRgb(238,244,251)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(207,221,237)),MinWidth=92};
            button.Click+=(_,_)=>callback();band.Children.Add(button);
            if(design)layoutActions.Add(button);
            return button;
        }
        RibbonTab("Home",out var homeBand);
        RibbonButton(homeBand,"Save",Save);
        RibbonButton(homeBand,"Undo (Ctrl+Z)",Undo);
        RibbonButton(homeBand,"Redo (Ctrl+Y)",Redo);
        RibbonButton(homeBand,"Open",OpenBulletin);
        RibbonButton(homeBand,"New Bulletin",NewBulletin);
        RibbonButton(homeBand,"Print Preview",Preview);
        RibbonButton(homeBand,"Edit Selected Text",()=>selectedView?.Focus());
        RibbonTab("Insert",out var insertBand);
        RibbonButton(insertBand,"Draw Text Box",()=>{drawingText=true;status.Text="Draw a text box: drag across the page to define its size.";},true);
        RibbonButton(insertBand,"Picture",InsertImage,true);
        RibbonButton(insertBand,"Delete Text Box",DeleteBox,true);
        RibbonButton(insertBand,"Delete Picture",DeleteImage,true);
        RibbonButton(insertBand,"Duplicate Object",DuplicateSelected,true);
        RibbonButton(insertBand,"Delete Selection",DeleteSelection,true);
        RibbonTab("Page Design",out var designBand);
        RibbonButton(designBand,"Page Setup",OpenDesignSettings,true);
        RibbonButton(designBand,"Apply Layout",ApplyLayoutPreset,true);
        RibbonButton(designBand,"Apply Text Style",ApplyFormat,true);
        RibbonButton(designBand,"Align Left",()=>AlignSelected("Left"),true);
        RibbonButton(designBand,"Center Text",()=>AlignSelected("Center"),true);
        RibbonButton(designBand,"Align Right",()=>AlignSelected("Right"),true);
        RibbonButton(designBand,"Page Color",ChoosePageColor,true);
        RibbonButton(designBand,"Page Background",OpenBackgroundDesigner,true);
        RibbonButton(designBand,"Page Border",TogglePageBorder,true);
        RibbonButton(designBand,"Bring to Front",BringToFront,true);
        RibbonButton(designBand,"Send to Back",SendToBack,true);
        RibbonButton(designBand,"Align Object Left",()=>AlignObject("Left"),true);
        RibbonButton(designBand,"Center Object",()=>AlignObject("Center"),true);
        RibbonButton(designBand,"Align Object Right",()=>AlignObject("Right"),true);
        RibbonButton(designBand,"Center Vertically",()=>AlignObject("Middle"),true);
        RibbonButton(designBand,"Top of Page",()=>AlignObject("Top"),true);
        RibbonButton(designBand,"Bottom of Page",()=>AlignObject("Bottom"),true);
        RibbonTab("View",out var viewBand);
        RibbonButton(viewBand,"Toggle Margin Guides",()=>{showGuides=!showGuides;Render();});
        RibbonButton(viewBand,"Toggle Grid",()=>{showGrid=!showGrid;Render();});
        RibbonButton(viewBand,"Fit Page",FitPageToWorkspace);
        RibbonButton(viewBand,"Actual Size",()=>SetZoom(100));
        RibbonButton(viewBand,"Zoom 125%",()=>SetZoom(125));
        ribbon.SelectedIndex=0;
        var tabs=new TabControl { Margin=new Thickness(8,2,8,6) };
        root.Children.Add(tabs);
        var editorTab=new TabItem { Header="  Edit Bulletin  " };
        var setupTab=new TabItem { Header="  Publisher Settings  " };
        tabs.Items.Add(editorTab);tabs.Items.Add(setupTab);
        tabs.SelectedIndex=0;
        var setupScroll=new ScrollViewer { VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        setupTab.Content=setupScroll;
        var setupPanel=new StackPanel { Margin=new Thickness(24),MaxWidth=660,
            HorizontalAlignment=HorizontalAlignment.Left };
        setupScroll.Content=setupPanel;
        setupPanel.Children.Add(new TextBlock { Text="PAGE SETUP & ADMINISTRATION",
            FontSize=22,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,12) });
        setupPanel.Children.Add(new TextBlock { Text=
            "All layout configuration is managed here. Staff can edit the bulletin in the first tab. " +
            "To change page layout, paper settings, fonts, or box positions, sign in with an administrator password.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18) });
        admin.Content="Unlock Publisher / Admin Layout";
        admin.Margin=new Thickness(0,4,0,14);
        setupPanel.Children.Add(admin);
        admin.IsChecked=false;
        admin.Checked+=(_,_)=>SwitchAdmin(true);
        admin.Unchecked+=(_,_)=>SwitchAdmin(false);
        Button SetupAction(string label,Action callback)
        {
            var button=new Button { Content=label,Padding=new Thickness(14,10,14,10),
                Margin=new Thickness(0,5,0,5),MinWidth=270,
                HorizontalAlignment=HorizontalAlignment.Left };
            button.Click+=(_,_)=>callback();
            setupPanel.Children.Add(button);return button;
        }
        // Administrator controls are completely hidden until successful authentication.
        adminSettingsPanel = new StackPanel();
        setupPanel.Children.Add(adminSettingsPanel);
        // Move the actions created below into the hidden administration section.
        pageSetupButton=SetupAction("Paper, Folding, Cover & Template Settings",OpenDesignSettings);
        accountsButton=SetupAction("My Account / Recovery Email",()=>AdminSecurity.ShowAccountSettings(this));
        SetupAction("My Administrative Settings",()=>AdminSecurity.ShowAccountSettings(this));
        setupPanel.Children.Add(new TextBlock { Text=
            "After unlocking, return to the Bulletin Page Editor tab to move or resize boxes. " +
            "Turn off the unlock checkbox when finished. A six-digit code sent to the verified recovery email is required to reset a forgotten password.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,16,0,0) });
        if(adminSettingsPanel != null)
        {
            foreach(var child in new UIElement[] { pageSetupButton!, accountsButton! })
            {
                setupPanel.Children.Remove(child);
                adminSettingsPanel.Children.Add(child);
            }
            // Transfer recovery/reset and help text to the same protected section.
            while(setupPanel.Children.Count > 4)
            {
                var item=setupPanel.Children[4];
                setupPanel.Children.RemoveAt(4);
                adminSettingsPanel.Children.Add(item);
            }
        }
        var churchNameButton = new Button { Content = "Change Church Name (Publisher Only)",
            Padding = new Thickness(14,10,14,10), Margin = new Thickness(0,14,0,5),
            HorizontalAlignment = HorizontalAlignment.Left };
        churchNameButton.Click += (_,_) => EditChurchName();
        setupPanel.Children.Add(churchNameButton);
        setupPanel.Children.Add(new TextBlock { Text = "Only signed-in Publishers can save the church name. Paper preset names are separate.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) });
        UpdateAdminControls();
        var bottom=new Border{Background=Brushes.White,Padding=new Thickness(8)};
        DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
        // The editor tab must be the final DockPanel child to fill remaining space.
        root.Children.Remove(tabs);root.Children.Add(tabs);
        status.TextWrapping=TextWrapping.Wrap;
        var statusRow=new DockPanel();
        var zoomPanel=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        zoomPanel.Children.Add(new TextBlock{Text="ZOOM",FontSize=11,Foreground=Brushes.DimGray,VerticalAlignment=VerticalAlignment.Center});
        zoomPanel.Children.Add(zoomSlider);zoomPanel.Children.Add(zoomLabel);
        zoomSlider.ValueChanged+=(_,_)=>{ if (!syncingZoom) SetZoom(zoomSlider.Value); };
        DockPanel.SetDock(zoomPanel,Dock.Right);statusRow.Children.Add(zoomPanel);
        statusRow.Children.Add(status);bottom.Child=statusRow;
        // Left to right: page navigation, Design Inspector, full-height fitted bulletin page.
        var workspace=new Grid();workspace.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(182)});
        workspace.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(265)});
        workspace.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});editorTab.Content=workspace;
        var nav=new StackPanel{Background=new SolidColorBrush(Color.FromRgb(232,238,246)),Margin=new Thickness(5)};
        Grid.SetColumn(nav,0);workspace.Children.Add(nav);
        nav.Children.Add(new TextBlock{Text="PAGES  /  NAVIGATION",FontSize=13,FontWeight=FontWeights.Bold,Foreground=new SolidColorBrush(Color.FromRgb(35,57,84)),Margin=new Thickness(12,16,6,12)});
        var navScroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=nav};
        workspace.Children.Remove(nav);Grid.SetColumn(navScroll,0);workspace.Children.Add(navScroll);
        for(int i=1;i<=Math.Clamp(d.PrintedSides,1,12);i++)
        {
            int n=i;
            var thumbnail=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(3)};
            var miniature=new Border{Width=52,Height=78,Background=Brushes.White,BorderBrush=Brushes.SlateGray,
                BorderThickness=new Thickness(1),Margin=new Thickness(0,0,10,0)};
            var miniContent=new StackPanel{Margin=new Thickness(5)};
            miniContent.Children.Add(new Border{Background=new SolidColorBrush(Color.FromRgb(56,85,117)),Height=5,Margin=new Thickness(0,2,0,6)});
            for(int line=0;line<5;line++)miniContent.Children.Add(new Border{Background=new SolidColorBrush(Color.FromRgb(194,204,217)),Height=2,Margin=new Thickness(0,2,0,2),Width=line%2==0?36:26,HorizontalAlignment=HorizontalAlignment.Left});
            miniature.Child=miniContent;thumbnail.Children.Add(miniature);
            thumbnail.Children.Add(new TextBlock{Text=i switch {1=>"1  Cover (locked)",2=>"2  Inside Left",3=>"3  Inside Right",4=>"4  Back Page",_=>"Page "+i},
                FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap,Width=80});
            var button=new Button{Content=thumbnail,Margin=new Thickness(6,4,6,4),Padding=new Thickness(7),
                Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(204,216,231)),HorizontalContentAlignment=HorizontalAlignment.Left};
            button.Click+=(_,_)=>{page=n;Render();};nav.Children.Add(button);pageButtons.Add(button);
        }
        var scroll=new ScrollViewer{HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,
            Background=new SolidColorBrush(Color.FromRgb(215,223,233))};
        Grid.SetColumn(scroll,2);workspace.Children.Add(scroll);
        pageViewport = scroll;
        scroll.SizeChanged += (_, _) => FitPageToWorkspace();
        scroll.Loaded += (_, _) => FitPageToWorkspace();
        surface.LayoutTransform=pageZoom;
        surface.MouseLeftButtonDown+=SurfaceMouseDown;
        surface.MouseMove+=SurfaceMouseMove;
        surface.MouseLeftButtonUp+=SurfaceMouseUp;
        PreviewKeyDown+=EditorKeyDown;
        var frame=new Border{Child=surface,BorderBrush=Brushes.DarkGray,BorderThickness=new Thickness(1),Margin=new Thickness(8),
            HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};scroll.Content=frame;
        var properties=new StackPanel{Margin=new Thickness(10)};
        var propScroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=Brushes.White,Content=properties};
        Grid.SetColumn(propScroll,1);workspace.Children.Add(propScroll);
        formattingScroll=propScroll;
        formattingPanel = properties;
        properties.Children.Add(new TextBlock{Text="DESIGN INSPECTOR",FontSize=15,FontWeight=FontWeights.Bold,Margin=new Thickness(4,8,4,12)});
        properties.Children.Add(new TextBlock{Text="Select a text box or image. Layout changes require Publisher permission.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,0,4,12)});
        properties.Children.Add(new TextBlock{Text="TEXT STYLE",FontWeight=FontWeights.Bold,Margin=new Thickness(4,8,4,12)});
        void Label(string label,UIElement element){properties.Children.Add(new TextBlock{Text=label,Margin=new Thickness(4,9,4,3)});properties.Children.Add(element);}
        sizeBox.Width=90;sizeBox.HorizontalAlignment=HorizontalAlignment.Left;
        widthBox.Width=90;widthBox.HorizontalAlignment=HorizontalAlignment.Left;
        heightBox.Width=90;heightBox.HorizontalAlignment=HorizontalAlignment.Left;
        fontBox.ItemsSource=new[]{"Arial","Calibri","Cambria","Georgia","Times New Roman","Verdana","Segoe UI","Tahoma"};
        fontBox.SelectedItem="Segoe UI";
        Label("Font family",fontBox);
        Label("Font size",sizeBox);properties.Children.Add(boldBox);
        colorBox.ItemsSource=fontColors.Keys.ToList();
        colorBox.SelectedItem="Charcoal";
        Label("Text color — choose a named color",colorBox);
        var colorRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(3,2,0,4)};
        colorRow.Children.Add(new TextBlock{Text="Selected color",VerticalAlignment=VerticalAlignment.Center});
        colorRow.Children.Add(colorPreview);properties.Children.Add(colorRow);
        colorBox.SelectionChanged+=(_,_)=>
        {
            UpdateColorPreview();
            if(syncingFontColor || syncingInspector || selected==null || selectedView==null)return;
            if(!TryColor(SelectedFontColor(),out var brush))return;
            ApplySelectionProperty(TextElement.ForegroundProperty,brush);
            status.Text="Text color applied to selection or next typed characters.";
        };
        
        UpdateColorPreview();
        alignBox.ItemsSource=new[]{"Left","Center","Right"};alignBox.SelectedIndex=0;
        alignBox.Width=155;alignBox.HorizontalAlignment=HorizontalAlignment.Left;Label("Alignment",alignBox);
        // Staff formatting applies directly to the selected text box.
        // Selection synchronization must not be interpreted as a user edit.
        fontBox.SelectionChanged+=(_,_)=>{if(fontBox.SelectedItem is string f)ApplySelectionProperty(TextElement.FontFamilyProperty,new FontFamily(f));};
        boldBox.Checked+=(_,_)=>ApplySelectionProperty(TextElement.FontWeightProperty,FontWeights.Bold);
        boldBox.Unchecked+=(_,_)=>ApplySelectionProperty(TextElement.FontWeightProperty,FontWeights.Normal);
        alignBox.SelectionChanged+=(_,_)=>{if(alignBox.SelectedItem is string a)AlignSelected(a);};
        sizeBox.LostFocus+=(_,_)=>ApplySelectedSize();
        sizeBox.KeyDown+=(_,e)=>
        {
            if(e.Key==Key.Enter){ApplySelectedSize();e.Handled=true;}
        };

        Label("Box width (Layout Mode)",widthBox);Label("Box height (Layout Mode)",heightBox);
        var apply=new Button{Content="Apply Formatting",Margin=new Thickness(4,14,4,8),Padding=new Thickness(8)};
        apply.Click+=(_,_)=>ApplyFormat();properties.Children.Add(apply);
        formattingButton=apply;
        addTextButton=new Button{Content="+ Add Text Box",Margin=new Thickness(4,10,4,3),Padding=new Thickness(8)};
        addTextButton.Click+=(_,_)=>AddBox();properties.Children.Add(addTextButton);
        deleteTextButton=new Button{Content="Delete Selected Text Box",Margin=new Thickness(4,3,4,10),Padding=new Thickness(8)};
        deleteTextButton.Click+=(_,_)=>DeleteBox();properties.Children.Add(deleteTextButton);
        properties.Children.Add(new Separator{Margin=new Thickness(4,10,4,10)});
        properties.Children.Add(new TextBlock{Text="IMAGES",FontWeight=FontWeights.Bold,Margin=new Thickness(4,4,4,8)});
        addImageButton=new Button{Content="+ Insert Image",Margin=new Thickness(4,3,4,3),Padding=new Thickness(8)};
        addImageButton.Click+=(_,_)=>InsertImage();properties.Children.Add(addImageButton);
        imageSizeButton=new Button{Content="Apply Width / Height to Image",Margin=new Thickness(4,3,4,3),Padding=new Thickness(8)};
        imageSizeButton.Click+=(_,_)=>ResizeImage();properties.Children.Add(imageSizeButton);
        deleteImageButton=new Button{Content="Delete Selected Image",Margin=new Thickness(4,3,4,10),Padding=new Thickness(8)};
        deleteImageButton.Click+=(_,_)=>DeleteImage();properties.Children.Add(deleteImageButton);
        properties.Children.Add(new Separator{Margin=new Thickness(4,12,4,10)});
        properties.Children.Add(new TextBlock{Text="BULLETIN LAYOUT PRESETS",FontWeight=FontWeights.Bold,Margin=new Thickness(4,4,4,8)});
        templateBox.ItemsSource=new[]{"Traditional Sunday","Modern Sunday","Announcements Focus","Communion Service","Special Service","Memorial / Funeral"};
        templateBox.SelectedIndex=0;
        Label("Apply a design to the current page",templateBox);
        var applyTemplate=new Button{Content="Apply Layout Preset",Margin=new Thickness(4,8,4,8),Padding=new Thickness(8)};
        applyTemplate.Click+=(_,_)=>ApplyLayoutPreset();properties.Children.Add(applyTemplate);
        templateApplyButton=applyTemplate;
        properties.Children.Add(new TextBlock{Text="Presets preserve your current wording. Save your bulletin before changing the layout.",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,Margin=new Thickness(4,0,4,8)});
        properties.Children.Add(new TextBlock{Text="EDIT: Click text and type, then Save.\n\nDESIGN: Staff can change fonts, colors and alignment. Publisher sign-in is required for images, backgrounds and positioning. Ctrl+drag to move.\n\nPAGE 1: Preprinted cover remains blank.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DarkSlateGray,Margin=new Thickness(4,20,4,4)});
        page=Math.Clamp(initialPage,1,Math.Clamp(d.PrintedSides,1,12));UpdateAdminControls();Render();
        status.Text="Pages autosave locally every few seconds. Save Bulletin still saves the full file manually.";
        pageAutosaveTimer.Tick += (_, _) => SavePageDraft();
        pageAutosaveTimer.Start();
        Closing += (_, _) => { pageAutosaveTimer.Stop(); SavePageDraft(true); };
    }
    // Pages unchecked in Page Setup are hidden from navigation and left blank in print.
    System.Collections.Generic.HashSet<int> HiddenPageNumbers()
    {
        return (settings.HiddenPages ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value.Trim(), out int number) ? number : -1)
            .Where(number => number > 0)
            .ToHashSet();
    }

    void RefreshPageNavigation()
    {
        var hidden = HiddenPageNumbers();
        int count = Math.Min(pageButtons.Count, Math.Clamp(settings.PrintedSides, 1, 12));
        for (int i = 0; i < pageButtons.Count; i++)
        {
            int number = i + 1;
            pageButtons[i].Visibility = number <= count && !hidden.Contains(number)
                ? Visibility.Visible : Visibility.Collapsed;
        }
        if (page < 1 || page > count || hidden.Contains(page))
        {
            page = Enumerable.Range(1, count)
                .FirstOrDefault(number => !hidden.Contains(number));
        }
    }

    // Upgrade older bulletins: combine the three separate title/date/verse
    // objects into one editable text box, preserving their displayed values.
    void CombineLegacyHeaderBoxes(BulletinPanel panel)
    {
        var boxes=panel.TextBoxes;
        if(boxes==null)return;
        var title=boxes.FirstOrDefault(b=>b.Field=="Title");
        var date=boxes.FirstOrDefault(b=>b.Field=="Date");
        var verse=boxes.FirstOrDefault(b=>b.Field=="Verse");
        if(title==null || date==null || verse==null)return;
        string header=string.Join("\n",new[]{Value(title),Value(date),Value(verse)});
        // Retain the original title object's identity, location and formatting.
        // A blank Field ensures that future edits persist as a single unit.
        title.Text=header;title.RichTextXaml="";
        title.Field="";
        title.X=Math.Min(title.X,date.X);
        title.Y=Math.Min(title.Y,Math.Min(date.Y,verse.Y));
        title.Width=Math.Max(title.Width,Math.Max(date.Width,verse.Width));
        title.Height=Math.Max(88,Math.Max(title.Y+title.Height,Math.Max(date.Y+date.Height,verse.Y+verse.Height))-title.Y);
        title.FontSize=14;
        title.Bold=false;
        title.Alignment="Center";
        boxes.Remove(date);
        boxes.Remove(verse);
    }

    void Render()
    {
        surface.Children.Clear();views.Clear();selected=null;selectedView=null;selectedImage=null;selectedImageView=null;selectionHandles.Clear();surface.Background=Brushes.White;
        RefreshPageNavigation();
        for(int i=0;i<pageButtons.Count;i++)pageButtons[i].BorderBrush=i==page-1?Brushes.DodgerBlue:Brushes.LightSlateGray;
        if (page == 0)
        {
            status.Text = "No pages are visible. Open Page Setup and select at least one page.";
            return;
        }
        if(page==1 && settings.LockPreprintedCover)
        {
            if(settings.CoverPreviewMode == "Show cover picture")
            {
                var cover = DecodeBackground(settings.CoverPreviewImageData);
                if(cover != null)
                    surface.Children.Add(new Image{Source=cover,Width=528,Height=816,
                        Stretch=Stretch.Uniform,IsHitTestVisible=false});
            }
            else if(settings.CoverPreviewMode == "Blank page")
                surface.Children.Add(new TextBlock{Text="PAGE 1 — PREPRINTED COVER\nLOCKED: NO INK PRINTED",
                    TextAlignment=TextAlignment.Center,FontSize=18,Width=528,Margin=new Thickness(0,280,0,0)});
            status.Text="Page 1 is a locked preprinted cover. Nothing prints in this position.";return;
        }
        if(page>bulletin.Panels.Count){status.Text="Page not available.";return;}
        var p=bulletin.Panels[page-1];p.TextBoxes??=new();p.Images??=new();
        if(p.TextBoxes.Count==0 && p.UseChurchTemplate)PublisherLayout.Ensure(bulletin,page);
        if(page==2) CombineLegacyHeaderBoxes(p);
        if(p.TextBoxes.Count==0 && (!string.IsNullOrWhiteSpace(p.Title) || !string.IsNullOrWhiteSpace(p.Body)))
        {
            if(!string.IsNullOrWhiteSpace(p.Title))p.TextBoxes.Add(new BulletinTextBox{Text=p.Title,X=24,Y=20,Width=480,Height=55,FontSize=17,Bold=true,Alignment="Center"});
            if(!string.IsNullOrWhiteSpace(p.Body))p.TextBoxes.Add(new BulletinTextBox{Text=p.Body,X=24,Y=90,Width=480,Height=690,FontSize=11});
        }
        try { surface.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(p.BackgroundColor)); }
        catch { surface.Background=Brushes.White; }
        DrawBackgroundImage(p);
        foreach(var item in p.TextBoxes)Draw(item);
        p.Images??=new();
        foreach(var image in p.Images)DrawImage(image);
        DrawPageBorder(p);
        DrawGuides();
        status.Text=$"PAGE {page}  |  Click any text to edit it. Changes appear in Print Preview. Save Bulletin to keep your work.";
    }
    void Draw(BulletinTextBox item)
    {
        var tb = new RichTextBox {
            Document = RichTextStorage.Create(item, Value(item)),
            Width = item.Width, Height = item.Height,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.Transparent, BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1), Padding = new Thickness(2),
            AcceptsReturn = true, AcceptsTab = false
        };
        tb.Document.PageWidth = Math.Max(10, item.Width - 8);
        Canvas.SetLeft(tb,item.X);Canvas.SetTop(tb,item.Y);
        Panel.SetZIndex(tb,item.ZIndex);surface.Children.Add(tb);views[item.Id]=tb;
        tb.GotFocus+=(_,_)=>Select(item,tb);
        tb.TextChanged+=(_,_)=>
        {
            if (!restoringHistory) Checkpoint();
            item.Text=RichTextStorage.PlainText(tb.Document);
            item.RichTextXaml=RichTextStorage.Save(tb.Document);
            if(!string.IsNullOrEmpty(item.Field))
                typeof(Bulletin).GetProperty(item.Field)?.SetValue(bulletin,item.Text);
        };
        tb.PreviewMouseLeftButtonDown+=(_,e)=>
        {
            if(adminUnlocked && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {Select(item,tb);Checkpoint();dragging=true;dragStart=e.GetPosition(surface);startX=item.X;startY=item.Y;tb.CaptureMouse();e.Handled=true;}
        };
        tb.PreviewMouseMove+=(_,e)=>
        {
            if(!dragging||!tb.IsMouseCaptured)return;
            var pos=e.GetPosition(surface);
            item.X=Math.Clamp(startX+pos.X-dragStart.X,0,Math.Max(0,528-item.Width));
            item.Y=Math.Clamp(startY+pos.Y-dragStart.Y,0,Math.Max(0,816-item.Height));
            Canvas.SetLeft(tb,item.X);Canvas.SetTop(tb,item.Y);ShowSelectionHandles();e.Handled=true;
        };
        tb.PreviewMouseLeftButtonUp+=(_,e)=>{if(dragging){dragging=false;tb.ReleaseMouseCapture();e.Handled=true;}};
    }
    // The page must remain fully visible in the available right-hand workspace.
    // Zoom requests are capped at the largest scale that fits both dimensions.
    ScrollViewer? pageViewport;
    bool syncingZoom;
    double MaximumFittingZoom()
    {
        if (pageViewport == null) return 100;
        double width = pageViewport.ViewportWidth > 0 ? pageViewport.ViewportWidth : pageViewport.ActualWidth;
        double height = pageViewport.ViewportHeight > 0 ? pageViewport.ViewportHeight : pageViewport.ActualHeight;
        if (width <= 0 || height <= 0) return 100;
        // Include frame border and its eight-pixel margin on each side.
        return Math.Max(1, Math.Min((width - 20) / 528.0, (height - 20) / 816.0) * 100.0);
    }
    void FitPageToWorkspace()
    {
        double fit = MaximumFittingZoom();
        SetZoom(fit);
    }
    void SetZoom(double percent)
    {
        double fit = MaximumFittingZoom();
        percent = Math.Max(1, Math.Min(percent, fit));
        pageZoom.ScaleX = percent / 100.0;
        pageZoom.ScaleY = percent / 100.0;
        zoomLabel.Text = $"{percent:0}% (fit {fit:0}%)";
        syncingZoom = true;
        try
        {
            zoomSlider.Minimum = Math.Min(20, fit);
            if (Math.Abs(zoomSlider.Value - percent) > 0.1) zoomSlider.Value = percent;
        }
        finally { syncingZoom = false; }
    }
    void AlignSelected(string alignment)
    {
        if(selectedView==null)return;
        alignBox.SelectedItem=alignment;
        ApplySelectionProperty(Paragraph.TextAlignmentProperty,
            alignment=="Center"?TextAlignment.Center:alignment=="Right"?TextAlignment.Right:TextAlignment.Left);
    }

    static BitmapImage? DecodeBackground(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(data));
            var bmp = new BitmapImage(); bmp.BeginInit();
            bmp.CacheOption=BitmapCacheOption.OnLoad; bmp.StreamSource=stream;
            bmp.EndInit(); bmp.Freeze(); return bmp;
        }
        catch { return null; }
    }
    void DrawBackgroundImage(BulletinPanel panel)
    {
        var bitmap=DecodeBackground(panel.BackgroundImageData);
        if(bitmap==null)return;
        var image=new Image{Source=bitmap,Width=528,Height=816,Stretch=Stretch.UniformToFill,IsHitTestVisible=false};
        Canvas.SetLeft(image,0);Canvas.SetTop(image,0);Panel.SetZIndex(image,-1000000);
        surface.Children.Add(image);
        double brightness=Math.Clamp(panel.BackgroundBrightness,0.2,1.8);
        if(brightness<1)
        {
            var shade=new Rectangle{Width=528,Height=816,Fill=new SolidColorBrush(Color.FromArgb((byte)Math.Round((1-brightness)*255),0,0,0)),IsHitTestVisible=false};
            Panel.SetZIndex(shade,-999999);surface.Children.Add(shade);
        }
        else if(brightness>1)
        {
            var wash=new Rectangle{Width=528,Height=816,Fill=new SolidColorBrush(Color.FromArgb((byte)Math.Round((brightness-1)/0.8*200),255,255,255)),IsHitTestVisible=false};
            Panel.SetZIndex(wash,-999999);surface.Children.Add(wash);
        }
    }
    void OpenBackgroundDesigner()
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout || (page==1 && settings.LockPreprintedCover))return;
        var target=bulletin.Panels[page-1];
        string originalImage=target.BackgroundImageData;
        double originalBrightness=target.BackgroundBrightness;
        string workingImage=originalImage;
        var dialog=new Window{Title="Page Background — Image and Brightness",Owner=this,Width=600,Height=660,
            MinWidth=510,MinHeight=520,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var root=new DockPanel{Margin=new Thickness(16)};dialog.Content=root;
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        var body=new StackPanel();var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=body};root.Children.Add(scroll);
        body.Children.Add(new TextBlock{Text="Background image",FontSize=19,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,8)});
        body.Children.Add(new TextBlock{Text="Drop an image here, paste from the clipboard (Ctrl+V), or browse. Text boxes stay above the image.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)});
        var preview=new Border{Height=340,BorderBrush=Brushes.SlateGray,BorderThickness=new Thickness(1),Background=Brushes.White,AllowDrop=true};
        body.Children.Add(preview);
        var previewImage=new Image{Stretch=Stretch.UniformToFill};
        var previewShade=new Border{Background=Brushes.Transparent,IsHitTestVisible=false};
        var previewGrid=new Grid{ClipToBounds=true};previewGrid.Children.Add(previewImage);previewGrid.Children.Add(previewShade);preview.Child=previewGrid;
        var row=new WrapPanel{Margin=new Thickness(0,10,0,8)};body.Children.Add(row);
        Button Btn(string title,Action action){var b=new Button{Content=title,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,0)};b.Click+=(_,_)=>action();row.Children.Add(b);return b;}
        bool LoadBytes(byte[] bytes)
        {
            if(bytes.Length>12*1024*1024){MessageBox.Show(dialog,"Choose an image under 12 MB.");return false;}
            try
            {
                using var ms=new MemoryStream(bytes);var bmp=new BitmapImage();bmp.BeginInit();bmp.CacheOption=BitmapCacheOption.OnLoad;bmp.StreamSource=ms;bmp.EndInit();bmp.Freeze();
                workingImage=Convert.ToBase64String(bytes);previewImage.Source=bmp;return true;
            }
            catch{MessageBox.Show(dialog,"This image format could not be loaded.");return false;}
        }
        void LoadFile(string file)
        {
            try{LoadBytes(File.ReadAllBytes(file));}catch(Exception ex){MessageBox.Show(dialog,ex.Message,"Image Error");}
        }
        void PasteImage()
        {
            try
            {
                if(Clipboard.ContainsImage())
                {
                    var bitmap=Clipboard.GetImage();if(bitmap==null)return;
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var ms=new MemoryStream();encoder.Save(ms);LoadBytes(ms.ToArray());
                }
                else if(Clipboard.ContainsFileDropList() && Clipboard.GetFileDropList().Count>0)LoadFile(Clipboard.GetFileDropList()[0]);
                else MessageBox.Show(dialog,"Copy an image first, then click Paste Image.");
            }
            catch(Exception ex){MessageBox.Show(dialog,ex.Message,"Paste Error");}
        }
        Btn("Browse Image",()=>{var pick=new Microsoft.Win32.OpenFileDialog{Filter="Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif"};if(pick.ShowDialog(dialog)==true)LoadFile(pick.FileName);});
        Btn("Paste Image",PasteImage);
        Btn("Remove Image",()=>{workingImage="";previewImage.Source=null;});
        preview.Drop+=(_,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length>0)LoadFile(files[0]);};
        preview.DragOver+=(_,e)=>{e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
        dialog.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.V && Keyboard.Modifiers==ModifierKeys.Control){PasteImage();e.Handled=true;}};
        body.Children.Add(new TextBlock{Text="Image brightness",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,10,0,3)});
        var brightness=new Slider{Minimum=20,Maximum=180,Value=Math.Clamp(originalBrightness*100,20,180),TickFrequency=10,IsSnapToTickEnabled=false};body.Children.Add(brightness);
        var level=new TextBlock{Margin=new Thickness(0,0,0,12)};body.Children.Add(level);
        void PreviewBrightness()
        {
            double b=brightness.Value/100.0;level.Text=$"Brightness: {brightness.Value:0}% (100% = original)";
            if(b<1)previewShade.Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round((1-b)*255),0,0,0));
            else if(b>1)previewShade.Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round((b-1)/0.8*200),255,255,255));
            else previewShade.Background=Brushes.Transparent;
        }
        brightness.ValueChanged+=(_,_)=>PreviewBrightness();PreviewBrightness();
        previewImage.Source=DecodeBackground(workingImage);
        var cancel=new Button{Content="Cancel",Padding=new Thickness(15,8,15,8),Margin=new Thickness(5)};
        cancel.Click+=(_,_)=>dialog.DialogResult=false;actions.Children.Add(cancel);
        var apply=new Button{Content="Apply Background",Padding=new Thickness(15,8,15,8),Margin=new Thickness(5)};
        apply.Click+=(_,_)=>
        {
            double chosen=brightness.Value/100.0;
            if(workingImage!=originalImage || Math.Abs(chosen-originalBrightness)>0.001)Checkpoint();
            target.BackgroundImageData=workingImage;target.BackgroundBrightness=chosen;
            dialog.DialogResult=true;
        };actions.Children.Add(apply);
        dialog.ShowDialog();Render();
    }
    void ChoosePageColor()
    {
        if (!adminUnlocked || !AdminSecurity.CanEditLayout || (page == 1 && settings.LockPreprintedCover)) return;
        var palette = new (string Name, string Hex)[]
        {
            ("White", "#FFFFFF"), ("Ivory", "#FFFFF0"), ("Cream", "#FFFDD0"),
            ("Beige", "#F5F5DC"), ("Light Gray", "#F2F2F2"), ("Silver", "#D9D9D9"),
            ("Light Blue", "#E7F1FF"), ("Sky Blue", "#D5EDFF"), ("Navy Blue", "#243956"),
            ("Light Green", "#E9F5E7"), ("Sage Green", "#DDE8D8"), ("Mint Green", "#E3F8EE"),
            ("Light Yellow", "#FFF7D6"), ("Gold", "#F4D35E"), ("Peach", "#FFE5D0"),
            ("Light Pink", "#FCE8EE"), ("Lavender", "#EEE6FA"), ("Burgundy", "#783B4C"),
            ("Charcoal", "#333333"), ("Black", "#000000"),
            ("Powder Blue", "#B0E0E6"), ("Royal Blue", "#4169E1"), ("Teal", "#008080"),
            ("Turquoise", "#40E0D0"), ("Seafoam", "#C7EDE6"), ("Forest Green", "#228B22"),
            ("Olive", "#808000"), ("Lime", "#D9F99D"), ("Sunflower", "#FFDA63"),
            ("Orange", "#FFA500"), ("Coral", "#FF7F50"), ("Salmon", "#FA8072"),
            ("Rose", "#F4C2C2"), ("Hot Pink", "#FF69B4"), ("Purple", "#800080"),
            ("Lilac", "#C8A2C8"), ("Plum", "#DDA0DD"), ("Tan", "#D2B48C"),
            ("Chocolate", "#D2691E"), ("Brown", "#8B4513"), ("Slate", "#708090")
        };
        var dlg = new Window { Title="Page Background Color", Owner=this, Width=400, Height=280,
            WindowStartupLocation=WindowStartupLocation.CenterOwner, ResizeMode=ResizeMode.NoResize };
        var panel = new StackPanel { Margin=new Thickness(20) };
        panel.Children.Add(new TextBlock { Text="Page color", FontWeight=FontWeights.Bold, Margin=new Thickness(0,0,0,8) });
        panel.Children.Add(new TextBlock { Text="Choose a named color. Use the Up and Down arrow keys to preview it immediately.",
            TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,10) });
        var picker = new ComboBox { Height=34 };
        foreach (var color in palette) picker.Items.Add(new ComboBoxItem { Content=color.Name, Tag=color.Hex });
        var original = bulletin.Panels[page-1].BackgroundColor;
        int index = Array.FindIndex(palette, c => string.Equals(c.Hex, original, StringComparison.OrdinalIgnoreCase));
        picker.SelectedIndex = index >= 0 ? index : 0;
        var swatch = new Border { Height=42, Margin=new Thickness(0,10,0,10),
            BorderBrush=Brushes.Gray, BorderThickness=new Thickness(1) };
        panel.Children.Add(picker);panel.Children.Add(swatch);
        void PreviewColor()
        {
            if (picker.SelectedItem is not ComboBoxItem chosen) return;
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString((string)chosen.Tag));
            swatch.Background = brush;
            surface.Background = brush;
        }
        picker.SelectionChanged += (_,_) => PreviewColor();
        PreviewColor();
        var actions = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right };
        var cancel = new Button { Content="Cancel", Padding=new Thickness(14,7,14,7), Margin=new Thickness(5) };
        cancel.Click += (_,_) => dlg.DialogResult=false;
        var apply = new Button { Content="Apply Color", Padding=new Thickness(14,7,14,7), Margin=new Thickness(5) };
        apply.Click += (_,_) =>
        {
            if (picker.SelectedItem is not ComboBoxItem chosen) return;
            var hex = (string)chosen.Tag;
            if (!string.Equals(original,hex,StringComparison.OrdinalIgnoreCase)) Checkpoint();
            bulletin.Panels[page-1].BackgroundColor=hex;
            dlg.DialogResult=true;
        };
        actions.Children.Add(cancel);actions.Children.Add(apply);panel.Children.Add(actions);
        dlg.Content=panel;
        dlg.ShowDialog();
        Render(); // Cancel restores the original color; Apply persists the chosen color.
    }
    void TogglePageBorder()
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout || (page==1 && settings.LockPreprintedCover))return;
        Checkpoint();
        var p=bulletin.Panels[page-1];p.ShowBorder=!p.ShowBorder;Render();
    }
    void DrawPageBorder(BulletinPanel panel)
    {
        if(!panel.ShowBorder)return;
        var border=new Rectangle{Width=480,Height=768,Stroke=new SolidColorBrush(Color.FromRgb(67,85,104)),
            StrokeThickness=1.5,Fill=Brushes.Transparent,IsHitTestVisible=false};
        Canvas.SetLeft(border,24);Canvas.SetTop(border,24);
        Panel.SetZIndex(border,1000000);surface.Children.Add(border);
    }
    void DrawGuides()
    {
        // Nonprinting layout guides: never added to the saved bulletin model.
        // Hit testing is disabled so staff can click text through the guides.
        if(showGrid)
        {
            for(int x=24;x<528;x+=24)
            {
                var line=new Line{X1=x,Y1=0,X2=x,Y2=816,Stroke=new SolidColorBrush(Color.FromArgb(75,132,165,201)),StrokeThickness=.6,IsHitTestVisible=false};
                Panel.SetZIndex(line,1000001);surface.Children.Add(line);guideOverlays.Add(line);
            }
            for(int y=24;y<816;y+=24)
            {
                var line=new Line{X1=0,Y1=y,X2=528,Y2=y,Stroke=new SolidColorBrush(Color.FromArgb(75,132,165,201)),StrokeThickness=.6,IsHitTestVisible=false};
                Panel.SetZIndex(line,1000001);surface.Children.Add(line);guideOverlays.Add(line);
            }
        }
        if(showGuides)
        {
            var margin=new Rectangle{Width=480,Height=768,Stroke=new SolidColorBrush(Color.FromArgb(170,28,123,176)),
                StrokeThickness=1,StrokeDashArray=new DoubleCollection{5,4},Fill=Brushes.Transparent,IsHitTestVisible=false};
            Canvas.SetLeft(margin,24);Canvas.SetTop(margin,24);
            Panel.SetZIndex(margin,1000001);surface.Children.Add(margin);guideOverlays.Add(margin);
            var center=new Line{X1=264,Y1=0,X2=264,Y2=816,Stroke=new SolidColorBrush(Color.FromArgb(100,28,123,176)),
                StrokeThickness=.8,StrokeDashArray=new DoubleCollection{4,5},IsHitTestVisible=false};
            Panel.SetZIndex(center,1000001);surface.Children.Add(center);guideOverlays.Add(center);
        }
    }
    static bool TryColor(string value,out SolidColorBrush brush)
    {
        brush=Brushes.Black;
        if(value.Length!=7 || value[0]!='#' || !System.Text.RegularExpressions.Regex.IsMatch(value,"^#[0-9A-Fa-f]{6}$"))return false;
        try {brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));return true;}
        catch{return false;}
    }
    string SelectedFontColor() => fontColors.TryGetValue(colorBox.SelectedItem?.ToString()??"",out var hex)
        ? hex : "#222222";
    void SelectFontColor(string hex)
    {
        string? name=fontColors.FirstOrDefault(kv=>string.Equals(kv.Value,hex,StringComparison.OrdinalIgnoreCase)).Key;
        if(name==null)
        {
            // Preserve a custom color from an older saved bulletin without showing hex codes in the dropdown.
            const string custom="Custom (saved color)";
            fontColors[custom]=hex;
            syncingFontColor=true;
            try { colorBox.ItemsSource=fontColors.Keys.ToList(); }
            finally { syncingFontColor=false; }
            name=custom;
        }
        syncingFontColor=true;
        try { colorBox.SelectedItem=name; }
        finally { syncingFontColor=false; }
        UpdateColorPreview();
    }
    void UpdateColorPreview()
    {
        colorPreview.Background=TryColor(SelectedFontColor(),out var brush)?brush:Brushes.Transparent;
    }
    void ApplyLayoutPreset()
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout)return;
        if(page==1 && settings.LockPreprintedCover)return;
        if(page>bulletin.Panels.Count)return;
        var preset=templateBox.SelectedItem?.ToString()??"Traditional Sunday";
        if(MessageBox.Show(this,$"Apply {preset} to page {page}? Existing text will be kept, but boxes will be rearranged.",
            "Apply Layout",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        Checkpoint();
        var panel=bulletin.Panels[page-1];panel.TextBoxes??=new();
        if(panel.TextBoxes.Count==0 && panel.UseChurchTemplate)PublisherLayout.Ensure(bulletin,page);
        var boxes=panel.TextBoxes;
        if(boxes.Count==0){MessageBox.Show(this,"Add text boxes first, then apply a layout.");return;}
        string ink=preset switch
        {
            "Modern Sunday"=>"#146C94",
            "Announcements Focus"=>"#526C60",
            "Communion Service"=>"#783B4C",
            "Special Service"=>"#986D22",
            "Memorial / Funeral"=>"#243956",
            _=>"#243956"
        };
        bool twoColumns=preset=="Traditional Sunday" || preset=="Announcements Focus";
        double top=24;
        for(int i=0;i<boxes.Count;i++)
        {
            var item=boxes[i];
            if(i==0)
            {
                item.X=24;item.Y=top;item.Width=480;item.Height=55;
                item.FontSize=preset=="Modern Sunday"?22:18;
                item.Bold=true;item.Alignment="Center";item.Color=ink;
                top=95;continue;
            }
            if(twoColumns)
            {
                int slot=i-1;int column=slot%2;int row=slot/2;
                item.X=column==0?28:272;item.Y=top+row*112;
                item.Width=228;item.Height=104;
                item.FontSize=11;item.Alignment="Left";
            }
            else
            {
                item.X=30;item.Y=top+(i-1)*Math.Min(120,690.0/Math.Max(1,boxes.Count-1));
                item.Width=468;item.Height=Math.Min(110,690.0/Math.Max(1,boxes.Count-1)-6);
                item.FontSize=11;item.Alignment="Left";
            }
            item.Bold=(i==1);item.Color=(i==1?ink:"#222222");
            item.X=Math.Clamp(item.X,0,528-item.Width);
            item.Y=Math.Clamp(item.Y,0,816-item.Height);
        }
        Render();status.Text=$"Applied {preset} to page {page}. Review fit and save your bulletin.";
    }
    // Selection handles are editable canvas objects, not decorative placeholders.
    void ClearSelectionHandles()
    {
        foreach(var element in selectionHandles)surface.Children.Remove(element);
        selectionHandles.Clear();
    }
    void ShowSelectionHandles()
    {
        ClearSelectionHandles();
        if(!adminUnlocked || !AdminSecurity.CanEditLayout)return;
        double x=selected?.X??selectedImage?.X??0,y=selected?.Y??selectedImage?.Y??0;
        double w=selected?.Width??selectedImage?.Width??0,h=selected?.Height??selectedImage?.Height??0;
        if(selected==null && selectedImage==null)return;
        var outline=new Border{Width=w,Height=h,BorderBrush=Brushes.DodgerBlue,BorderThickness=new Thickness(1.5),IsHitTestVisible=false};
        Canvas.SetLeft(outline,x);Canvas.SetTop(outline,y);Panel.SetZIndex(outline,1000002);surface.Children.Add(outline);selectionHandles.Add(outline);
        var move=new Thumb{Width=Math.Max(24,Math.Min(w,100)),Height=15,Background=new SolidColorBrush(Color.FromRgb(31,102,163)),Cursor=Cursors.SizeAll,
            ToolTip="Drag to move object"};
        Canvas.SetLeft(move,x);Canvas.SetTop(move,Math.Max(0,y-16));Panel.SetZIndex(move,1000003);surface.Children.Add(move);selectionHandles.Add(move);
        move.DragDelta+=(_,e)=>MoveSelected(e.HorizontalChange,e.VerticalChange);
        var resize=new Thumb{Width=13,Height=13,Background=Brushes.White,BorderBrush=Brushes.DodgerBlue,
            BorderThickness=new Thickness(2),Cursor=Cursors.SizeNWSE,ToolTip="Drag to resize object"};
        Canvas.SetLeft(resize,x+w-6);Canvas.SetTop(resize,y+h-6);Panel.SetZIndex(resize,1000003);surface.Children.Add(resize);selectionHandles.Add(resize);
        resize.DragDelta+=(_,e)=>ResizeSelected(e.HorizontalChange,e.VerticalChange);
    }
    void MoveSelected(double dx,double dy)
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout)return;
        Checkpoint();
        if(selected!=null && selectedView!=null)
        {
            selected.X=Math.Clamp(selected.X+dx,0,528-selected.Width);
            selected.Y=Math.Clamp(selected.Y+dy,0,816-selected.Height);
            Canvas.SetLeft(selectedView,selected.X);Canvas.SetTop(selectedView,selected.Y);
        }
        else if(selectedImage!=null && selectedImageView!=null)
        {
            selectedImage.X=Math.Clamp(selectedImage.X+dx,0,528-selectedImage.Width);
            selectedImage.Y=Math.Clamp(selectedImage.Y+dy,0,816-selectedImage.Height);
            Canvas.SetLeft(selectedImageView,selectedImage.X);Canvas.SetTop(selectedImageView,selectedImage.Y);
        }
        ShowSelectionHandles();
    }
    void ResizeSelected(double dx,double dy)
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout)return;
        Checkpoint();
        if(selected!=null && selectedView!=null)
        {
            selected.Width=Math.Clamp(selected.Width+dx,35,528-selected.X);
            selected.Height=Math.Clamp(selected.Height+dy,20,816-selected.Y);
            selectedView.Width=selected.Width;selectedView.Height=selected.Height;selectedView.Document.PageWidth=Math.Max(10,selected.Width-8);
            widthBox.Text=selected.Width.ToString("0");heightBox.Text=selected.Height.ToString("0");
        }
        else if(selectedImage!=null && selectedImageView!=null)
        {
            selectedImage.Width=Math.Clamp(selectedImage.Width+dx,20,528-selectedImage.X);
            selectedImage.Height=Math.Clamp(selectedImage.Height+dy,20,816-selectedImage.Y);
            selectedImageView.Width=selectedImage.Width;selectedImageView.Height=selectedImage.Height;
            widthBox.Text=selectedImage.Width.ToString("0");heightBox.Text=selectedImage.Height.ToString("0");
        }
        ShowSelectionHandles();
    }
    void SurfaceMouseDown(object sender,MouseButtonEventArgs e)
    {
        if(!drawingText || !adminUnlocked || !AdminSecurity.CanEditLayout)return;
        if(page==1 && settings.LockPreprintedCover)return;
        drawingStart=e.GetPosition(surface);
        drawingRectangle=new Rectangle{Stroke=Brushes.DodgerBlue,StrokeThickness=1.5,StrokeDashArray=new DoubleCollection{4,3},
            Fill=new SolidColorBrush(Color.FromArgb(24,36,112,200)),IsHitTestVisible=false};
        Canvas.SetLeft(drawingRectangle,drawingStart.X);Canvas.SetTop(drawingRectangle,drawingStart.Y);
        surface.Children.Add(drawingRectangle);surface.CaptureMouse();e.Handled=true;
    }
    void SurfaceMouseMove(object sender,MouseEventArgs e)
    {
        if(drawingRectangle==null)return;
        var pt=e.GetPosition(surface);
        Canvas.SetLeft(drawingRectangle,Math.Clamp(Math.Min(pt.X,drawingStart.X),0,528));
        Canvas.SetTop(drawingRectangle,Math.Clamp(Math.Min(pt.Y,drawingStart.Y),0,816));
        drawingRectangle.Width=Math.Abs(pt.X-drawingStart.X);
        drawingRectangle.Height=Math.Abs(pt.Y-drawingStart.Y);
    }
    void SurfaceMouseUp(object sender,MouseButtonEventArgs e)
    {
        if(drawingRectangle==null)return;
        var pt=e.GetPosition(surface);surface.ReleaseMouseCapture();surface.Children.Remove(drawingRectangle);drawingRectangle=null;
        drawingText=false;
        double x=Math.Clamp(Math.Min(drawingStart.X,pt.X),0,493),y=Math.Clamp(Math.Min(drawingStart.Y,pt.Y),0,796);
        double w=Math.Clamp(Math.Abs(pt.X-drawingStart.X),35,528-x),h=Math.Clamp(Math.Abs(pt.Y-drawingStart.Y),20,816-y);
        Checkpoint();
        var item=new BulletinTextBox{Text="Click to type",X=x,Y=y,Width=w,Height=h,ZIndex=HighestZ()+1};
        bulletin.Panels[page-1].TextBoxes.Add(item);Draw(item);Select(item,views[item.Id]);views[item.Id].Focus();
        status.Text="Text box created. Type directly, drag blue bar to move, drag corner square to resize.";
        e.Handled=true;
    }
    void DeleteSelection(){if(selected!=null)DeleteBox();else if(selectedImage!=null)DeleteImage();}
    void DuplicateSelected()
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout)return;
        if(selected!=null)
        {
            Checkpoint();
            var a=selected;
            var copy=new BulletinTextBox{Text=Value(a),RichTextXaml=a.RichTextXaml,X=Math.Min(a.X+15,528-a.Width),Y=Math.Min(a.Y+15,816-a.Height),
                Width=a.Width,Height=a.Height,FontSize=a.FontSize,FontFamily=a.FontFamily,Bold=a.Bold,Alignment=a.Alignment,Color=a.Color,ZIndex=HighestZ()+1};
            bulletin.Panels[page-1].TextBoxes.Add(copy);Draw(copy);Select(copy,views[copy.Id]);
        }
        else if(selectedImage!=null)
        {
            Checkpoint();
            var a=selectedImage;
            var copy=new BulletinImageBox{ImageData=a.ImageData,X=Math.Min(a.X+15,528-a.Width),Y=Math.Min(a.Y+15,816-a.Height),Width=a.Width,Height=a.Height,ZIndex=HighestZ()+1};
            bulletin.Panels[page-1].Images.Add(copy);Render();
        }
    }
    // Z-order is saved with each object and used by both editor and print preview.
    int HighestZ() => Math.Max(0, bulletin.Panels[page-1].TextBoxes.Select(t=>t.ZIndex)
        .Concat(bulletin.Panels[page-1].Images.Select(i=>i.ZIndex)).DefaultIfEmpty(0).Max());
    int LowestZ() => Math.Min(0, bulletin.Panels[page-1].TextBoxes.Select(t=>t.ZIndex)
        .Concat(bulletin.Panels[page-1].Images.Select(i=>i.ZIndex)).DefaultIfEmpty(0).Min());
    void BringToFront()
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout)return;
        Checkpoint();
        int z=HighestZ()+1;
        if(selected!=null)selected.ZIndex=z;
        else if(selectedImage!=null)selectedImage.ZIndex=z;
        else return;
        if(selectedView!=null)Panel.SetZIndex(selectedView,z);
        if(selectedImageView!=null)Panel.SetZIndex(selectedImageView,z);
        ShowSelectionHandles();status.Text="Selected object moved to front. Save to keep the order.";
    }
    void SendToBack()
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout)return;
        Checkpoint();
        int z=LowestZ()-1;
        if(selected!=null)selected.ZIndex=z;
        else if(selectedImage!=null)selectedImage.ZIndex=z;
        else return;
        if(selectedView!=null)Panel.SetZIndex(selectedView,z);
        if(selectedImageView!=null)Panel.SetZIndex(selectedImageView,z);
        ShowSelectionHandles();status.Text="Selected object moved to back. Save to keep the order.";
    }
    void AlignObject(string placement)
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout)return;
        if(selected==null && selectedImage==null){status.Text="Select a text box or picture first.";return;}
        double w=selected?.Width??selectedImage!.Width;
        double h=selected?.Height??selectedImage!.Height;
        double x=selected?.X??selectedImage!.X;
        double y=selected?.Y??selectedImage!.Y;
        Checkpoint();
        switch(placement)
        {
            case "Left": x=24;break;
            case "Center": x=(528-w)/2;break;
            case "Right": x=504-w;break;
            case "Top": y=24;break;
            case "Middle": y=(816-h)/2;break;
            case "Bottom": y=792-h;break;
        }
        x=Math.Clamp(x,0,Math.Max(0,528-w));
        y=Math.Clamp(y,0,Math.Max(0,816-h));
        if(selected!=null){selected.X=x;selected.Y=y;Canvas.SetLeft(selectedView!,x);Canvas.SetTop(selectedView!,y);}
        else {selectedImage!.X=x;selectedImage.Y=y;Canvas.SetLeft(selectedImageView!,x);Canvas.SetTop(selectedImageView!,y);}
        ShowSelectionHandles();status.Text=$"Aligned selected object: {placement}.";
    }
    void EditorKeyDown(object sender,KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { Undo(); e.Handled=true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) { Redo(); e.Handled=true; return; }
        if(Keyboard.Modifiers==ModifierKeys.Control && e.Key==Key.D){DuplicateSelected();e.Handled=true;return;}
        if(Keyboard.Modifiers==ModifierKeys.Control && e.Key==Key.S){Save();e.Handled=true;return;}
        if(!adminUnlocked || !AdminSecurity.CanEditLayout)return;
        if(e.OriginalSource is TextBox)return; // Keep arrow keys and Delete for text editing.
        double step=Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?10:1;
        switch(e.Key)
        {
            case Key.Left:MoveSelected(-step,0);break;
            case Key.Right:MoveSelected(step,0);break;
            case Key.Up:MoveSelected(0,-step);break;
            case Key.Down:MoveSelected(0,step);break;
            default:return;
        }
        e.Handled=true;
    }
    void InsertImage()
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout)return;
        if(page==1 && settings.LockPreprintedCover)return;
        var dlg=new Microsoft.Win32.OpenFileDialog{Title="Insert image",Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif"};
        if(dlg.ShowDialog()!=true)return;
        try
        {
            byte[] bytes=File.ReadAllBytes(dlg.FileName);
            if(bytes.Length>8*1024*1024){MessageBox.Show(this,"Choose an image smaller than 8 MB.");return;}
            Checkpoint();
            var image=new BulletinImageBox{ImageData=Convert.ToBase64String(bytes),X=40,Y=80,Width=240,Height=180,ZIndex=HighestZ()+1};
            bulletin.Panels[page-1].Images.Add(image);
            Render();
            status.Text="Image embedded in bulletin. Ctrl+drag to position; use Width / Height to resize.";
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message,"Insert Image Failed");}
    }
    void DrawImage(BulletinImageBox item)
    {
        try
        {
            byte[] bytes=Convert.FromBase64String(item.ImageData);
            using var stream=new MemoryStream(bytes);
            var bitmap=new BitmapImage();
            bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;
            bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
            var view=new Image{Source=bitmap,Width=item.Width,Height=item.Height,Stretch=Stretch.Uniform};
            Canvas.SetLeft(view,item.X);Canvas.SetTop(view,item.Y);Panel.SetZIndex(view,item.ZIndex);surface.Children.Add(view);
            view.MouseLeftButtonDown+=(_,e)=>
            {
                if(!adminUnlocked)return;
                selected=null;selectedView=null;selectedImage=item;selectedImageView=view;ShowSelectionHandles();
                widthBox.Text=item.Width.ToString(CultureInfo.InvariantCulture);
                heightBox.Text=item.Height.ToString(CultureInfo.InvariantCulture);
                if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Checkpoint();dragging=true;dragStart=e.GetPosition(surface);startX=item.X;startY=item.Y;
                    view.CaptureMouse();
                }
                e.Handled=true;
            };
            view.MouseMove+=(_,e)=>
            {
                if(!dragging || !view.IsMouseCaptured)return;
                var pt=e.GetPosition(surface);
                item.X=Math.Clamp(startX+pt.X-dragStart.X,0,Math.Max(0,528-item.Width));
                item.Y=Math.Clamp(startY+pt.Y-dragStart.Y,0,Math.Max(0,816-item.Height));
                Canvas.SetLeft(view,item.X);Canvas.SetTop(view,item.Y);ShowSelectionHandles();
            };
            view.MouseLeftButtonUp+=(_,e)=>
            {
                if(view.IsMouseCaptured){dragging=false;view.ReleaseMouseCapture();e.Handled=true;}
            };
        }
        catch{status.Text="An image could not be displayed.";}
    }
    void ResizeImage()
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout || selectedImage==null || selectedImageView==null)return;
        Checkpoint();
        if(double.TryParse(widthBox.Text,out double w))selectedImage.Width=Math.Clamp(w,20,528);
        if(double.TryParse(heightBox.Text,out double h))selectedImage.Height=Math.Clamp(h,20,816);
        selectedImage.X=Math.Clamp(selectedImage.X,0,528-selectedImage.Width);
        selectedImage.Y=Math.Clamp(selectedImage.Y,0,816-selectedImage.Height);
        selectedImageView.Width=selectedImage.Width;selectedImageView.Height=selectedImage.Height;
        Canvas.SetLeft(selectedImageView,selectedImage.X);Canvas.SetTop(selectedImageView,selectedImage.Y);ShowSelectionHandles();
    }
    void DeleteImage()
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout || selectedImage==null)return;
        if(MessageBox.Show(this,"Delete selected image?","Delete Image",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        Checkpoint();
        bulletin.Panels[page-1].Images.Remove(selectedImage);Render();
    }
    string Value(BulletinTextBox item)=>string.IsNullOrEmpty(item.Field)?item.Text:
        typeof(Bulletin).GetProperty(item.Field)?.GetValue(bulletin)?.ToString()??item.Text;
    void Select(BulletinTextBox item,RichTextBox view)
    {
        if(selectedView!=null)selectedView.BorderBrush=Brushes.Transparent;
        selectedImage=null;selectedImageView=null;
        selected=item;selectedView=view;view.BorderBrush=Brushes.DodgerBlue;ShowSelectionHandles();
        syncingInspector=true;
        try
        {
            sizeBox.Text=item.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            widthBox.Text=item.Width.ToString(System.Globalization.CultureInfo.InvariantCulture);
            heightBox.Text=item.Height.ToString(System.Globalization.CultureInfo.InvariantCulture);
            boldBox.IsChecked=item.Bold;alignBox.SelectedItem=item.Alignment;
            fontBox.SelectedItem=item.FontFamily;SelectFontColor(item.Color);
        }
        finally { syncingInspector=false; }
    }
    void AddBox()
    {
        if(page==1 && settings.LockPreprintedCover)return;
        if(!adminUnlocked || !AdminSecurity.CanEditLayout){MessageBox.Show("Publisher layout permission required.");return;}
        Checkpoint();
        var item=new BulletinTextBox{Text="New text — click here to edit",X=30,Y=35,Width=450,Height=75,ZIndex=HighestZ()+1};
        bulletin.Panels[page-1].TextBoxes.Add(item);Draw(item);Select(item,views[item.Id]);views[item.Id].Focus();
    }
    void DeleteBox()
    {
        if(!adminUnlocked||!AdminSecurity.CanEditLayout||selected==null){MessageBox.Show("Select a text box and unlock Publisher layout to delete it.");return;}
        if(MessageBox.Show("Delete the selected text box?","Delete",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        Checkpoint();
        bulletin.Panels[page-1].TextBoxes.Remove(selected);Render();
    }
    // RichTextBox selection can be empty (caret). WPF applies collapsed-selection
    // character properties to future typing without rewriting existing words.
    void ApplySelectionProperty(DependencyProperty property, object value)
    {
        if(syncingInspector || restoringHistory || selected==null || selectedView==null)return;
        Checkpoint();
        selectedView.Selection.ApplyPropertyValue(property,value);
        selected.Text=RichTextStorage.PlainText(selectedView.Document);
        selected.RichTextXaml=RichTextStorage.Save(selectedView.Document);
    }
    void ApplySelectedSize()
    {
        if(double.TryParse(sizeBox.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out double n) ||
           double.TryParse(sizeBox.Text,out n))
            ApplySelectionProperty(TextElement.FontSizeProperty,Math.Clamp(n,6,60));
    }
    void ApplyInspectorTextStyle()
    {
        if(syncingInspector || selectedView==null)return;
        string family=fontBox.SelectedItem?.ToString()??"Segoe UI";
        ApplySelectionProperty(TextElement.FontFamilyProperty,new FontFamily(family));
        ApplySelectionProperty(TextElement.FontWeightProperty,
            boldBox.IsChecked==true?FontWeights.Bold:FontWeights.Normal);
        if(double.TryParse(sizeBox.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out double size) ||
           double.TryParse(sizeBox.Text,out size))
            ApplySelectionProperty(TextElement.FontSizeProperty,Math.Clamp(size,6,60));
        string alignment=alignBox.SelectedItem?.ToString()??"Left";
        ApplySelectionProperty(Paragraph.TextAlignmentProperty,
            alignment=="Center"?TextAlignment.Center:alignment=="Right"?TextAlignment.Right:TextAlignment.Left);
        status.Text="Formatted highlighted text (or typing at the cursor).";
    }
    void ApplyFormat()
    {
        if(selected==null || selectedView==null)return;
        ApplyInspectorTextStyle();
        if(adminUnlocked && AdminSecurity.CanEditLayout)
        {
            Checkpoint();
            if(double.TryParse(widthBox.Text,out var w))selected.Width=Math.Clamp(w,35,528);
            if(double.TryParse(heightBox.Text,out var h))selected.Height=Math.Clamp(h,20,816);
            selected.X=Math.Min(selected.X,528-selected.Width);
            selected.Y=Math.Min(selected.Y,816-selected.Height);
            selectedView.Width=selected.Width;selectedView.Height=selected.Height;selectedView.Document.PageWidth=Math.Max(10,selected.Width-8);
            selectedView.Document.PageWidth=Math.Max(10,selected.Width-8);
            Canvas.SetLeft(selectedView,selected.X);Canvas.SetTop(selectedView,selected.Y);
            ShowSelectionHandles();
        }
    }
    void SwitchAdmin(bool requested)
    {
        if(updatingAdminToggle)return;
        if(requested && !adminUnlocked)
        {
            if(!AdminSecurity.SignIn(this) || !AdminSecurity.CanEditLayout)
            {
                updatingAdminToggle=true;admin.IsChecked=false;updatingAdminToggle=false;
                UpdateAdminControls();
                if(AdminSecurity.Current!=null)
                {
                    MessageBox.Show(this,"Signed in. Layout editing requires Publisher permission. Your own account settings remain available.");
                    AdminSecurity.ShowAccountSettings(this);
                    return;
                }
                if(MessageBox.Show(this,
                    "Unable to unlock. Would you like to reset your password using your verified email?",
                    "Administrator Access",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)
                    AdminSecurity.ResetPassword(this);
                return;
            }
            adminUnlocked=true;
        }
        else if(!requested)adminUnlocked=false;
        UpdateAdminControls();
        status.Text=adminUnlocked
            ? "Publisher design unlocked: ribbon, page setup, positioning and formatting available."
            : "Staff mode: text and font formatting available. Page layout and positioning are locked.";
    }
    void UpdateAdminControls()
    {
        adminUnlocked = adminUnlocked && AdminSecurity.CanEditLayout;
        if(adminSettingsPanel!=null)adminSettingsPanel.Visibility=adminUnlocked?Visibility.Visible:Visibility.Collapsed;
        if(formattingScroll!=null)formattingScroll.Visibility=Visibility.Visible;
        if(pageSetupButton!=null)pageSetupButton.IsEnabled=adminUnlocked;
        if(accountsButton!=null)accountsButton.IsEnabled=adminUnlocked;
        sizeBox.IsEnabled=true;
        widthBox.IsEnabled=adminUnlocked;
        heightBox.IsEnabled=adminUnlocked;
        boldBox.IsEnabled=true;
        alignBox.IsEnabled=true;
        fontBox.IsEnabled=true;colorBox.IsEnabled=true;
        if(addTextButton!=null)addTextButton.IsEnabled=adminUnlocked;
        if(deleteTextButton!=null)deleteTextButton.IsEnabled=adminUnlocked;
        if(addImageButton!=null)addImageButton.IsEnabled=adminUnlocked;
        if(deleteImageButton!=null)deleteImageButton.IsEnabled=adminUnlocked;
        if(imageSizeButton!=null)imageSizeButton.IsEnabled=adminUnlocked;
        if(formattingButton!=null)formattingButton.IsEnabled=true;
        if(templateApplyButton!=null)templateApplyButton.IsEnabled=adminUnlocked;
        foreach(var button in layoutActions)button.IsEnabled=adminUnlocked;
    }
    void OpenDesignSettings()
    {
        if(!adminUnlocked || !AdminSecurity.CanEditLayout)
        {
            MessageBox.Show(this,"Publisher permission or an explicit layout grant is required.",
                "Page Setup Locked",MessageBoxButton.OK,MessageBoxImage.Information);
            return;
        }
        var dialog = new DesignStudioWindow { Owner = this };
        // Paper preset names do not control the application church name.
        dialog.ShowDialog();
        if (dialog.SettingsSaved)
        {
            settings = DesignSettings.Load();
            RefreshAppHeader();
            while (bulletin.Panels.Count < Math.Max(4, settings.PrintedSides))
                bulletin.Panels.Add(new BulletinPanel());
            Render();
            status.Text = page == 0
                ? "No pages selected. Open Page Setup and check at least one page."
                : "Page setup saved. Page navigation now reflects the selected visible pages.";
        }
        else RefreshAppHeader();
    }
    void NewBulletin()
    {
        if(MessageBox.Show("Start a new bulletin? Save your current work first.",
            "New Bulletin",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        SavePageDraft(true);
        draftKey = "new-" + Guid.NewGuid().ToString("N");
        bulletin=new Bulletin();ResetHistory();
        for(int i=0;i<Math.Max(4,settings.PrintedSides);i++)bulletin.Panels.Add(new BulletinPanel());
        for(int n=2;n<=4;n++){bulletin.Panels[n-1].UseChurchTemplate=true;PublisherLayout.Ensure(bulletin,n);}
        page=2;admin.IsChecked=false;Render();
        SavePageDraft(true);
        status.Text="New bulletin created. Its pages will autosave separately; use Save for a full bulletin file.";
    }
    void OpenBulletin()
    {
        var dlg=new Microsoft.Win32.OpenFileDialog{
            Title="Open saved church bulletin",
            Filter="Bulletin files (*.bulletin.json)|*.bulletin.json|JSON files (*.json)|*.json"};
        if(dlg.ShowDialog()!=true)return;
        try
        {
            var loaded=Services.BulletinStorage.Load(dlg.FileName);
            loaded.Panels??=new();
            while(loaded.Panels.Count<Math.Max(4,settings.PrintedSides))
                loaded.Panels.Add(new BulletinPanel());
            SavePageDraft(true);
            bulletin=loaded;ResetHistory();
            draftKey = Services.PageDraftStorage.KeyForFile(dlg.FileName);
            RestorePageDraft();
            page=2;admin.IsChecked=false;Render();
            status.Text="Opened: "+dlg.FileName;
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"Unable to open bulletin",MessageBoxButton.OK,MessageBoxImage.Error);}
    }
    void Save()
    {
        var dlg=new Microsoft.Win32.SaveFileDialog{Filter="Bulletin files|*.bulletin.json",FileName="ValleyBaptist_"+DateTime.Now.ToString("yyyy-MM-dd")+".bulletin.json"};
        if(dlg.ShowDialog()==true)
        {
            Services.BulletinStorage.Save(dlg.FileName,bulletin);
            draftKey = Services.PageDraftStorage.KeyForFile(dlg.FileName);
            SavePageDraft(true);
            status.Text="Full bulletin saved manually: "+dlg.FileName;
        }
    }
    void Preview()
    {
        var doc=BulletinPrintBuilder.Create(bulletin,settings,true);
        var win=new Window{Title="Bulletin Print Preview",Width=1050,Height=800,Owner=this};
        win.Content=new DocumentViewer{Document=doc};win.ShowDialog();
    }
}
