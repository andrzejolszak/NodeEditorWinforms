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
using System.Reflection;
using RoundedRect = Microsoft.Msagl.Core.Geometry.Curves.RoundedRect;

namespace NodeEditor
{
    /// <summary>
    /// Class that represents one instance of node.
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class NodeVisual : Node
    {
        public enum NodeType
        {
            Normal,
            New,
            Subsystem,
            SubsystemInlet,
            SubsystemOutlet
        }

        public const string NewSpecialNodeName = "*new*";
        public const string NewSubsystemNodeNamePrefix = "*s*";
        public const string NewSubsystemInletNodeNamePrefix = "*i*";
        public const string NewSubsystemOutletNodeNamePrefix = "*o*";
        public const float NodeWidth = 140;
        public const float HeaderHeight = 32;

        /// <summary>
        /// Current node name.
        /// </summary>
        public string Name { get; private set; }

        internal MethodInfo MethodInf { get; set; }
        public bool IsInteractive { get; set; }
        public bool IsSelected { get; set; }
        public bool InvokeOnLoad { get; set; }
        public FeedbackType Feedback { get; set; }
        public Control CustomEditorAv { get; set; }
        public string GUID = Guid.NewGuid().ToString();
        public Color NodeColorAv = Color.FromArgb(0xFF, 0xF8, 0xF8, 0xFF);

        private List<SocketVisual> _inputSocketsCache;
        private List<SocketVisual> _outputSocketsCache;
        private List<SocketVisual> _allSocketsOrdered;

        public NodesGraph SubsystemGraph { get; set; }

        public NodesGraph OwnerGraph { get; set; }

        /// <summary>
        /// Tag for various puposes - may be used freely.
        /// </summary>
        public int Int32Tag = 0;

        internal int CustomWidth = -1;
        internal int CustomHeight = -1;

        public float X => (float)this.BoundingBox.Left;

        public float Y => (float)this.BoundingBox.Bottom;

        public NodeType Type { get; private set; }

        internal NodeVisual(string name, double x0, double y0) : base(new RoundedRect(new Microsoft.Msagl.Core.Geometry.Rectangle(x0, y0, x0, y0), 1, 1))
        {
            this.Feedback = FeedbackType.None;
            this.Name = name;
            
            if (name == NewSpecialNodeName)
            {
                this.Type = NodeType.New;
            }
            else if (name.StartsWith(NewSubsystemNodeNamePrefix))
            {
                this.Type = NodeType.Subsystem;
            }
            else if (name.StartsWith(NewSubsystemInletNodeNamePrefix))
            {
                this.Type = NodeType.SubsystemInlet;
            }
            else if (name.StartsWith(NewSubsystemOutletNodeNamePrefix))
            {
                this.Type = NodeType.SubsystemOutlet;
            }
            else
            {
                this.Type = NodeType.Normal;
            }
        }

        public void ResetSocketsCache()
        {
            this._inputSocketsCache = null;
            this._outputSocketsCache = null;
            this._allSocketsOrdered = null;
        }

        public (List<SocketVisual> Inputs, List<SocketVisual> Outputs, List<SocketVisual> All) GetSockets()
        {
            if(_allSocketsOrdered != null)
            {
                return (_inputSocketsCache, _outputSocketsCache, _allSocketsOrdered);
            }

            var inputSocketList = new List<SocketVisual>();
            var outputSocketList = new List<SocketVisual>();
            var allSocketsList = new List<SocketVisual>();

            if (MethodInf is null)
            {
                if (this.Type == NodeType.SubsystemInlet)
                {
                    SocketVisual outSocket = new SocketVisual(this);
                    outSocket.Type = typeof(object);
                    outSocket.Name = "out-passthrough";
                    outputSocketList.Add(outSocket);
                    allSocketsList.Add(outSocket);
                }
                else if (this.Type == NodeType.SubsystemOutlet)
                {
                    SocketVisual inSocket = new SocketVisual(this);
                    inSocket.Type = typeof(object);
                    inSocket.Name = "in-passthrough";
                    inSocket.Input = true;
                    inSocket.HotInput = true;
                    inputSocketList.Add(inSocket);
                    allSocketsList.Add(inSocket);
                }
                else if (this.Type == NodeType.Subsystem)
                {
                    // TODO will need to invalidate this cache
                    // TODO: hot sockets
                    // TODO: probably create new socket objects?
                    // TOOD: clean connections? or refer only by name?
                    float inlParamsCount = this.SubsystemGraph.Nodes.Cast<NodeVisual>().Count(x => x.Type == NodeType.SubsystemInlet);
                    float outlParamsCount = this.SubsystemGraph.Nodes.Cast<NodeVisual>().Count(x => x.Type == NodeType.SubsystemOutlet);

                    foreach (NodeVisual sn in this.SubsystemGraph.Nodes)
                    {
                        if (sn.Type == NodeType.SubsystemInlet)
                        {
                            SocketVisual newInPassthrough = new SocketVisual(this)
                            {
                                Type = typeof(object),
                                Name = sn.Name,
                                Input = true,
                                HotInput = true
                            };
                            inputSocketList.Add(newInPassthrough);
                            allSocketsList.Add(newInPassthrough);
                        }
                        else if (sn.Type == NodeType.SubsystemOutlet)
                        {
                            SocketVisual newOutPassthrough = new SocketVisual(this)
                            {
                                Type = typeof(object),
                                Name = sn.Name
                            };
                            outputSocketList.Add(newOutPassthrough);
                            allSocketsList.Add(newOutPassthrough);
                        }
                    }
                }

                LayoutSockets();

                _outputSocketsCache = outputSocketList;
                _inputSocketsCache = inputSocketList;
                _allSocketsOrdered = allSocketsList;

                return (_inputSocketsCache, _outputSocketsCache, _allSocketsOrdered);
            }

            var NodeWidth = GetNodeBounds().Width;

            string[] curry = this.Name.Split(' ').Skip(1).ToArray();

            ParameterInfo[] parms = MethodInf.GetParameters().OrderBy(x => x.Position).ToArray();
            int outParamsCount = parms.Count(x => x.IsOut);
            int inParamsCount = parms.Count(x => !x.IsOut);

            for (int i = 0; i < parms.Length; i++)
            {
                ParameterInfo pp = parms[i];
                SocketVisual socket = null;
                if (pp.IsOut)
                {
                    socket = new SocketVisual(this)
                    {
                        Type = pp.ParameterType,
                        Name = pp.Name
                    };

                    outputSocketList.Add(socket);
                }
                else
                {
                    bool addCurry = false;
                    if (curry.Length > 0 && curry.Length == inParamsCount)
                    {
                        addCurry = true;
                    }
                    else if (curry.Length > 0)
                    {
                        this.Feedback = FeedbackType.Error;
                    }

                    socket = new SocketVisual(this)
                    {
                        Type = pp.ParameterType,
                        Name = pp.Name,
                        Input = true,
                        HotInput = inputSocketList.Count == 0
                    };

                    if (addCurry && curry[inputSocketList.Count] != "*")
                    {
                        socket.CurryDefault = Convert.ChangeType(curry[inputSocketList.Count], pp.ParameterType);
                    }

                    inputSocketList.Add(socket);
                }

                allSocketsList.Add(socket);
            }

            LayoutSockets();

            _outputSocketsCache = outputSocketList;
            _inputSocketsCache = inputSocketList;
            _allSocketsOrdered = allSocketsList;

            return (_inputSocketsCache, _outputSocketsCache, _allSocketsOrdered);
        
            void LayoutSockets()
            {
                for (int i = 0; i < inputSocketList.Count; i++)
                {
                    SocketVisual s = inputSocketList[i];
                    double xOffset = inputSocketList.Count == 1 ? -s.Width / 2 : i * (this.Width - s.Width) / (inputSocketList.Count - 1) - this.Width / 2;
                    s.SetLocationOffset(xOffset, -this.Height / 2);
                }

                for (int i = 0; i < outputSocketList.Count; i++)
                {
                    SocketVisual s = outputSocketList[i];
                    double xOffset = outputSocketList.Count == 1 ? -s.Width / 2 : i * (this.Width - s.Width) / (outputSocketList.Count - 1) - this.Width / 2;
                    s.SetLocationOffset(xOffset, this.Height / 2 - s.Height);
                }
            }
        }

        /// <summary>
        /// Returns current size of the node.
        /// </summary>        
        public Size GetNodeBounds()
        {
            if (this.BoundingBox.Width != 0 && this.BoundingBox.Height != 0)
            {
                return new Size(this.BoundingBox.Width, this.BoundingBox.Height);
            }

            var csize = new Size(0, 0);

            if (CustomEditorAv != null && this.Name != NewSpecialNodeName && CustomEditorAv.Width > 0 && CustomEditorAv.Height > 0)
            {
                csize = new Size((float)CustomEditorAv.Width + 10, (float)(CustomEditorAv.Height + HeaderHeight + 8 + SocketVisual.SocketHeight));
            }

            var h = HeaderHeight;

            csize = new Size(Math.Max(csize.Width, NodeWidth), Math.Max(csize.Height, h));

            if (CustomWidth >= 0)
            {
                csize = csize.WithWidth(CustomWidth);
            }

            if(CustomHeight >= 0)
            {
                csize = csize.WithHeight(CustomHeight);
            }

            this.BoundaryCurve = CurveFactory.CreateRectangle(new Microsoft.Msagl.Core.Geometry.Rectangle(this.BoundingBox.Left, this.BoundingBox.Bottom, new Microsoft.Msagl.Core.Geometry.Point(csize.Width, csize.Height)));

            return new Size((float)this.BoundingBox.Width, (float)this.BoundingBox.Height);
;
        }

        public void DrawAv(DrawingContext g, PointerPoint mouse, bool isRunMode)
        {
            var rect = new Rect(new Point((float)X, (float)Y), GetNodeBounds()).PixelAlign();

            // Draw shadow
            bool isHover = rect.Contains(mouse.Position);
            g.FillRectangle(Brushes.DarkGray, rect.Translate(isHover ? new Vector(6, 6) : new Vector(4, 4)));
            
            var feedrect = rect;
            feedrect = feedrect.Inflate(10);
            
            if (Feedback == FeedbackType.Warning)
            {
                g.DrawRectangle(new Pen(Brushes.Yellow, 3), feedrect);
            }
            else if (Feedback == FeedbackType.Error)
            {
                g.DrawRectangle(new Pen(Brushes.Red, 3), feedrect);
            }

            Color fillColor = NodeColorAv;
            
            if (IsSelected && !isRunMode)
            {
                fillColor = Colors.PaleGoldenrod;
            }
            
            g.FillRectangle(new SolidColorBrush(fillColor), rect);

            g.DrawRectangle(AvaloniaUtils.BlackPen1, rect);
            
            if (this.IsInteractive)
            {
                g.DrawLine(AvaloniaUtils.BlackPen1, new Point(rect.X + rect.Width - 5, rect.Y), new Point(rect.X + rect.Width - 5, rect.Y + rect.Height));
            }
            
            if (this.Name != NewSpecialNodeName)
            {
                FormattedText formattedText = new FormattedText(Name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, AvaloniaUtils.FontMonospaceNormal, 11, Brushes.Black);
                g.DrawText(formattedText, new Point((float)this.X + 3, (float)this.Y + HeaderHeight / 4));
            }
            
            foreach (var socet in GetSockets().All)
            {
                socet.DrawAv(g, mouse, this, isRunMode);
            }
        }

        public void Execute(INodesContext context)
        {
            context.CurrentProcessingNode = this;

            if (this.MethodInf is null)
            {
                return;
            }

            _ = this.GetSockets();
            object[] parameters = this._allSocketsOrdered.Select(x => x.Input ? x.CurriedValue : null).ToArray();

            MethodInf.Invoke(context, parameters);
            for (int i = 0; i < this._allSocketsOrdered.Count; i++)
            {
                SocketVisual sock = this._allSocketsOrdered[i];
                if (!sock.Input)
                {
                    sock.Value = parameters[i];
                }
            }

            Color orgColor = this.NodeColorAv;
            _ = Animate.Instance?.Recolor(
                this.GUID,
                orgColor,
                x =>
                {
                    this.NodeColorAv = x;
                },
                Easings.ExpIn,
                50,
                Colors.Gold).ContinueWith(
                    t => Animate.Instance?.Recolor(
                        this.GUID,
                        this.NodeColorAv,
                        y =>
                        {
                            this.NodeColorAv = y;
                        },
                        Easings.CubicOut,
                        100,
                        orgColor)
                );
        }

        public void LayoutEditor()
        {
            if (CustomEditorAv != null)
            {
                if (this.Name == NewSpecialNodeName)
                {
                    CustomEditorAv[Avalonia.Controls.Canvas.LeftProperty] = 4d + this.X;
                    CustomEditorAv[Avalonia.Controls.Canvas.TopProperty] = 8 + this.Y;
                }
                else
                {
                    CustomEditorAv[Avalonia.Controls.Canvas.LeftProperty] = 4d + this.X;
                    CustomEditorAv[Avalonia.Controls.Canvas.TopProperty] = HeaderHeight + 4 + this.Y;
                }
            }
        }
    }
}
