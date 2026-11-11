using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using ApexLights.Core;

namespace ApexLights;

public sealed class Overlay : Window
{
    private readonly Settings settings;
    private readonly Size previousFootprint;
    private readonly Action changed, toggle;
    private readonly Grid root = new();
    private readonly Border outline = new() { CornerRadius = new(7), IsHitTestVisible = false };
    private readonly Thumb grip = new() { Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.SizeNWSE, ToolTip = "Resize strip" };
    private IntPtr hwnd;
    private bool placed;
    private double resizeOrigin, resizeDelta;
    internal DrivingStrip Strip { get; } = new();
    public bool Editing { get; private set; }
    public string? Shortcut { get; private set; }

    public Overlay(Settings settings, Action changed, Action toggle, Size previousFootprint)
    {
        this.settings = settings;
        this.previousFootprint = previousFootprint;
        this.changed = changed;
        this.toggle = toggle;
        Title = "ApexLights Strip";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        root.Children.Add(Strip);
        root.Children.Add(outline);
        root.Children.Add(grip);
        Content = root;
        grip.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
            """<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><Grid Background="Transparent"><Path Stroke="#FF794A" StrokeThickness="1.6" Data="M 3,12 L 12,3 M 8,12 L 12,8"/></Grid></ControlTemplate>""");
        grip.DragStarted += (_, _) => { resizeOrigin = Width; resizeDelta = 0; };
        grip.DragDelta += (_, e) =>
        {
            resizeDelta += e.HorizontalChange;
            settings.Scale = Math.Clamp((resizeOrigin + resizeDelta) / (DrivingStrip.Footprint(settings).Width * Density), 0.5, 2.5);
            ApplySize();
            changed();
        };
        root.MouseLeftButtonDown += (_, e) =>
        {
            if (!Editing || e.Handled || grip.IsMouseOver) return;
            DragMove();
            settings.CenteredPlacement = false;
            CapturePosition();
        };
        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)!.AddHook(Message);
            if (RegisterHotKey(hwnd, 1, 0x4003, 0x4C)) Shortcut = "Ctrl + Alt + L";
            else if (RegisterHotKey(hwnd, 1, 0x4007, 0x4C)) Shortcut = "Ctrl + Alt + Shift + L";
            ApplySize();
            SetEdit(false);
        };
        ContentRendered += (_, _) =>
        {
            if (!placed) { placed = true; RestorePlacement(); }
        };
        SizeChanged += (_, _) => RecenterAfterLayout();
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(ApplySize);
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        Closed += (_, _) => {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
            UnregisterHotKey(hwnd, 1);
        };
        ApplySize();
    }

    private void DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(CheckMonitor);
    }

    private double Density => Math.Min(1, 1.5 / VisualTreeHelper.GetDpi(this).DpiScaleY);

    public void ApplySize()
    {
        var size = DrivingStrip.Footprint(settings);
        Width = size.Width * settings.Scale * Density;
        Height = size.Height * settings.Scale * Density;
        Opacity = settings.Opacity;
        Strip.Configure(settings);
    }

    public void SetEdit(bool edit)
    {
        Editing = edit;
        long style = GetWindowLongPtr(hwnd, -20).ToInt64() | 0x80; // TOOLWINDOW
        const long passive = 0x20 | 0x08000000; // TRANSPARENT + NOACTIVATE on layered WPF hwnd
        SetWindowLongPtr(hwnd, -20, new IntPtr(edit ? style & ~passive : style | passive));
        root.Background = edit ? new SolidColorBrush(Color.FromArgb(35, 30, 34, 36)) : Brushes.Transparent;
        root.Cursor = edit ? Cursors.SizeAll : null;
        outline.BorderBrush = edit ? Brushes.Coral : null;
        outline.BorderThickness = new Thickness(edit ? 1.5 : 0);
        grip.Visibility = edit ? Visibility.Visible : Visibility.Hidden;
    }

    private IntPtr Message(IntPtr h, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        if (message == 0x312 && w.ToInt32() == 1) { toggle(); handled = true; }
        if (message == 0x84 && !Editing) { handled = true; return new IntPtr(-1); }
        return IntPtr.Zero;
    }

    private void RestorePlacement()
    {
        if (settings.X is not int x || settings.Y is not int y)
        {
            Position(Forms.Screen.PrimaryScreen!, null, null);
            return;
        }
        var point = new Drawing.Point(x, y);
        var screen = Forms.Screen.FromPoint(point);
        if (!screen.Bounds.Contains(point)) { Position(Forms.Screen.PrimaryScreen!, null, null); return; }
        bool centered = settings.CenteredPlacement ?? WasLegacyDefault(screen, x, y);
        if (!centered && !settings.CompactPlacement)
        {
            var current = DrivingStrip.Footprint(settings);
            var dpi = VisualTreeHelper.GetDpi(this);
            x += (int)Math.Round((previousFootprint.Width - current.Width * Density) * settings.Scale * dpi.DpiScaleX / 2);
            y += (int)Math.Round((previousFootprint.Height - current.Height * Density) * settings.Scale * dpi.DpiScaleY / 2);
        }
        Position(screen, centered ? null : x, centered ? null : y);
    }

    private bool WasLegacyDefault(Forms.Screen screen, int x, int y)
    {
        var area = screen.WorkingArea;
        if (y != area.Top + Math.Min(100, area.Height / 8)) return false;
        // Early versions saved the auto-position as a top-left corner before any accessories were enabled.
        double bareWidth = 18 + settings.Count * settings.LensSize + (settings.Count - 1) * settings.Gap;
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        bool Matches(double width) => Math.Abs(x - (area.Left + (area.Width - width) / 2)) <= 1;
        return Matches(bareWidth * settings.Scale * dpi) || Matches(373 * dpi);
    }

    private bool recenterPending;
    private void RecenterAfterLayout()
    {
        if (!placed || settings.CenteredPlacement != true || recenterPending) return;
        recenterPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            recenterPending = false;
            if (IsVisible && settings.CenteredPlacement == true) Place(null, null);
        });
    }

    // Win32 screen and window coordinates are physical pixels, including negative monitor origins.
    public void Place(int? x, int? y)
    {
        if (x is int left && y is int top)
        {
            var point = new Drawing.Point(left, top);
            var target = Forms.Screen.FromPoint(point);
            if (target.Bounds.Contains(point)) { Position(target, left, top); return; }
        }
        Position(Forms.Screen.FromHandle(hwnd), null, null);
    }

    private void Position(Forms.Screen screen, int? x, int? y)
    {
        if (!GetWindowRect(hwnd, out var rect)) return;
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        var area = screen.WorkingArea;
        var bounds = screen.Bounds;
        bool centered = !x.HasValue || !y.HasValue;
        int left = x ?? bounds.Left + (bounds.Width - width) / 2;
        int top = y ?? bounds.Top + (int)Math.Round(bounds.Height * .30 - height / 2d);
        left = Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - width));
        top = Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - height));
        settings.CenteredPlacement = centered;
        settings.CompactPlacement = true;
        SetWindowPos(hwnd, IntPtr.Zero, left, top, 0, 0, 0x15);
        CapturePosition();
    }

    public void CapturePosition()
    {
        if (!placed || !GetWindowRect(hwnd, out var rect)) return;
        settings.X = rect.Left;
        settings.Y = rect.Top;
        changed();
    }

    public void CheckMonitor()
    {
        if (!placed || !GetWindowRect(hwnd,out var bounds)) return;
        var rect = Drawing.Rectangle.FromLTRB(bounds.Left,bounds.Top,bounds.Right,bounds.Bottom);
        if (settings.CenteredPlacement == true) Place(null, null);
        else if (!Forms.Screen.FromRectangle(rect).WorkingArea.Contains(rect)) Place(bounds.Left,bounds.Top);
    }

    [StructLayout(LayoutKind.Sequential)] private struct Bounds { public int Left, Top, Right, Bottom; }
    [DllImport("user32")] private static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
    [DllImport("user32")] private static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
    [DllImport("user32", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr h, int index, IntPtr value);
    [DllImport("user32")] private static extern bool GetWindowRect(IntPtr h, out Bounds bounds);
    [DllImport("user32")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
