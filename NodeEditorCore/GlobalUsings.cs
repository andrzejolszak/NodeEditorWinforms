global using Brush = System.Drawing.Brush;
global using Color = System.Drawing.Color;
global using Control = System.Windows.Forms.Control;
global using FontFamily = System.Drawing.FontFamily;
global using LinearGradientBrush = System.Drawing.Drawing2D.LinearGradientBrush;
global using Pen = System.Drawing.Pen;
global using Point = System.Drawing.Point;
global using Rectangle = System.Drawing.Rectangle;
global using ScrollEventArgs = System.Windows.Forms.ScrollEventArgs;
global using Size = System.Drawing.Size;
global using ToolTip = System.Windows.Forms.ToolTip;
global using UserControl = System.Windows.Forms.UserControl;
global using Label = System.Windows.Forms.Label;
global using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
global using ScrollEventType = System.Windows.Forms.ScrollEventType;
global using TextBox = System.Windows.Forms.TextBox;
global using Image = System.Drawing.Image;
global using Brushes = System.Drawing.Brushes;


public static class AvaloniaUtils
{
    public static readonly Typeface FontMonospaceNormal = new Typeface(Avalonia.Media.FontFamily.Parse("Consolas,Menlo,Monospace"), Avalonia.Media.FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
    public static readonly Typeface FontMonospaceItalic = new Typeface(Avalonia.Media.FontFamily.Parse("Consolas,Menlo,Monospace"), Avalonia.Media.FontStyle.Italic, FontWeight.Normal, FontStretch.Normal);
    public static readonly Typeface FontMonospaceCondensed = new Typeface(Avalonia.Media.FontFamily.Parse("Consolas,Menlo,Monospace"), Avalonia.Media.FontStyle.Normal, FontWeight.Normal, FontStretch.Condensed);
    public static readonly Avalonia.Media.Pen BlackPen1 = new Avalonia.Media.Pen(Avalonia.Media.Brushes.Black);
    public static readonly Avalonia.Media.Pen BlackPen2 = new Avalonia.Media.Pen(Avalonia.Media.Brushes.Black, 2);

    public static readonly Lazy<Avalonia.Input.Cursor> CursorHand = new Lazy<Avalonia.Input.Cursor>(() => Avalonia.Input.Cursor.Parse("Hand"));

    public static Rect ToAvRect(this RectangleF self) => new Rect(self.X, self.Y, self.Width, self.Height);
    public static Avalonia.Size ToAvSize(this SizeF self) => new Avalonia.Size(self.Width, self.Height);
    public static Avalonia.Media.Color ToAvColor(this Color self) => new Avalonia.Media.Color(self.A, self.R, self.G, self.B);
}