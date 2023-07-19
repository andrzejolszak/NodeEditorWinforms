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
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using System.Drawing.Drawing2D;
using Ellipse = Microsoft.Msagl.Core.Geometry.Curves.Ellipse;
using LineSegment = Microsoft.Msagl.Core.Geometry.Curves.LineSegment;
using P2 = Microsoft.Msagl.Core.Geometry.Point;
using Polyline = Microsoft.Msagl.Core.Geometry.Curves.Polyline;
using RoundedRect = Microsoft.Msagl.Core.Geometry.Curves.RoundedRect;

namespace NodeEditor
{    
    public class NodeConnection : Edge
    {
        public NodeVisual OutputNode => this.Source as NodeVisual;
        public string OutputSocketName { get; }
        public NodeVisual InputNode => this.Target as NodeVisual;
        public string InputSocketName { get; }

        public string GUIDEphemeral = Guid.NewGuid().ToString();

        public Pen PenEmhemeral = Pens.Black;
        public Avalonia.Media.Pen PenEmhemeralAv = AvaloniaUtils.BlackPen1;

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
            if (SourcePort is null)
            {
                this.SourcePort = this.OutputSocket;
            }

            if (TargetPort is null)
            {
                this.TargetPort = this.InputSocket;
            }

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

            if (this.Curve is null)
            {
                PointF[] points = DrawConnection(g, this.PenEmhemeral, begin, end);
                return points;
            }
            else
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.SmoothingMode = SmoothingMode.HighQuality;

                GraphicsPath path = CreateGraphicsPath(this.Curve);
                g.DrawPath(this.PenEmhemeral, path);

                return path.PathPoints;

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

        public Avalonia.Point[] DrawAv(DrawingContext g, bool isHover, bool isRunMode, Animate animate)
        {
            if (SourcePort is null)
            {
                this.SourcePort = this.OutputSocket;
            }
            
            if (TargetPort is null)
            {
                this.TargetPort = this.InputSocket;
            }
            
            if (!this.IsHover && isHover)
            {
                this.PenEmhemeralAv = AvaloniaUtils.BlackPen2;
            }
            else if (this.IsHover && !isHover)
            {
                this.PenEmhemeralAv = AvaloniaUtils.BlackPen1;
            }
            
            this.IsHover = isHover;
            
            // this.PenEmhemeral.LineCap = Avalonia.Media.PenLineCap.Square;
            
            var beginSocket = this.OutputSocket.GetBounds().ToAvRect();
            var endSocket = this.InputSocket.GetBounds().ToAvRect();
            var begin = beginSocket.Translate(new Avalonia.Vector(beginSocket.Width / 2f, beginSocket.Height));
            var end = endSocket.Translate(new Avalonia.Vector(endSocket.Width / 2f, 0f));
            
            if (this.Curve is null)
            {
                Avalonia.Point[] points = DrawConnectionAv(g, this.PenEmhemeralAv, begin.Center, end.Center);
                return points;
            }
            else
            {
                GraphicsPath path = CreateGraphicsPath(this.Curve);
                Avalonia.Point[] avPoints = path.PathPoints.Select(x => new Avalonia.Point(x.X, x.Y)).ToArray();
                g.DrawGeometry(null, this.PenEmhemeralAv, new PolylineGeometry(avPoints, false));
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

        public static Avalonia.Point[] DrawConnectionAv(DrawingContext g,  Avalonia.Media.Pen pen, Avalonia.Point output, Avalonia.Point input)
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
            
            g.DrawGeometry(null, pen, new PolylineGeometry(points, false));
            
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
