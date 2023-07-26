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

using System.Data;
using System.Reflection;
using AnimateForms.Core;
using AvaloniaEdit;
using Sharplog.KME;

namespace NodeEditor
{
    /// <summary>
    /// Main control of Node Editor Winforms
    /// </summary>
    public class NodesControlAv : Avalonia.Controls.UserControl
    {
        public NodesGraph MainGraph { get; private set; }
        public readonly Animate Animate = new Animate();
        private bool needRepaint = true;
        private System.Timers.Timer timer = new System.Timers.Timer();
        private bool mdown;
        private Avalonia.Input.PointerPoint _lastMouseState;
        private SocketVisual dragSocket;
        private NodeVisual dragSocketNode;
        private Avalonia.Point dragConnectionBegin;
        private Avalonia.Point dragConnectionEnd;

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
        public event Action<Rect> OnShowLocation = delegate { };

        private Avalonia.Point selectionStart;

        private Avalonia.Point selectionEnd;

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
            
            this.AttachedToVisualTree += this.OnNodesControl_VisibleChanged;
            this.PointerPressed += this.OnNodesControl_MousePressed;
            this.PointerMoved += this.OnNodesControl_MouseMove;
            this.PointerReleased += this.OnNodesControl_MouseUp;
            this.KeyDown += this.OnNodesControl_KeyDown;

            timer.Interval = 30;
            timer.Elapsed += TimerOnTick;
            timer.Start();

            this.Cursor = this.IsRunMode ? Avalonia.Input.Cursor.Default : AvaloniaUtils.CursorHand.Value;

            this.OnNodeSelected += OnNodeContextSelected;

            this.Content = new Avalonia.Controls.Canvas();
        }

        private void ContextOnFeedbackInfo(string message, NodeVisual nodeVisual, FeedbackType type, object tag, bool breakExecution)
        {
            nodeVisual.Feedback = type;
            this.breakExecution = breakExecution;
            OnNodeHint(message);
        }

        public void TimerOnTick(object sender, EventArgs eventArgs)
        {
            if (needRepaint || this.Animate.AnimationUpdated)
            {
                Dispatcher.UIThread.Invoke(this.InvalidateVisual);
            }
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

            MainGraph.DrawAv(e, this._lastMouseState, this.IsRunMode, this.Animate, this.Bounds.Width, this.Bounds.Height);

            if (dragSocket != null)
            {
                NodeConnection.DrawDragConnectionAv(e, AvaloniaUtils.BlackPen1, dragConnectionBegin, dragConnectionEnd);
            }

            if (selectionStart != default)
            {                
                e.DrawRectangle(new SolidColorBrush(Avalonia.Media.Colors.CornflowerBlue, 0.2), new Avalonia.Media.Pen(Avalonia.Media.Colors.DodgerBlue.ToUInt32()), MakeRect(selectionStart, selectionEnd ,true));
            }

            e.DrawEllipse(new SolidColorBrush(this.IsFocused ? Avalonia.Media.Colors.GreenYellow :  Avalonia.Media.Colors.Pink, 0.5), null, new Rect(this._lastMouseState.Position.X-5, this._lastMouseState.Position.Y-5, 10, 10));

            needRepaint = false;
        }

        private static Avalonia.Rect MakeRect(Avalonia.Point a, Avalonia.Point b, bool round)
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

        public void OnNodesControl_MouseMove(object s, PointerEventArgs eventArgs) => this.OnNodesControl_MouseMove(eventArgs.GetCurrentPoint(this));

        public void OnNodesControl_MouseMove(PointerPoint pointerPoint)
        {
            Avalonia.Point prevPosition = this._lastMouseState.Position;

            _lastMouseState = pointerPoint;

            var em = _lastMouseState.Position;
            if (selectionStart != default)
            {
                selectionEnd = em;
            }

            if (mdown && !IsRunMode)
            {                                            
                foreach (var node in MainGraph.NodesTyped.Where(x => x.IsSelected))
                {
                    node.BoundaryCurve.Translate(new Microsoft.Msagl.Core.Geometry.Point(em.X - prevPosition.X, em.Y - prevPosition.Y));

                    node.LayoutEditor();
                }

                if (MainGraph.NodesTyped.Any(x => x.IsSelected))
                {
                    var n = MainGraph.NodesTyped.FirstOrDefault(x => x.IsSelected);
                    var bound = new Rect(new Avalonia.Point(n.X,n.Y), n.GetNodeBounds());
                    foreach (var node in MainGraph.NodesTyped.Where(x=>x.IsSelected))
                    {
                        bound = bound.Union(new Rect(new Avalonia.Point(node.X, node.Y), node.GetNodeBounds()));
                    }

                    OnShowLocation(bound);
                }

                InvalidateVisual();
                
                if (dragSocket != null)
                {
                    var center = new Avalonia.Point(dragSocket.Location.X + dragSocket.Width/2, dragSocket.Location.Y + dragSocket.Height/2);
                    if (dragSocket.Input)
                    {
                        dragConnectionBegin = dragConnectionBegin
                            .WithX(dragConnectionBegin.X + em.X - prevPosition.X)
                            .WithY(dragConnectionBegin.Y + em.Y - prevPosition.Y);
                        dragConnectionEnd = center;
                        OnShowLocation(new Rect(dragConnectionBegin, new Avalonia.Size(10, 10)));
                    }
                    else
                    {
                        dragConnectionBegin = center;
                        dragConnectionEnd = dragConnectionEnd
                            .WithX(dragConnectionEnd.X + em.X - prevPosition.X)
                            .WithY(dragConnectionEnd.Y + em.Y - prevPosition.Y);
                        OnShowLocation(new Rect(dragConnectionEnd, new Avalonia.Size(10, 10)));
                    }
                    
                }

            }            

            needRepaint = true;
        }

        public void OnNodesControl_MousePressed(object sender, PointerPressedEventArgs eventArgs) => this.OnNodesControl_MousePressed(eventArgs.GetCurrentPoint(this).Properties.PointerUpdateKind, eventArgs.ClickCount, eventArgs.KeyModifiers);

        public void OnNodesControl_MousePressed(PointerUpdateKind updateKind, int clickCount, KeyModifiers keyModifiers = KeyModifiers.None)
        {
            if (Context == null) return;

            if (clickCount == 2)
            {
                var selectedNode = MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                    x => new Rect(new Avalonia.Point(x.X, x.Y), x.GetNodeBounds()).Contains(this._lastMouseState.Position));

                if (selectedNode?.SubsystemGraph != null)
                {
                    this.OnSubgraphOpenRequest?.Invoke(selectedNode);
                    return;
                }

                if (IsRunMode || selectedNode != null)
                {
                    return;
                }

                var newAutocompleteNode = new NodeVisual(NodeVisual.NewSpecialNodeName, this._lastMouseState.Position.X, this._lastMouseState.Position.Y - NodeVisual.HeaderHeight)
                {
                    IsInteractive = false,
                    CustomWidth = -1,
                    CustomHeight = -1,
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
                completion.ExternalCompletions.AddRange(Context.GetType().GetMethods().Where(x => x.GetCustomAttributes(typeof(NodeAttribute), false).Any()).Select(x => x.Name.ToLowerInvariant())
                    .Concat(new[] { NodeVisual.NewSubsystemNodeNamePrefix, NodeVisual.NewSubsystemInletNodeNamePrefix, NodeVisual.NewSubsystemOutletNodeNamePrefix })
                    .Select(x => new Completion.CompletionItem(x, x)));

                textEditor.AddHandler(
                    TextEditor.KeyDownEvent,
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
                InvalidateVisual();
                needRepaint = true;

                return;
            }

            if (updateKind == PointerUpdateKind.RightButtonPressed)
            {
                var node = MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                        x => new Rect(new Avalonia.Point(x.X, x.Y), x.GetNodeBounds()).Contains(this._lastMouseState.Position));

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
                selectionStart  = default;                

                Focus();

                if (keyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    foreach(NodeVisual n in MainGraph.Nodes)
                    {
                        n.IsSelected = false;
                    }
                }

                var node =
                    MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                        x => new Rect(new Avalonia.Point(x.X, x.Y), x.GetNodeBounds()).Contains(this._lastMouseState.Position));

                if (!mdown && !IsRunMode)
                {
                    var nodeWhole =
                    MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                        x => new Rect(new Avalonia.Point(x.X, x.Y), x.GetNodeBounds()).Contains(this._lastMouseState.Position));
                    if (nodeWhole != null)
                    {
                        node = nodeWhole;
                        var socket = nodeWhole.GetSockets().All.FirstOrDefault(x => x.GetBounds().Contains(this._lastMouseState.Position));
                        if (socket != null)
                        {
                            if (keyModifiers.HasFlag(KeyModifiers.Control))
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
                            dragConnectionBegin = this._lastMouseState.Position;
                            dragConnectionEnd = this._lastMouseState.Position;
                            mdown = true;
                        }
                    }
                    else
                    {
                        selectionStart = selectionEnd = this._lastMouseState.Position;
                    }
                }

                if (node != null && !mdown && dragSocket == null)
                {
                    node.IsSelected = true;

                    if (node.CustomEditorAv != null)
                    {
                        node.CustomEditorAv.BringIntoView();
                    }

                    mdown = true;

                    InvalidateVisual();
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
                    if (IsConnectable(hoverConn.OutputSocket, s.Inputs.First())
                        && IsConnectable(hoverConn.InputSocket, s.Outputs.First()))
                    {
                        MainGraph.AddEdge(new NodeConnection(hoverConn.OutputNode, hoverConn.OutputSocketName, node, s.Inputs.First().Name));
                        MainGraph.AddEdge(new NodeConnection(node, s.Outputs.First().Name, hoverConn.InputNode, hoverConn.InputSocketName));
                        MainGraph.RemoveEdge(hoverConn);
                    }
                }
            }

            if (selectionStart != default)
            {
                var rect = MakeRect(selectionStart, selectionEnd, false);
                foreach (NodeVisual x in MainGraph.Nodes)
                {
                    x.IsSelected = rect.Contains(new Rect(new Avalonia.Point(x.X, x.Y), x.GetNodeBounds()));
                }

                selectionStart = default;
            }

            if (dragSocket != null)
            {
                var nodeWhole =
                    MainGraph.NodesTyped.OrderByDescending(x => x.BoundingBox.LeftTop).FirstOrDefault(
                        x => new Rect(new Avalonia.Point(x.X, x.Y), x.GetNodeBounds()).Contains(this._lastMouseState.Position));
                if (nodeWhole != null)
                {
                    var socket = nodeWhole.GetSockets().All.FirstOrDefault(x => x.GetBounds().Contains(this._lastMouseState.Position));
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

        public void SwapAutocompleteNode(Avalonia.Controls.Control tb, string text, NodeVisual newAutocompleteNode)
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
                string name = text.Trim();
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
                    object inst = Activator.CreateInstance(attrib.CustomEditor);
                    replacementNode.CustomEditorAv = null;
                    if (inst is Avalonia.Controls.Control asCtrl)
                    {
                        replacementNode.CustomEditorAv = asCtrl;
                        asCtrl[Avalonia.Controls.TextBox.BackgroundProperty] = new Avalonia.Media.SolidColorBrush(newAutocompleteNode.NodeColorAv);
                        (this.Content as Avalonia.Controls.Canvas).Children.Add(asCtrl);
                    }

                    replacementNode.LayoutEditor();
                }
            }

            if ((object)tb is Avalonia.Controls.Control editorCtrl)
            {
                (this.Content as Avalonia.Controls.Canvas).Children.Remove(editorCtrl);
            }

            MainGraph.Nodes.Remove(newAutocompleteNode);
            MainGraph.Nodes.Add(replacementNode);

            InvalidateVisual();
            needRepaint = true;
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

            InvalidateVisual();
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

            InvalidateVisual();
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
                    (this.Content as Avalonia.Controls.Canvas).Children.Remove(n.CustomEditorAv);
                    foreach(NodeConnection e in MainGraph.EdgesTyped.Where(x => x.OutputNode == n || x.InputNode == n).ToArray())
                    {
                        MainGraph.RemoveEdge(e);
                    }
                }

                MainGraph.Nodes = MainGraph.Nodes.Except(selected).ToList();
            }

            InvalidateVisual();
        }

        private void DeleteHoveredConns()
        {
            foreach (NodeConnection e in MainGraph.EdgesTyped.Where(x => x.IsHover).ToArray())
            {
                MainGraph.RemoveEdge(e);
            }

            InvalidateVisual();
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

            this.needRepaint = true;
        }

        private void ToggleRunMode()
        {
            this.IsRunMode = !this.IsRunMode;
            this.Cursor = this.IsRunMode ? Avalonia.Input.Cursor.Default : AvaloniaUtils.CursorHand.Value;

            this.needRepaint = true;

            if (!this.IsRunMode)
            {
                return;
            }

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
        private void Execute(Stack<NodeVisual> queue)
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
                init.Feedback = FeedbackType.None;

                try
                {
                    init.Execute(Context, this.Animate);
                }
                catch(Exception ex)
                {
                    init.Feedback = FeedbackType.Error;
                }

                foreach (var connection in init.OwnerGraph.EdgesTyped)
                {
                    if (connection.OutputNode != init)
                    {
                        continue;
                    }

                    connection.PropagateValue(Context, this.Animate);
                }

                foreach (var connection in init.OwnerGraph.EdgesTyped.Where(x => x.OutputNode == init && x.OutputSocket.Value != null))
                {
                    if (connection.InputSocket.HotInput)
                    {
                        if (connection.InputNode.Type == NodeVisual.NodeType.Subsystem)
                        {
                            NodeVisual inlet = connection.InputNode.SubsystemGraph.NodesTyped.Single(x => x.Name == connection.InputSocketName);
                            inlet.GetSockets().Outputs.Single().Value = connection.InputSocket.Value;
                            nodeQueue.Push(inlet);
                        }
                        else if (connection.InputNode.Type == NodeVisual.NodeType.SubsystemOutlet)
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

        private bool HasImpact(NodeVisual startNode, NodeVisual endNode)
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

        public void OnNodesControl_KeyDown(object sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
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
        }

        public void OnNodesControl_VisibleChanged(object sender, EventArgs e)
        {
            if (!this.IsVisible)
            {
            }
        }

        private void OnNodeContextSelected(NodeVisual o)
        {
            if (this.IsRunMode && o.IsInteractive)
            {
                this.Execute(new Stack<NodeVisual>(new[] { o }));
            }
        }

        public void Initialize(INodesContext context, NodesGraph mainGraph)
        {
            this.Context = context;
            this.MainGraph = mainGraph;

            (this.Content as Avalonia.Controls.Canvas).Children.Clear();
            foreach(NodeVisual n in mainGraph.NodesTyped.Where(x => x.CustomEditorAv != null))
            {
                n.CustomEditorAv.UpdateLayout();
                (this.Content as Avalonia.Controls.Canvas).Children.Add(n.CustomEditorAv);
                n.LayoutEditor();
            }

            this.MinHeight= 600;
            this.Bounds = new Avalonia.Rect(0, 0, 800, 600);

            this.InvalidateVisual();
        }
    }
}
