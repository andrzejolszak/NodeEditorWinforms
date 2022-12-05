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

using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Linq;

namespace NodeEditor
{
    /// <summary>
    /// Class that represents one instance of node.
    /// </summary>
    public class NodeVisual : Node
    {
        public enum NodeType
        {
            Normal,
            New,
            Subsystem,
            Inlet,
            Outlet
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
        internal int Order { get; set; }
        public bool IsInteractive { get; set; }
        public bool IsSelected { get; set; }
        public bool InvokeOnLoad { get; set; }
        public FeedbackType Feedback { get; set; }
        public Control CustomEditor { get; internal set; }
        public string GUID = Guid.NewGuid().ToString();
        public Color NodeColor = Color.FromArgb(0xFF, 0xF8, 0xF8, 0xFF);
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
            this.Feedback = FeedbackType.Debug;
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
                this.Type = NodeType.Inlet;
            }
            else if (name.StartsWith(NewSubsystemOutletNodeNamePrefix))
            {
                this.Type = NodeType.Outlet;
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

        internal (List<SocketVisual> Inputs, List<SocketVisual> Outputs, List<SocketVisual> All) GetSockets()
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
                if (this.Type == NodeType.Inlet)
                {
                    SocketVisual outSocket = new SocketVisual(this);
                    outSocket.Type = typeof(object);
                    outSocket.Name = "out-passthrough";
                    outputSocketList.Add(outSocket);
                    allSocketsList.Add(outSocket);
                }
                else if (this.Type == NodeType.Outlet)
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
                    float inlParamsCount = this.SubsystemGraph.Nodes.Count(x => x.Type == NodeType.Inlet);
                    float outlParamsCount = this.SubsystemGraph.Nodes.Count(x => x.Type == NodeType.Outlet);

                    foreach (NodeVisual sn in this.SubsystemGraph.Nodes)
                    {
                        if (sn.Type == NodeType.Inlet)
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
                        else if (sn.Type == NodeType.Outlet)
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
        public SizeF GetNodeBounds()
        {
            if (this.BoundingBox.Width != 0 && this.BoundingBox.Height != 0)
            {
                return new SizeF((float)this.BoundingBox.Width, (float)this.BoundingBox.Height);
            }

            var csize = new SizeF();
            if (CustomEditor != null && CustomEditor.ClientSize != default && this.Name != NewSpecialNodeName)
            {
                csize = new SizeF(CustomEditor.ClientSize.Width, CustomEditor.ClientSize.Height + HeaderHeight + 8 + SocketVisual.SocketHeight);                
            }

            var h = HeaderHeight;

            csize.Width = Math.Max(csize.Width, NodeWidth);
            csize.Height = Math.Max(csize.Height, h);

            if(CustomWidth >= 0)
            {
                csize.Width = CustomWidth;
            }

            if(CustomHeight >= 0)
            {
                csize.Height = CustomHeight;
            }

            this.BoundingBox = new Microsoft.Msagl.Core.Geometry.Rectangle(this.BoundingBox.Left, this.BoundingBox.Bottom, new Microsoft.Msagl.Core.Geometry.Point(csize.Width, csize.Height));

            return new SizeF((float)this.BoundingBox.Width, (float)this.BoundingBox.Height);
;
        }

        /// <summary>
        /// Allows node to be drawn on given Graphics context.       
        /// </summary>
        /// <param name="g">Graphics context.</param>
        /// <param name="mouseLocation">Location of the mouse relative to NodesControl instance.</param>
        /// <param name="mouseButtons">Mouse buttons that are pressed while drawing node.</param>
        public void Draw(Graphics g, Point mouseLocation, MouseButtons mouseButtons, bool isRunMode)
        {
            var rect = new RectangleF(new PointF((float)this.X, (float)this.Y), GetNodeBounds());

            // Draw shadow
            bool isHover = rect.Contains(mouseLocation);
            int offset = isHover ? 6 : 4;
            g.FillRectangle(Brushes.DarkGray, new RectangleF(new PointF(this.X + offset, this.Y + offset), rect.Size));
            
            var feedrect = rect;
            feedrect.Inflate(10, 10);

            if (Feedback == FeedbackType.Warning)
            {
                g.DrawRectangle(new Pen(Color.Yellow, 3), Rectangle.Round(feedrect));
            }
            else if (Feedback == FeedbackType.Error)
            {
                g.DrawRectangle(new Pen(Color.Red, 3), Rectangle.Round(feedrect));
            }

            Color fillColor = NodeColor;

            if (IsSelected && !isRunMode)
            {
                fillColor = Color.PaleGoldenrod;
            }

            g.FillRectangle(new SolidBrush(fillColor), rect);

            if (this.CustomEditor != null && (
                this.CustomEditor.BackColor == NodeColor 
                || this.CustomEditor.BackColor == Color.White 
                || this.CustomEditor.BackColor == Color.PaleGoldenrod 
                || this.CustomEditor.BackColor == SystemColors.Control 
                || this.CustomEditor.BackColor == SystemColors.Window))
            {
                this.CustomEditor.BackColor = fillColor;
            }

            g.DrawRectangle(Pens.Black, Rectangle.Round(rect));

            if (this.IsInteractive)
            {
                g.DrawLine(Pens.Black, rect.X + rect.Width - 5, rect.Y, rect.X + rect.Width - 5, rect.Y + rect.Height);
            }

            if (this.Name != NewSpecialNodeName)
            {
                g.DrawString(Name, SystemFonts.DefaultFont, Brushes.Black, new PointF((float)this.X + 3, (float)this.Y + HeaderHeight / 4));
            }

            foreach (var socet in GetSockets().All)
            {
                socet.Draw(g, mouseLocation, mouseButtons);
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
        }

        internal void LayoutEditor()
        {
            if (CustomEditor != null)
            {
                if (this.Name == NewSpecialNodeName)
                {
                    CustomEditor.Location = new Point((int)this.X + 4, (int)this.Y + 8);
                }
                else
                {
                    CustomEditor.Location = new Point((int)(this.X + 4), (int)(this.Y + HeaderHeight + 4));
                }
            }
        }
    }
}
