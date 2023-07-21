using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Utils;

public static class AvaloniaUtils
{
    public static readonly Typeface FontMonospaceNormal = new Typeface(Avalonia.Media.FontFamily.Parse("Consolas,Menlo,Monospace"), Avalonia.Media.FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
    public static readonly Typeface FontMonospaceItalic = new Typeface(Avalonia.Media.FontFamily.Parse("Consolas,Menlo,Monospace"), Avalonia.Media.FontStyle.Italic, FontWeight.Normal, FontStretch.Normal);
    public static readonly Typeface FontMonospaceCondensed = new Typeface(Avalonia.Media.FontFamily.Parse("Consolas,Menlo,Monospace"), Avalonia.Media.FontStyle.Normal, FontWeight.Normal, FontStretch.Condensed);
    public static readonly Avalonia.Media.Pen BlackPen1 = new Avalonia.Media.Pen(Avalonia.Media.Brushes.Black);
    public static readonly Avalonia.Media.Pen BlackPen2 = new Avalonia.Media.Pen(Avalonia.Media.Brushes.Black, 2);

    public static readonly Lazy<Avalonia.Input.Cursor> CursorHand = new Lazy<Avalonia.Input.Cursor>(() => Avalonia.Input.Cursor.Parse("Hand"));
    public static Rect PixelAlign(this Rect self) => PixelSnapHelpers.PixelAlign(self, new Avalonia.Size(1, 1));
    public static Avalonia.Point PixelAlign(this Avalonia.Point self) => new Avalonia.Point(PixelSnapHelpers.PixelAlign(self.X, 1), PixelSnapHelpers.PixelAlign(self.Y, 1));

}