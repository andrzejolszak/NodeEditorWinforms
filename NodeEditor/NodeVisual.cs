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
using System.Reflection;
using System.Windows.Forms;

namespace NodeEditor
{
    /// <summary>
    /// Class that represents one instance of node.
    /// </summary>
    public class NodeVisual : Node
    {
        public const string NewName = "*new*";
        public const float NodeWidth = 140;
        public const float HeaderHeight = 32;

        /// <summary>
        /// Current node name.
        /// </summary>
        public string Name { get; set; }

        internal MethodInfo MethodInf { get; set; }
        internal int Order { get; set; }
        public bool IsInteractive { get; set; }
        public bool IsSelected { get; set; }
        public bool InvokeOnLoad { get; set; }
        public FeedbackType Feedback { get; set; }
        public Control CustomEditor { get; internal set; }
        public string GUID = Guid.NewGuid().ToString();
        public Color NodeColor = System.Drawing.Color.GhostWhite;
        private List<SocketVisual> _inputSocketsCache;
        private List<SocketVisual> _outputSocketsCache;
        private List<SocketVisual> _allSocketsOrdered;

        /// <summary>
        /// Tag for various puposes - may be used freely.
        /// </summary>
        public int Int32Tag = 0;
        public string XmlExportName { get; internal set; }

        internal int CustomWidth = -1;
        internal int CustomHeight = -1;

        public float X => (float)this.BoundingBox.Left;

        public float Y => (float)this.BoundingBox.Bottom;

        internal NodeVisual(double x0, double y0) : base(new RoundedRect(new Microsoft.Msagl.Core.Geometry.Rectangle(x0, y0, x0, y0), 1, 1))
        {
            Feedback = FeedbackType.Debug;
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

            var NodeWidth = GetNodeBounds().Width;

            ParameterInfo[] parms = MethodInf.GetParameters().OrderBy(x => x.Position).ToArray();
            int outParamsCount = parms.Count(x => x.IsOut);
            int inParamsCount = parms.Count(x => !x.IsOut);
            for (int i = 0; i < parms.Length; i++)
            {
                ParameterInfo pp = parms[i];
                SocketVisual socket = null;
                if (pp.IsOut)
                {
                    socket = new SocketVisual(this, new Microsoft.Msagl.Core.Geometry.Point(i * (NodeWidth - SocketVisual.SocketWidth) / outParamsCount - NodeWidth / 2, this.Height / 2 - SocketVisual.SocketHeight));
                    socket.Type = pp.ParameterType;
                    socket.Name = pp.Name;

                    outputSocketList.Add(socket);
                }
                else
                {
                    socket = new SocketVisual(this, new Microsoft.Msagl.Core.Geometry.Point(i * ( NodeWidth - SocketVisual.SocketWidth) / inParamsCount - NodeWidth / 2, -this.Height / 2));
                    socket.Type = pp.ParameterType;
                    socket.Name = pp.Name;
                    socket.Input = true;
                    socket.HotInput = i == 0;

                    inputSocketList.Add(socket);
                }

                allSocketsList.Add(socket);
            }

            _outputSocketsCache = outputSocketList;
            _inputSocketsCache = inputSocketList;
            _allSocketsOrdered = allSocketsList;

            return (_inputSocketsCache, _outputSocketsCache, _allSocketsOrdered);
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
            if (CustomEditor != null && CustomEditor.ClientSize != default && this.Name != NewName)
            {
                csize = new SizeF(CustomEditor.ClientSize.Width, CustomEditor.ClientSize.Height + HeaderHeight + 8);                
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
        public void Draw(Graphics g, Point mouseLocation, MouseButtons mouseButtons)
        {
            var rect = new RectangleF(new PointF((float)this.X, (float)this.Y), GetNodeBounds());

            var feedrect = rect;
            feedrect.Inflate(10, 10);

            if (Feedback == FeedbackType.Warning)
            {
                g.DrawRectangle(new Pen(System.Drawing.Color.Yellow, 4), Rectangle.Round(feedrect));
            }
            else if (Feedback == FeedbackType.Error)
            {
                g.DrawRectangle(new Pen(System.Drawing.Color.Red, 5), Rectangle.Round(feedrect));
            }

            Color fillColor = NodeColor;

            bool isHover = rect.Contains(mouseLocation);
            if (isHover)
            {
                fillColor = Color.White;
            }

            if (IsSelected)
            {
                fillColor = Color.PaleGoldenrod;
            }

            g.FillRectangle(new SolidBrush(fillColor), rect);

            if (this.CustomEditor != null)
            {
                this.CustomEditor.BackColor = fillColor;
            }

            g.DrawRectangle(Pens.Black, Rectangle.Round(rect));

            if (this.IsInteractive)
            {
                g.DrawLine(Pens.Black, rect.X + rect.Width - 5, rect.Y, rect.X + rect.Width - 5, rect.Y + rect.Height);
            }

            if (this.Name != NewName)
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

            _ = this.GetSockets();
            object[] parameters = this._allSocketsOrdered.Select(x => x.Input ? x.Value : null).ToArray();

            MethodInf.Invoke(context, parameters);
            for (int i = 0; i < this._allSocketsOrdered.Count; i++)
            {
                SocketVisual sock = this._allSocketsOrdered[i];
                if (sock.Input)
                {
                    continue;
                }

                sock.Value = parameters[i];
            }
        }

        internal void LayoutEditor()
        {
            if (CustomEditor != null)
            {
                if (this.Name == NewName)
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
