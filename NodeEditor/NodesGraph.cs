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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NodeEditor
{
    public class NodesGraph
    {
        private const int HoverThrottlingMs = 10;
        public List<NodeVisual> Nodes = new List<NodeVisual>();
        internal List<NodeConnection> Connections = new List<NodeConnection>();
        internal KDTree<float, NodeConnection> KdTree = null;
        private List<(NodeConnection, PointF[])> _points = new List<(NodeConnection, PointF[])>();
        private float _pointsChecksum = 0;
        private bool _treeRecalc = false;
        private bool _hoverRecalc = false;
        private NodeConnection _hoverConnection;

        public string GUID = Guid.NewGuid().ToString();

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
                var osoc = connection.OutputSocket;
                var beginSocket = osoc.GetBounds();
                var isoc = connection.InputSocket;
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

        /// <summary>
        /// Restores node graph state from previously serialized binary data.
        /// </summary>
        /// <param name="data"></param>
        public static NodesGraph[] Deserialize(byte[] data, INodesContext context)
        {
            List<(NodeVisual, string)> nodesWithSubsystems = new List<(NodeVisual, string)>();
            List<NodesGraph> graphs = new List<NodesGraph>();
            using (var br = new BinaryReader(new MemoryStream(data)))
            {
                var ident = br.ReadString();
                if (ident != "NodeSystemP") return null;
                var version = br.ReadInt32();

                var graphCount = br.ReadInt32();

                for (int g = 0; g < graphCount; g++)
                {
                    NodesGraph graph = new NodesGraph();
                    var graphGuid = br.ReadString();
                    graph.GUID = graphGuid;
                    graphs.Add(graph);

                    int nodeCount = br.ReadInt32();
                    for (int i = 0; i < nodeCount; i++)
                    {
                        var nv = DeserializeNode(br, context);
                        if (nv.Item1 != null)
                        {
                            graph.Nodes.Add(nv.Item1);
                            nodesWithSubsystems.Add(nv);
                        }
                    }

                    var connectionsCount = br.ReadInt32();
                    for (int i = 0; i < connectionsCount; i++)
                    {
                        var og = br.ReadString();
                        NodeVisual outputNode = graph.Nodes.FirstOrDefault(x => x.GUID == og);
                        string outputSocketName = br.ReadString();
                        var ig = br.ReadString();
                        NodeVisual inputNode = graph.Nodes.FirstOrDefault(x => x.GUID == ig);
                        string inputSocketName = br.ReadString();
                        var con = new NodeConnection(outputNode, outputSocketName, inputNode, inputSocketName);
                        br.ReadBytes(br.ReadInt32()); //read additional data

                        graph.Connections.Add(con);
                    }

                    br.ReadBytes(br.ReadInt32()); //read additional data
                }
            }

            // Assign subsystems
            foreach ((NodeVisual, string) nodesWithSubsystem in nodesWithSubsystems)
            {
                if (nodesWithSubsystem.Item2 != null && nodesWithSubsystem.Item2 != "")
                {
                    nodesWithSubsystem.Item1.SubsystemGraph = graphs.Single(x => x.GUID == nodesWithSubsystem.Item2);
                }
            }

            return graphs.ToArray();
        }

        /// <summary>
        /// Serializes current node graph to binary data.
        /// </summary>        
        public static byte[] Serialize(NodesGraph mainGraph)
        {
            List<NodesGraph> graphs = new List<NodesGraph>() { mainGraph };
            graphs.AddRange(mainGraph.Nodes.Where(x => x.SubsystemGraph != null).Select(x => x.SubsystemGraph));
            using (var bw = new BinaryWriter(new MemoryStream()))
            {
                bw.Write("NodeSystemP"); //recognization string
                bw.Write(1000); //version

                bw.Write(graphs.Count);

                foreach (NodesGraph graph in graphs)
                {
                    bw.Write(graph.GUID);
                    bw.Write(graph.Nodes.Count);
                    foreach (var node in graph.Nodes)
                    {
                        SerializeNode(bw, node);
                    }

                    bw.Write(graph.Connections.Count);
                    foreach (var connection in graph.Connections)
                    {
                        bw.Write(connection.OutputNode.GUID);
                        bw.Write(connection.OutputSocketName);

                        bw.Write(connection.InputNode.GUID);
                        bw.Write(connection.InputSocketName);
                        bw.Write(0); //additional data size per connection
                    }

                    bw.Write(0); //additional data size per graph
                }

                return (bw.BaseStream as MemoryStream).ToArray();
            }
        }

        public static void SerializeNode(BinaryWriter bw, NodeVisual node)
        {
            bw.Write(node.GUID);
            bw.Write(node.X);
            bw.Write(node.Y);
            bw.Write(node.IsInteractive);
            bw.Write(node.Name);
            bw.Write(node.Order);
            if (node.CustomEditor == null)
            {
                bw.Write("");
                bw.Write("");
            }
            else
            {
                bw.Write(node.CustomEditor.GetType().Assembly.GetName().Name);
                bw.Write(node.CustomEditor.GetType().FullName);
            }
            bw.Write(node.MethodInf.Name);
            bw.Write(node.SubsystemGraph?.GUID ?? "");
            bw.Write(8); //additional data size per node
            bw.Write(node.Int32Tag);
            bw.Write(node.NodeColor.ToArgb());
        }

        public static (NodeVisual, string) DeserializeNode(BinaryReader br, INodesContext context)
        {
            string id = br.ReadString();
            var loadedNode = new NodeVisual(br.ReadSingle(), br.ReadSingle());
            loadedNode.GUID = id;
            loadedNode.IsInteractive = br.ReadBoolean();
            loadedNode.Name = br.ReadString();
            loadedNode.Order = br.ReadInt32();
            var customEditorAssembly = br.ReadString();
            var customEditor = br.ReadString();
            loadedNode.MethodInf = context.GetType().GetMethod(br.ReadString());
            string subsystemGuid = br.ReadString();

            if (loadedNode.MethodInf is null)
            {
                br.ReadBytes(br.ReadInt32());
                var additional2 = br.ReadInt32(); //read additional data
                if (additional2 >= 4)
                {
                    loadedNode.Int32Tag = br.ReadInt32();
                    if (additional2 >= 8)
                    {
                        loadedNode.NodeColor = Color.FromArgb(br.ReadInt32());
                    }
                }
                if (additional2 > 8)
                {
                    br.ReadBytes(additional2 - 8);
                }

                return (null, null);
            }

            var attribute = loadedNode.MethodInf.GetCustomAttributes(typeof(NodeAttribute), false)
                                        .Cast<NodeAttribute>()
                                        .FirstOrDefault();
            if (attribute != null)
            {
                loadedNode.CustomWidth = attribute.Width;
                loadedNode.CustomHeight = attribute.Height;
                loadedNode.InvokeOnLoad = attribute.InvokeOnLoad;
            }

            var additional = br.ReadInt32(); //read additional data
            if (additional >= 4)
            {
                loadedNode.Int32Tag = br.ReadInt32();
                if (additional >= 8)
                {
                    loadedNode.NodeColor = Color.FromArgb(br.ReadInt32());
                }
            }
            if (additional > 8)
            {
                br.ReadBytes(additional - 8);
            }

            if (customEditor != "")
            {
                if (customEditor == "System.Windows.Forms.Label")
                {
                    loadedNode.CustomEditor = new Label();
                }
                else if (customEditor == "System.Windows.Forms.TextBox")
                {
                    loadedNode.CustomEditor = new TextBox();
                }
                else
                {
                    loadedNode.CustomEditor = Activator.CreateInstance(AppDomain.CurrentDomain, customEditorAssembly, customEditor).Unwrap() as Control;
                }

                Control ctrl = loadedNode.CustomEditor;
                if (ctrl != null)
                {
                    ctrl.BackColor = loadedNode.NodeColor;
                    ctrl.Tag = (loadedNode, context);
                }

                loadedNode.LayoutEditor();
            }
            return (loadedNode, subsystemGuid);
        }
    }
}
