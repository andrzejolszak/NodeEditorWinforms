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
using System.Data;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Linq;
using AnimateForms.Core;
using Microsoft.Msagl.Core.Layout;
using Timer = System.Windows.Forms.Timer;
using System.Configuration;
using System.Net.Sockets;
using System.Windows.Forms.VisualStyles;

namespace NodeEditor
{
    /// <summary>
    /// Main control of Node Editor Winforms
    /// </summary>
    public partial class NodesControl : UserControl
    {
        public NodesGraph MainGraph { get; set; } = new NodesGraph();
        public readonly Animate Animate = new Animate();
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

        public bool EdgeRoutingEnabled { get; private set; }

        /// <summary>
        /// Occurs when user selects a node. In the object will be passed node settings for unplugged inputs/outputs.
        /// </summary>
        public event Action<NodeVisual> OnNodeSelected = delegate { };

        /// <summary>
        /// Occurs when node would to share its description.
        /// </summary>
        public event Action<string> OnNodeHint = delegate { };

        public event Action<NodeVisual> OnSubgraphOpenRequest = delegate { };

        /// <summary>
        /// Indicates which part of control should be actually visible. It is useful when dragging nodes out of autoscroll parent control,
        /// to guarantee that moving node/connection is visible to user.
        /// </summary>
        public event Action<RectangleF> OnShowLocation = delegate { };

        private Point lastMouseLocation;

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

            this.Cursor = this.IsRunMode ? Cursors.Default : Cursors.Hand;
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
                DeleteSelectedNodes(null);
                DeleteHoveredConns();
            }
        }

        private void TimerOnTick(object sender, EventArgs eventArgs)
        {
            if (DesignMode) return;

            if (needRepaint || this.Animate.AnimationUpdated)
            {
                Invalidate();
            }
        }

        private void NodesControl_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;

            if (this.EdgeRoutingEnabled)
            {
                MainGraph.RouteEdges();
            }

            MainGraph.Draw(e.Graphics, PointToClient(MousePosition), MouseButtons, this.IsRunMode, this.Animate);

            if (dragSocket != null)
            {
                var pen = new Pen(Color.Black, 2);
                NodeConnection.DrawConnection(e.Graphics, pen, dragConnectionBegin, dragConnectionEnd);
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
                foreach (var node in MainGraph.NodesTyped.Where(x => x.IsSelected))
                {
                    node.BoundaryCurve.Translate(new Microsoft.Msagl.Core.Geometry.Point(em.X - lastmpos.X, em.Y - lastmpos.Y));

                    node.LayoutEditor();
                }

                if (MainGraph.NodesTyped.Any(x => x.IsSelected))
                {
                    var n = MainGraph.NodesTyped.FirstOrDefault(x => x.IsSelected);
                    var bound = new RectangleF(new PointF(n.X,n.Y), n.GetNodeBounds());
                    foreach (var node in MainGraph.NodesTyped.Where(x=>x.IsSelected))
                    {
                        bound = RectangleF.Union(bound, new RectangleF(new PointF(node.X, node.Y), node.GetNodeBounds()));
                    }

                    OnShowLocation(bound);
                }

                Invalidate();
                
                if (dragSocket != null)
                {
                    var center = new PointF((float)dragSocket.Location.X + dragSocket.Width/2, (float)dragSocket.Location.Y + dragSocket.Height/2);
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
                    foreach(NodeVisual n in MainGraph.Nodes)
                    {
                        n.IsSelected = false;
                    }
                }

                var node =
                    MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                        x => new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()).Contains(e.Location));

                if (!mdown && !IsRunMode)
                {
                    var nodeWhole =
                    MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
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
                                    MainGraph.EdgesTyped.FirstOrDefault(
                                        x => x.InputNode == nodeWhole && x.InputSocketName == socket.Name);

                                if (connection != null)
                                {
                                    dragSocket = connection.OutputSocket;
                                    dragSocketNode = connection.OutputNode;
                                }
                                else
                                {
                                    connection =
                                        MainGraph.EdgesTyped.FirstOrDefault(
                                            x => x.OutputNode == nodeWhole && x.OutputSocketName == socket.Name);

                                    if (connection != null)
                                    {
                                        dragSocket = connection.InputSocket;
                                        dragSocketNode = connection.InputNode;
                                    }
                                }

                                MainGraph.RemoveEdge(connection);
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
            var allow = otype == typeof(Bang) || itype == typeof(Bang) || otype == itype || otype.IsSubclassOf(itype);
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
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetTypes().Any(o => o.FullName == fullTypeName));
        }

        private Assembly AssemblyResolver(AssemblyName assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == assemblyName.FullName);
        }

        private void NodesControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (!IsRunMode
                && this.MainGraph.NodesTyped.Count(x => x.IsSelected) == 1
                && this.MainGraph.EdgesTyped.Count(x => x.IsHover) == 1)
            {
                NodeConnection hoverConn = this.MainGraph.EdgesTyped.First(x => x.IsHover);
                NodeVisual node = MainGraph.NodesTyped.First(x => x.IsSelected);
                var s = node.GetSockets();
                if (s.Inputs.Count > 0 && s.Outputs.Count > 0 && !MainGraph.EdgesTyped.Any(x => x.InputNode == node || x.OutputNode == node))
                {
                    if (IsConnectable(hoverConn.OutputSocket, s.Inputs.First())
                        && IsConnectable(hoverConn.InputSocket, s.Outputs.First()))
                    {
                        MainGraph.AddEdge(new NodeConnection(hoverConn.OutputNode, hoverConn.OutputSocketName, node, s.Inputs.First().Name));
                        MainGraph.AddEdge(new NodeConnection(node, s.Outputs.First().Name, hoverConn.InputNode, hoverConn.InputSocketName));
                        MainGraph.RemoveEdge(hoverConn);
                    }
                }
            }

            if (selectionStart != PointF.Empty)
            {
                var rect = MakeRect(selectionStart, selectionEnd);
                foreach (NodeVisual x in MainGraph.Nodes)
                {
                    x.IsSelected = rect.Contains(new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()));
                }

                selectionStart = PointF.Empty;
            }

            if (dragSocket != null)
            {
                var nodeWhole =
                    MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                        x => new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()).Contains(e.Location));
                if (nodeWhole != null)
                {
                    var socket = nodeWhole.GetSockets().All.FirstOrDefault(x => x.GetBounds().Contains(e.Location));
                    if (socket != null)
                    {
                        if (IsConnectable(dragSocket,socket) && dragSocket.Input != socket.Input)
                        {                                                        
                            NodeConnection nc = null;
                            if (!dragSocket.Input)
                            {
                                nc = new NodeConnection(dragSocketNode, dragSocket.Name, nodeWhole, socket.Name);
                            }
                            else
                            {
                                nc = new NodeConnection(nodeWhole, socket.Name, dragSocketNode, dragSocket.Name);
                            }

                            MainGraph.AddEdge(nc);
                        }
                    }
                }
            }
           
            dragSocket = null;
            mdown = false;
            needRepaint = true;
        }

        private void NodesControl_DoubleMouseClick(object sender, MouseEventArgs e)
        {
            NodeVisual selectedNode = MainGraph.NodesTyped.FirstOrDefault(x => x.IsSelected);
            if (selectedNode?.SubsystemGraph != null)
            {
                this.OnSubgraphOpenRequest?.Invoke(selectedNode);
                return;
            }

            if (IsRunMode || selectedNode != null)
            {
                return;
            }

            var newAutocompleteNode = new NodeVisual(NodeVisual.NewSpecialNodeName, lastMouseLocation.X, lastMouseLocation.Y - NodeVisual.HeaderHeight)
            {
                IsInteractive = false,
                CustomWidth = -1,
                CustomHeight = -1,
                OwnerGraph = MainGraph
            };

            TextBox tb = new TextBox();
            tb.Width = (int)NodeVisual.NodeWidth - 4;
            tb.Height = (int)NodeVisual.HeaderHeight - 4;
            tb.BackColor = newAutocompleteNode.NodeColor;
            tb.BorderStyle = BorderStyle.None;
            tb.KeyPress += (s, ee) =>
            {
                if (ee.KeyChar == (char)Keys.Enter)
                {
                    SwapNode();
                    ee.Handled = true;
                }
            };

            newAutocompleteNode.CustomEditor = tb;
            Controls.Add(newAutocompleteNode.CustomEditor);
            newAutocompleteNode.LayoutEditor();

            AutocompleteMenuNS.AutocompleteMenu autocompleteMenu = new AutocompleteMenuNS.AutocompleteMenu();
            autocompleteMenu.Items = Context.GetType().GetMethods().Where(x => x.GetCustomAttributes(typeof(NodeAttribute), false).Any()).Select(x => x.Name.ToLowerInvariant())
                .Concat(new[] { NodeVisual.NewSubsystemNodeNamePrefix, NodeVisual.NewSubsystemInletNodeNamePrefix, NodeVisual.NewSubsystemOutletNodeNamePrefix })
                .ToArray();
            autocompleteMenu.SetAutocompleteMenu(tb, autocompleteMenu);

            MainGraph.Nodes.Add(newAutocompleteNode);
            Refresh();
            needRepaint = true;
            tb.Focus();

            void SwapNode()
            {
                NodeVisual replacementNode = null;

                if (tb.Text.StartsWith(NodeVisual.NewSubsystemNodeNamePrefix))
                {
                    string name = tb.Text.Substring(NodeVisual.NewSubsystemNodeNamePrefix.Length).Trim();
                    if (name == "" || name == this.MainGraph.GUID)
                    {
                        return;
                    }

                    replacementNode = new NodeVisual(NodeVisual.NewSubsystemNodeNamePrefix + " " + name, newAutocompleteNode.X, newAutocompleteNode.Y)
                    {
                        SubsystemGraph = new NodesGraph() { GUID = name }
                    };
                    replacementNode.SubsystemGraph.OwnerNode = replacementNode;
                    replacementNode.OwnerGraph = MainGraph;
                }
                else if (tb.Text.StartsWith(NodeVisual.NewSubsystemInletNodeNamePrefix))
                {
                    string name = tb.Text.Substring(NodeVisual.NewSubsystemInletNodeNamePrefix.Length).Trim();
                    if (name == "" || this.MainGraph.NodesTyped.Any(x => x.Name == NodeVisual.NewSubsystemInletNodeNamePrefix + " " + name))
                    {
                        return;
                    }

                    replacementNode = new NodeVisual(NodeVisual.NewSubsystemInletNodeNamePrefix + " " + name, newAutocompleteNode.X, newAutocompleteNode.Y);
                    replacementNode.OwnerGraph = MainGraph;
                }
                else if (tb.Text.StartsWith(NodeVisual.NewSubsystemOutletNodeNamePrefix))
                {
                    string name = tb.Text.Substring(NodeVisual.NewSubsystemOutletNodeNamePrefix.Length).Trim();
                    if (name == "" || this.MainGraph.NodesTyped.Any(x => x.Name == NodeVisual.NewSubsystemOutletNodeNamePrefix + " " + name))
                    {
                        return;
                    }

                    replacementNode = new NodeVisual(NodeVisual.NewSubsystemOutletNodeNamePrefix + " " + name, newAutocompleteNode.X, newAutocompleteNode.Y);
                    replacementNode.OwnerGraph = MainGraph;
                }
                else
                {
                    string name = tb.Text.Trim();
                    var methods = Context.GetType().GetMethods();
                    MethodInfo info = methods.SingleOrDefault(x => x.Name.Equals(name.Split(' ')[0], StringComparison.InvariantCultureIgnoreCase));
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

                    replacementNode = new NodeVisual(name, newAutocompleteNode.X, newAutocompleteNode.Y)
                    {
                        MethodInf = info,
                        IsInteractive = attrib.IsInteractive,
                        CustomWidth = attrib.Width,
                        CustomHeight = attrib.Height,
                        InvokeOnLoad = attrib.InvokeOnLoad,
                        OwnerGraph = MainGraph
                    };

                    if (attrib.CustomEditor != null)
                    {
                        Control ctrl = null;
                        replacementNode.CustomEditor = ctrl = Activator.CreateInstance(attrib.CustomEditor) as Control;
                        if (ctrl != null)
                        {
                            ctrl.BackColor = newAutocompleteNode.NodeColor;
                            Controls.Add(ctrl);
                        }

                        replacementNode.LayoutEditor();
                    }
                }

                Controls.Remove(tb);
                MainGraph.Nodes.Remove(newAutocompleteNode);
                MainGraph.Nodes.Add(replacementNode);

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
                var node = MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                        x => new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()).Contains(e.Location));

                if (node != null || MainGraph.NodesTyped.Any(x => x.IsSelected))
                {
                    var context = new ContextMenuStrip();

                    context.Items.Add("Delete Node(s)", null, ((o, args) =>
                    {
                        DeleteSelectedNodes(node);
                    }));

                    context.Items.Add("Duplicate Node(s)", null, ((o, args) =>
                    {
                        DuplicateSelectedNodes(node);
                    }));

                    context.Items.Add("Change Color ...", null, ((o, args) =>
                    {
                        ChangeSelectedNodesColor(node);
                    }));

                    if (MainGraph.NodesTyped.Count(x => x.IsSelected) == 2)
                    {
                        var sel = MainGraph.NodesTyped.Where(x => x.IsSelected).ToArray();
                        context.Items.Add("Check Impact ???", null, ((o, args) =>
                        {
                            if (HasImpact(sel[0], sel[1]) || HasImpact(sel[1], sel[0]))
                            {
                                MessageBox.Show("One node has impact on other.", "Impact detected.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show("These nodes not impacts themselves.", "No impact.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }));
                    }

                    context.Show(MousePosition);
                }
                else if (MainGraph.EdgesTyped.Any(x => x.IsHover))
                {
                    var context = new ContextMenuStrip();

                    context.Items.Add("Delete Connection(s)", null, ((o, args) =>
                    {
                        DeleteHoveredConns();
                    }));

                    context.Show(MousePosition);
                }
            }
        }

        private void ChangeSelectedNodesColor(NodeVisual node)
        {
            NodeVisual[] selected = MainGraph.NodesTyped.Where(x => x.IsSelected).Concat(new[] { node }).Where(x => x != null).ToArray();

            ColorDialog cd = new ColorDialog();
            cd.FullOpen = true;
            if (cd.ShowDialog() == DialogResult.OK)
            {
                foreach (var n in selected)
                {
                    n.NodeColor = cd.Color;
                }
            }
            Refresh();
            needRepaint = true;
        }

        private void DuplicateSelectedNodes(NodeVisual node)
        {
            NodeVisual[] selected = MainGraph.NodesTyped.Where(x => x.IsSelected).Concat(new[] { node }).Where(x => x != null).ToArray();
            foreach (NodeVisual n in MainGraph.Nodes)
            {
                n.IsSelected = false;
            }

            foreach (var n in selected)
            {
                int count = selected.Length;
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                // TODO: this is broker for subsystems
                NodesGraph.SerializeNode(bw, n);
                ms.Seek(0, SeekOrigin.Begin);
                var br = new BinaryReader(ms);
                var clone = NodesGraph.DeserializeNode(br, this.Context).Item1;
                clone.BoundaryCurve.Translate(new Microsoft.Msagl.Core.Geometry.Point(40, 40));
                clone.GUID = Guid.NewGuid().ToString();
                clone.IsSelected = true;
                clone.CustomEditor?.BringToFront();
                MainGraph.Nodes.Add(clone);
                br.Dispose();
                bw.Dispose();
                ms.Dispose();
            }

            Invalidate();
        }

        private void DeleteSelectedNodes(NodeVisual node)
        {
            NodeVisual[] selected = MainGraph.NodesTyped.Where(x => x.IsSelected).Concat(new[] { node }).Where(x => x != null).ToArray();

            if (selected.Length > 0)
            {
                if (selected.Length == 1)
                {
                    NodeVisual sNode = selected[0];
                    if (MainGraph.EdgesTyped.Count(x => x.InputNode == sNode) == 1
                        && MainGraph.EdgesTyped.Count(x => x.OutputNode == sNode) == 1)
                    {
                        var s = sNode.GetSockets();
                        NodeConnection? inCon = MainGraph.EdgesTyped.FirstOrDefault(x => x.InputSocket == s.Inputs.First());
                        NodeConnection? outCon = MainGraph.EdgesTyped.FirstOrDefault(x => x.OutputSocket == s.Outputs.First());

                        if (inCon is not null
                            && outCon is not null 
                            && IsConnectable(inCon.OutputSocket, outCon.InputSocket))
                        {
                            MainGraph.AddEdge(new NodeConnection(inCon.OutputNode, inCon.OutputSocketName, outCon.InputNode, outCon.InputSocketName));
                        }
                    }
                }

                foreach (var n in selected)
                {
                    Controls.Remove(n.CustomEditor);
                    foreach(NodeConnection e in MainGraph.EdgesTyped.Where(x => x.OutputNode == n || x.InputNode == n).ToArray())
                    {
                        MainGraph.RemoveEdge(e);
                    }
                }

                MainGraph.Nodes = MainGraph.Nodes.Except(selected).ToList();
            }

            Invalidate();
        }

        private void DeleteHoveredConns()
        {
            foreach (NodeConnection e in MainGraph.EdgesTyped.Where(x => x.IsHover).ToArray())
            {
                MainGraph.RemoveEdge(e);
            }

            Invalidate();
        }

        public void ToggleEdgeRouting()
        {
            this.EdgeRoutingEnabled = !this.EdgeRoutingEnabled;
            if (!EdgeRoutingEnabled)
            {
                foreach (NodeConnection c in this.MainGraph.EdgesTyped)
                {
                    c.Curve = null;
                }
            }

            this.needRepaint = true;
        }

        public void ToggleRunMode()
        {
            this.IsRunMode = !this.IsRunMode;
            this.Cursor = this.IsRunMode ? Cursors.Default : Cursors.Hand;

            this.needRepaint = true;

            Stack<NodeVisual> nodeQueue = new Stack<NodeVisual>();
            foreach(NodeVisual node in MainGraph.NodesTyped.Reverse<NodeVisual>())
            {
                if (node.InvokeOnLoad)
                {
                    nodeQueue.Push(node);
                }
            }

            this.Execute(nodeQueue);
        }

        /// <summary>
        /// Executes whole node graph (when called parameterless) or given node when specified.
        /// </summary>
        /// <param name="node"></param>
        public void Execute(Stack<NodeVisual> queue)
        {
            var nodeQueue = queue;
            while (nodeQueue.Count > 0)
            {
                //Refresh();
                if (breakExecution)
                {
                    breakExecution = false;
                    return;
                }

                if (nodeQueue.Count == 0)
                {
                    return;
                }

                NodeVisual init = nodeQueue.Pop();
                init.Feedback = FeedbackType.Debug;

                try
                {
                    init.Execute(Context, this.Animate);
                }
                catch(Exception ex)
                {
                    init.Feedback = FeedbackType.Error;
                }

                foreach (var connection in MainGraph.EdgesTyped)
                {
                    if (connection.OutputNode != init)
                    {
                        continue;
                    }

                    connection.PropagateValue(Context, this.Animate);
                }

                foreach (var connection in MainGraph.EdgesTyped.Where(x => x.OutputNode == init && x.OutputSocket.Value != null))
                {
                    if (connection.InputSocket.HotInput)
                    {
                        if (connection.InputNode.Type == NodeVisual.NodeType.Subsystem)
                        {
                            NodeVisual inlet = connection.InputNode.SubsystemGraph.NodesTyped.Single(x => x.Name == connection.InputSocketName);
                            inlet.GetSockets().Outputs.Single().Value = connection.InputSocket.Value;
                            nodeQueue.Push(inlet);
                        }
                        else if (connection.InputNode.Type == NodeVisual.NodeType.Outlet)
                        {
                            SocketVisual subsystemOutput = connection.InputNode.OwnerGraph.OwnerNode.GetSockets().Outputs.Single(x => x.Name == connection.InputNode.Name);
                            subsystemOutput.Value = connection.InputSocket.Value;
                            nodeQueue.Push(connection.InputNode.OwnerGraph.OwnerNode);
                        }
                        else
                        {
                            nodeQueue.Push(connection.InputNode);
                        }
                    }
                }

                this.needRepaint = true;
            }
        }

        public bool HasImpact(NodeVisual startNode, NodeVisual endNode)
        {
            var connections = MainGraph.EdgesTyped.Where(x => x.OutputNode == startNode);
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
    }

    public class Bang
    {
        public static Bang Instance = new Bang();
    }
}
