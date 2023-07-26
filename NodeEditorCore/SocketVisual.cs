/*
 * The MIT License (MIT)
 *
 * Copyright (c) 2021 Mariusz Komorowski (komorra)
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
 * and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES 
 * OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE 
 * OR OTHER DEALINGS IN THE SOFTWARE.
 */

using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using System.Drawing.Drawing2D;

namespace NodeEditor
{
    [DebuggerDisplay("I={Input}, {Name} = {Value}")]
    public class SocketVisual : RelativeFloatingPort
    {
        public const float SocketHeight = 8;

        public const float SocketWidth = 16;

        public SocketVisual(NodeVisual nodeVisual) : base(() => null, () => nodeVisual.Center)
        {
        }

        public float Width { get; set; } = SocketWidth;
        public float Height { get; set; } = SocketHeight;
        public string Name { get; set; }
        public Type Type { get; set; }
        public bool Input { get; set; }
        public bool HotInput { get; set; }

        public object Value { get; set; }

        public object CurryDefault { get; set; }

        public object CurriedValue => Value ?? CurryDefault;

        public override Microsoft.Msagl.Core.Geometry.Point LocationOffset => this._locationOffset;

        private Microsoft.Msagl.Core.Geometry.Point _locationOffset;

        public override ICurve Curve => CurveFactory.CreateRectangle(this.Width, this.Height, new Microsoft.Msagl.Core.Geometry.Point(this.Location.X + this.Width / 2, this.Location.Y + this.Height / 2));

        public void SetLocationOffset(double x, double y)
        {
            this._locationOffset = new Microsoft.Msagl.Core.Geometry.Point(x, y);
        }

        public Rect GetBounds()
        {
            return new Rect(this.Location.X, this.Location.Y, Width, Height);
        }

        public void DrawAv(DrawingContext g, PointerPoint mouse)
        {
            float x = (float)this.Location.X;
            float y = (float)this.Location.Y;

            var socketRect = new Rect(x, y, Width, Height).PixelAlign();
            var hover = socketRect.Contains(mouse.Position);

            if (hover)
            {
                socketRect.Inflate(new Thickness(2, 0)).PixelAlign();
                if (Input)
                {
                    FormattedText formattedText = new FormattedText(Name + ":" + CurriedValue, CultureInfo.InvariantCulture, Avalonia.Media.FlowDirection.LeftToRight, AvaloniaUtils.FontMonospaceCondensed, 9, Avalonia.Media.Brushes.Blue);
                    g.DrawText(formattedText, new Avalonia.Point(x, y - SocketHeight * 2));
                }
                else
                {
                    FormattedText formattedText = new FormattedText(Name + ":" + CurriedValue, CultureInfo.InvariantCulture, Avalonia.Media.FlowDirection.LeftToRight, AvaloniaUtils.FontMonospaceCondensed, 9, Avalonia.Media.Brushes.Blue);
                    g.DrawText(formattedText, new Avalonia.Point(x, y + SocketHeight));
                }
            }

            g.FillRectangle(this.CurriedValue == null ? Avalonia.Media.Brushes.DarkGray : Avalonia.Media.Brushes.Black, socketRect);
            if (CurryDefault != null && Value == null)
            {
                g.FillRectangle(Avalonia.Media.Brushes.PaleGoldenrod, socketRect);
            }

            FormattedText ft = new FormattedText(Type.Name.Substring(0, 1).ToLowerInvariant(), CultureInfo.InvariantCulture, Avalonia.Media.FlowDirection.LeftToRight, AvaloniaUtils.FontMonospaceCondensed, 9, Avalonia.Media.Brushes.White);
            g.DrawText(ft, new Avalonia.Point(x + Width/2 - ft.Width/2, y));
        }
    }
}
