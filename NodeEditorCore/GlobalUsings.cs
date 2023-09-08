using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Utils;

public static class AvaloniaUtils
{
    public static readonly Typeface FontMonospaceNormal = new Typeface(FontFamily.Parse("Consolas,Menlo,Monospace"), FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
    public static readonly Typeface FontMonospaceItalic = new Typeface(FontFamily.Parse("Consolas,Menlo,Monospace"), FontStyle.Italic, FontWeight.Normal, FontStretch.Normal);
    public static readonly Typeface FontMonospaceCondensed = new Typeface(FontFamily.Parse("Consolas,Menlo,Monospace"), FontStyle.Normal, FontWeight.Normal, FontStretch.Condensed);
    public static readonly Pen BlackPen1 = new Pen(Brushes.Black);
    public static readonly Pen BlackPen2 = new Pen(Brushes.Black, 2);

    public static readonly Lazy<Cursor> CursorHand = new Lazy<Cursor>(() => Cursor.Parse("Hand"));
    public static Rect PixelAlign(this Rect self) => PixelSnapHelpers.PixelAlign(self, new Size(1, 1));
    public static Point PixelAlign(this Point self) => new Point(PixelSnapHelpers.PixelAlign(self.X, 1), PixelSnapHelpers.PixelAlign(self.Y, 1));

    public static Point ToPoint(this Microsoft.Msagl.Core.Geometry.Point msaglPoint) => new Point(msaglPoint.X, msaglPoint.Y);
    public static Microsoft.Msagl.Core.Geometry.Point ToMsaglPoint(this Point msaglPoint) => new Microsoft.Msagl.Core.Geometry.Point(msaglPoint.X, msaglPoint.Y);
}