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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace NodeEditor
{
    /// <summary>
    /// Main control of Node Editor Winforms
    /// </summary>
    [ToolboxBitmap(typeof(NodesControl), "nodeed")]
    public partial class NodesControl : UserControl
    {
        internal class NodeToken
        {
            public MethodInfo Method;
            public NodeAttribute Attribute;
        }

        private NodesGraph graph = new NodesGraph();
        private bool needRepaint = true;
        private Timer timer = new Timer();
        private bool mdown;
        private Point lastmpos;
        private SocketVisual dragSocket;
        private NodeVisual dragSocketNode;
        private PointF dragConnectionBegin;
        private PointF dragConnectionEnd;

        /// <summary>
        /// Context of the editor. You should set here an instance that implements INodesContext interface.
        /// In context you should define your nodes (methods decorated by Node attribute).
        /// </summary>
        public INodesContext Context
        {
            get { return context; }
            set
            {
                if (context != null)
                {
                    context.FeedbackInfo -= ContextOnFeedbackInfo;
                }
                context = value;
                if (context != null)
                {
                    context.FeedbackInfo += ContextOnFeedbackInfo;
                }
            }
        }

        public bool IsRunMode { get; private set; }

        /// <summary>
        /// Occurs when user selects a node. In the object will be passed node settings for unplugged inputs/outputs.
        /// </summary>
        public event Action<NodeVisual> OnNodeSelected = delegate { };

        /// <summary>
        /// Occurs when node would to share its description.
        /// </summary>
        public event Action<string> OnNodeHint = delegate { };

        /// <summary>
        /// Indicates which part of control should be actually visible. It is useful when dragging nodes out of autoscroll parent control,
        /// to guarantee that moving node/connection is visible to user.
        /// </summary>
        public event Action<RectangleF> OnShowLocation = delegate { };

        private readonly Dictionary<ToolStripMenuItem,int> allContextItems = new Dictionary<ToolStripMenuItem, int>();

        private Point lastMouseLocation;

        private Point autoScroll;

        private PointF selectionStart;

        private PointF selectionEnd;

        private INodesContext context;

        private bool breakExecution = false;        

        /// <summary>
        /// Default constructor
        /// </summary>
        public NodesControl()
        {
            InitializeComponent();
            timer.Interval = 30;
            timer.Tick += TimerOnTick;
            timer.Start();        
            KeyDown += OnKeyDown;
            SetStyle(ControlStyles.Selectable, true);
        }

        private void ContextOnFeedbackInfo(string message, NodeVisual nodeVisual, FeedbackType type, object tag, bool breakExecution)
        {
            this.breakExecution = breakExecution;
            if (breakExecution)
            {
                nodeVisual.Feedback = type;
                OnNodeHint(message);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 7)
            {                
                return;                
            }
            base.WndProc(ref m);
        }

        private void OnKeyDown(object sender, KeyEventArgs keyEventArgs)
        {
            if (keyEventArgs.KeyCode == Keys.Delete)
            {
                DeleteSelectedNodes();
            }
        }

        private void TimerOnTick(object sender, EventArgs eventArgs)
        {
            if (DesignMode) return;
            if (needRepaint)
            {
                Invalidate();
            }
        }

        private void NodesControl_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;            

            graph.Draw(e.Graphics, PointToClient(MousePosition), MouseButtons);            

            if (dragSocket != null)
            {
                var pen = new Pen(Color.Black, 2);
                NodesGraph.DrawConnection(e.Graphics, pen, dragConnectionBegin, dragConnectionEnd);
            }

            if (selectionStart != PointF.Empty)
            {
                var rect = Rectangle.Round(MakeRect(selectionStart, selectionEnd));
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(50, Color.CornflowerBlue)), rect);
                e.Graphics.DrawRectangle(new Pen(Color.DodgerBlue), rect);
            }

            needRepaint = false;
        }

        private static RectangleF MakeRect(PointF a, PointF b)
        {
            var x1 = a.X;
            var x2 = b.X;
            var y1 = a.Y;
            var y2 = b.Y;
            return new RectangleF(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        }

        private void NodesControl_MouseMove(object sender, MouseEventArgs e)
        {
            var em = PointToScreen(e.Location);
            if (selectionStart != PointF.Empty)
            {
                selectionEnd = e.Location;
            }
            if (mdown && !IsRunMode)
            {                                            
                foreach (var node in graph.Nodes.Where(x => x.IsSelected))
                {
                    node.X += em.X - lastmpos.X;
                    node.Y += em.Y - lastmpos.Y;

                    node.LayoutEditor();
                }

                if (graph.Nodes.Exists(x => x.IsSelected))
                {
                    var n = graph.Nodes.FirstOrDefault(x => x.IsSelected);
                    var bound = new RectangleF(new PointF(n.X,n.Y), n.GetNodeBounds());
                    foreach (var node in graph.Nodes.Where(x=>x.IsSelected))
                    {
                        bound = RectangleF.Union(bound, new RectangleF(new PointF(node.X, node.Y), node.GetNodeBounds()));
                    }
                    OnShowLocation(bound);
                }
                Invalidate();
                
                if (dragSocket != null)
                {
                    var center = new PointF(dragSocket.ParentNode.X + dragSocket.DX + dragSocket.Width/2f, dragSocket.ParentNode.Y + dragSocket.DY + dragSocket.Height/2f);
                    if (dragSocket.Input)
                    {
                        dragConnectionBegin.X += em.X - lastmpos.X;
                        dragConnectionBegin.Y += em.Y - lastmpos.Y;
                        dragConnectionEnd = center;
                        OnShowLocation(new RectangleF(dragConnectionBegin, new SizeF(10, 10)));
                    }
                    else
                    {
                        dragConnectionBegin = center;
                        dragConnectionEnd.X += em.X - lastmpos.X;
                        dragConnectionEnd.Y += em.Y - lastmpos.Y;
                        OnShowLocation(new RectangleF(dragConnectionEnd, new SizeF(10, 10)));
                    }
                    
                }
                lastmpos = em;
            }            

            needRepaint = true;
        }

        private void NodesControl_MouseDown(object sender, MouseEventArgs e)
        {                        
            if (e.Button == MouseButtons.Left)
            {
                selectionStart  = PointF.Empty;                

                Focus();

                if ((ModifierKeys & Keys.Shift) != Keys.Shift)
                {
                    graph.Nodes.ForEach(x => x.IsSelected = false);
                }

                var node =
                    graph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                        x => new RectangleF(new PointF(x.X, x.Y), x.GetHeaderSize()).Contains(e.Location));

                if (!mdown && !IsRunMode)
                {
                    var nodeWhole =
                    graph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                        x => new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()).Contains(e.Location));
                    if (nodeWhole != null)
                    {
                        node = nodeWhole;
                        var socket = nodeWhole.GetSockets().All.FirstOrDefault(x => x.GetBounds().Contains(e.Location));
                        if (socket != null)
                        {
                            if ((ModifierKeys & Keys.Control) == Keys.Control)
                            {
                                var connection =
                                    graph.Connections.FirstOrDefault(
                                        x => x.InputNode == nodeWhole && x.InputSocketName == socket.Name);

                                if (connection != null)
                                {
                                    dragSocket = connection.OutputSocket;
                                    dragSocketNode = connection.OutputNode;
                                }
                                else
                                {
                                    connection =
                                        graph.Connections.FirstOrDefault(
                                            x => x.OutputNode == nodeWhole && x.OutputSocketName == socket.Name);

                                    if (connection != null)
                                    {
                                        dragSocket = connection.InputSocket;
                                        dragSocketNode = connection.InputNode;
                                    }
                                }

                                graph.Connections.Remove(connection);
                            }
                            else
                            {
                                dragSocket = socket;
                                dragSocketNode = nodeWhole;
                            }
                            dragConnectionBegin = e.Location;
                            dragConnectionEnd = e.Location;
                            mdown = true;
                            lastmpos = PointToScreen(e.Location);
                        }
                    }
                    else
                    {
                        selectionStart = selectionEnd = e.Location;
                    }
                }

                if (node != null && !mdown && dragSocket == null)
                {

                    node.IsSelected = true;

                    node.Order = graph.Nodes.Min(x => x.Order) - 1;
                    if (node.CustomEditor != null)
                    {
                        node.CustomEditor.BringToFront();
                    }
                    mdown = true;
                    lastmpos = PointToScreen(e.Location);

                    Refresh();
                }

                if (node != null)
                {
                    OnNodeSelected(node);
                }
            }

            needRepaint = true;
        }

        private bool IsConnectable(SocketVisual a, SocketVisual b)
        {
            var input = a.Input ? a : b;
            var output = a.Input ? b : a;
            var otype = Type.GetType(output.Type.FullName.Replace("&", ""), AssemblyResolver, TypeResolver);
            var itype = Type.GetType(input.Type.FullName.Replace("&", ""), AssemblyResolver, TypeResolver);
            if (otype == null || itype == null) return false;
            var allow = otype == typeof(Bang) || otype == itype || otype.IsSubclassOf(itype);
            return allow;
        }

        private Type TypeResolver(Assembly assembly, string name, bool inh)
        {
            if (assembly == null) assembly = ResolveAssembly(name);
            if (assembly == null) return null;
            return assembly.GetType(name);
        }

        private Assembly ResolveAssembly(string fullTypeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(x => x.GetTypes().Any(o => o.FullName == fullTypeName));
        }

        private Assembly AssemblyResolver(AssemblyName assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == assemblyName.FullName);
        }

        private void NodesControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (selectionStart != PointF.Empty)
            {
                var rect = MakeRect(selectionStart, selectionEnd);
                graph.Nodes.ForEach(
                    x => x.IsSelected = rect.Contains(new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds())));
                selectionStart = PointF.Empty;
            }

            if (dragSocket != null)
            {
                var nodeWhole =
                    graph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                        x => new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()).Contains(e.Location));
                if (nodeWhole != null)
                {
                    var socket = nodeWhole.GetSockets().All.FirstOrDefault(x => x.GetBounds().Contains(e.Location));
                    if (socket != null)
                    {
                        if (IsConnectable(dragSocket,socket) && dragSocket.Input != socket.Input)
                        {                                                        
                            var nc = new NodeConnection();
                            if (!dragSocket.Input)
                            {
                                nc.OutputNode = dragSocketNode;
                                nc.OutputSocketName = dragSocket.Name;
                                nc.InputNode = nodeWhole;
                                nc.InputSocketName = socket.Name;
                            }
                            else
                            {
                                nc.InputNode = dragSocketNode;
                                nc.InputSocketName = dragSocket.Name;
                                nc.OutputNode = nodeWhole;
                                nc.OutputSocketName = socket.Name;
                            }

                            graph.Connections.Add(nc);
                        }
                    }
                }
            }
           
            dragSocket = null;
            mdown = false;
            needRepaint = true;
        }

        private void AddToMenu(ToolStripItemCollection items, NodeToken token, string path, EventHandler click)
        {
            var pathParts = path.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
            var first = pathParts.FirstOrDefault();
            ToolStripMenuItem item = null;
            if (!items.ContainsKey(first))
            {
                item = new ToolStripMenuItem(first);
                item.Name = first;                
                item.Tag = token;
                items.Add(item);
            }
            else
            {
                item = items[first] as ToolStripMenuItem;
            }
            var next = string.Join("/", pathParts.Skip(1));
            if (!string.IsNullOrEmpty(next))
            {
                item.MouseEnter += (sender, args) => OnNodeHint("");
                AddToMenu(item.DropDownItems, token, next, click);
            }
            else
            {
                item.Click += click;
                item.Click += (sender, args) =>
                {
                    var i = allContextItems.Keys.FirstOrDefault(x => x.Name == item.Name);
                    allContextItems[i]++;
                };
                item.MouseEnter += (sender, args) => OnNodeHint(token.Attribute.Description ?? "");
                if (!allContextItems.Keys.Any(x => x.Name == item.Name))
                {
                    allContextItems.Add(item, 0);
                }
            }
        }

        public MethodInfo DummyMethod() => MethodInfo.GetCurrentMethod() as MethodInfo;

        private void NodesControl_DoubleMouseClick(object sender, MouseEventArgs e)
        {
            if (IsRunMode)
            {
                return;
            }

            var nv = new NodeVisual();
            nv.X = lastMouseLocation.X;
            nv.Y = lastMouseLocation.Y;
            nv.MethodInf = this.DummyMethod();
            nv.IsInteractive = false;
            nv.Name = NodeVisual.NewName;
            nv.Order = graph.Nodes.Count;
            nv.XmlExportName = "";
            nv.CustomWidth = -1;
            nv.CustomHeight = -1;

            TextBox tb = new TextBox();
            tb.Width = (int)NodeVisual.NodeWidth - 2;
            tb.Height = (int)NodeVisual.HeaderHeight - 2;
            tb.KeyPress += (s, ee) =>
            {
                if (ee.KeyChar == (char)Keys.Enter)
                {
                    SwapNode();
                }
            };

            nv.CustomEditor = tb;
            nv.CustomEditor.Tag = (nv, this.context);
            Controls.Add(nv.CustomEditor);
            nv.LayoutEditor();

            AutocompleteMenuNS.AutocompleteMenu autocompleteMenu = new AutocompleteMenuNS.AutocompleteMenu();
            autocompleteMenu.Items = Context.GetType().GetMethods().Where(x => x.GetCustomAttributes(typeof(NodeAttribute), false).Any()).Select(x => x.Name.ToLowerInvariant()).ToArray();
            autocompleteMenu.SetAutocompleteMenu(tb, autocompleteMenu);
            autocompleteMenu.Selected += (_, eee) => SwapNode();

            graph.Nodes.Add(nv);
            Refresh();
            needRepaint = true;

            void SwapNode()
            {
                var methods = Context.GetType().GetMethods();
                MethodInfo info = methods.SingleOrDefault(x => x.Name.Equals(tb.Text, StringComparison.InvariantCultureIgnoreCase));
                if (info is null)
                {
                    return;
                }

                NodeAttribute attrib = info.GetCustomAttributes(typeof(NodeAttribute), false)
                    .Cast<NodeAttribute>()
                    .FirstOrDefault();
                if (attrib is null)
                {
                    return;
                }

                var nv2 = new NodeVisual();
                nv2.X = nv.X;
                nv2.Y = nv.Y;
                nv2.MethodInf = info;
                nv2.IsInteractive = attrib.IsInteractive;
                nv2.Name = attrib.Name;
                nv2.Order = graph.Nodes.Count;
                nv2.XmlExportName = attrib.XmlExportName;
                nv2.CustomWidth = attrib.Width;
                nv2.CustomHeight = attrib.Height;

                Controls.Remove(tb);
                graph.Nodes.Remove(nv);

                if (attrib.CustomEditor != null)
                {
                    Control ctrl = null;
                    nv2.CustomEditor = ctrl = Activator.CreateInstance(attrib.CustomEditor) as Control;
                    if (ctrl != null)
                    {
                        ctrl.Tag = (nv2, this.context);
                        Controls.Add(ctrl);
                    }
                    nv2.LayoutEditor();
                }

                graph.Nodes.Add(nv2);

                Refresh();
                needRepaint = true;
            }
        }

        private void NodesControl_MouseClick(object sender, MouseEventArgs e)
        {
            lastMouseLocation = e.Location;

            if (Context == null) return;

            if (e.Button == MouseButtons.Right)
            {
                var methods = Context.GetType().GetMethods();
                var nodes =
                    methods.Select(
                        x =>
                            new
                                NodeToken()
                            {
                                Method = x,
                                Attribute =
                                    x.GetCustomAttributes(typeof (NodeAttribute), false)
                                        .Cast<NodeAttribute>()
                                        .FirstOrDefault()
                            }).Where(x => x.Attribute != null);

                var context = new ContextMenuStrip();
                if (graph.Nodes.Exists(x=>x.IsSelected))
                {
                    context.Items.Add("Delete Node(s)", null, ((o, args) =>
                    {
                        DeleteSelectedNodes();
                    }));
                    context.Items.Add("Duplicate Node(s)", null, ((o, args) =>
                    {
                        DuplicateSelectedNodes();
                    }));
                    context.Items.Add("Change Color ...", null, ((o, args) =>
                    {
                        ChangeSelectedNodesColor();
                    }));
                    if(graph.Nodes.Count(x=>x.IsSelected)==2)
                    {
                        var sel = graph.Nodes.Where(x => x.IsSelected).ToArray();
                        context.Items.Add("Check Impact", null, ((o,args)=>
                        {
                            if(HasImpact(sel[0],sel[1]) || HasImpact(sel[1],sel[0]))
                            {
                                MessageBox.Show("One node has impact on other.", "Impact detected.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show("These nodes not impacts themselves.", "No impact.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }));                       
                    }
                    context.Items.Add(new ToolStripSeparator());
                }
                if (allContextItems.Values.Any(x => x > 0))
                {
                    var handy = allContextItems.Where(x => x.Value > 0 && !string.IsNullOrEmpty(((x.Key.Tag) as NodeToken).Attribute.Menu)).OrderByDescending(x => x.Value).Take(8);
                    foreach (var kv in handy)
                    {
                        context.Items.Add(kv.Key);
                    }
                    context.Items.Add(new ToolStripSeparator());
                }
                foreach (var node in nodes.OrderBy(x=>x.Attribute.Path))
                {
                    AddToMenu(context.Items, node, node.Attribute.Path, (s,ev) =>
                    {
                        var tag = (s as ToolStripMenuItem).Tag as NodeToken;

                        var nv = new NodeVisual();
                        nv.X = lastMouseLocation.X;
                        nv.Y = lastMouseLocation.Y;
                        nv.MethodInf = node.Method;
                        nv.IsInteractive = node.Attribute.IsInteractive;
                        nv.Name = node.Attribute.Name;
                        nv.Order = graph.Nodes.Count;
                        nv.XmlExportName = node.Attribute.XmlExportName;
                        nv.CustomWidth = node.Attribute.Width;
                        nv.CustomHeight = node.Attribute.Height;

                        if (node.Attribute.CustomEditor != null)
                        {
                            Control ctrl = null;
                            nv.CustomEditor = ctrl = Activator.CreateInstance(node.Attribute.CustomEditor) as Control;
                            if (ctrl != null)
                            {
                                ctrl.Tag = (nv, this.context);                                
                                Controls.Add(ctrl);                                                               
                            }
                            nv.LayoutEditor();
                        }

                        graph.Nodes.Add(nv);
                        Refresh();
                        needRepaint = true;
                    });                    
                }
                context.Show(MousePosition);
            }
        }

        private void ChangeSelectedNodesColor()
        {
            ColorDialog cd = new ColorDialog();
            cd.FullOpen = true;
            if (cd.ShowDialog() == DialogResult.OK)
            {
                foreach (var n in graph.Nodes.Where(x => x.IsSelected))
                {
                    n.NodeColor = cd.Color;
                }
            }
            Refresh();
            needRepaint = true;
        }

        private void DuplicateSelectedNodes()
        {
            var cloned = new List<NodeVisual>();
            foreach (var n in graph.Nodes.Where(x => x.IsSelected))
            {
                int count = graph.Nodes.Count(x => x.IsSelected);
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                SerializeNode(bw, n);
                ms.Seek(0, SeekOrigin.Begin);
                var br = new BinaryReader(ms);
                var clone = DeserializeNode(br);
                clone.X += 40;
                clone.Y += 40;
                clone.GUID = Guid.NewGuid().ToString();
                cloned.Add(clone);
                br.Dispose();
                bw.Dispose();
                ms.Dispose();
            }
            graph.Nodes.ForEach(x => x.IsSelected = false);
            cloned.ForEach(x => x.IsSelected = true);
            cloned.Where(x => x.CustomEditor != null).ToList().ForEach(x => x.CustomEditor.BringToFront());
            graph.Nodes.AddRange(cloned);
            Invalidate();
        }

        private void DeleteSelectedNodes()
        {
            if (graph.Nodes.Exists(x => x.IsSelected))
            {
                foreach (var n in graph.Nodes.Where(x => x.IsSelected))
                {
                    Controls.Remove(n.CustomEditor);
                    graph.Connections.RemoveAll(
                        x => x.OutputNode == n || x.InputNode == n);
                }
                graph.Nodes.RemoveAll(x => graph.Nodes.Where(n => n.IsSelected).Contains(x));
            }
            Invalidate();
        }

        public void ToggleRunMode()
        {
            this.IsRunMode = !this.IsRunMode;
            this.Cursor = this.IsRunMode ? Cursors.Hand : Cursors.Default;
            this.context.OnRunModeToggled(this.IsRunMode);
        }

        /// <summary>
        /// Executes whole node graph (when called parameterless) or given node when specified.
        /// </summary>
        /// <param name="node"></param>
        public void Execute(NodeVisual node)
        {            
            var nodeQueue = new Queue<NodeVisual>();
            nodeQueue.Enqueue(node);

            while (nodeQueue.Count > 0)
            {
                //Refresh();
                if (breakExecution)
                {
                    breakExecution = false;
                    return;
                }

                NodeVisual init = nodeQueue.Dequeue();
                if (init != null)
                {
                    init.Feedback = FeedbackType.Debug;

                    // TODO: reset outputs/values
                    init.Execute(Context);

                    foreach (var connection in graph.Connections)
                    {
                        if (connection.OutputNode != init || connection.OutputSocket.Value is Bang)
                        {
                            continue;
                        }

                        connection.InputSocket.Value = connection.OutputSocket.Value;
                    }

                    foreach (var connection in graph.Connections.Where(x => x.OutputNode == init && x.OutputSocket.Value != null))
                    {
                        if (connection.InputSocket.HotInput)
                        {
                            nodeQueue.Enqueue(connection.InputNode);
                        }
                    }
                }
            }
        }

        public List<NodeVisual> GetNodes(params string[] nodeNames)
        {
            var nodes = graph.Nodes.Where(x => nodeNames.Contains(x.Name));
            return nodes.ToList();
        }

        public bool HasImpact(NodeVisual startNode, NodeVisual endNode)
        {
            var connections = graph.Connections.Where(x => x.OutputNode == startNode);
            foreach (var connection in connections)
            {
                if(connection.InputNode == endNode)
                {
                    return true;
                }
                bool nextImpact = HasImpact(connection.InputNode, endNode);
                if(nextImpact)
                {
                    return true;
                }
            }

            return false;
        }

        public string ExportToXml()
        {
            var xml = new XmlDocument();

            XmlElement el = (XmlElement)xml.AppendChild(xml.CreateElement("NodeGrap"));
            el.SetAttribute("Created", DateTime.Now.ToString());
            var nodes = el.AppendChild(xml.CreateElement("Nodes"));
            foreach (var node in graph.Nodes)
            {
                var xmlNode = (XmlElement)nodes.AppendChild(xml.CreateElement("Node"));
                xmlNode.SetAttribute("Name", node.XmlExportName);
                xmlNode.SetAttribute("Id", node.GetGuid());
            }
            var connections = el.AppendChild(xml.CreateElement("Connections"));
            foreach (var conn in graph.Connections)
            {
                var xmlConn = (XmlElement)nodes.AppendChild(xml.CreateElement("Connection"));
                xmlConn.SetAttribute("OutputNodeId", conn.OutputNode.GetGuid());
                xmlConn.SetAttribute("OutputNodeSocket", conn.OutputSocketName);
                xmlConn.SetAttribute("InputNodeId", conn.InputNode.GetGuid());
                xmlConn.SetAttribute("InputNodeSocket", conn.InputSocketName);
            }
            StringBuilder sb = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace
            };
            using (XmlWriter writer = XmlWriter.Create(sb, settings))
            {
                xml.Save(writer);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Serializes current node graph to binary data.
        /// </summary>        
        public byte[] Serialize()
        {
            using (var bw = new BinaryWriter(new MemoryStream()))
            {
                bw.Write("NodeSystemP"); //recognization string
                bw.Write(1000); //version
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
                return (bw.BaseStream as MemoryStream).ToArray();
            }
        }
        
        private static void SerializeNode(BinaryWriter bw, NodeVisual node)
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
            bw.Write(8); //additional data size per node
            bw.Write(node.Int32Tag);
            bw.Write(node.NodeColor.ToArgb());
        }

        /// <summary>
        /// Restores node graph state from previously serialized binary data.
        /// </summary>
        /// <param name="data"></param>
        public void Deserialize(byte[] data)
        {
            using (var br = new BinaryReader(new MemoryStream(data)))
            {
                var ident = br.ReadString();
                if (ident != "NodeSystemP") return;
                graph.Connections.Clear();
                graph.Nodes.Clear();
                Controls.Clear();

                var version = br.ReadInt32();
                int nodeCount = br.ReadInt32();
                for (int i = 0; i < nodeCount; i++)
                {
                    var nv = DeserializeNode(br);
                    if (nv != null)
                    {
                        graph.Nodes.Add(nv);
                    }
                }
                var connectionsCount = br.ReadInt32();
                for (int i = 0; i < connectionsCount; i++)
                {
                    var con = new NodeConnection();
                    var og = br.ReadString();
                    con.OutputNode = graph.Nodes.FirstOrDefault(x => x.GUID == og);
                    con.OutputSocketName = br.ReadString();
                    var ig = br.ReadString();
                    con.InputNode = graph.Nodes.FirstOrDefault(x => x.GUID == ig);
                    con.InputSocketName = br.ReadString();
                    br.ReadBytes(br.ReadInt32()); //read additional data

                    graph.Connections.Add(con);
                }
                br.ReadBytes(br.ReadInt32()); //read additional data
            }
            Refresh();
        }

        private NodeVisual DeserializeNode(BinaryReader br)
        {
            var nv = new NodeVisual();
            nv.GUID = br.ReadString();
            nv.X = br.ReadSingle();
            nv.Y = br.ReadSingle();
            nv.IsInteractive = br.ReadBoolean();
            nv.Name = br.ReadString();
            nv.Order = br.ReadInt32();
            var customEditorAssembly = br.ReadString();
            var customEditor = br.ReadString();
            nv.MethodInf = Context.GetType().GetMethod(br.ReadString());

            if (nv.MethodInf is null)
            {
                br.ReadBytes(br.ReadInt32());
                var additional2 = br.ReadInt32(); //read additional data
                if (additional2 >= 4)
                {
                    nv.Int32Tag = br.ReadInt32();
                    if (additional2 >= 8)
                    {
                        nv.NodeColor = Color.FromArgb(br.ReadInt32());
                    }
                }
                if (additional2 > 8)
                {
                    br.ReadBytes(additional2 - 8);
                }

                return null;
            }

            var attribute = nv.MethodInf.GetCustomAttributes(typeof(NodeAttribute), false)
                                        .Cast<NodeAttribute>()
                                        .FirstOrDefault();
            if(attribute!=null)
            {
                nv.CustomWidth = attribute.Width;
                nv.CustomHeight = attribute.Height;
            }

            var additional = br.ReadInt32(); //read additional data
            if (additional >= 4)
            {
                nv.Int32Tag = br.ReadInt32();
                if(additional >= 8)
                {
                    nv.NodeColor = Color.FromArgb(br.ReadInt32());
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
                    nv.CustomEditor = new Label();
                }
                else if (customEditor == "System.Windows.Forms.TextBox")
                {
                    nv.CustomEditor = new TextBox();
                }
                else
                {
                    nv.CustomEditor =
                        Activator.CreateInstance(AppDomain.CurrentDomain, customEditorAssembly, customEditor).Unwrap() as Control;
                }

                Control ctrl = nv.CustomEditor;
                if (ctrl != null)
                {
                    ctrl.Tag = (nv, this.context);
                    Controls.Add(ctrl);
                }
                nv.LayoutEditor();
            }
            return nv;
        }

        /// <summary>
        /// Clears node graph state.
        /// </summary>
        public void Clear()
        {
            graph.Nodes.Clear();
            graph.Connections.Clear();
            Controls.Clear();
            Refresh();
        }
    }

    public class Bang
    {
        public static Bang Instance = new Bang();
    }
}
