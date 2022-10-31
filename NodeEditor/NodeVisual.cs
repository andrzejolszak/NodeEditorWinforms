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
        private List<SocketVisual> _socketsCache;

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

        internal List<SocketVisual> GetSockets()
        {
            if(_socketsCache != null)
            {
                return _socketsCache;
            }

            var socketList = new List<SocketVisual>();

            var NodeWidth = GetNodeBounds().Width;

            ParameterInfo[] inputs = GetInputs();
            float len = inputs.Length > 1 ? inputs.Length - 1 : 1;
            for (int i = 0; i < inputs.Length; i++)
            {
                ParameterInfo input = inputs[i];
                var socket = new SocketVisual(this);
                socket.Type = input.ParameterType;
                socket.Height = SocketVisual.SocketHeight;
                socket.Name = input.Name;
                socket.Width = SocketVisual.SocketWidth;
                socket.DX = i * ((NodeWidth - SocketVisual.SocketWidth) / len);
                socket.DY = 0;
                socket.Input = true;

                socketList.Add(socket);
            }
            var ctx = GetNodeContext() as DynamicNodeContext;
            ParameterInfo[] outputs = GetOutputs();
            len = outputs.Length > 1 ? outputs.Length - 1 : 1;
            for (int i = 0; i < outputs.Length; i++)
            {
                ParameterInfo output = outputs[i];
                var socket = new SocketVisual(this);
                socket.Type = output.ParameterType;
                socket.Height = SocketVisual.SocketHeight;
                socket.Name = output.Name;
                socket.Width = SocketVisual.SocketWidth;
                socket.DX = i * ((NodeWidth - SocketVisual.SocketWidth) / len);
                socket.DY = HeaderHeight - SocketVisual.SocketHeight;
                socket.Value = ctx[socket.Name];              
                socketList.Add(socket);
            }

            _socketsCache = socketList;
            return _socketsCache;
        }

        /// <summary>
        /// Returns node context which is dynamic type. It will contain all node default input/output properties.
        /// </summary>
        public object GetNodeContext()
        {
            const string stringTypeName = "System.String";

            if (nodeContext == null)
            {                
                dynamic context = new DynamicNodeContext();

                foreach (var input in GetInputs())
                {
                    var contextName = input.Name.Replace(" ", "");
                    if (input.ParameterType.FullName.Replace("&", "") == stringTypeName)
                    {
                        context[contextName] = string.Empty;
                    }
                    else
                    {
                        try
                        {
                            context[contextName] = Activator.CreateInstance(AppDomain.CurrentDomain, input.ParameterType.Assembly.GetName().Name,
                            input.ParameterType.FullName.Replace("&", "").Replace(" ", "")).Unwrap();
                        }
                        catch (MissingMethodException ex) //For case when type does not have default constructor
                        {
                            context[contextName] = null;
                        }
                    }
                }
                foreach (var output in GetOutputs())
                {
                    var contextName = output.Name.Replace(" ", "");
                    if (output.ParameterType.FullName.Replace("&", "") == stringTypeName)
                    {
                        context[contextName] = string.Empty;
                    }
                    else
                    {
                        try
                        {
                            context[contextName] = Activator.CreateInstance(AppDomain.CurrentDomain, output.ParameterType.Assembly.GetName().Name,
                            output.ParameterType.FullName.Replace("&", "").Replace(" ", "")).Unwrap();
                        }
                        catch(MissingMethodException ex) //For case when type does not have default constructor
                        {
                            context[contextName] = null;
                        }
                    }
                }

                nodeContext = context;
            }
            return nodeContext;
        }

        internal ParameterInfo[] GetInputs()
        {
            return MethodInf.GetParameters().Where(x => !x.IsOut).ToArray();
        }

        internal ParameterInfo[] GetOutputs()
        {
            return MethodInf.GetParameters().Where(x => x.IsOut).ToArray();
        }

        /// <summary>
        /// Returns current size of the node.
        /// </summary>        
        public SizeF GetNodeBounds()
        {
            var csize = new SizeF();
            if (CustomEditor != null)
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

            g.DrawString(Name, SystemFonts.DefaultFont, Brushes.Black, new PointF(X + 3, Y + HeaderHeight/4));       

            var sockets = GetSockets();
            foreach (var socet in sockets)
            {
                socet.Draw(g, mouseLocation, mouseButtons);
            }
        }

        public void Execute(INodesContext context)
        {
            context.CurrentProcessingNode = this;

            var dc = (GetNodeContext() as DynamicNodeContext);
            var parametersDict = MethodInf.GetParameters().OrderBy(x => x.Position).ToDictionary(x => x.Name, x => dc[x.Name]);
            var parameters = parametersDict.Values.ToArray();

            int ndx = 0;
            MethodInf.Invoke(context, parameters);
            foreach (var kv in parametersDict.ToArray())
            {
                parametersDict[kv.Key] = parameters[ndx];
                ndx++;
            }

            var outs = GetSockets();

            
            foreach (var parameter in dc.ToArray())
            {
                dc[parameter] = parametersDict[parameter];
                var o = outs.FirstOrDefault(x => x.Name == parameter);
                //if (o != null)
                Debug.Assert(o != null, "Output not found");
                {
                    o.Value = dc[parameter];
                }                                
            }
        }

        internal void LayoutEditor()
        {
            if (CustomEditor != null)
            {
                CustomEditor.Location = new Point((int)( X + 1 + 40 + SocketVisual.SocketHeight), (int) (Y + HeaderHeight + 4));
            }
        }
    }
}
