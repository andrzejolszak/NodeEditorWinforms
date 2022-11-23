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

using Microsoft.Msagl.Core.Layout;
using System.Linq;

namespace NodeEditor
{    
    internal class NodeConnection : Edge
    {
        public NodeVisual OutputNode => this.Source as NodeVisual;
        public string OutputSocketName { get; }
        public NodeVisual InputNode => this.Target as NodeVisual;
        public string InputSocketName { get; }

        public NodeConnection(NodeVisual outputNode, string outputSocketName, NodeVisual inputNode, string inputSocketName)
            : base(outputNode, inputNode)
        {
            OutputSocketName = outputSocketName;
            InputSocketName = inputSocketName;
        }

        public SocketVisual OutputSocket => OutputNode.GetSockets().Outputs.FirstOrDefault(x => x.Name == OutputSocketName);

        public SocketVisual InputSocket => InputNode.GetSockets().Inputs.FirstOrDefault(x => x.Name == InputSocketName);
    }
}
