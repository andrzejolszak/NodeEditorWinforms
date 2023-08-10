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
using Microsoft.Msagl.Routing.Rectilinear;
using Supercluster.KDTree;

namespace NodeEditor
{
    public class NodesGraph : GeometryGraph
    {
        public NodeVisual OwnerNode { get; set; }
        private const int HoverThrottlingMs = 10;
        internal KDTree<float, NodeConnection> KdTree = null;
        private List<(NodeConnection, Point[])> _pointsAv = new List<(NodeConnection, Point[])>();
        private float _pointsChecksum = 0;
        private bool _treeRecalc = false;
        private bool _hoverRecalc = false;
        private NodeConnection _hoverConnection;

        public IEnumerable<NodeVisual> NodesTyped => this.Nodes.Cast<NodeVisual>();

        public IEnumerable<NodeConnection> EdgesTyped => this.Edges.Cast<NodeConnection>();

        public string GUID = Guid.NewGuid().ToString();

        public void DrawAv(DrawingContext g, PointerPoint mouse, bool isRunMode, double width, double height)
        {           
            g.FillRectangle(new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), new Rect(0,0, width, height));
            
            var orderedNodes = Nodes.OrderBy(x => x.BoundingBox.LeftTop);
            foreach (var node in orderedNodes)
            {
                (node as NodeVisual).DrawAv(g, mouse, isRunMode);
            }
            
            if (KdTree != null && !_hoverRecalc)
            {
                _hoverRecalc = true;
                Task.Run(() =>
                {
                    _hoverConnection = KdTree.RadialSearch(new float[] { (float)mouse.Position.X, (float)mouse.Position.Y }, 50, 1).FirstOrDefault()?.Item2;
                    Task.Delay(HoverThrottlingMs);
                    _hoverRecalc = false;
                });
            }

            _pointsAv.Clear();
            
            foreach (NodeConnection connection in this.Edges)
            {
                bool isHover = connection == _hoverConnection;
                Point[] points = connection.DrawAv(g, isHover, isRunMode);
                _pointsAv.Add((connection, points));
            }
            
            float newChecksum = this._pointsAv.SelectMany(x => x.Item2).Sum(x => (float)x.X);
            if (newChecksum != _pointsChecksum && !_treeRecalc)
            {
                _treeRecalc = true;
                _pointsChecksum = newChecksum;
                Task.Run(() =>
                {
                    (NodeConnection, Point[])[] pointsCopy = _pointsAv.ToArray();
                    float[][] points = pointsCopy.SelectMany(x => x.Item2.Select(y => (x.Item1, new float[] { (float)y.X, (float)y.Y }))).Select(x => x.Item2).ToArray();
                    NodeConnection[] conns = pointsCopy.SelectMany(x => x.Item2.Select(y => (x.Item1, new float[] { (float)y.X, (float)y.Y }))).Select(x => x.Item1).ToArray();
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
                            nv.Item1.OwnerGraph = graph;
                            nodesWithSubsystems.Add(nv);
                        }
                    }

                    var connectionsCount = br.ReadInt32();
                    for (int i = 0; i < connectionsCount; i++)
                    {
                        var og = br.ReadString();
                        NodeVisual outputNode = graph.NodesTyped.FirstOrDefault(x => x.GUID == og);
                        string outputSocketName = br.ReadString();
                        var ig = br.ReadString();
                        NodeVisual inputNode = graph.NodesTyped.FirstOrDefault(x => x.GUID == ig);
                        string inputSocketName = br.ReadString();
                        var con = new NodeConnection(outputNode, outputSocketName, inputNode, inputSocketName);
                        br.ReadBytes(br.ReadInt32()); //read additional data

                        graph.AddEdge(con);
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
                    nodesWithSubsystem.Item1.SubsystemGraph.OwnerNode = nodesWithSubsystem.Item1;
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
            graphs.AddRange(mainGraph.NodesTyped.Where(x => x.SubsystemGraph != null).Select(x => x.SubsystemGraph));
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
                        SerializeNode(bw, node as NodeVisual);
                    }

                    bw.Write(graph.Edges.Count);
                    foreach (NodeConnection connection in graph.Edges)
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
            bw.Write(node.NodeDesc.Name);
            bw.Write(node.Name);
            if (node.CustomEditorAv == null)
            {
                bw.Write("");
                bw.Write("");
            }
            else
            {
                bw.Write(node.CustomEditorAv.GetType().Assembly.GetName().Name);
                bw.Write(node.CustomEditorAv.GetType().FullName);
            }
            bw.Write(node.SubsystemGraph?.GUID ?? "");
            bw.Write(8); //additional data size per node
            bw.Write(node.Int32Tag);
            bw.Write(node.NodeColorAv.ToUInt32());
        }

        public static (NodeVisual, string) DeserializeNode(BinaryReader br, INodesContext context)
        {
            Dictionary<string, NodeDescriptor> descs = context.GetNodeDescriptors().ToDictionary(x => x.Name);
            string id = br.ReadString();
            float x = br.ReadSingle();
            float y = br.ReadSingle();
            string nodeDescName = br.ReadString();
            string name = br.ReadString();

            var loadedNode = new NodeVisual(name, x, y);
            loadedNode.GUID = id;
            var customEditorAssembly = br.ReadString();
            var customEditor = br.ReadString();

            if (descs.TryGetValue(nodeDescName, out NodeDescriptor? desc))
            {
                loadedNode.NodeDesc = desc;
            }

            string subsystemGuid = br.ReadString();

            var additional = br.ReadInt32(); //read additional data
            if (additional >= 4)
            {
                loadedNode.Int32Tag = br.ReadInt32();
                if (additional >= 8)
                {
                    loadedNode.NodeColorAv = Color.FromUInt32(br.ReadUInt32());
                }
            }
            if (additional > 8)
            {
                br.ReadBytes(additional - 8);
            }

            if (customEditor != "")
            {
                if (customEditor == "Avalonia.Controls.Label" || customEditor == "System.Windows.Forms.Label")
                {
                    loadedNode.CustomEditorAv = new Avalonia.Controls.Label();
                }
                else if (customEditor == "Avalonia.Controls.TextBox" || customEditor == "System.Windows.Forms.TextBox")
                {
                    loadedNode.CustomEditorAv = new TextBox();
                }
                else
                {
                    loadedNode.CustomEditorAv = Activator.CreateInstance(customEditorAssembly, customEditor).Unwrap() as Control;
                }

                loadedNode.CustomEditorAv.MinWidth = loadedNode.CustomEditorAv.Width = NodeVisual.NodeWidth;
                loadedNode.CustomEditorAv.MinHeight = loadedNode.CustomEditorAv.Height = NodeVisual.HeaderHeight;

                loadedNode.LayoutEditor();
            }
            return (loadedNode, subsystemGuid);
        }

        internal void AddEdge(NodeConnection nodeConnection)
        {
            this.Edges.Add(nodeConnection);
            nodeConnection.InputNode.AddInEdge(nodeConnection);
            nodeConnection.OutputNode.AddOutEdge(nodeConnection);
        }

        internal void RemoveEdge(NodeConnection nodeConnection)
        {
            this.Edges.Remove(nodeConnection);
            nodeConnection.InputNode.RemoveInEdge(nodeConnection);
            nodeConnection.OutputNode.RemoveOutEdge(nodeConnection);
        }

        internal void RouteEdges()
        {
            //RectilinearInteractiveEditor.CreatePortsAndRouteEdges(3, 1, this.Nodes, this.Edges, Microsoft.Msagl.Core.Routing.EdgeRoutingMode.Rectilinear, true, true);

            foreach(Edge e in this.Edges)
            {
                e.EdgeGeometry.Waypoints = new []
                {
                    e.EdgeGeometry.SourcePort.Location + new Microsoft.Msagl.Core.Geometry.Point(SocketVisual.SocketWidth / 2, -1),
                    e.EdgeGeometry.TargetPort.Location + new Microsoft.Msagl.Core.Geometry.Point(SocketVisual.SocketWidth / 2, 1),
                };
            }

            var rectRouter = new RectilinearEdgeRouter(this, 3, 2, true);
            rectRouter.Run();


            // var router = new SplineRouter(graph, 3, 3, Math.PI / 6, new BundlingSettings());
            // router.Run();

            //var nodes = this.Nodes.Select(n => n.BoundaryCurve).ToArray();
            //var portRouter = new Microsoft.Msagl.Routing.InteractiveEdgeRouter(nodes, 3, 3, 1);
            //portRouter.Run();
            //foreach(var e in this.Edges.ToArray())
            //{
            //    DrawEdgeWithPort(e, portRouter, 2.5, 0.5);
            //}

            void DrawEdgeWithPort(Edge edge, Microsoft.Msagl.Routing.InteractiveEdgeRouter portRouter, double par0, double par1)
            {

                var port0 = new CurvePort(edge.SourcePort.Curve, par0);
                var port1 = new CurvePort(edge.TargetPort.Curve, par1);

                Microsoft.Msagl.Core.Geometry.SmoothedPolyline sp;
                var spline = portRouter.RouteSplineFromPortToPortWhenTheWholeGraphIsReady(port0, port1, true, out sp);

                Arrowheads.TrimSplineAndCalculateArrowheads(edge.EdgeGeometry,
                                                             edge.SourcePort.Curve,
                                                             edge.TargetPort.Curve,
                                                             spline, true);

            }
        }
    }
}
