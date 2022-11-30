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

using Microsoft.Msagl.Core.Layout;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NodeEditor
{
    public class SocketVisual : RelativeFloatingPort
    {
        public const float SocketHeight = 8;

        public const float SocketWidth = 16;

        public SocketVisual(NodeVisual nodeVisual, Microsoft.Msagl.Core.Geometry.Point locationOffset) : base(() => null, () => nodeVisual.Center, locationOffset)
        {
            this.ParentNode = nodeVisual;
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

        public NodeVisual ParentNode { get; }

        public void Draw(Graphics g, Point mouseLocation, MouseButtons mouseButtons)
        {
            float x = (float)(this.ParentNode.Center.X + this.LocationOffset.X);
            float y = (float)(this.ParentNode.Center.Y + this.LocationOffset.Y);

            var socketRect = new RectangleF(x, y, Width, Height);
            var hover = socketRect.Contains(mouseLocation);

            if (hover)
            {
                g.SmoothingMode = SmoothingMode.HighSpeed;
                g.InterpolationMode = InterpolationMode.Low;

                socketRect.Inflate(2, 0);
                var fontBrush = Brushes.Blue;
                if (Input)
                {
                    var sf = new StringFormat();
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(Name + ":" + CurriedValue, SystemFonts.SmallCaptionFont, fontBrush, new RectangleF(x, y - SocketHeight * 2, 1000, Height * 2), sf);
                }
                else
                {
                    var sf = new StringFormat();
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(Name + ":" + CurriedValue, SystemFonts.SmallCaptionFont, fontBrush, new RectangleF(x, y + SocketHeight, 1000, Height * 2), sf);
                }
            }

            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.SmoothingMode = SmoothingMode.HighQuality;

            g.FillRectangle(this.CurriedValue == null ? Brushes.DarkGray : Brushes.Black, socketRect);
            if (CurryDefault != null && Value == null)
            {
                g.FillRectangle(Brushes.PaleGoldenrod, socketRect);
            }

            g.DrawString(Type.Name.Substring(0, 1).ToLowerInvariant(), SystemFonts.SmallCaptionFont, Brushes.White, new RectangleF(x + 2, y - 6, Width * 2, Height * 2));
        }

        public RectangleF GetBounds()
        {
            return new RectangleF((float)(this.ParentNode.Center.X + this.LocationOffset.X), (float)(this.ParentNode.Center.Y + this.LocationOffset.Y), Width, Height);
        }
    }
}
