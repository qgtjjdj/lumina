using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumina.Views;

public enum IconKind
{
    Folder,
    Image,
    ChevronLeft,
    ChevronRight,
    Play,
    Pause,
    RotateCw,
    RotateCcw,
    FlipH,
    FlipV,
    Info,
    Star,
    Sun,
    Moon,
    Search,
    Close,
    Min,
    Max,
    Restore,
    Copy,
    External,
    Help,
    Refresh,
    Shuffle,
    Trash,
    Sidebar
}

public class Icon : Control
{
    public static readonly StyledProperty<IconKind> KindProperty =
        AvaloniaProperty.Register<Icon, IconKind>(nameof(Kind));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<Icon, IBrush?>(nameof(Foreground));

    private static readonly Dictionary<IconKind, Geometry> Shapes = Build();

    static Icon()
    {
        AffectsRender<Icon>(KindProperty, ForegroundProperty);
    }

    public IconKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (!Shapes.TryGetValue(Kind, out var geometry)) return;
        var brush = Foreground ?? Brushes.White;
        var sx = Bounds.Width / 24.0;
        var sy = Bounds.Height / 24.0;
        if (sx <= 0 || sy <= 0) return;
        using (context.PushTransform(Matrix.CreateScale(sx, sy)))
            context.DrawGeometry(brush, null, geometry);
    }

    private static Dictionary<IconKind, Geometry> Build()
    {
        var map = new Dictionary<IconKind, Geometry>();
        void Add(IconKind kind, string path) => map[kind] = StreamGeometry.Parse(path);

        Add(IconKind.Folder, "M3,7.2 A2.2,2.2 0 0 1 5.2,5 H9.2 L11.2,7.2 H18.8 A2.2,2.2 0 0 1 21,9.4 V17.8 A2.2,2.2 0 0 1 18.8,20 H5.2 A2.2,2.2 0 0 1 3,17.8 Z");
        Add(IconKind.Image, "M5,5 H19 A2,2 0 0 1 21,7 V17 A2,2 0 0 1 19,19 H5 A2,2 0 0 1 3,17 V7 A2,2 0 0 1 5,5 Z M8,10 A1.5,1.5 0 1 0 8,7.5 A1.5,1.5 0 0 0 8,10 Z M6,16 L9.2,12.4 L12,15 L14.4,12.2 L18,16 Z");
        Add(IconKind.ChevronLeft, "M14.8,5.2 L8.2,12 L14.8,18.8 L16.2,17.4 L11,12 L16.2,6.6 Z");
        Add(IconKind.ChevronRight, "M9.2,5.2 L15.8,12 L9.2,18.8 L7.8,17.4 L13,12 L7.8,6.6 Z");
        Add(IconKind.Play, "M8,5.5 L19,12 L8,18.5 Z");
        Add(IconKind.Pause, "M6,5 H10 V19 H6 Z M14,5 H18 V19 H14 Z");
        Add(IconKind.RotateCw, "M16.8,6.2 A8,8 0 1 0 20,13 H17.2 A5.4,5.4 0 1 1 14.6,8.2 L12.2,10.6 H20 V3 Z");
        Add(IconKind.RotateCcw, "M7.2,6.2 A8,8 0 1 1 4,13 H6.8 A5.4,5.4 0 1 0 9.4,8.2 L11.8,10.6 H4 V3 Z");
        Add(IconKind.FlipH, "M11,4 H13 V20 H11 Z M7.5,7.5 L3,12 L7.5,16.5 Z M16.5,7.5 L21,12 L16.5,16.5 Z");
        Add(IconKind.FlipV, "M4,11 H20 V13 H4 Z M7.5,7.5 L12,3 L16.5,7.5 Z M7.5,16.5 L12,21 L16.5,16.5 Z");
        Add(IconKind.Info, "M12,2.5 A9.5,9.5 0 1 0 12,21.5 A9.5,9.5 0 0 0 12,2.5 Z M11,10.5 H13 V17.5 H11 Z M11,6.5 H13 V8.5 H11 Z");
        Add(IconKind.Star, "M12,3.2 L14.7,8.8 L20.8,9.6 L16.3,13.8 L17.5,19.8 L12,16.8 L6.5,19.8 L7.7,13.8 L3.2,9.6 L9.3,8.8 Z");
        Add(IconKind.Sun, "M12,7.2 A4.8,4.8 0 1 0 12,16.8 A4.8,4.8 0 0 0 12,7.2 Z M11,1.8 H13 V4.4 H11 Z M11,19.6 H13 V22.2 H11 Z M1.8,11 H4.4 V13 H1.8 Z M19.6,11 H22.2 V13 H19.6 Z M4.4,4.4 L6.2,6.2 L4.8,7.6 L3,5.8 Z M17.8,17.8 L19.6,19.6 L18.2,21 L16.4,19.2 Z M17.8,6.2 L19.6,4.4 L21,5.8 L19.2,7.6 Z M4.4,19.6 L6.2,17.8 L7.6,19.2 L5.8,21 Z");
        Add(IconKind.Moon, "M14.2,3.2 A8.2,8.2 0 1 0 20.6,14.2 A6.4,6.4 0 0 1 14.2,3.2 Z");
        Add(IconKind.Search, "M10.5,4 A6.5,6.5 0 1 0 10.5,17 A6.5,6.5 0 0 0 10.5,4 Z M15.2,15.2 L20,20 L18.6,21.4 L13.8,16.6 A8,8 0 1 1 15.2,15.2 Z");
        Add(IconKind.Close, "M6.2,5 L12,10.8 L17.8,5 L19,6.2 L13.2,12 L19,17.8 L17.8,19 L12,13.2 L6.2,19 L5,17.8 L10.8,12 L5,6.2 Z");
        Add(IconKind.Min, "M5,11 H19 V13 H5 Z");
        Add(IconKind.Max, "M5,5 H19 V19 H5 Z M7,7 V17 H17 V7 Z");
        Add(IconKind.Restore, "M8,8 H19 V19 H8 Z M5,5 H16 V7 H7 V16 H5 Z");
        Add(IconKind.Copy, "M8,7 H18 A2,2 0 0 1 20,9 V19 A2,2 0 0 1 18,21 H8 A2,2 0 0 1 6,19 V9 A2,2 0 0 1 8,7 Z M4,5 H15 V7 H6 V17 H4 Z");
        Add(IconKind.External, "M14,4 H20 V10 H18 V7.4 L10.7,14.7 L9.3,13.3 L16.6,6 H14 Z M6,6 H12 V8 H8 V18 H18 V14 H20 V20 H6 Z");
        Add(IconKind.Help, "M12,3 A9,9 0 1 0 12,21 A9,9 0 0 0 12,3 Z M12,16.6 A1.15,1.15 0 1 1 12,18.9 A1.15,1.15 0 0 1 12,16.6 Z M9.4,9.2 A2.7,2.7 0 0 1 14.6,10.4 C14.6,12 12,12.2 12,14 H10.6 C10.6,11.4 13.2,11.2 13.2,10.3 A1.3,1.3 0 0 0 10.6,10.1 Z");
        Add(IconKind.Refresh, "M19.5,12 A7.5,7.5 0 1 1 16.4,5.6 L14,8 H21 V1.2 L18.6,3.6 A9.5,9.5 0 1 0 21.4,12 Z");
        Add(IconKind.Shuffle, "M4,7 H7.2 L17,17 H20 V19 H16.2 L6.4,9 H4 Z M14.8,7 H20 V9 H16.4 L14.6,10.8 L13.2,9.4 Z M9.4,14.6 L10.8,16 L7.2,19 H4 V17 H6.4 Z");
        Add(IconKind.Trash, "M9,4 H15 L16,6 H20 V8 H18.2 L17.2,20 H6.8 L5.8,8 H4 V6 H8 Z M8.2,8 L9,18 H15 L15.8,8 Z");
        Add(IconKind.Sidebar, "M4,5 H20 A1,1 0 0 1 21,6 V18 A1,1 0 0 1 20,19 H4 A1,1 0 0 1 3,18 V6 A1,1 0 0 1 4,5 Z M10,7 V17 H19 V7 Z");
        return map;
    }
}
