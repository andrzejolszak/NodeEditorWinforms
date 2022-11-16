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

using Supercluster.KDTree;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NodeEditor
{
    internal class NodesGraph
    {
        private const int HoverThrottlingMs = 10;
        internal List<NodeVisual> Nodes = new List<NodeVisual>();
        internal List<NodeConnection> Connections = new List<NodeConnection>();
        internal KDTree<float, NodeConnection> KdTree = null;
        private List<(NodeConnection, PointF[])> _points = new List<(NodeConnection, PointF[])>();
        private float _pointsChecksum = 0;
        private bool _treeRecalc = false;
        private bool _hoverRecalc = false;
        private NodeConnection _hoverConnection;

        public void Draw(Graphics g, Point mouseLocation, MouseButtons mouseButtons)
        {
            g.InterpolationMode = InterpolationMode.Low;
            g.SmoothingMode = SmoothingMode.HighSpeed;

            g.FillRectangle(new SolidBrush(Color.FromArgb(220, Color.White)), g.ClipBounds);

            foreach (var node in Nodes)
            {
                var rect = new RectangleF(new PointF((float)node.X, (float)node.Y), node.GetNodeBounds());
                bool isHover = rect.Contains(mouseLocation);
                int offset = isHover ? 6 : 4;
                g.FillRectangle(Brushes.DarkGray, new RectangleF(new PointF(node.X+ offset, node.Y+ offset), node.GetNodeBounds()));
            }

            if (KdTree != null && !_hoverRecalc)
            {
                _hoverRecalc = true;
                Task.Run(() =>
                {
                    _hoverConnection = KdTree.RadialSearch(new float[] { mouseLocation.X, mouseLocation.Y }, 50, 1).FirstOrDefault()?.Item2;
                    Task.Delay(HoverThrottlingMs);
                    _hoverRecalc = false;
                });
            }

            _points.Clear();

            var cpen = Pens.Black;
            var epen = new Pen(Color.Gold, 3);
            var epen2 = new Pen(Color.Black, 2);
            // epen2.StartCap = LineCap.ArrowAnchor;

            for (int i = 0; i < this.Connections.Count; i++)
            {
                NodeConnection connection = this.Connections[i];
                var osoc = connection.OutputNode.GetSockets().Outputs.FirstOrDefault(x => x.Name == connection.OutputSocketName);
                var beginSocket = osoc.GetBounds();
                var isoc = connection.InputNode.GetSockets().Inputs.FirstOrDefault(x => x.Name == connection.InputSocketName);
                var endSocket = isoc.GetBounds();
                var begin = beginSocket.Location + new SizeF(beginSocket.Width / 2f, beginSocket.Height);
                var end = endSocket.Location + new SizeF(endSocket.Width / 2f, 0f);

                bool isHover = connection == _hoverConnection;
                PointF[] points = DrawConnection(g, isHover ? epen2 : cpen, begin, end);
                _points.Add((connection, points));
            }

            float newChecksum = this._points.SelectMany(x => x.Item2).Sum(x => x.X);
            if (newChecksum != _pointsChecksum && !_treeRecalc)
            {
                _treeRecalc = true;
                _pointsChecksum = newChecksum;
                Task.Run(() =>
                {
                    (NodeConnection, PointF[])[] pointsCopy = _points.ToArray();
                    float[][] points = pointsCopy.SelectMany(x => x.Item2.Select(y => (x.Item1, new float[] { y.X, y.Y }))).Select(x => x.Item2).ToArray();
                    NodeConnection[] conns = pointsCopy.SelectMany(x => x.Item2.Select(y => (x.Item1, new float[] { y.X, y.Y }))).Select(x => x.Item1).ToArray();
                    KdTree = new KDTree<float, NodeConnection>(2, points, conns, (x, y) =>
                    {
                        float dist = 0f;
                        for (int i = 0; i < x.Length; i++)
                        {
                            dist += (x[i] - y[i]) * (x[i] - y[i]);
                        }

                        return dist;
                    });

                    Task.Delay(HoverThrottlingMs);

                    _treeRecalc = false;
                });
            }

            var orderedNodes = Nodes.OrderByDescending(x => x.Order);
            foreach (var node in orderedNodes)
            {
                node.Draw(g, mouseLocation, mouseButtons);
            }
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
                float amount = i/(float) (interpolation - 1);
               
                var d = Math.Min(Math.Abs(input.X - output.X), 50);
                var a = new PointF(output.X, (float) Scale(amount, 0, 1, output.Y, output.Y + d));
                var b = new PointF(input.X, (float) Scale(amount, 0, 1, input.Y-d, input.Y));

                var bas = Sat(Scale(amount, 0, 1, 0, 1));       
                var cos = Math.Cos(bas*Math.PI);
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
                    g.DrawLine(pen, points[i-1], points[i]);
                }
            }

            g.DrawLines(pen, points);

            return points;
        }

        public static double Sat(double x)
        {
            if (x < 0) return 0;
            if (x > 1) return 1;
            return x;
        }


        public static double Scale(double x, double a, double b, double c, double d)
        {
            double s = (x - a)/(b - a);
            return s*(d - c) + c;
        }

        public static PointF Lerp(PointF a, PointF b, float amount)
        {
            PointF result = new PointF();

            result.X = a.X*(1f - amount) + b.X*amount;
            result.Y = a.Y*(1f - amount) + b.Y*amount;

            return result;
        }
    }
}
