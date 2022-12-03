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

namespace NodeEditor
{
    /// <summary>
    /// Main control of Node Editor Winforms
    /// </summary>
    public partial class NodesControl : UserControl
    {
        public NodesGraph MainGraph { get; set; } = new NodesGraph();
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

            MainGraph.Draw(e.Graphics, PointToClient(MousePosition), MouseButtons);            

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
                foreach (var node in MainGraph.Nodes.Where(x => x.IsSelected))
                {
                    node.BoundaryCurve.Translate(new Microsoft.Msagl.Core.Geometry.Point(em.X - lastmpos.X, em.Y - lastmpos.Y));

                    node.LayoutEditor();
                }

                if (MainGraph.Nodes.Exists(x => x.IsSelected))
                {
                    var n = MainGraph.Nodes.FirstOrDefault(x => x.IsSelected);
                    var bound = new RectangleF(new PointF(n.X,n.Y), n.GetNodeBounds());
                    foreach (var node in MainGraph.Nodes.Where(x=>x.IsSelected))
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
                    MainGraph.Nodes.ForEach(x => x.IsSelected = false);
                }

                var node =
                    MainGraph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                        x => new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds()).Contains(e.Location));

                if (!mdown && !IsRunMode)
                {
                    var nodeWhole =
                    MainGraph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
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
                                    MainGraph.Connections.FirstOrDefault(
                                        x => x.InputNode == nodeWhole && x.InputSocketName == socket.Name);

                                if (connection != null)
                                {
                                    dragSocket = connection.OutputSocket;
                                    dragSocketNode = connection.OutputNode;
                                }
                                else
                                {
                                    connection =
                                        MainGraph.Connections.FirstOrDefault(
                                            x => x.OutputNode == nodeWhole && x.OutputSocketName == socket.Name);

                                    if (connection != null)
                                    {
                                        dragSocket = connection.InputSocket;
                                        dragSocketNode = connection.InputNode;
                                    }
                                }

                                MainGraph.Connections.Remove(connection);
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

                    node.Order = MainGraph.Nodes.Min(x => x.Order) - 1;
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
                MainGraph.Nodes.ForEach(
                    x => x.IsSelected = rect.Contains(new RectangleF(new PointF(x.X, x.Y), x.GetNodeBounds())));
                selectionStart = PointF.Empty;
            }

            if (dragSocket != null)
            {
                var nodeWhole =
                    MainGraph.Nodes.OrderBy(x => x.Order).FirstOrDefault(
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

                            MainGraph.Connections.Add(nc);
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
            NodeVisual selectedNode = MainGraph.Nodes.FirstOrDefault(x => x.IsSelected);
            if (selectedNode?.SubsystemGraph != null)
            {
                this.OnSubgraphOpenRequest?.Invoke(selectedNode);
                return;
            }

            if (IsRunMode || selectedNode != null)
            {
                return;
            }

            var newAutocompleteNode = new NodeVisual(NodeVisual.NewSpecialNodeName, lastMouseLocation.X, lastMouseLocation.Y - NodeVisual.HeaderHeight);
            newAutocompleteNode.IsInteractive = false;
            newAutocompleteNode.Order = MainGraph.Nodes.Count;
            newAutocompleteNode.CustomWidth = -1;
            newAutocompleteNode.CustomHeight = -1;
            newAutocompleteNode.OwnerGraph = MainGraph;

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

                    replacementNode = new NodeVisual(NodeVisual.NewSubsystemNodeNamePrefix + " " + name, newAutocompleteNode.X, newAutocompleteNode.Y);
                    replacementNode.Order = MainGraph.Nodes.Count;
                    replacementNode.SubsystemGraph = new NodesGraph() { GUID = name };
                    replacementNode.SubsystemGraph.OwnerNode = replacementNode;
                    replacementNode.OwnerGraph = MainGraph;
                }
                else if (tb.Text.StartsWith(NodeVisual.NewSubsystemInletNodeNamePrefix))
                {
                    string name = tb.Text.Substring(NodeVisual.NewSubsystemInletNodeNamePrefix.Length).Trim();
                    if (name == "" || this.MainGraph.Nodes.Any(x => x.Name == NodeVisual.NewSubsystemInletNodeNamePrefix + " " + name))
                    {
                        return;
                    }

                    replacementNode = new NodeVisual(NodeVisual.NewSubsystemInletNodeNamePrefix + " " + name, newAutocompleteNode.X, newAutocompleteNode.Y);
                    replacementNode.Order = MainGraph.Nodes.Count;
                    replacementNode.OwnerGraph = MainGraph;
                }
                else if (tb.Text.StartsWith(NodeVisual.NewSubsystemOutletNodeNamePrefix))
                {
                    string name = tb.Text.Substring(NodeVisual.NewSubsystemOutletNodeNamePrefix.Length).Trim();
                    if (name == "" || this.MainGraph.Nodes.Any(x => x.Name == NodeVisual.NewSubsystemOutletNodeNamePrefix + " " + name))
                    {
                        return;
                    }

                    replacementNode = new NodeVisual(NodeVisual.NewSubsystemOutletNodeNamePrefix + " " + name, newAutocompleteNode.X, newAutocompleteNode.Y);
                    replacementNode.Order = MainGraph.Nodes.Count;
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

                    replacementNode = new NodeVisual(name, newAutocompleteNode.X, newAutocompleteNode.Y);
                    replacementNode.MethodInf = info;
                    replacementNode.IsInteractive = attrib.IsInteractive;
                    replacementNode.Order = MainGraph.Nodes.Count;
                    replacementNode.CustomWidth = attrib.Width;
                    replacementNode.CustomHeight = attrib.Height;
                    replacementNode.InvokeOnLoad = attrib.InvokeOnLoad;
                    replacementNode.OwnerGraph = MainGraph;

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
                var context = new ContextMenuStrip();
                if (MainGraph.Nodes.Exists(x=>x.IsSelected))
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
                    if(MainGraph.Nodes.Count(x=>x.IsSelected)==2)
                    {
                        var sel = MainGraph.Nodes.Where(x => x.IsSelected).ToArray();
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

                context.Show(MousePosition);
            }
        }

        private void ChangeSelectedNodesColor()
        {
            ColorDialog cd = new ColorDialog();
            cd.FullOpen = true;
            if (cd.ShowDialog() == DialogResult.OK)
            {
                foreach (var n in MainGraph.Nodes.Where(x => x.IsSelected))
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
            foreach (var n in MainGraph.Nodes.Where(x => x.IsSelected))
            {
                int count = MainGraph.Nodes.Count(x => x.IsSelected);
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                // TODO: this is broker for subsystems
                NodesGraph.SerializeNode(bw, n);
                ms.Seek(0, SeekOrigin.Begin);
                var br = new BinaryReader(ms);
                var clone = NodesGraph.DeserializeNode(br, this.Context).Item1;
                clone.BoundaryCurve.Translate(new Microsoft.Msagl.Core.Geometry.Point(40, 40));
                clone.GUID = Guid.NewGuid().ToString();
                cloned.Add(clone);
                br.Dispose();
                bw.Dispose();
                ms.Dispose();
            }

            MainGraph.Nodes.ForEach(x => x.IsSelected = false);
            cloned.ForEach(x => x.IsSelected = true);
            cloned.Where(x => x.CustomEditor != null).ToList().ForEach(x => x.CustomEditor.BringToFront());
            MainGraph.Nodes.AddRange(cloned);
            Invalidate();
        }

        private void DeleteSelectedNodes()
        {
            if (MainGraph.Nodes.Exists(x => x.IsSelected))
            {
                foreach (var n in MainGraph.Nodes.Where(x => x.IsSelected))
                {
                    Controls.Remove(n.CustomEditor);
                    MainGraph.Connections.RemoveAll(
                        x => x.OutputNode == n || x.InputNode == n);
                }
                MainGraph.Nodes.RemoveAll(x => MainGraph.Nodes.Where(n => n.IsSelected).Contains(x));
            }
            Invalidate();
        }

        public void ToggleRunMode()
        {
            this.IsRunMode = !this.IsRunMode;
            this.Cursor = this.IsRunMode ? Cursors.Default : Cursors.Hand;

            Stack<NodeVisual> nodeQueue = new Stack<NodeVisual>();
            foreach(NodeVisual node in MainGraph.Nodes.Reverse<NodeVisual>())
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

                // TODO: reset outputs/values
                init.Execute(Context);

                foreach (var connection in MainGraph.Connections)
                {
                    if (connection.OutputNode != init || (connection.OutputSocket.Value is Bang && connection.InputSocket.Type != typeof(Bang)))
                    {
                        continue;
                    }

                    connection.InputSocket.Value = connection.OutputSocket.Value;
                }

                foreach (var connection in MainGraph.Connections.Where(x => x.OutputNode == init && x.OutputSocket.Value != null))
                {
                    if (connection.InputSocket.HotInput)
                    {
                        if (connection.InputNode.Type == NodeVisual.NodeType.Subsystem)
                        {
                            NodeVisual inlet = connection.InputNode.SubsystemGraph.Nodes.Single(x => x.Name == connection.InputSocketName);
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
            }
        }

        public bool HasImpact(NodeVisual startNode, NodeVisual endNode)
        {
            var connections = MainGraph.Connections.Where(x => x.OutputNode == startNode);
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
