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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NodeEditor
{
    public class SocketVisual
    {
        public const float SocketHeight = 8;

        public const float SocketWidth = 16;

        public SocketVisual(NodeVisual nodeVisual)
        {
            this.ParentNode = nodeVisual;
        }

        public float DX { get; set; }
        public float DY { get; set; }

        public float Width { get; set; }
        public float Height { get; set; }
        public string Name { get; set; }
        public Type Type { get; set; }
        public bool Input { get; set; }
        public object Value { get; set; }

        public NodeVisual ParentNode { get; }

        public void Draw(Graphics g, Point mouseLocation, MouseButtons mouseButtons)
        {
            float x = this.ParentNode.X + DX;
            float y = this.ParentNode.Y + DY;

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
                    g.DrawString(Name + ":" + Value, SystemFonts.SmallCaptionFont, fontBrush, new RectangleF(x, y - SocketHeight * 2, 1000, Height * 2), sf);
                }
                else
                {
                    var sf = new StringFormat();
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(Name + ":" + Value, SystemFonts.SmallCaptionFont, fontBrush, new RectangleF(x, y + SocketHeight, 1000, Height * 2), sf);
                }
            }

            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.SmoothingMode = SmoothingMode.HighQuality;

            g.FillRectangle(this.Value == null ? Brushes.DarkGray : Brushes.Black, socketRect);
            g.DrawString(Type.Name.Substring(0, 1).ToLowerInvariant(), SystemFonts.SmallCaptionFont, Brushes.White, new RectangleF(x + 2, y - 6, Width * 2, Height * 2));
        }

        public RectangleF GetBounds()
        {
            return new RectangleF(this.ParentNode.X + DX, this.ParentNode.Y + DY, Width, Height);
        }
    }
}
