using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using ApexLights.Core;
using Forms = System.Windows.Forms;

namespace ApexLights;

// Rendered by both the preview and the small, passive HUD windows. No settings theme resources.
internal sealed class AwarenessCue(CuePlace place) : FrameworkElement
{
    private int count;
    private RaceFlag flag;
    public bool Active => count > 0 || flag != 0;
    public static Size Design(CuePlace place) => place == CuePlace.Flag ? new(168, 116) : new(150, 156);

    public void Display(IndicatorFrame frame, IndicatorSettings options)
    {
        int next = !options.PeripheralBands ? 0 : place switch { CuePlace.Left => frame.Left, CuePlace.Right => frame.Right, _ => 0 };
        // Routine transitions and the limiter remain in the strip. The forward flag uses the same selected warning.
        RaceFlag nextFlag = place == CuePlace.Flag && options.FloatingFlag &&
            IndicatorPolicy.Prominence(frame.Flag) is FlagProminence.Critical or FlagProminence.Caution ? frame.Flag : 0;
        if (count == next && flag == nextFlag) return;
        count = next; flag = nextFlag; InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!Active) return;
        Brush art = place == CuePlace.Flag ? HudArt.Flag(flag) : HudArt.Wing(place == CuePlace.Left, count);
        dc.DrawRectangle(art, null, new Rect(RenderSize));
    }
}

internal sealed class AwarenessDisplay : IDisposable
{
    private readonly Overlay anchor;
    private readonly Dictionary<CuePlace,CueWindow> windows = new();
    private IndicatorFrame last;
    private IndicatorSettings? options;
    private bool disposed;
    public AwarenessDisplay(Overlay anchor)
    {
        this.anchor = anchor;
        anchor.LocationChanged += Moved;
        anchor.DpiChanged += DpiChanged;
        SystemEvents.DisplaySettingsChanged += DisplaysChanged;
    }
    public void Configure(IndicatorSettings next)
    {
        options = next;
        UpdateWindows(last, true);
    }

    public void Display(IndicatorFrame frame)
    {
        if (last.Left == frame.Left && last.Right == frame.Right && last.Flag == frame.Flag) return;
        last = frame;
        UpdateWindows(frame, false);
    }

    private void UpdateWindows(IndicatorFrame frame, bool reposition)
    {
        if (options == null || disposed) return;
        SetCue(CuePlace.Left, options.PeripheralBands && frame.Left > 0);
        SetCue(CuePlace.Right, options.PeripheralBands && frame.Right > 0);
        SetCue(CuePlace.Flag, options.FloatingFlag && frame.Flag != RaceFlag.None);

        void SetCue(CuePlace place, bool visible)
        {
            windows.TryGetValue(place, out var window);
            if (!visible)
            {
                if (window?.IsVisible == true) window.Hide();
                return;
            }
            if (window == null)
            {
                window = new CueWindow(place);
                windows.Add(place, window);
            }
            window.Cue.Display(frame, options);
            if (reposition || !window.IsVisible) Position(window);
            if (!window.IsVisible) window.Show();
        }
    }
    private void Position(CueWindow w)
    {
        if (options == null) return;
        var hwnd = new WindowInteropHelper(anchor).Handle;
        if (hwnd == IntPtr.Zero) return;
        var bounds = Forms.Screen.FromHandle(hwnd).Bounds;
        var dpi = VisualTreeHelper.GetDpi(anchor);
        var design = AwarenessCue.Design(w.Place);
        double size = w.Place == CuePlace.Flag ? options.FlagScale : options.SideScale;
        w.Width = design.Width*size; w.Height = design.Height*size; w.Opacity = options.CueOpacity;
        int width = Math.Min(bounds.Width,(int)Math.Ceiling(w.Width*dpi.DpiScaleX));
        int height = Math.Min(bounds.Height,(int)Math.Ceiling(w.Height*dpi.DpiScaleY));
        var (x,y)=CuePosition.Within(w.Place,bounds.Width,bounds.Height,width,height,options.SideInset,options.FlagElevation);
        w.Move(bounds.Left+(int)Math.Round(x),bounds.Top+(int)Math.Round(y),width,height);
    }
    private void Moved(object? s,EventArgs e) { foreach(var w in windows.Values) if(w.IsVisible) Position(w); }
    private void DpiChanged(object s,DpiChangedEventArgs e) => QueuePosition();
    private void DisplaysChanged(object? s,EventArgs e)
    {
        if (!disposed && !anchor.Dispatcher.HasShutdownStarted)
            anchor.Dispatcher.BeginInvoke(() => { if (!disposed) { anchor.CheckMonitor(); Moved(null,EventArgs.Empty); } });
    }
    private void QueuePosition()
    {
        if (!disposed && !anchor.Dispatcher.HasShutdownStarted)
            anchor.Dispatcher.BeginInvoke(() => { if (!disposed) Moved(null,EventArgs.Empty); });
    }
    public void Dispose()
    {
        disposed=true;
        anchor.LocationChanged-=Moved; anchor.DpiChanged-=DpiChanged;
        SystemEvents.DisplaySettingsChanged-=DisplaysChanged;
        foreach(var w in windows.Values) if (w.IsLoaded) w.Close();
    }
}

internal sealed class CueWindow : Window
{
    public CuePlace Place { get; }
    public AwarenessCue Cue { get; }
    public CueWindow(CuePlace place)
    {
        Place=place; Cue=new(place); Title="ApexLights "+place+" cue";
        WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; AllowsTransparency=true;
        Background=Brushes.Transparent; ShowInTaskbar=false; ShowActivated=false; Topmost=true;
        WindowStartupLocation=WindowStartupLocation.Manual; Focusable=false; IsHitTestVisible=false; Content=Cue;
        SourceInitialized+=(_,_)=>{
            var h=new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(h,-20,new IntPtr(GetWindowLongPtr(h,-20).ToInt64()|0x080000A0));
        };
    }
    public void Move(int x,int y,int width,int height) =>
        SetWindowPos(new WindowInteropHelper(this).EnsureHandle(),IntPtr.Zero,x,y,width,height,0x14);
    [DllImport("user32",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
    [DllImport("user32",EntryPoint="SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr v);
    [DllImport("user32")] private static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int hgt,uint flags);
}
