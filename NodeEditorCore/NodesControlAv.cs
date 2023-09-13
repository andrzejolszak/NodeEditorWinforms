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

using AvaloniaEdit;
using Sharplog.KME;
using System.Data;
using System.Reflection;

namespace NodeEditor
{
    /// <summary>
    /// Main control of Node Editor Winforms
    /// </summary>
    public class NodesControlAv : UserControl
    {
        private System.Timers.Timer timer = new System.Timers.Timer();

        private PointerPoint _lastMouseState;

        private bool _skipMouseMovePostSwap = false;

        /// <summary>
        /// Context of the editor. You should set here an instance that implements INodesContext interface.
        /// In context you should define your nodes (methods decorated by Node attribute).
        /// </summary>
        public INodesContext Context
        {
            get { return context; }
            private set
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

        public NodesGraph MainGraph { get; private set; }

        public bool IsRunMode { get; private set; }

        public bool EdgeRoutingEnabled { get; private set; }

        public SocketVisual? DragStartSocket { get; private set; }

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
        public event Action<Rect> OnShowLocation = delegate { };

        private Point selectionStart;

        private Point selectionEnd;

        private INodesContext context;

        private bool breakExecution = false;

        public NodeVisual? Owner { get; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public NodesControlAv(NodeVisual? owner)
        {
            this.Owner = owner;
            this.Focusable = true;

            this.AttachedToVisualTree += (e, s) => this.Focus();
            this.PointerPressed += this.OnNodesControl_MousePressed;
            this.PointerMoved += this.OnNodesControl_MouseMove;
            this.PointerReleased += this.OnNodesControl_MouseUp;
            this.KeyDown += this.OnNodesControl_KeyDown;

            timer.Interval = 24;
            timer.Elapsed += TimerOnTick;
            timer.Start();

            this.Cursor = this.IsRunMode ? Cursor.Default : AvaloniaUtils.CursorHand.Value;

            this.OnNodeSelected += OnNodeContextSelected;

            this.Content = new Canvas();
        }

        private void ContextOnFeedbackInfo(string message, NodeVisual nodeVisual, FeedbackType type, object tag, bool breakExecution)
        {
            nodeVisual.Feedback = type;
            this.breakExecution = breakExecution;
            OnNodeHint(message);
        }

        public void TimerOnTick(object sender, EventArgs eventArgs)
        {
            Dispatcher.UIThread.Invoke(this.InvalidateVisual);
        }

        public override void Render(DrawingContext e)
        {
            if (this.MainGraph is null)
            {
                base.Render(e);
                return;
            }

            // TODO: Avalonia deferred render?: https://github.com/AvaloniaUI/Avalonia/issues/5264
            if (this.EdgeRoutingEnabled)
            {
                MainGraph.RouteEdges();
            }

            MainGraph.DrawAv(e, this._lastMouseState, this.IsRunMode, this.Bounds.Width, this.Bounds.Height, this.DragStartSocket);

            if (DragStartSocket != null)
            {
                NodeConnection.DrawDragConnectionAv(e, AvaloniaUtils.BlackPen1, this.DragStartSocket.GetBounds().Center, this._lastMouseState.Position, null);
            }

            if (selectionStart != default)
            {
                e.DrawRectangle(new SolidColorBrush(Colors.CornflowerBlue, 0.2), new Pen(Colors.DodgerBlue.ToUInt32()), MakeRect(selectionStart, selectionEnd, true));
            }

            e.DrawEllipse(new SolidColorBrush(this.IsFocused ? Colors.GreenYellow : Colors.Pink, 0.5), null, new Rect(this._lastMouseState.Position.X - 5, this._lastMouseState.Position.Y - 5, 10, 10));
        }

        private static Rect MakeRect(Point a, Point b, bool round)
        {
            var x1 = a.X;
            var x2 = b.X;
            var y1 = a.Y;
            var y2 = b.Y;

            var x = Math.Min(x1, x2);
            var y = Math.Min(y1, y2);
            var w = Math.Abs(x2 - x1);
            var h = Math.Abs(y2 - y1);
            return new Rect(round ? Math.Round(x) : x, round ? Math.Round(y) : y, round ? Math.Round(w) : w, round ? Math.Round(h) : h);
        }

        public void OnNodesControl_MouseMove(object s, PointerEventArgs eventArgs)
        {
            if (!this.IsFocused)
            {
                return;
            }
            
            if (this._skipMouseMovePostSwap)
            {
                this._skipMouseMovePostSwap = false;
                return;
            }

            this.OnNodesControl_MouseMove(eventArgs.GetCurrentPoint(this));
        }

        public void OnNodesControl_MouseMove(PointerPoint pointerPoint)
        {
            Point prevPosition = this._lastMouseState.Position;

            _lastMouseState = pointerPoint;

            var em = _lastMouseState.Position;
            if (selectionStart != default)
            {
                selectionEnd = em;
            }

            if (pointerPoint.Properties.IsLeftButtonPressed && !IsRunMode)
            {
                foreach (var node in MainGraph.NodesTyped.Where(x => x.IsSelected))
                {
                    node.BoundaryCurve.Translate(new Microsoft.Msagl.Core.Geometry.Point(em.X - prevPosition.X, em.Y - prevPosition.Y));

                    node.LayoutEditor();
                }

                if (MainGraph.NodesTyped.Any(x => x.IsSelected))
                {
                    var n = MainGraph.NodesTyped.FirstOrDefault(x => x.IsSelected);
                    var bound = new Rect(new Point(n.X, n.Y), n.GetNodeBounds());
                    foreach (var node in MainGraph.NodesTyped.Where(x => x.IsSelected))
                    {
                        bound = bound.Union(new Rect(new Point(node.X, node.Y), node.GetNodeBounds()));
                    }

                    OnShowLocation(bound);
                }
            }
        }

        public void OnNodesControl_MousePressed(object sender, PointerPressedEventArgs eventArgs) => this.OnNodesControl_MousePressed(eventArgs.GetCurrentPoint(this).Properties.PointerUpdateKind, eventArgs.ClickCount, eventArgs.KeyModifiers);

        public void OnNodesControl_MousePressed(PointerUpdateKind updateKind, int clickCount, KeyModifiers keyModifiers = KeyModifiers.None)
        {
            if (Context == null) return;

            if (clickCount == 2)
            {
                var selectedNode = MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                    x => new Rect(new Point(x.X, x.Y), x.GetNodeBounds()).Contains(this._lastMouseState.Position));

                if (selectedNode?.SubsystemGraph != null)
                {
                    this.OnSubgraphOpenRequest?.Invoke(selectedNode);
                    return;
                }

                if (IsRunMode || selectedNode != null)
                {
                    return;
                }

                foreach (NodeVisual n in MainGraph.Nodes)
                {
                    n.IsSelected = false;
                }

                var newAutocompleteNode = new NodeVisual(NodeVisual.NewSpecialNodeName, this._lastMouseState.Position.X, this._lastMouseState.Position.Y - NodeVisual.HeaderHeight)
                {
                    OwnerGraph = MainGraph
                };

                TextEditor textEditor = new TextEditor()
                {
                    Name = "Editor",
                    FontFamily = AvaloniaUtils.FontMonospaceNormal.FontFamily,
                    FontWeight = FontWeight.Normal,
                    FontSize = 12,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                };

                textEditor.TextArea.RightClickMovesCaret = true;
                textEditor.TextArea.TextView.Options.AllowScrollBelowDocument = false;
                textEditor.ContextMenu = new ContextMenu
                {
                    ItemsSource = new List<MenuItem>
                    {
                        new MenuItem { Header = "Copy", InputGesture = new KeyGesture(Key.C, KeyModifiers.Control) },
                        new MenuItem { Header = "Paste", InputGesture = new KeyGesture(Key.V, KeyModifiers.Control) },
                        new MenuItem { Header = "Cut", InputGesture = new KeyGesture(Key.X, KeyModifiers.Control) }
                    }
                };

                textEditor.Width = textEditor.MinWidth = (int)NodeVisual.NodeWidth - 4;
                textEditor.Height = textEditor.MinHeight = (int)NodeVisual.HeaderHeight - 8;
                textEditor.Background = new SolidColorBrush(newAutocompleteNode.NodeColorAv);
                textEditor.BorderThickness = new Thickness(0);
                textEditor.AttachedToVisualTree += (s, e) =>
                {
                    Dispatcher.UIThread.InvokeAsync(() => textEditor.Focus());
                };

                Completion completion = new Completion(textEditor);
                completion.ExternalCompletions.AddRange(Context.GetNodeDescriptors().Select(x => x.Name.ToLowerInvariant())
                    .Concat(new[] { NodeVisual.NewSubsystemNodeNamePrefix, NodeVisual.NewSubsystemInletNodeNamePrefix, NodeVisual.NewSubsystemOutletNodeNamePrefix })
                    .Select(x => new Completion.CompletionItem(x, x)));

                textEditor.AddHandler(
                    KeyDownEvent,
                    (s, e) =>
                    {
                        if (e.Key == Key.Enter)
                        {
                            string fullText = completion.IsVisible ? completion.SelectedItem.Text : textEditor.Text;
                            SwapAutocompleteNode(textEditor, fullText, newAutocompleteNode);
                            e.Handled = true;
                        }
                    },
                    RoutingStrategies.Tunnel);

                newAutocompleteNode.CustomEditorAv = textEditor;
                newAutocompleteNode.LayoutEditor();
                (this.Content as Canvas).Children.Add(newAutocompleteNode.CustomEditorAv);

                MainGraph.Nodes.Add(newAutocompleteNode);
                newAutocompleteNode.IsSelected = true;

                return;
            }

            if (updateKind == PointerUpdateKind.RightButtonPressed)
            {
                var node = MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                        x => new Rect(new Point(x.X, x.Y), x.GetNodeBounds()).Contains(this._lastMouseState.Position));

                // TODO: need to use avalon context menu
                // var context = new ContextMenuStrip();
                // if (node != null || MainGraph.NodesTyped.Any(x => x.IsSelected))
                // {
                //     context.Items.Add("Delete Node(s)", null, ((o, args) =>
                //     {
                //         DeleteSelectedNodes(node);
                //     }));
                // 
                //     context.Items.Add("Duplicate Node(s)", null, ((o, args) =>
                //     {
                //         DuplicateSelectedNodes(node);
                //     }));
                // 
                //     context.Items.Add("Change Color ...", null, ((o, args) =>
                //     {
                //         ChangeSelectedNodesColor(node);
                //     }));
                // 
                //     if (MainGraph.NodesTyped.Count(x => x.IsSelected) == 2)
                //     {
                //         var sel = MainGraph.NodesTyped.Where(x => x.IsSelected).ToArray();
                //         context.Items.Add("Check Impact ???", null, ((o, args) =>
                //         {
                //             if (HasImpact(sel[0], sel[1]) || HasImpact(sel[1], sel[0]))
                //             {
                //                 MessageBox.Show("One node has impact on other.", "Impact detected.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //             }
                //             else
                //             {
                //                 MessageBox.Show("These nodes not impacts themselves.", "No impact.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //             }
                //         }));
                //     }
                // 
                //     context.Show((int)this._lastMouseState.Position.X, (int)this._lastMouseState.Position.Y);
                // }
                // else if (MainGraph.EdgesTyped.Any(x => x.IsHover))
                // {
                //     context.Items.Add("Delete Connection(s)", null, ((o, args) =>
                //     {
                //         DeleteHoveredConns();
                //     }));
                // }
                // 
                // context.Show((int)this._lastMouseState.Position.X, (int)this._lastMouseState.Position.Y);
            }
            else if (updateKind == PointerUpdateKind.LeftButtonPressed)
            {
                selectionStart = default;

                Focus();

                var node = MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                    x => new Rect(new Point(x.X, x.Y), x.GetNodeBounds()).Contains(this._lastMouseState.Position));

                if (!keyModifiers.HasFlag(KeyModifiers.Control) && node?.IsSelected != true)
                {
                    foreach (NodeVisual n in MainGraph.Nodes)
                    {
                        n.IsSelected = false;
                    }
                }

                if (!IsRunMode)
                {
                    var nodeWhole =
                    MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                        x => new Rect(new Point(x.X, x.Y), x.GetNodeBounds()).Contains(this._lastMouseState.Position));
                    if (nodeWhole != null)
                    {
                        node = nodeWhole;

                        StartDragConnection(keyModifiers, nodeWhole, this._lastMouseState.Position);
                    }
                    else
                    {
                        selectionStart = selectionEnd = this._lastMouseState.Position;
                    }
                }

                if (node != null && DragStartSocket == null)
                {
                    if (keyModifiers.HasFlag(KeyModifiers.Control) && node.IsSelected)
                    {
                        node.IsSelected = false;
                    }
                    else
                    {
                        node.IsSelected = true;
                    }

                    if (node.CustomEditorAv != null)
                    {
                        node.CustomEditorAv.BringIntoView();
                    }
                }

                if (node != null)
                {
                    OnNodeSelected(node);
                }
            }
        }

        private void StartDragConnection(KeyModifiers keyModifiers, NodeVisual? nodeWhole, Point mousePosition)
        {
            var socket = nodeWhole.GetSockets().All.FirstOrDefault(x => x.GetBounds().Contains(mousePosition));
            if (socket != null)
            {
                if (keyModifiers.HasFlag(KeyModifiers.Control))
                {
                    var connection =
                        MainGraph.EdgesTyped.FirstOrDefault(
                            x => x.InputNode == nodeWhole && x.InputSocketName == socket.Name);

                    if (connection != null)
                    {
                        // Re-drag existing connection's end
                        DragStartSocket = connection.OutputSocket;
                    }
                    else
                    {
                        // Re-drag existing connection's start
                        connection =
                            MainGraph.EdgesTyped.FirstOrDefault(
                                x => x.OutputNode == nodeWhole && x.OutputSocketName == socket.Name);

                        if (connection != null)
                        {
                            DragStartSocket = connection.InputSocket;
                        }
                    }

                    // The old edge gets removed
                    MainGraph.RemoveEdge(connection);
                }
                else
                {
                    // Start new connection drag
                    DragStartSocket = socket;
                }
            }
        }

        public void OnNodesControl_MouseUp(object sender, PointerReleasedEventArgs eventArgs)
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
                    MainGraph.AddEdge(new NodeConnection(hoverConn.OutputNode, hoverConn.OutputSocketName, node, s.Inputs.First().Name));
                    MainGraph.AddEdge(new NodeConnection(node, s.Outputs.First().Name, hoverConn.InputNode, hoverConn.InputSocketName));
                    MainGraph.RemoveEdge(hoverConn);
                }
            }

            if (selectionStart != default)
            {
                var rect = MakeRect(selectionStart, selectionEnd, false);
                foreach (NodeVisual x in MainGraph.Nodes)
                {
                    x.IsSelected = rect.Contains(new Rect(new Point(x.X, x.Y), x.GetNodeBounds()));
                }

                selectionStart = default;
            }

            EndDragConnection(this._lastMouseState.Position);
        }

        private void EndDragConnection(Point mousePosition)
        {
            if (DragStartSocket != null)
            {
                var nodeWhole =
                    MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                        x => new Rect(new Point(x.X, x.Y), x.GetNodeBounds()).Contains(mousePosition));
                if (nodeWhole != null)
                {
                    var socket = nodeWhole.GetSockets().All.FirstOrDefault(x => x.GetBounds().Contains(mousePosition));
                    if (socket != null)
                    {
                        if (DragStartSocket.Input != socket.Input)
                        {
                            NodeConnection nc = null;
                            if (!DragStartSocket.Input)
                            {
                                nc = new NodeConnection(DragStartSocket.Parent, DragStartSocket.Name, nodeWhole, socket.Name);
                            }
                            else
                            {
                                nc = new NodeConnection(nodeWhole, socket.Name, DragStartSocket.Parent, DragStartSocket.Name);
                            }

                            MainGraph.AddEdge(nc);
                        }
                    }
                }

                DragStartSocket = null;
            }
        }

        public void SwapAutocompleteNode(Control tb, string text, NodeVisual newAutocompleteNode)
        {
            NodeVisual replacementNode = null;

            if (text.StartsWith(NodeVisual.NewSubsystemNodeNamePrefix))
            {
                string name = text.Substring(NodeVisual.NewSubsystemNodeNamePrefix.Length).Trim();
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
            else if (text.StartsWith(NodeVisual.NewSubsystemInletNodeNamePrefix))
            {
                string name = text.Substring(NodeVisual.NewSubsystemInletNodeNamePrefix.Length).Trim();
                if (name == "" || this.MainGraph.NodesTyped.Any(x => x.Name == NodeVisual.NewSubsystemInletNodeNamePrefix + " " + name))
                {
                    return;
                }

                replacementNode = new NodeVisual(NodeVisual.NewSubsystemInletNodeNamePrefix + " " + name, newAutocompleteNode.X, newAutocompleteNode.Y);
                replacementNode.OwnerGraph = MainGraph;
                replacementNode.OwnerGraph.OwnerNode?.ResetSocketsCache();
            }
            else if (text.StartsWith(NodeVisual.NewSubsystemOutletNodeNamePrefix))
            {
                string name = text.Substring(NodeVisual.NewSubsystemOutletNodeNamePrefix.Length).Trim();
                if (name == "" || this.MainGraph.NodesTyped.Any(x => x.Name == NodeVisual.NewSubsystemOutletNodeNamePrefix + " " + name))
                {
                    return;
                }

                replacementNode = new NodeVisual(NodeVisual.NewSubsystemOutletNodeNamePrefix + " " + name, newAutocompleteNode.X, newAutocompleteNode.Y);
                replacementNode.OwnerGraph = MainGraph;
                replacementNode.OwnerGraph.OwnerNode?.ResetSocketsCache();
            }
            else
            {
                Dictionary<string, NodeDescriptor> descs = Context.GetNodeDescriptors().ToDictionary(x => x.Name.ToLowerInvariant());
                string name = text.Trim();
                if (!descs.TryGetValue(name.Split(' ')[0].ToLowerInvariant(), out NodeDescriptor desc))
                {
                    return;
                }

                replacementNode = new NodeVisual(name, newAutocompleteNode.X, newAutocompleteNode.Y)
                {
                    NodeDesc = desc,
                    OwnerGraph = MainGraph,
                };

                if (desc.CustomEditorFactory != null)
                {
                    replacementNode.CustomEditorAv = desc.CustomEditorFactory.Invoke(this);
                    replacementNode.CustomEditorAv[BackgroundProperty] = new SolidColorBrush(newAutocompleteNode.NodeColorAv);
                    (this.Content as Canvas).Children.Add(replacementNode.CustomEditorAv);

                    replacementNode.LayoutEditor();
                }
            }

            if ((object)tb is Control editorCtrl)
            {
                (this.Content as Canvas).Children.Remove(editorCtrl);
            }

            MainGraph.Nodes.Remove(newAutocompleteNode);
            MainGraph.Nodes.Add(replacementNode);

            replacementNode.IsSelected = true;
            this._skipMouseMovePostSwap = true;

            this.Focus();
        }

        private void ChangeSelectedNodesColor(NodeVisual node)
        {
            NodeVisual[] selected = MainGraph.NodesTyped.Where(x => x.IsSelected).Concat(new[] { node }).Where(x => x != null).ToArray();

            // ColorDialog cd = new ColorDialog();
            // cd.FullOpen = true;
            // if (cd.ShowDialog() == DialogResult.OK)
            // {
            //     foreach (var n in selected)
            //     {
            //         n.NodeColor = cd.Color;
            //     }
            // }
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

                // TODO: this is broken for subsystems
                NodesGraph.SerializeNode(bw, n);
                ms.Seek(0, SeekOrigin.Begin);
                var br = new BinaryReader(ms);
                var clone = NodesGraph.DeserializeNode(br, this.Context).Item1;
                clone.BoundaryCurve.Translate(new Microsoft.Msagl.Core.Geometry.Point(40, 40));
                clone.GUID = Guid.NewGuid().ToString();
                clone.IsSelected = true;
                clone.CustomEditorAv?.BringIntoView();
                MainGraph.Nodes.Add(clone);
                br.Dispose();
                bw.Dispose();
                ms.Dispose();
            }
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

                        if (inCon is not null && outCon is not null)
                        {
                            MainGraph.AddEdge(new NodeConnection(inCon.OutputNode, inCon.OutputSocketName, outCon.InputNode, outCon.InputSocketName));
                        }
                    }
                }

                foreach (var n in selected)
                {
                    (this.Content as Canvas).Children.Remove(n.CustomEditorAv);
                    foreach (NodeConnection e in MainGraph.EdgesTyped.Where(x => x.OutputNode == n || x.InputNode == n).ToArray())
                    {
                        MainGraph.RemoveEdge(e);
                        if (e.OutputSocket.Value == e.InputSocket.Value)
                        {
                            e.InputSocket.Value = null;
                        }
                    }
                }

                MainGraph.Nodes = MainGraph.Nodes.Except(selected).ToList();
            }
        }

        private void DeleteHoveredConns()
        {
            foreach (NodeConnection e in MainGraph.EdgesTyped.Where(x => x.IsHover).ToArray())
            {
                MainGraph.RemoveEdge(e);
            }
        }

        private void ToggleEdgeRouting()
        {
            this.EdgeRoutingEnabled = !this.EdgeRoutingEnabled;
            if (!EdgeRoutingEnabled)
            {
                foreach (NodeConnection c in this.MainGraph.EdgesTyped)
                {
                    c.Curve = null;
                }
            }
        }

        private void ToggleRunMode()
        {
            this.IsRunMode = !this.IsRunMode;
            this.Cursor = this.IsRunMode ? Cursor.Default : AvaloniaUtils.CursorHand.Value;

            if (!this.IsRunMode)
            {
                return;
            }

            Stack<(NodeVisual, Bang?)> nodeQueue = new Stack<(NodeVisual, Bang?)>();
            foreach (NodeVisual node in MainGraph.NodesTyped.Reverse<NodeVisual>())
            {
                if (node.NodeDesc.InvokeOnLoad)
                {
                    nodeQueue.Push((node, null));
                }
            }

            this.Execute(nodeQueue);
        }

        /// <summary>
        /// Executes whole node graph (when called parameterless) or given node when specified.
        /// </summary>
        /// <param name="node"></param>
        private void Execute(Stack<(NodeVisual, Bang?)> queue)
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

                (NodeVisual, Bang?) curr = nodeQueue.Pop();
                curr.Item1.Feedback = FeedbackType.None;

                try
                {
                    if (curr.Item1.Type == NodeVisual.NodeType.Normal)
                    {
                        curr.Item1.Execute(Context, curr.Item2);
                    }
                }
                catch (Exception ex)
                {
                    curr.Item1.Feedback = FeedbackType.Error;
                }

                List<NodeConnection> propagated = new List<NodeConnection>();
                foreach (var connection in curr.Item1.OwnerGraph.EdgesTyped)
                {
                    if (connection.OutputNode != curr.Item1)
                    {
                        continue;
                    }

                    bool wasPropagated = connection.PropagateValue(Context);
                    if (wasPropagated)
                    {
                        propagated.Add(connection);
                    }
                }

                foreach (var connection in propagated)
                {
                    if (!connection.InputSocket.HotInput)
                    {
                        continue;
                    }

                    Bang? triggerBang = connection.OutputSocket.Value as Bang;
                    if (connection.InputNode.Type == NodeVisual.NodeType.Subsystem)
                    {
                        NodeVisual inlet = connection.InputNode.SubsystemGraph.NodesTyped.Single(x => x.Name == connection.InputSocketName);
                        inlet.GetSockets().Outputs.Single().Value = connection.InputSocket.Value;
                        nodeQueue.Push((inlet, triggerBang));
                    }
                    else if (connection.InputNode.Type == NodeVisual.NodeType.SubsystemOutlet)
                    {
                        SocketVisual subsystemOutput = connection.InputNode.OwnerGraph.OwnerNode.GetSockets().Outputs.Single(x => x.Name == connection.InputNode.Name);
                        subsystemOutput.Value = connection.InputSocket.Value;
                        nodeQueue.Push((connection.InputNode.OwnerGraph.OwnerNode, triggerBang));
                    }
                    else
                    {
                        nodeQueue.Push((connection.InputNode, triggerBang));
                    }

                }
            }
        }

        private bool HasImpact(NodeVisual startNode, NodeVisual endNode)
        {
            var connections = MainGraph.EdgesTyped.Where(x => x.OutputNode == startNode);
            foreach (var connection in connections)
            {
                if (connection.InputNode == endNode)
                {
                    return true;
                }
                bool nextImpact = HasImpact(connection.InputNode, endNode);
                if (nextImpact)
                {
                    return true;
                }
            }

            return false;
        }

        public void OnNodesControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Space)
            {
                return;
            }

            if (e.Key == Key.Delete && !IsRunMode)
            {
                DeleteSelectedNodes(null);
                DeleteHoveredConns();
            }

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.E)
            {
                this.ToggleRunMode();
            }

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.R)
            {
                this.ToggleEdgeRouting();
            }

            Microsoft.Msagl.Core.Geometry.Point mousePoint = this._lastMouseState.Position.ToMsaglPoint();
            NodeVisual? currentNode = MainGraph.NodesTyped.Where(x => x.IsSelected).OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault();
            if (currentNode is null && this.DragStartSocket is not null)
            {
                currentNode = MainGraph.NodesTyped.Where(x => x.BoundingBox.Contains(mousePoint)).OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault();
            }

            Microsoft.Msagl.Core.Geometry.Rectangle rect = currentNode?.BoundingBox ?? new Microsoft.Msagl.Core.Geometry.Rectangle(this._lastMouseState.Position.ToMsaglPoint());

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Enter)
            {
                Point translated = new Point(this._lastMouseState.Position.X, this._lastMouseState.Position.Y + 100);
                if (currentNode is not null)
                {
                    currentNode.IsSelected = false;
                    Point newLocation = this.DragStartSocket?.Input switch
                    {
                        null => new Point(currentNode.BoundingBox.Left, currentNode.BoundingBox.Top + currentNode.BoundingBox.Height + 50),
                        false => new Point(currentNode.BoundingBox.Left, currentNode.BoundingBox.Top + currentNode.BoundingBox.Height + 50),
                        true => new Point(currentNode.BoundingBox.Left, currentNode.BoundingBox.Top - 50 - NodeVisual.HeaderHeight),
                    };

                    translated = newLocation;
                }

                PointerPoint p = new PointerPoint(this._lastMouseState.Pointer, translated, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed));
                OnNodesControl_MouseMove(p);

                this.OnNodesControl_MousePressed(PointerUpdateKind.LeftButtonPressed, 2);

                this._lastMouseState = new PointerPoint(this._lastMouseState.Pointer, new Point(translated.X + 10, translated.Y - 10), new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed));
            }

            Point origPoint = this._lastMouseState.Position;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                Point translatedPoint = this._lastMouseState.Position;
                bool match = false;
                if (e.Key == Key.Right)
                {
                    translatedPoint = new Point(translatedPoint.X + 10, translatedPoint.Y);
                    match = true;
                }
                else if (e.Key == Key.Left)
                {
                    translatedPoint = new Point(Math.Max(0, translatedPoint.X - 10), translatedPoint.Y);
                    match = true;
                }
                else if (e.Key == Key.Up)
                {
                    translatedPoint = new Point(translatedPoint.X, Math.Max(0, translatedPoint.Y - 10));
                    match = true;
                }
                else if (e.Key == Key.Down)
                {
                    translatedPoint = new Point(translatedPoint.X, translatedPoint.Y + 10);
                    match = true;
                }

                if (match)
                {
                    PointerPoint p = new PointerPoint(this._lastMouseState.Pointer, translatedPoint, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed));
                    OnNodesControl_MouseMove(p);
                }
            }
            else
            {
                NodeVisual? nextNode = null;
                if (e.Key == Key.Right)
                {
                    nextNode = MainGraph.NodesTyped
                        .Where(x => !x.IsSelected && rect.Left < x.BoundingBox.Left)
                        .OrderBy(x => Math.Sqrt(Promote(Math.Abs(rect.Right - x.BoundingBox.Left)) + Penalize(Math.Abs(rect.Center.Y - x.BoundingBox.Center.Y))))
                        .FirstOrDefault();
                }
                else if (e.Key == Key.Left)
                {
                    nextNode = MainGraph.NodesTyped
                        .Where(x => !x.IsSelected && rect.Right > x.BoundingBox.Right)
                        .OrderBy(x => Math.Sqrt(Promote(Math.Abs(rect.Left - x.BoundingBox.Right)) + Penalize(Math.Abs(rect.Center.Y - x.BoundingBox.Center.Y))))
                        .FirstOrDefault();
                }
                else if (e.Key == Key.Up)
                {
                    nextNode = MainGraph.NodesTyped
                        .Where(x => !x.IsSelected && rect.Top > x.BoundingBox.Top)
                        .OrderBy(x => Math.Sqrt(Penalize(Math.Abs(rect.Center.X - x.BoundingBox.Center.X)) + Promote(Math.Abs(rect.Top - x.BoundingBox.Center.Y))))
                        .FirstOrDefault();
                }
                else if (e.Key == Key.Down)
                {
                    nextNode = MainGraph.NodesTyped
                        .Where(x => !x.IsSelected && rect.Top < x.BoundingBox.Top)
                        .OrderBy(x => rect.Top + rect.Height < x.BoundingBox.Top ? 0 : 1)
                        .ThenBy(x => Math.Sqrt(Penalize(Math.Abs(rect.Center.X - x.BoundingBox.Center.X)) + Promote(Math.Abs(rect.Center.Y - x.BoundingBox.Top))))
                        .FirstOrDefault();
                }

                if (nextNode is not null)
                {
                    Point translatedPoint = nextNode.BoundingBox.Center.ToPoint();
                    this._lastMouseState = new PointerPoint(this._lastMouseState.Pointer, translatedPoint, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed));
                    if (this.DragStartSocket is null)
                    {
                        this.OnNodesControl_MousePressed(PointerUpdateKind.LeftButtonPressed, 1);
                    }
                    else
                    {
                        OnNodesControl_MouseMove(this._lastMouseState);
                    }

                    return;
                }

                double Penalize(double y) => 1.5 * y;
                double Promote(double y) => y;
            }

            int? index = null;
            bool isInput = false;
            if (e.Key == Key.Escape && this.DragStartSocket != null)
            {
                Point translatedPoint =this.DragStartSocket.Parent.BoundingBox.Center.ToPoint();
                this.DragStartSocket = null;
                this._lastMouseState = new PointerPoint(this._lastMouseState.Pointer, translatedPoint, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed));
                this.OnNodesControl_MousePressed(PointerUpdateKind.LeftButtonPressed, 1);
            }
            else if (e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                index = e.Key - Key.D1;
                isInput = true;
            }
            else
            {
                isInput = false;
                index = e.Key switch
                {
                    Key.Q => 0,
                    Key.W => 1,
                    Key.E => 2,
                    Key.R => 3,
                    Key.T => 4,
                    Key.Y => 5,
                    Key.U => 6,
                    Key.I => 7,
                    Key.O => 8,
                    _ => null
                };
            }

            if (index is not null)
            {
                SocketVisual? hoverSocket = MainGraph.NodesTyped.SelectMany(x => x.GetSockets().All).Where(x => x.ActiveHover).OrderByDescending(x => x.Location).FirstOrDefault();
                if (hoverSocket is not null)
                {
                    if (this.DragStartSocket is not null && currentNode is null)
                    {
                        Microsoft.Msagl.Core.Geometry.Point point = this._lastMouseState.Position.ToMsaglPoint();
                        currentNode = MainGraph.NodesTyped.Where(x => x.BoundingBox.Contains(point, 0)).OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault();
                    }

                    var center = hoverSocket.Curve.BoundingBox.Center;

                    // We already have a socket selected
                    List<SocketVisual> sockets = isInput ? hoverSocket.Parent.GetSockets().Inputs : hoverSocket.Parent.GetSockets().Outputs;

                    SocketVisual? keyboardTargetSocket = sockets.Count > index ? sockets[index.Value] : null;
                    if (this.DragStartSocket is null && keyboardTargetSocket == hoverSocket)
                    {
                        // Double-tap
                        this._lastMouseState = new PointerPoint(this._lastMouseState.Pointer, center.ToPoint(), new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed));
                        this.StartDragConnection(KeyModifiers.None, hoverSocket.Parent, center.ToPoint());
                    }
                    else if (this.DragStartSocket is not null && this.DragStartSocket.Input != hoverSocket.Input && hoverSocket == keyboardTargetSocket)
                    {
                        this.EndDragConnection(center.ToPoint());
                        hoverSocket.Parent.IsSelected = true;
                    }
                    else
                    {
                        // Deselect old, select new
                        currentNode = hoverSocket.Parent;
                    }
                }

                if (currentNode is not null)
                {
                    List<SocketVisual> sockets = isInput ? currentNode.GetSockets().Inputs : currentNode.GetSockets().Outputs;
                    if (sockets.Count > index)
                    {
                        SocketVisual sv = sockets[index.Value];
                        var center = sv.Curve.BoundingBox.Center;
                        if (!sv.ActiveHover)
                        {
                            // Hover a socket
                            currentNode.IsSelected = false;
                            this._lastMouseState = new PointerPoint(this._lastMouseState.Pointer, center.ToPoint(), new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed));
                        }
                    }
                }
            }
        }

        private void OnNodeContextSelected(NodeVisual o)
        {
            if (this.IsRunMode && o.NodeDesc.IsInteractive)
            {
                Bang triggerBang = new Bang()
                {
                    LeftMouseButtonPressed = this._lastMouseState.Properties.IsLeftButtonPressed,
                    RightMouseButtonPressed = this._lastMouseState.Properties.IsRightButtonPressed
                };
                this.Execute(new Stack<(NodeVisual, Bang?)>(new[] { (o, triggerBang) }));
            }
        }

        public void Initialize(INodesContext context, NodesGraph mainGraph)
        {
            this.Context = context;
            this.MainGraph = mainGraph;

            (this.Content as Canvas).Children.Clear();
            foreach (NodeVisual n in mainGraph.NodesTyped.Where(x => x.CustomEditorAv != null))
            {
                n.CustomEditorAv.UpdateLayout();
                (this.Content as Canvas).Children.Add(n.CustomEditorAv);
                n.LayoutEditor();
            }

            this.MinHeight = 600;
            this.Bounds = new Rect(0, 0, 800, 600);
        }
    }
}
