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

using AnimateForms.Core;
using Microsoft.Msagl.Core.Layout;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace NodeEditor
{    
    internal class NodeConnection : Edge
    {
        public NodeVisual OutputNode => this.Source as NodeVisual;
        public string OutputSocketName { get; }
        public NodeVisual InputNode => this.Target as NodeVisual;
        public string InputSocketName { get; }

        public string GUIDEphemeral = Guid.NewGuid().ToString();

        public Pen PenEmhemeral = Pens.Black;

        public bool IsHover { get; private set; }

        public NodeConnection(NodeVisual outputNode, string outputSocketName, NodeVisual inputNode, string inputSocketName)
            : base(outputNode, inputNode)
        {
            OutputSocketName = outputSocketName;
            InputSocketName = inputSocketName;
        }

        public SocketVisual OutputSocket => OutputNode.GetSockets().Outputs.FirstOrDefault(x => x.Name == OutputSocketName);

        public SocketVisual InputSocket => InputNode.GetSockets().Inputs.FirstOrDefault(x => x.Name == InputSocketName);

        public PointF[] Draw(Graphics g, bool isHover, bool isRunMode, Animate animate)
        {
            if (!this.IsHover && isHover)
            {
                this.PenEmhemeral = new Pen(System.Drawing.Color.Black, 2);
            }
            else if (this.IsHover && !isHover)
            {
                this.PenEmhemeral = Pens.Black;
            }

            this.IsHover = isHover;

            // this.PenEmhemeral.StartCap = LineCap.ArrowAnchor;

            var beginSocket = this.OutputSocket.GetBounds();
            var endSocket = this.InputSocket.GetBounds();
            var begin = beginSocket.Location + new SizeF(beginSocket.Width / 2f, beginSocket.Height);
            var end = endSocket.Location + new SizeF(endSocket.Width / 2f, 0f);

            PointF[] points = DrawConnection(g, this.PenEmhemeral, begin, end);

            return points;
        }

        public static PointF[] DrawConnection(Graphics g, Pen pen, PointF output, PointF input)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.SmoothingMode = SmoothingMode.HighQuality;

            if (input == output)
                return new PointF[0];

            const int interpolation = 48;

            PointF[] points = new PointF[interpolation];
            for (int i = 0; i < interpolation; i++)
            {
                float amount = i / (float)(interpolation - 1);

                var d = Math.Min(Math.Abs(input.X - output.X), 50);
                var a = new PointF(output.X, (float)Scale(amount, 0, 1, output.Y, output.Y + d));
                var b = new PointF(input.X, (float)Scale(amount, 0, 1, input.Y - d, input.Y));

                var bas = Sat(Scale(amount, 0, 1, 0, 1));
                var cos = Math.Cos(bas * Math.PI);
                if (cos < 0)
                {
                    cos = -Math.Pow(-cos, 0.2);
                }
                else
                {
                    cos = Math.Pow(cos, 0.2);
                }

                // amount = (float)cos * -0.5f + 0.5f;

                var f = Lerp(a, b, amount);
                points[i] = f;

                if (i > 0 && pen.StartCap == LineCap.ArrowAnchor)
                {
                    g.DrawLine(pen, points[i - 1], points[i]);
                }
            }

            g.DrawLines(pen, points);

            return points;

            double Sat(double x)
            {
                if (x < 0) return 0;
                if (x > 1) return 1;
                return x;
            }

            double Scale(double x, double a, double b, double c, double d)
            {
                double s = (x - a) / (b - a);
                return s * (d - c) + c;
            }

            PointF Lerp(PointF a, PointF b, float amount)
            {
                PointF result = new PointF();

                result.X = a.X * (1f - amount) + b.X * amount;
                result.Y = a.Y * (1f - amount) + b.Y * amount;

                return result;
            }
        }

        public void PropagateValue(INodesContext context, Animate animate)
        {
            bool isUpdate = this.InputSocket.Value != this.OutputSocket.Value;
            if (!(this.OutputSocket.Value is Bang && this.InputSocket.Type != typeof(Bang)))
            {
                this.InputSocket.Value = this.OutputSocket.Value;
            }

            if (isUpdate)
            {
                Pen orgPen = this.PenEmhemeral;
                _ = animate.Recolor(
                    this.GUIDEphemeral,
                    orgPen.Color,
                    x => this.PenEmhemeral = new Pen(x, this.PenEmhemeral.Width),
                    Easings.CubicIn,
                    200,
                    System.Drawing.Color.DarkGoldenrod).ContinueWith(
                        t => animate.Recolor(
                            this.GUIDEphemeral,
                            this.PenEmhemeral.Color,
                            x => this.PenEmhemeral = new Pen(x, this.PenEmhemeral.Width),
                            Easings.CubicOut,
                            50,
                            orgPen.Color)
                    );
            }
        }
    }
}
