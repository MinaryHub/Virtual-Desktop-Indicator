using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace VirtualDesktopIndicator.Linux.Services;

/// <summary>Draws the little "VD" tray icon at runtime so we ship no image asset.</summary>
public static class IconFactory
{
    public static WindowIcon BuildTrayIcon()
    {
        var pixel = new PixelSize(32, 32);
        var rtb = new RenderTargetBitmap(pixel, new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            var bg = new SolidColorBrush(Color.FromArgb(230, 30, 30, 30));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(220, 90, 160, 250)), 2);
            ctx.DrawRectangle(bg, pen, new RoundedRect(new Rect(1, 1, 30, 30), 6));

            var text = new FormattedText("VD", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("sans-serif"), 13, Brushes.White);
            ctx.DrawText(text, new Point((32 - text.Width) / 2, (32 - text.Height) / 2));
        }

        using var ms = new MemoryStream();
        rtb.Save(ms);
        ms.Position = 0;
        return new WindowIcon(ms);
    }
}
