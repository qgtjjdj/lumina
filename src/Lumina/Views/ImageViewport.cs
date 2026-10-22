using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Lumina.Views;

public enum ZoomMode
{
    Fit,
    Fill,
    Actual,
    Free
}

public enum StageBackdrop
{
    Void,
    Ink,
    Paper,
    Checker
}

public class ImageViewport : Control
{
    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<ImageViewport, Bitmap?>(nameof(Source));

    public static readonly StyledProperty<ZoomMode> ZoomModeProperty =
        AvaloniaProperty.Register<ImageViewport, ZoomMode>(nameof(ZoomMode), ZoomMode.Fit);

    public static readonly StyledProperty<double> UserScaleProperty =
        AvaloniaProperty.Register<ImageViewport, double>(nameof(UserScale), 1);

    public static readonly StyledProperty<double> PanXProperty =
        AvaloniaProperty.Register<ImageViewport, double>(nameof(PanX));

    public static readonly StyledProperty<double> PanYProperty =
        AvaloniaProperty.Register<ImageViewport, double>(nameof(PanY));

    public static readonly StyledProperty<int> RotationProperty =
        AvaloniaProperty.Register<ImageViewport, int>(nameof(Rotation));

    public static readonly StyledProperty<bool> FlipHProperty =
        AvaloniaProperty.Register<ImageViewport, bool>(nameof(FlipH));

    public static readonly StyledProperty<bool> FlipVProperty =
        AvaloniaProperty.Register<ImageViewport, bool>(nameof(FlipV));

    public static readonly StyledProperty<StageBackdrop> StageProperty =
        AvaloniaProperty.Register<ImageViewport, StageBackdrop>(nameof(Stage));

    public static readonly StyledProperty<int> FileWidthProperty =
        AvaloniaProperty.Register<ImageViewport, int>(nameof(FileWidth));

    public static readonly StyledProperty<int> FileHeightProperty =
        AvaloniaProperty.Register<ImageViewport, int>(nameof(FileHeight));

    public static readonly StyledProperty<int> PhotoTokenProperty =
        AvaloniaProperty.Register<ImageViewport, int>(nameof(PhotoToken));

    public static readonly StyledProperty<bool> CrossfadeProperty =
        AvaloniaProperty.Register<ImageViewport, bool>(nameof(Crossfade), true);

    public static readonly DirectProperty<ImageViewport, string> ZoomLabelProperty =
        AvaloniaProperty.RegisterDirect<ImageViewport, string>(nameof(ZoomLabel), o => o.ZoomLabel);

    private static readonly IBrush VoidBase = new SolidColorBrush(Color.Parse("#090A0C"));
    private static readonly IBrush InkBase = new SolidColorBrush(Color.Parse("#050505"));
    private static readonly IBrush PaperBase = new SolidColorBrush(Color.Parse("#D9D3C8"));
    private static readonly IBrush CheckerA = new SolidColorBrush(Color.Parse("#121418"));
    private static readonly IBrush CheckerB = new SolidColorBrush(Color.Parse("#1C2028"));

    private static readonly RadialGradientBrush VoidGlow = CreateGlow("#2A231B", "#00090A0C");
    private static readonly RadialGradientBrush InkGlow = CreateGlow("#161616", "#00050505");
    private static readonly RadialGradientBrush PaperGlow = CreateGlow("#F7F3EC", "#00D9D3C8");

    private string _zoomLabel = "Einpassen";
    private bool _drag;
    private Point _last;
    private int _renderedRotation = int.MinValue;
    private bool _askedDetail;
    private Bitmap? _fadeFrom;
    private double _fade = 1;
    private DispatcherTimer? _fadeTimer;
    private readonly System.Diagnostics.Stopwatch _fadeWatch = new();

    public event EventHandler? DetailRequested;
    public event EventHandler<Bitmap>? BitmapRetired;

    static ImageViewport()
    {
        AffectsRender<ImageViewport>(
            SourceProperty,
            ZoomModeProperty,
            UserScaleProperty,
            PanXProperty,
            PanYProperty,
            RotationProperty,
            FlipHProperty,
            FlipVProperty,
            StageProperty,
            FileWidthProperty,
            FileHeightProperty);
    }

    public ImageViewport()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public ZoomMode ZoomMode
    {
        get => GetValue(ZoomModeProperty);
        set => SetValue(ZoomModeProperty, value);
    }

    public double UserScale
    {
        get => GetValue(UserScaleProperty);
        set => SetValue(UserScaleProperty, value);
    }

    public double PanX
    {
        get => GetValue(PanXProperty);
        set => SetValue(PanXProperty, value);
    }

    public double PanY
    {
        get => GetValue(PanYProperty);
        set => SetValue(PanYProperty, value);
    }

    public int Rotation
    {
        get => GetValue(RotationProperty);
        set => SetValue(RotationProperty, value);
    }

    public bool FlipH
    {
        get => GetValue(FlipHProperty);
        set => SetValue(FlipHProperty, value);
    }

    public bool FlipV
    {
        get => GetValue(FlipVProperty);
        set => SetValue(FlipVProperty, value);
    }

    public StageBackdrop Stage
    {
        get => GetValue(StageProperty);
        set => SetValue(StageProperty, value);
    }

    public int FileWidth
    {
        get => GetValue(FileWidthProperty);
        set => SetValue(FileWidthProperty, value);
    }

    public int FileHeight
    {
        get => GetValue(FileHeightProperty);
        set => SetValue(FileHeightProperty, value);
    }

    public int PhotoToken
    {
        get => GetValue(PhotoTokenProperty);
        set => SetValue(PhotoTokenProperty, value);
    }

    public bool Crossfade
    {
        get => GetValue(CrossfadeProperty);
        set => SetValue(CrossfadeProperty, value);
    }

    public string ZoomLabel
    {
        get => _zoomLabel;
        private set => SetAndRaise(ZoomLabelProperty, ref _zoomLabel, value);
    }

    public void Fit() => SetMode(ZoomMode.Fit);

    public void Fill() => SetMode(ZoomMode.Fill);

    public void Actual() => SetMode(ZoomMode.Actual);

    public void ZoomBy(double factor)
    {
        if (Source is null || Bounds.Width < 2) return;
        ApplyZoom(new Point(Bounds.Width / 2, Bounds.Height / 2), factor);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PhotoTokenProperty)
            _askedDetail = false;

        if (change.Property == SourceProperty)
        {
            var oldBmp = change.OldValue as Bitmap;
            var newBmp = change.NewValue as Bitmap;
            if (oldBmp is not null && newBmp is not null && !ReferenceEquals(oldBmp, newBmp) && ZoomMode == ZoomMode.Free
                && oldBmp.PixelSize.Width > 0 && newBmp.PixelSize.Width > 0
                && oldBmp.PixelSize.Width != newBmp.PixelSize.Width)
            {
                var adjusted = Math.Clamp(UserScale * oldBmp.PixelSize.Width / newBmp.PixelSize.Width, 0.02, 32);
                if (Math.Abs(adjusted - UserScale) > 0.0001)
                    UserScale = adjusted;
            }

            if (oldBmp is not null && !ReferenceEquals(oldBmp, newBmp))
            {
                if (Crossfade && newBmp is not null)
                    BeginFade(oldBmp);
                else
                {
                    CancelFade(retire: true);
                    Retire(oldBmp);
                }
            }
        }

        if (change.Property == FileWidthProperty)
            UpdateLabel();

        if (change.Property == BoundsProperty || change.Property == SourceProperty || change.Property == ZoomModeProperty
            || change.Property == RotationProperty)
        {
            UpdateLabel();
            ConsiderDetail();
        }
    }

    public override void Render(DrawingContext context)
    {
        DrawStage(context);
        var bitmap = Source;
        if (bitmap is null || Bounds.Width < 2 || Bounds.Height < 2) return;

        var scale = EffectiveScale();
        if (_fadeFrom is not null && _fade < 0.999)
        {
            var fromScale = scale;
            if (bitmap.PixelSize.Width > 0 && _fadeFrom.PixelSize.Width > 0)
                fromScale = scale * bitmap.PixelSize.Width / _fadeFrom.PixelSize.Width;
            DrawBitmap(context, _fadeFrom, fromScale, 1 - _fade);
            DrawBitmap(context, bitmap, scale, _fade);
        }
        else
        {
            DrawBitmap(context, bitmap, scale, 1);
        }

        var rot = NormalizedRotation();
        if (_renderedRotation != rot)
        {
            _renderedRotation = rot;
            UpdateLabel();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (Source is null) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        var factor = e.Delta.Y > 0 ? 1.12 : 1 / 1.12;
        ApplyZoom(e.GetPosition(this), factor);
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        Focus();
        if (point.Properties.IsMiddleButtonPressed)
        {
            Fit();
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed) return;
        if (e.ClickCount >= 2)
        {
            if (ZoomMode == ZoomMode.Fit) Actual();
            else Fit();
            e.Handled = true;
            return;
        }

        _drag = true;
        _last = e.GetPosition(this);
        e.Pointer.Capture(this);
        Cursor = new Cursor(StandardCursorType.SizeAll);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_drag) return;
        var position = e.GetPosition(this);
        var delta = position - _last;
        _last = position;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) < 0.4) return;
        if (ZoomMode != ZoomMode.Free)
        {
            UserScale = EffectiveScale();
            ZoomMode = ZoomMode.Free;
        }

        PanX += delta.X;
        PanY += delta.Y;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_drag) return;
        _drag = false;
        e.Pointer.Capture(null);
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _fadeTimer?.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void SetMode(ZoomMode mode)
    {
        ZoomMode = mode;
        PanX = 0;
        PanY = 0;
        UpdateLabel();
        ConsiderDetail();
        InvalidateVisual();
    }

    private void ApplyZoom(Point cursor, double factor)
    {
        var old = EffectiveScale();
        if (old <= 0.0001) return;
        var next = Math.Clamp(old * factor, 0.02, 32);
        var rx = cursor.X - (Bounds.Width / 2 + PanX);
        var ry = cursor.Y - (Bounds.Height / 2 + PanY);
        var k = next / old;
        PanX = cursor.X - rx * k - Bounds.Width / 2;
        PanY = cursor.Y - ry * k - Bounds.Height / 2;
        UserScale = next;
        ZoomMode = ZoomMode.Free;
        UpdateLabel();
        ConsiderDetail();
        InvalidateVisual();
    }

    private double EffectiveScale()
    {
        var bitmap = Source;
        if (bitmap is null) return 1;
        var (w, h) = OrientedSize(bitmap);
        if (w < 1 || h < 1 || Bounds.Width < 2 || Bounds.Height < 2) return 1;

        return ZoomMode switch
        {
            ZoomMode.Fill => Math.Max(Bounds.Width / w, Bounds.Height / h),
            ZoomMode.Actual => (1.0 / RenderScale()) / BitmapPerFile(),
            ZoomMode.Free => UserScale <= 0 ? 1 : UserScale,
            _ => Math.Min(Math.Max(1, Bounds.Width - 72) / w, Math.Max(1, Bounds.Height - 168) / h)
        };
    }

    private (double W, double H) OrientedSize(Bitmap bitmap)
    {
        var w = bitmap.PixelSize.Width;
        var h = bitmap.PixelSize.Height;
        return NormalizedRotation() is 90 or 270 ? (h, w) : (w, h);
    }

    private int NormalizedRotation()
    {
        var rot = Rotation % 360;
        if (rot < 0) rot += 360;
        return rot;
    }

    private double RenderScale()
    {
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        return scale <= 0.01 ? 1 : scale;
    }

    private double BitmapPerFile()
    {
        var bitmap = Source;
        if (bitmap is null || FileWidth <= 0) return 1;
        var width = bitmap.PixelSize.Width;
        if (width <= 0) return 1;
        return Math.Clamp(width / (double)FileWidth, 0.01, 1);
    }

    private void ConsiderDetail()
    {
        if (_askedDetail || Source is null) return;
        var ratio = BitmapPerFile();
        if (ratio >= 0.985) return;
        var magnification = EffectiveScale() * RenderScale() * ratio;
        if (ZoomMode != ZoomMode.Actual && magnification < 0.62) return;
        _askedDetail = true;
        DetailRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateLabel()
    {
        if (Source is null)
        {
            ZoomLabel = "—";
            return;
        }

        var percent = EffectiveScale() * RenderScale() * BitmapPerFile() * 100.0;
        ZoomLabel = ZoomMode switch
        {
            ZoomMode.Fit => $"Einpassen · {percent:0}%",
            ZoomMode.Fill => $"Füllen · {percent:0}%",
            ZoomMode.Actual => "100%",
            _ => $"{percent:0}%"
        };
    }

    private void DrawBitmap(DrawingContext context, Bitmap bitmap, double scale, double opacity)
    {
        if (opacity <= 0.01) return;
        if (opacity < 0.999)
        {
            using (context.PushOpacity(opacity))
                DrawTransformed(context, bitmap, scale);
            return;
        }

        DrawTransformed(context, bitmap, scale);
    }

    private void DrawTransformed(DrawingContext context, Bitmap bitmap, double scale)
    {
        var pw = bitmap.PixelSize.Width;
        var ph = bitmap.PixelSize.Height;
        if (pw <= 0 || ph <= 0) return;

        var rot = NormalizedRotation();
        var cx = Bounds.Width / 2 + PanX;
        var cy = Bounds.Height / 2 + PanY;
        using (context.PushTransform(Matrix.CreateTranslation(cx, cy)))
        using (context.PushTransform(Matrix.CreateRotation(rot * Math.PI / 180.0)))
        using (context.PushTransform(Matrix.CreateScale(FlipH ? -1 : 1, FlipV ? -1 : 1)))
        {
            var rect = new Rect(-pw * scale / 2, -ph * scale / 2, pw * scale, ph * scale);
            context.DrawImage(bitmap, rect);
        }
    }

    private void BeginFade(Bitmap from)
    {
        if (!ReferenceEquals(_fadeFrom, from))
            Retire(_fadeFrom);
        _fadeFrom = from;
        _fade = 0;
        _fadeWatch.Restart();
        _fadeTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _fadeTimer.Tick -= OnFadeTick;
        _fadeTimer.Tick += OnFadeTick;
        _fadeTimer.Start();
        InvalidateVisual();
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        var t = Math.Clamp(_fadeWatch.Elapsed.TotalMilliseconds / 200.0, 0, 1);
        _fade = t * t * (3 - 2 * t);
        InvalidateVisual();
        if (t < 1) return;
        _fadeTimer?.Stop();
        var from = _fadeFrom;
        _fadeFrom = null;
        Retire(from);
    }

    private void CancelFade(bool retire)
    {
        _fadeTimer?.Stop();
        var from = _fadeFrom;
        _fadeFrom = null;
        _fade = 1;
        if (retire) Retire(from);
    }

    private void Retire(Bitmap? bitmap)
    {
        if (bitmap is null) return;
        BitmapRetired?.Invoke(this, bitmap);
    }

    private void DrawStage(DrawingContext context)
    {
        if (Stage == StageBackdrop.Checker)
        {
            const int cell = 18;
            var a = CheckerA;
            var b = CheckerB;
            for (var y = 0; y < Bounds.Height; y += cell)
            {
                for (var x = 0; x < Bounds.Width; x += cell)
                {
                    var alt = ((x / cell) + (y / cell)) % 2 == 0;
                    context.FillRectangle(alt ? a : b, new Rect(x, y, cell, cell));
                }
            }

            return;
        }

        if (Stage == StageBackdrop.Paper)
        {
            context.FillRectangle(PaperBase, Bounds);
            context.FillRectangle(PaperGlow, Bounds);
            return;
        }

        if (Stage == StageBackdrop.Ink)
        {
            context.FillRectangle(InkBase, Bounds);
            context.FillRectangle(InkGlow, Bounds);
            return;
        }

        context.FillRectangle(VoidBase, Bounds);
        context.FillRectangle(VoidGlow, Bounds);
    }

    private static RadialGradientBrush CreateGlow(string inner, string outer)
    {
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.42, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 0.38, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.72, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.85, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(new GradientStop(Color.Parse(inner), 0));
        brush.GradientStops.Add(new GradientStop(Color.Parse(outer), 1));
        return brush;
    }
}
