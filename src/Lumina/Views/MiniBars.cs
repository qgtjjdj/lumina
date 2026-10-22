using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumina.Views;

public class MiniBars : Control
{
    public static readonly StyledProperty<int[]?> ValuesProperty =
        AvaloniaProperty.Register<MiniBars, int[]?>(nameof(Values));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MiniBars, IBrush?>(nameof(Foreground));

    static MiniBars()
    {
        AffectsRender<MiniBars>(ValuesProperty, ForegroundProperty);
    }

    public int[]? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var values = Values;
        if (values is null || values.Length == 0 || Bounds.Width <= 1 || Bounds.Height <= 1) return;
        var brush = Foreground ?? Brushes.Goldenrod;
        var slot = Bounds.Width / values.Length;
        var max = 40.0;
        for (var i = 0; i < values.Length; i++)
        {
            var h = Bounds.Height * (Math.Clamp(values[i], 0, 40) / max);
            if (h < 1) h = 1;
            context.FillRectangle(brush, new Rect(i * slot + 0.35, Bounds.Height - h, Math.Max(0.8, slot - 0.7), h));
        }
    }
}
