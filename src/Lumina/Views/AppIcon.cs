using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Lumina.Views;

public static class AppIcon
{
    public static WindowIcon Create()
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(64, 64), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(new SolidColorBrush(Color.Parse("#14120E")), new Rect(0, 0, 64, 64));
            var gold = new Pen(new SolidColorBrush(Color.Parse("#E4B15A")), 3);
            context.DrawEllipse(null, gold, new Rect(10, 10, 44, 44));
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#E4B15A")), null, new Rect(26, 26, 12, 12));
        }

        var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return new WindowIcon(stream);
    }
}
