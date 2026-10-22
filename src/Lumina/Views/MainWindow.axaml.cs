using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lumina.Models;
using Lumina.ViewModels;

namespace Lumina.Views;

public partial class MainWindow : Window
{
    public string[] StartupArgs { get; set; } = [];

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKey, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
        Viewport.PropertyChanged += (_, e) =>
        {
            if (e.Property == ImageViewport.ZoomLabelProperty)
                ZoomReadout.Text = Viewport.ZoomLabel;
        };
        Viewport.DetailRequested += (_, _) =>
        {
            if (DataContext is MainViewModel vm) vm.RequestDetail();
        };
        Viewport.BitmapRetired += (_, bitmap) =>
        {
            if (DataContext is MainViewModel vm) vm.ReleaseBitmap(bitmap);
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not MainViewModel vm) return;
        vm.FullscreenRequested += full =>
        {
            WindowState = full ? WindowState.FullScreen : WindowState.Normal;
        };
        vm.FitRequested += () => Viewport.Fit();
        vm.ScrollRequested += CenterFilm;
    }

    private void CenterFilm(GalleryItem? item)
    {
        if (item is null) return;
        Film.ScrollIntoView(item);
        Dispatcher.UIThread.Post(() =>
        {
            if (Film.ContainerFromItem(item) is not Control container) return;
            var scroller = FindDescendant<ScrollViewer>(Film);
            if (scroller is null) return;
            var origin = container.TranslatePoint(new Point(0, 0), scroller);
            if (origin is null) return;
            var target = scroller.Offset.X + origin.Value.X - (scroller.Viewport.Width - container.Bounds.Width) / 2;
            var max = Math.Max(0, scroller.Extent.Width - scroller.Viewport.Width);
            scroller.Offset = new Vector(Math.Clamp(target, 0, max), scroller.Offset.Y);
        }, DispatcherPriority.Loaded);
    }

    private static T? FindDescendant<T>(Visual visual) where T : Visual
    {
        foreach (var child in visual.GetVisualChildren())
        {
            if (child is T typed) return typed;
            var nested = FindDescendant<T>(child);
            if (nested is not null) return nested;
        }

        return null;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is not MainViewModel vm) return;
        vm.Attach(StorageProvider);
        if (vm.Settings.WindowMaximized)
            WindowState = WindowState.Maximized;
        else
        {
            Width = Math.Clamp(vm.Settings.WindowWidth, MinWidth, 4000);
            Height = Math.Clamp(vm.Settings.WindowHeight, MinHeight, 3000);
        }

        try
        {
            Icon = AppIcon.Create();
        }
        catch
        {
            // The window is fine without a taskbar icon.
        }

        _ = vm.InitializeAsync(StartupArgs);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Settings.WindowMaximized = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Normal)
            {
                vm.Settings.WindowWidth = Width;
                vm.Settings.WindowHeight = Height;
            }

            vm.Shutdown();
        }

        base.OnClosing(e);
    }

    private void OnTitlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button or TextBox or Slider) return;
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2) ToggleMax();
        else BeginMoveDrag(e);
    }

    private void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object? sender, RoutedEventArgs e) => ToggleMax();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMax()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnHelpBackdrop(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button) return;
        if (DataContext is MainViewModel vm && e.Source == sender)
            vm.ShowHelp = false;
    }

    private void OnFit(object? sender, RoutedEventArgs e) => Viewport.Fit();
    private void OnFill(object? sender, RoutedEventArgs e) => Viewport.Fill();
    private void OnActual(object? sender, RoutedEventArgs e) => Viewport.Actual();
    private void OnZoomIn(object? sender, RoutedEventArgs e) => Viewport.ZoomBy(1.2);
    private void OnZoomOut(object? sender, RoutedEventArgs e) => Viewport.ZoomBy(1 / 1.2);

    private void ResizeNorth(object? sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.North, e);
    private void ResizeSouth(object? sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.South, e);
    private void ResizeWest(object? sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.West, e);
    private void ResizeEast(object? sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.East, e);

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel model) return;
        model.IsDragOver = false;
        var items = e.DataTransfer.TryGetFiles();
        if (items is null) return;
        var paths = items
            .Select(item => item.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToList();
        if (paths.Count == 0) return;
        await model.OpenDroppedAsync(paths);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        if (DataContext is MainViewModel vm)
            vm.IsDragOver = true;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsDragOver = false;
    }

    private void OnPreviewKey(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox) return;
        if (DataContext is not MainViewModel vm) return;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (ctrl && shift && e.Key == Key.O)
        {
            vm.OpenFolderCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Key.O)
        {
            vm.OpenFilesCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Key.C)
        {
            vm.CopyPathCommand.Execute(null);
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
                vm.NextCommand.Execute(null);
                break;
            case Key.Left:
            case Key.Up:
                vm.PrevCommand.Execute(null);
                break;
            case Key.Home:
                vm.FirstCommand.Execute(null);
                break;
            case Key.End:
                vm.LastCommand.Execute(null);
                break;
            case Key.Space:
                if (e.Source is Button) return;
                vm.ToggleSlideshowCommand.Execute(null);
                break;
            case Key.F:
            case Key.F11:
                vm.ToggleFullscreenCommand.Execute(null);
                break;
            case Key.Escape:
                if (vm.ShowHelp) vm.ShowHelp = false;
                else if (vm.IsFullscreen || WindowState == WindowState.FullScreen)
                {
                    vm.SetFullscreen(false);
                    WindowState = WindowState.Normal;
                }
                else return;
                break;
            case Key.OemPlus:
            case Key.Add:
                Viewport.ZoomBy(1.2);
                break;
            case Key.OemMinus:
            case Key.Subtract:
                Viewport.ZoomBy(1 / 1.2);
                break;
            case Key.D0:
            case Key.NumPad0:
                Viewport.Fit();
                break;
            case Key.D1:
            case Key.NumPad1:
                Viewport.Actual();
                break;
            case Key.D2:
            case Key.NumPad2:
                Viewport.Fill();
                break;
            case Key.R:
                if (shift) vm.RotateCcwCommand.Execute(null);
                else vm.RotateCwCommand.Execute(null);
                break;
            case Key.H:
                vm.FlipHorizontalCommand.Execute(null);
                break;
            case Key.V:
                vm.FlipVerticalCommand.Execute(null);
                break;
            case Key.I:
                vm.ToggleInfoCommand.Execute(null);
                break;
            case Key.F1:
                vm.ToggleHelpCommand.Execute(null);
                break;
            case Key.F5:
                vm.RefreshCommand.Execute(null);
                break;
            case Key.Delete:
                vm.RemoveFromSessionCommand.Execute(null);
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
