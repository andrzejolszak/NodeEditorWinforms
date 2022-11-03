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
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace NodeEditor
{
    /// <summary>
    /// Class that represents one instance of node.
    /// </summary>
    public class NodeVisual
    {
        public const string NewName = "*new*";
        public const float NodeWidth = 140;
        public const float HeaderHeight = 32;
        public const float ComponentPadding = 2;

        /// <summary>
        /// Current node name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Current node position X coordinate.
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Current node position Y coordinate.
        /// </summary>
        public float Y { get; set; }
        internal MethodInfo MethodInf { get; set; }
        internal int Order { get; set; }
        public bool IsInteractive { get; set; }
        public bool IsSelected { get; set; }
        public FeedbackType Feedback { get; set; }
        public object nodeContext { get; set; } 
        public Control CustomEditor { get; internal set; }
        public string GUID = Guid.NewGuid().ToString();
        public Color NodeColor = Color.LightCyan;
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

        internal NodeVisual()
        {
            Feedback = FeedbackType.Debug;
        }

        public string GetGuid()
        {
            return GUID;
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
            for (int i = 0; i < parms.Length; i++)
            {
                ParameterInfo pp = parms[i];
                SocketVisual socket = null;
                if (pp.IsOut)
                {
                    socket = new SocketVisual(this);
                    socket.Type = pp.ParameterType;
                    socket.Height = SocketVisual.SocketHeight;
                    socket.Name = pp.Name;
                    socket.Width = SocketVisual.SocketWidth;
                    socket.DX = i * (NodeWidth - SocketVisual.SocketWidth);
                    socket.DY = HeaderHeight - SocketVisual.SocketHeight;

                    outputSocketList.Add(socket);
                }
                else
                {
                    socket = new SocketVisual(this);
                    socket.Type = pp.ParameterType;
                    socket.Height = SocketVisual.SocketHeight;
                    socket.Name = pp.Name;
                    socket.Width = SocketVisual.SocketWidth;
                    socket.DX = i * (NodeWidth - SocketVisual.SocketWidth);
                    socket.DY = 0;
                    socket.Input = true;
                    socket.HotInput = i == 0;

                    inputSocketList.Add(socket);
                }

                allSocketsList.Add(socket);
            }

            foreach (SocketVisual s in outputSocketList)
            {
                s.DX /= outputSocketList.Count;
            }

            foreach (SocketVisual s in inputSocketList)
            {
                s.DX /= inputSocketList.Count;
            }

            _outputSocketsCache = outputSocketList;
            _inputSocketsCache = inputSocketList;
            _allSocketsOrdered = allSocketsList;

            return (_inputSocketsCache, _outputSocketsCache, _allSocketsOrdered);
        }

        internal ParameterInfo[] GetInputs()
        {
            return MethodInf.GetParameters().Where(x => !x.IsOut).OrderBy(x => x.Position).ToArray();
        }

        internal ParameterInfo[] GetOutputs()
        {
            return MethodInf.GetParameters().Where(x => x.IsOut).OrderBy(x => x.Position).ToArray();
        }

        /// <summary>
        /// Returns current size of the node.
        /// </summary>        
        public SizeF GetNodeBounds()
        {
            var csize = new SizeF();
            if (CustomEditor != null && CustomEditor.ClientSize != default && this.Name != NewName)
            {
                csize = new SizeF(CustomEditor.ClientSize.Width + 2 + 80 +SocketVisual.SocketHeight*2,
                    CustomEditor.ClientSize.Height + HeaderHeight + 8);                
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

            return new SizeF(csize.Width, csize.Height);
        }

        /// <summary>
        /// Returns current size of node caption (header belt).
        /// </summary>
        /// <returns></returns>
        public SizeF GetHeaderSize()
        {
            return new SizeF(GetNodeBounds().Width, HeaderHeight);
        }

        /// <summary>
        /// Allows node to be drawn on given Graphics context.       
        /// </summary>
        /// <param name="g">Graphics context.</param>
        /// <param name="mouseLocation">Location of the mouse relative to NodesControl instance.</param>
        /// <param name="mouseButtons">Mouse buttons that are pressed while drawing node.</param>
        public void Draw(Graphics g, Point mouseLocation, MouseButtons mouseButtons)
        {
            var rect = new RectangleF(new PointF(X,Y), GetNodeBounds());

            var feedrect = rect;
            feedrect.Inflate(10, 10);

            if (Feedback == FeedbackType.Warning)
            {
                g.DrawRectangle(new Pen(Color.Yellow, 4), Rectangle.Round(feedrect));
            }
            else if (Feedback == FeedbackType.Error)
            {
                g.DrawRectangle(new Pen(Color.Red, 5), Rectangle.Round(feedrect));
            }

            var caption = new RectangleF(new PointF(X,Y), GetHeaderSize());
            bool mouseHoverCaption = caption.Contains(mouseLocation);

            g.FillRectangle(new SolidBrush(NodeColor), rect);

            if (IsSelected)
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(180,Color.WhiteSmoke)), rect);
                g.FillRectangle(mouseHoverCaption ? Brushes.Gold : Brushes.Goldenrod, caption);
            }
            else
            {                
                g.FillRectangle(mouseHoverCaption ? Brushes.Cyan : Brushes.Aquamarine, caption);
            }

            g.DrawRectangle(Pens.Gray, Rectangle.Round(caption));
            g.DrawRectangle(Pens.Black, Rectangle.Round(rect));

            if (this.IsInteractive)
            {
                g.DrawLine(Pens.Black, rect.X + rect.Width - 5, rect.Y, rect.X + rect.Width - 5, rect.Y + rect.Height);
            }

            if (this.Name != NewName)
            {
                g.DrawString(Name, SystemFonts.DefaultFont, Brushes.Black, new PointF(X + 3, Y + HeaderHeight / 4));
            }

            var sockets = GetSockets();
            foreach (var socet in sockets.Inputs.Concat(sockets.Outputs))
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
                    CustomEditor.Location = new Point((int)X + 1, (int)Y + 1);
                }
                else
                {
                    CustomEditor.Location = new Point((int)(X + 1 + 40 + SocketVisual.SocketHeight), (int)(Y + HeaderHeight + 4));
                }
            }
        }
    }
}
