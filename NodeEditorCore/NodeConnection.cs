﻿/*
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
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using System.Drawing.Drawing2D;
using System.Drawing;
using Ellipse = Microsoft.Msagl.Core.Geometry.Curves.Ellipse;
using LineSegment = Microsoft.Msagl.Core.Geometry.Curves.LineSegment;
using P2 = Microsoft.Msagl.Core.Geometry.Point;
using Polyline = Microsoft.Msagl.Core.Geometry.Curves.Polyline;
using RoundedRect = Microsoft.Msagl.Core.Geometry.Curves.RoundedRect;
using System.Runtime.CompilerServices;

namespace NodeEditor
{
    [DebuggerDisplay("{OutputSocketName}->{InputSocketName}")]
    public class NodeConnection : Edge
    {
        public NodeVisual OutputNode => this.Source as NodeVisual;
        public string OutputSocketName { get; }
        public NodeVisual InputNode => this.Target as NodeVisual;
        public string InputSocketName { get; }
        public Avalonia.Media.DashStyle RunDashStyle { get; }

        public string GUIDEphemeral = Guid.NewGuid().ToString();

        public Avalonia.Media.Color BrushColor = Colors.Black;

        public bool IsHover { get; private set; }

        public NodeConnection(NodeVisual outputNode, string outputSocketName, NodeVisual inputNode, string inputSocketName)
            : base(outputNode, inputNode)
        {
            OutputSocketName = outputSocketName;
            InputSocketName = inputSocketName;
            this.RunDashStyle = new Avalonia.Media.DashStyle(new double[] { 2, 2 }, 0);
        }

        public SocketVisual OutputSocket => OutputNode.GetSockets().Outputs.FirstOrDefault(x => x.Name == OutputSocketName);

        public SocketVisual InputSocket => InputNode.GetSockets().Inputs.FirstOrDefault(x => x.Name == InputSocketName);

        public Avalonia.Point[] DrawAv(DrawingContext g, bool isHover, bool isRunMode)
        {
            if (SourcePort is null)
            {
                this.SourcePort = this.OutputSocket;
            }
            
            if (TargetPort is null)
            {
                this.TargetPort = this.InputSocket;
            }

            this.IsHover = isHover;
            Avalonia.Media.Pen pen = new Avalonia.Media.Pen(new SolidColorBrush(this.BrushColor), this.IsHover ? 2 : 1, this.RunDashStyle);

            // this.PenEmhemeral.LineCap = Avalonia.Media.PenLineCap.Square;

            var beginSocket = this.OutputSocket.GetBounds();
            var endSocket = this.InputSocket.GetBounds();
            var begin = beginSocket.Center.WithY(beginSocket.Y + beginSocket.Height);
            var end = endSocket.Center.WithY(endSocket.Y);
            
            if (this.Curve is null)
            {
                Avalonia.Point[] points = DrawDragConnectionAv(g, pen, begin, end, Math.Abs(this.RunDashStyle.Offset / 10d));
                return points;
            }
            else
            {
                GraphicsPath path = CreateGraphicsPath(this.Curve);
                Avalonia.Point[] avPoints = path.PathPoints.Select(x => new Avalonia.Point(x.X, x.Y)).ToArray();
                g.DrawGeometry(null, pen, new PolylineGeometry(avPoints, false));
                return avPoints;
            
                GraphicsPath CreateGraphicsPath(ICurve iCurve)
                {
                    var graphicsPath = new GraphicsPath();
                    if (iCurve == null)
                        return null;
            
                    var c = iCurve as Curve;
            
                    if (c != null)
                        HandleCurve(c, graphicsPath);
                    else
                    {
                        var ls = iCurve as LineSegment;
                        if (ls != null)
                            graphicsPath.AddLine(PointF(ls.Start), PointF(ls.End));
                        else
                        {
                            var seg = iCurve as CubicBezierSegment;
                            if (seg != null)
                                graphicsPath.AddBezier(PointF(seg.B(0)), PointF(seg.B(1)), PointF(seg.B(2)), PointF(seg.B(3)));
                            else
                            {
                                var ellipse = iCurve as Ellipse;
                                if (ellipse != null)
                                    AddEllipseSeg(graphicsPath, iCurve as Ellipse);
                                else
                                {
                                    var poly = iCurve as Polyline;
                                    if (poly != null) HandlePolyline(poly, graphicsPath);
                                    else
                                    {
                                        var rr = (RoundedRect)iCurve;
                                        HandleCurve(rr.Curve, graphicsPath);
                                    }
                                }
                            }
                        }
                    }
            
                    /* 
                     if (false) {
                         if (c != null) {
                             foreach (var s in c.Segments) {
                                 CubicBezierSegment cubic = s as CubicBezierSegment;
                                 if (cubic != null)
                                     foreach (var t in cubic.MaximalCurvaturePoints) {
                                         graphicsPath.AddPath(CreatePathOnCurvaturePoint(t, cubic), false);
                                     }
            
                             }
                         } else {
                             CubicBezierSegment cubic = iCurve as CubicBezierSegment;
                             if (cubic != null) {
                                 foreach (var t in cubic.MaximalCurvaturePoints) {
                                     graphicsPath.AddPath(CreatePathOnCurvaturePoint(t, cubic), false);
                                 }
                             }
                         }
                     }
            
                      */
            
                    return graphicsPath;
                }
            
                PointF PointF(P2 p)
                {
                    return new PointF((float)p.X, (float)p.Y);
                }
            
                void HandlePolyline(Polyline poly, GraphicsPath graphicsPath)
                {
                    graphicsPath.AddLines(poly.Select(PointF).ToArray());
                    if (poly.Closed)
                        graphicsPath.CloseFigure();
                }
            
                void HandleCurve(Curve c, GraphicsPath graphicsPath)
                {
                    foreach (ICurve seg in c.Segments)
                    {
                        var cubic = seg as CubicBezierSegment;
                        if (cubic != null)
                            graphicsPath.AddBezier(PointF(cubic.B(0)), PointF(cubic.B(1)), PointF(cubic.B(2)),
                                                   PointF(cubic.B(3)));
                        else
                        {
                            var ls = seg as LineSegment;
                            if (ls != null)
                                graphicsPath.AddLine(PointF(ls.Start), PointF(ls.End));
                            else
                            {
                                var el = seg as Ellipse;
                                //                            double del = (el.ParEnd - el.ParStart)/11.0;
                                //                            graphicsPath.AddLines(Enumerable.Range(1, 10).Select(i => el[el.ParStart + del*i]).
                                //                                    Select(p => new PointF((float) p.X, (float) p.Y)).ToArray());
            
                                AddEllipseSeg(graphicsPath, el);
                            }
                        }
                    }
                }
            
                void AddEllipseSeg(GraphicsPath graphicsPath, Ellipse el)
                {
                    const double ToDegreesMultiplier = 180 / Math.PI;
            
                    double sweepAngle;
                    Microsoft.Msagl.Core.Geometry.Rectangle box;
                    float startAngle;
                    GetGdiArcDimensions(el, out startAngle, out sweepAngle, out box);
            
                    graphicsPath.AddArc((float)box.Left,
                                        (float)box.Bottom,
                                        (float)box.Width,
                                        (float)box.Height,
                                        startAngle,
                                        (float)sweepAngle);
            
                    void GetGdiArcDimensions(Ellipse ellipse, out float startAngle, out double sweepAngle, out Microsoft.Msagl.Core.Geometry.Rectangle box)
                    {
                        box = ellipse.FullBox();
                        startAngle = EllipseStandardAngle(ellipse, ellipse.ParStart);
                        bool orientedCcw = ellipse.OrientedCounterclockwise();
                        if (Math.Abs((Math.Abs(ellipse.ParEnd - ellipse.ParStart) - Math.PI * 2)) < 0.001)//we have a full ellipse
                            sweepAngle = 360;
                        else
                            sweepAngle = (orientedCcw ? P2.Angle(ellipse.Start, ellipse.Center, ellipse.End) : P2.Angle(ellipse.End, ellipse.Center, ellipse.Start))
                                * ToDegreesMultiplier;
                        if (!orientedCcw)
                            sweepAngle = -sweepAngle;
                    }
            
                    float EllipseStandardAngle(Ellipse ellipse, double angle)
                    {
                        P2 p = Math.Cos(angle) * ellipse.AxisA + Math.Sin(angle) * ellipse.AxisB;
                        return (float)(Math.Atan2(p.Y, p.X) * ToDegreesMultiplier);
                    }
                }
            }
        }

        public static Avalonia.Point[] DrawDragConnectionAv(DrawingContext g, Avalonia.Media.Pen pen, Avalonia.Point output, Avalonia.Point input, double? animationMultiplier)
        {
           
            if (input == output)
                return new Avalonia.Point[0];
            
            const int interpolation = 48;

            Avalonia.Point[] points = new Avalonia.Point[interpolation];
            for (int i = 0; i < interpolation; i++)
            {
                float amount = i / (float)(interpolation - 1);
            
                var d = Math.Min(Math.Abs(input.X - output.X), 50);
                var a = new Avalonia.Point(output.X, (float)Scale(amount, 0, 1, output.Y, output.Y + d));
                var b = new Avalonia.Point(input.X, (float)Scale(amount, 0, 1, input.Y - d, input.Y));
            
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
            
                if (i > 0 && pen.LineCap == Avalonia.Media.PenLineCap.Square)
                {
                    g.DrawLine(pen, points[i - 1], points[i]);
                }
            }

            PolylineGeometry geom = new PolylineGeometry(points, false);
            g.DrawGeometry(null, pen, geom);

            double contour = geom.ContourLength;
            if (animationMultiplier is not null && geom.TryGetPointAtDistance(animationMultiplier.Value * contour, out Avalonia.Point markerLocation))
            {
                g.DrawEllipse(Avalonia.Media.Brushes.Maroon, null, markerLocation, 3, 3);
            }
            
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

            Avalonia.Point Lerp(Avalonia.Point a, Avalonia.Point b, float amount)
            {
                Avalonia.Point result = new Avalonia.Point(a.X * (1f - amount) + b.X * amount, a.Y * (1f - amount) + b.Y * amount);
                return result;
            }
        }

        public bool PropagateValue(INodesContext context)
        {
            if (this.OutputSocket.Value is null)
            {
                return false;
            }

            _ = Animate.Instance?.Ease(
                this.GUIDEphemeral,
                0d,
                x => this.RunDashStyle.Offset = x,
                Easings.CubicIn,
                1000,
                -10d);

            if (this.InputSocket.Type == typeof(Bang))
            {
                this.InputSocket.Value = Bang.Instance;
                return true;
            }
            else if (this.OutputSocket.Value is not Bang || this.InputSocket.Type == typeof(object))
            {
                this.InputSocket.Value = this.OutputSocket.Value;
                return true;
            }

            return this.OutputSocket.Value is Bang;
        }
    }
}
