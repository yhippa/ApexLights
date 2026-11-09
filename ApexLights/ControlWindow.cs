using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;
using ApexLights.Core;

namespace ApexLights;

public sealed partial class ControlWindow : Window
{
    private readonly Runtime app;
    private Settings S => app.Config;
    private readonly StackPanel page = new();
    private StackPanel section = new();
    private readonly TextBlock pageTitle = new();
    private readonly Border stage = new();
    private readonly Canvas cueLayer = new() { IsHitTestVisible=false };
    private readonly AwarenessCue previewLeft = new(CuePlace.Left), previewRight = new(CuePlace.Right), previewFlag = new(CuePlace.Flag);
    private readonly ListBox navigation = new() { BorderThickness = new(0), Background = Brushes.Transparent };
    private ResourceDictionary? palette;
    private readonly Dictionary<string,bool> disclosure = new();
    private static readonly string[] Sections = ["Demo / Testing","Shift Lights","Gear Indicator","Proximity Awareness","Racing Flags","Overlay Appearance"];
    private readonly ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock sourceLabel = Label("", 11), reading = Label("", 13);
    private readonly TextBlock notice = Label("", 12);
    private readonly Button edit = new(), hide = new() { Content = "Hide settings" };
    private readonly LedStrip preview = new();
    private int tab;
    private bool demo;
    private string carKey = "", carName = "";
    private Thresholds sessionPoints = Thresholds.Default;
    private string? editorKey;
    private double? previousScale;
    private string? previousWarning;
    private FontFamily Mono => (FontFamily)FindResource("UiMono");
    private static Brush BrushOf(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;
    private static TextBlock Label(string text, double size = 13) => new() { Text = text, FontSize = size,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private static void Named(FrameworkElement control, string name) => AutomationProperties.SetName(control, name);

    public ControlWindow(Runtime app)
    {
        this.app = app;
        Title = "ApexLights Settings";
        var work = SystemParameters.WorkArea;
        Width = Math.Min(980, work.Width - 16); Height = Math.Min(800, work.Height - 16);
        MinWidth = Math.Min(660, Width); MinHeight = Math.Min(440, Height);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false; UseLayoutRounding = true; FontSize = 14;
        Resources.MergedDictionaries.Add(Dictionary("Theme.xaml"));
        ApplyTheme();
        SourceInitialized += (_, _) => ApplyWindowTheme();
        FontFamily = (FontFamily)FindResource("UiFont");
        Closing += (_, e) => { if (!app.Exiting) { e.Cancel = true; Hide(); } };
        SystemParameters.StaticPropertyChanged += SystemPreferenceChanged;
        Closed += (_, _) => SystemParameters.StaticPropertyChanged -= SystemPreferenceChanged;
        app.Overlay.LocationChanged += MonitorChanged;
        Closed += (_, _) => app.Overlay.LocationChanged -= MonitorChanged;
        BuildShell();
        Configure();
        navigation.SelectedIndex = 0;
    }

    private static ResourceDictionary Dictionary(string file) => new()
    { Source = new Uri("/ApexLights;component/" + file, UriKind.Relative) };

    private void ApplyTheme()
    {
        if (palette != null) Resources.MergedDictionaries.Remove(palette);
        palette = Dictionary(S.Theme == SettingsTheme.Dark ? "ThemeDark.xaml" : "ThemeLight.xaml");
        if (SystemParameters.HighContrast)
        {
            foreach (string name in new[]{"UiCanvas","UiNav","UiCard"}) palette[name]=SystemColors.WindowBrush;
            foreach (string name in new[]{"UiText","UiMuted","UiStroke"}) palette[name]=SystemColors.WindowTextBrush;
        }
        Resources.MergedDictionaries.Add(palette);
        ApplyWindowTheme();
        SetResourceReference(BackgroundProperty,"UiCanvas"); SetResourceReference(ForegroundProperty,"UiText");
    }

    private void ApplyWindowTheme()
    {
        // WPF synchronizes ThemeMode with the window dictionaries. Set it after
        // updating our palette and again when the native window first exists.
#pragma warning disable WPF0001
        ThemeMode = S.Theme == SettingsTheme.Dark ? System.Windows.ThemeMode.Dark : System.Windows.ThemeMode.Light;
#pragma warning restore WPF0001
    }

    private void SystemPreferenceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast)) Dispatcher.BeginInvoke(ApplyTheme);
    }

    private void BuildShell()
    {
        var shell = new Grid(); shell.SetResourceReference(Panel.BackgroundProperty,"UiCanvas"); Content = shell;
        var navColumn = new ColumnDefinition { Width = new GridLength(212) };
        shell.ColumnDefinitions.Add(navColumn); shell.ColumnDefinitions.Add(new ColumnDefinition());
        SizeChanged += (_,_) => navColumn.Width = new GridLength(ActualWidth < 800 ? 174 : 212);
        var sidebar = new DockPanel { LastChildFill=true };
        var side = new Border { Child=sidebar, BorderThickness=new(0,0,1,0) };
        side.SetResourceReference(Border.BackgroundProperty,"UiNav"); side.SetResourceReference(Border.BorderBrushProperty,"UiStroke");
        shell.Children.Add(side);
        var brand = new StackPanel { Orientation=Orientation.Horizontal,Margin=new(16,24,8,24) };
        var mark = new Path { Data = Geometry.Parse("M0,24 L16,0 25,0 11,24Z M19,24 L29,8 37,24Z"),
            Width=29,Height=20,Stretch=Stretch.Uniform,Margin=new(0,0,8,0) };
        mark.SetResourceReference(Shape.FillProperty,"BrandOrange"); brand.Children.Add(mark);
        var wordmark=Label("ApexLights",22); wordmark.FontWeight=FontWeights.SemiBold; brand.Children.Add(wordmark);
        DockPanel.SetDock(brand,Dock.Top); sidebar.Children.Add(brand);
        var navTitle=Label("Settings",12); navTitle.SetResourceReference(TextBlock.ForegroundProperty,"UiMuted");
        navTitle.Margin=new(16,0,8,8); DockPanel.SetDock(navTitle,Dock.Top); sidebar.Children.Add(navTitle);
        navigation.Margin=new(8,0,8,0); navigation.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        navigation.SetResourceReference(ItemsControl.ItemContainerStyleProperty, "NavigationItem");
        Named(navigation,"Settings sections");
        string[] icons=["\uE768","\uE9D9","\uE8EF","\uE7B8","\uE7C1","\uE7F4"];
        for (int i=0;i<Sections.Length;i++)
        {
            var line=new StackPanel{Orientation=Orientation.Horizontal};
            var icon=Label(icons[i],15); icon.FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            icon.Width=24; line.Children.Add(icon); line.Children.Add(Label(Sections[i]));
            var item=new ListBoxItem{Content=line,MinHeight=40,Padding=new(8,4,4,4)};
            Named(item,Sections[i]); navigation.Items.Add(item);
        }
        navigation.SelectionChanged+=(_,_)=>{ tab=navigation.SelectedIndex; BuildPage(); scroll.ScrollToHome(); };
        sidebar.Children.Add(navigation);
        var right=new DockPanel{Margin=(Thickness)FindResource("PageInset")};
        Grid.SetColumn(right,1); shell.Children.Add(right);
        pageTitle.SetResourceReference(StyleProperty,"PageTitle"); DockPanel.SetDock(pageTitle,Dock.Top); right.Children.Add(pageTitle);
        var previewCard=BuildPreview(); DockPanel.SetDock(previewCard,Dock.Top); right.Children.Add(previewCard);
        var footer=new DockPanel{Margin=new(0,8,0,0)};
        edit.Click+=(_,_)=>app.ToggleEdit(); hide.Click+=(_,_)=>Hide();
        edit.MinHeight=hide.MinHeight=32; edit.SetResourceReference(StyleProperty,"AccentButtonStyle");
        DockPanel.SetDock(edit,Dock.Left); footer.Children.Add(edit);
        DockPanel.SetDock(hide,Dock.Right); footer.Children.Add(hide);
        var shortcut=Label(app.Overlay.Shortcut??"",12); shortcut.Margin=new(12,0,8,0);
        shortcut.SetResourceReference(TextBlock.ForegroundProperty,"UiMuted"); footer.Children.Add(shortcut);
        DockPanel.SetDock(footer,Dock.Bottom); right.Children.Add(footer);
        notice.Margin=new(0,4,0,0); notice.SetResourceReference(TextBlock.ForegroundProperty,"UiText");
        DockPanel.SetDock(notice,Dock.Bottom);right.Children.Add(notice);
        scroll.Focusable=false; scroll.Content=page; right.Children.Add(scroll);
    }

    private Border BuildPreview()
    {
        var contents=new DockPanel();
        var bar=new DockPanel{Margin=new(0,0,0,8)};
        var contrast=new ComboBox{ItemsSource=new[]{"Dark track","Light track"},SelectedIndex=(int)S.Backdrop,MinWidth=116};
        Named(contrast,"Preview background");
        contrast.SelectionChanged+=(_,_)=>{
            if (contrast.SelectedIndex<0)return;
            S.Backdrop=(PreviewBackdrop)contrast.SelectedIndex; SetBackdrop(); app.InterfaceChanged();
        };
        DockPanel.SetDock(contrast,Dock.Right); bar.Children.Add(contrast);
        var title=Label("Live preview",15); title.FontWeight=FontWeights.SemiBold; bar.Children.Add(title);
        DockPanel.SetDock(bar,Dock.Top); contents.Children.Add(bar);
        var screen = new Grid { ClipToBounds = true };
        screen.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        screen.RowDefinitions.Add(new RowDefinition());
        stage.Child = screen; stage.CornerRadius = new(4); stage.Height = 138;
        SetBackdrop(); contents.Children.Add(stage);
        screen.Children.Add(preview);
        Grid.SetRow(cueLayer, 1);
        screen.Children.Add(cueLayer);
        cueLayer.Children.Add(previewLeft);cueLayer.Children.Add(previewRight);cueLayer.Children.Add(previewFlag);
        cueLayer.SizeChanged+=(_,_)=>PositionPreview();
        var meta=new DockPanel{Margin=new(0,8,0,0)}; reading.HorizontalAlignment=HorizontalAlignment.Right; reading.FontFamily=Mono;
        DockPanel.SetDock(reading,Dock.Right);meta.Children.Add(reading); sourceLabel.FontFamily=Mono;
        sourceLabel.SetResourceReference(TextBlock.ForegroundProperty,"UiMuted");meta.Children.Add(sourceLabel);
        DockPanel.SetDock(meta,Dock.Bottom);contents.Children.Insert(1,meta);
        var card=new Border{Child=contents};card.SetResourceReference(StyleProperty,"SectionCard");return card;
    }

    private void SetBackdrop() => stage.SetResourceReference(Border.BackgroundProperty,
        S.Backdrop == PreviewBackdrop.Dark ? "TrackDark" : "TrackLight");

    public void Configure()
    {
        preview.Configure(app.Appearance);
        preview.Opacity = S.Opacity;
        previewLeft.Opacity = previewRight.Opacity = previewFlag.Opacity = app.Appearance.Indicators.CueOpacity;
        PositionPreview();
    }
    private void MonitorChanged(object? sender, EventArgs e) => PositionPreview();

    private void PositionPreview()
    {
        var options=app.Appearance.Indicators;
        var bounds = Forms.Screen.FromHandle(new WindowInteropHelper(app.Overlay).Handle).Bounds;
        double monitorAspect = (double)bounds.Width / bounds.Height;
        double margin = Math.Min(44, cueLayer.ActualWidth * .08);
        preview.Margin = new Thickness(margin, 8, margin, 4);
        Place(previewLeft,CuePlace.Left,options.SideScale*.36);
        Place(previewRight,CuePlace.Right,options.SideScale*.36);
        Place(previewFlag,CuePlace.Flag,options.FlagScale*.45);
        void Place(AwarenessCue visual,CuePlace place,double scale)
        {
            var design=AwarenessCue.Design(place);
            visual.Width=design.Width*scale;visual.Height=design.Height*scale;
            var (x,y)=CuePosition.Within(place,cueLayer.ActualWidth,cueLayer.ActualHeight,visual.Width,visual.Height,options.SideInset,options.FlagElevation, monitorAspect);
            Canvas.SetLeft(visual,x);Canvas.SetTop(visual,y);
        }
    }
    public void RefreshSourceSelection() => BuildPage();
    public void DisplayFrame(LightFrame frame, IndicatorFrame indicators, IndicatorFrame cues)
    {
        preview.Display(frame);
        preview.DisplayIndicators(indicators);
        var opts = app.Appearance.Indicators;
        previewLeft.Display(cues,opts); previewRight.Display(cues,opts); previewFlag.Display(cues,opts);
    }
    public void ModeChanged() { edit.Content = app.Overlay.Editing ? "Lock overlay" : "Edit overlay"; }

    public void Display(LightFrame frame, double rpm, int? gear, bool isDemo, Sample? live, string? warning)
    {
        preview.Display(frame);
        string identity = live?.Car.Key ?? "";
        if (isDemo != demo || identity != carKey || (live is not null && live.Car.Points != sessionPoints))
        {
            demo = isDemo; carKey = identity;
            carName = live?.Car.Name ?? "";
            sessionPoints = live?.Car.Points ?? Thresholds.Default;
            BuildPage();
        }
        string mode = live != null ? "LIVE  /  " + carName : demo ? "DEMO  /  " +
            (!app.DemoSignals.Connected ? "DISCONNECTED" : app.Sweep ? "RPM SWEEP" : "MANUAL RPM") : "WAITING FOR IRACING";
        if (sourceLabel.Text != mode) sourceLabel.Text = mode;
        string output = live == null && (!demo || !app.DemoSignals.Connected) ? "— RPM" : $"{rpm:N0} RPM" + (gear != null ? $"   G {GearDisplay.Format(gear)}" : "");
        if (reading.Text != output) reading.Text = output;
        if (previousWarning != warning) { previousWarning = warning; SetNotice(warning ?? ""); }
        if (tab == 5 && previousScale != S.Scale) BuildPage();
    }

    private void SetNotice(string text) { notice.Text = text; notice.Visibility = text == "" ? Visibility.Collapsed : Visibility.Visible; }

    private void BuildPage()
    {
        page.Children.Clear();
        SetNotice(previousWarning ?? "");
        ModeChanged();
        if (tab < 0 || tab >= Sections.Length) return;
        pageTitle.Text = Sections[tab];
        switch (tab)
        {
            case 0: TelemetryPage(); break;
            case 1: ShiftLightsPage(); break;
            case 2: GearPage(); break;
            case 3: ProximityPage(); break;
            case 4: FlagsPage(); break;
            case 5: AppearancePage(); break;
        }
    }

    private void Heading(string title, bool collapsible = false)
    {
        section = new StackPanel();
        if (collapsible)
        {
            var expander = new Expander { Header=title, Content=section, IsExpanded=disclosure.GetValueOrDefault(title),
                HorizontalContentAlignment=HorizontalAlignment.Stretch, Margin=new(0,0,0,8),Padding=new(12) };
            expander.Expanded += (_,_) => disclosure[title]=true;
            expander.Collapsed += (_,_) => disclosure[title]=false;
            page.Children.Add(expander); return;
        }
        var caption=Label(title,15);caption.SetResourceReference(StyleProperty,"SectionTitle");section.Children.Add(caption);
        var card=new Border{Child=section};card.SetResourceReference(StyleProperty,"SectionCard");page.Children.Add(card);
    }

    private void Hint(string text)
    {
        var label=Label(text,12);label.SetResourceReference(StyleProperty,"SupportingText");section.Children.Add(label);
    }
    private ComboBox Choices(string label,string[] options,int selected,Action<int> change)
    {
        var row=new Grid{Margin=new(0,4,0,4)};
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(166)});
        row.Children.Add(Label(label,14));
        var combo=new ComboBox {ItemsSource=options,SelectedIndex=selected,MinHeight=32,HorizontalContentAlignment=HorizontalAlignment.Stretch};
        Named(combo,label);combo.SelectionChanged+=(_,_)=>{if(combo.SelectedIndex>=0)change(combo.SelectedIndex);};
        Grid.SetColumn(combo,1);row.Children.Add(combo);section.Children.Add(row);return combo;
    }

    private Slider Range(string label, double min, double max, double value, double step, Func<double, string> format, Action<double> change)
    {
        var row = new DockPanel { Margin = new(0, 4, 0, 4) };
        var title = Label(label); title.Width = 110; row.Children.Add(title);
        var number = Label(format(value), 12); number.Width = 76; number.TextAlignment = TextAlignment.Right; number.FontFamily = Mono;
        DockPanel.SetDock(number, Dock.Right); row.Children.Add(number);
        var slider = new Slider { Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true, Value = value,
            SmallChange = step, LargeChange = Math.Max(step, (max - min) / 10) };
        Named(slider, label);
        slider.ValueChanged += (_, e) => { number.Text = format(e.NewValue); change(e.NewValue); };
        slider.IsMoveToPointEnabled=true;slider.MinHeight=32;
        row.Children.Add(slider); section.Children.Add(row);
        return slider;
    }

    private ToggleButton Check(string label, bool value, Action<bool> change)
    {
        var row = new DockPanel { Margin=new(0,4,0,4) };
        var box = new ToggleButton { Content=value?"On":"Off",IsChecked=value,MinWidth=64,MinHeight=32 };
        box.SetResourceReference(StyleProperty,"SettingSwitch");
        Named(box,label);
        box.Checked += (_, _) => box.Content = "On";
        box.Unchecked += (_, _) => box.Content = "Off";
        box.Click += (_, _) => change(box.IsChecked == true);
        DockPanel.SetDock(box,Dock.Right);row.Children.Add(box);row.Children.Add(Label(label,14));section.Children.Add(row);
        return box;
    }

    private void Changed()
    {
        if (tab != 0 && demo && app.DemoUsesCockpit && lookSelector?.IsLoaded == true)
            lookSelector.SelectedIndex = 1;
        app.SettingsChanged();
    }

    private void TelemetryPage()
    {
        Heading("Signal source");
        Choices("Input", ["Automatic", "Demo", "iRacing"], (int)S.Source, i =>
        {
            app.SetSource((SourceMode)i); BuildPage();
        });
        if (S.Source == SourceMode.Automatic) Hint("Runs a demo sweep until the first live session connects.");
        if (demo)
        {
            DemoScenarios();
            Heading("Demo controls");
            var sweep = Check("Sweep RPM automatically", app.Sweep, value => app.Sweep = value);
            Range("Manual RPM", 0, app.DemoMaximum, app.ManualRpm, 1, v => $"{v:N0}", value =>
            {
                app.Sweep = false; sweep.IsChecked = false; app.ManualRpm = value;
            });
            Heading("Demo thresholds", true);
            ThresholdEditor(S.Demo, true);
            DemoIndicators();
        }
        else if (carKey != "")
        {
            Heading(carName);
            ThresholdEditor(S.Cars.GetValueOrDefault(carKey, sessionPoints), false);
        }
        else Hint("Waiting for a session with engine RPM and gear telemetry.");
    }

    private void ThresholdEditor(Thresholds points, bool forDemo)
    {
        editorKey = forDemo ? null : carKey;
        var grid = new UniformGrid { Columns = 4, Margin = new(0, 6, 0, 4) };
        string[] names = ["First LED", "Last LED", "Shift", "Blink"];
        double[] values = [points.First, points.Last, points.Shift, points.Blink];
        var boxes = new TextBox[4];
        for (int i = 0; i < boxes.Length; i++)
        {
            var column = new StackPanel { Margin = new(0, 0, i == 3 ? 0 : 7, 0) };
            var label = Label(names[i], 11); label.SetResourceReference(TextBlock.ForegroundProperty,"UiMuted"); label.Margin = new(0, 0, 0, 4);
            column.Children.Add(label);
            boxes[i] = new TextBox { Text = values[i].ToString("0.##", CultureInfo.CurrentCulture), FontFamily = Mono };
            Named(boxes[i], names[i] + " RPM");
            column.Children.Add(boxes[i]); grid.Children.Add(column);
        }
        section.Children.Add(grid);
        var actions = new WrapPanel { Margin = new(0, 4, 0, 4) };
        var apply = new Button { Content = forDemo ? "Apply thresholds" : "Save car override" };
        apply.Click += (_, _) =>
        {
            double[] n = boxes.Select(b => double.TryParse(b.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var v) ? v : 0).ToArray();
            var next = new Thresholds(n[0], n[1], n[2], n[3]);
            if (!next.Valid) { SetNotice("Use 100–50,000 RPM. Last must exceed First. Shift and Blink must be at least First."); return; }
            if (forDemo) S.Demo = next;
            else if (editorKey == carKey && carKey != "") S.Cars[carKey] = next;
            Changed(); BuildPage(); SetNotice(forDemo ? "Demo thresholds applied." : "Override saved for this car.");
        };
        actions.Children.Add(apply);
        if (!forDemo && S.Cars.ContainsKey(carKey))
        {
            var reset = new Button { Content = "Use car defaults", Margin = new(8, 0, 0, 0) };
            reset.Click += (_, _) => { S.Cars.Remove(carKey); Changed(); BuildPage(); };
            actions.Children.Add(reset);
        }
        section.Children.Add(actions);
    }

    private void AppearancePage()
    {
        Heading("Settings appearance");
        Choices("Theme", ["Dark", "Light"], (int)S.Theme, i => { S.Theme=(SettingsTheme)i; ApplyTheme(); app.InterfaceChanged(); });
        Heading("Cockpit appearance");
        if (demo) DemoLookPicker();
        var cockpit = new Button { Content = "Apply cockpit preset", HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new(0, 5, 0, 3), ToolTip = "Use neon LEDs, a large right-hand gear pod, side chevrons and the status window." };
        cockpit.Click += (_, _) => { app.ApplyCockpitPreset(); BuildPage(); SetNotice("Cockpit preset applied."); };
        section.Children.Add(cockpit);
        Range("Opacity", .15, 1, S.Opacity, .01, v => $"{v:P0}", v => { S.Opacity = v; Changed(); });
        Check("Dark strip housing", S.DarkHousing, v => { S.DarkHousing = v; Changed(); });
        PlacementPage();
    }

    private void ShiftLightsPage()
    {
        Heading("LED strip");
        if (demo) DemoLookPicker();
        Choices("Segments", ["10", "11", "12"], S.Count - 10, i => { S.Count = 10 + i; Changed(); });
        Choices("Shape", ["Blade", "Round", "Square"], (int)S.Shape, i => { S.Shape = (LensShape)i; Changed(); });
        Choices("Progression", ["Left → right", "Right → left", "Outside in"], (int)S.Direction, i => { S.Direction = (Progression)i; Changed(); });
        var colors = new DockPanel { Margin = new(0, 5, 0, 5) };
        var title = Label("Zone colors"); title.Width = 110; colors.Children.Add(title);
        var swatches = new WrapPanel();
        for (int i = 0; i < 4; i++)
        {
            int zone = i;
            var swatch = new Button { Background = BrushOf(S.Colors[i]), Width = 42, Height = 24, Margin = new(0, 0, 5, 0),
                ToolTip = $"Zone {i+1} · {S.Colors[i]}" };
            Named(swatch, $"Zone {i+1} color");
            swatch.Click += (_, _) =>
            {
                using var dialog = new Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(S.Colors[zone]) };
                if (dialog.ShowDialog(new NativeOwner(new WindowInteropHelper(this).Handle)) != Forms.DialogResult.OK) return;
                S.Colors[zone] = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
                swatch.Background = BrushOf(S.Colors[zone]); swatch.ToolTip = $"Zone {zone+1} · {S.Colors[zone]}";
                Changed();
            };
            swatches.Children.Add(swatch);
        }
        colors.Children.Add(swatches); section.Children.Add(colors);
        Range("Lens size", 12, 44, S.LensSize, 1, v => $"{v:0} DIP", v => { S.LensSize = v; Changed(); });
        Range("Spacing", 1, 14, S.Gap, 1, v => $"{v:0} DIP", v => { S.Gap = v; Changed(); });
        Range("Brightness", .1, 1, S.Brightness, .01, v => $"{v:P0}", v => { S.Brightness = v; Changed(); });
        Range("Inactive LEDs", 0, 1, S.InactiveBrightness, .01, v => $"{v:P0}", v => { S.InactiveBrightness = v; Changed(); });
        Heading("Shift cue");
        Choices("Flash at", ["Shift RPM", "Blink RPM", "Off"], (int)S.Flash, i => { S.Flash = (FlashTrigger)i; Changed(); });
        Range("Flash rate", 1, 10, S.Hz, 1, v => $"{v:0} Hz", v => { S.Hz = (int)v; Changed(); });
        Range("RPM offset", -1500, 1500, S.Offset, 25, v => $"{v:+0;-0;0} RPM", v => { S.Offset = (int)v; Changed(); });
    }

    private void PlacementPage()
    {
        Heading("Overlay placement");
        Hint("Center the RPM pill 30% down this monitor, or use Edit Mode to drag and resize it.");
        previousScale = S.Scale;
        Range("Scale", .5, 2.5, S.Scale, .01, v => $"{v:P0}", v =>
        {
            app.Overlay.PreviewAtScale(null); S.Scale = v; previousScale = v; Changed();
        });
        var center = new Button { Content = "Center on this monitor", HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 6, 0, 10) };
        center.Click += (_, _) => app.Overlay.Place(null, null);
        section.Children.Add(center);
        Heading("Startup");
        Check("Start with settings hidden", S.StartMinimized, v => { S.StartMinimized = v; Changed(); });
        Hint("Open settings or exit from the ApexLights tray icon.");
    }

    private sealed record NativeOwner(IntPtr Handle) : Forms.IWin32Window;

}
