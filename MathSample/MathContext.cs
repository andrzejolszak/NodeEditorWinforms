using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NodeEditor;

namespace MathSample
{
    // Main context of the sample, each
    // method corresponds to a node by attribute decoration
    public class MathContext : INodesContext
    {
        public NodeVisual CurrentProcessingNode { get; set; }

        public event Action<string, NodeVisual, FeedbackType, object, bool> FeedbackInfo;

        [Node]
        public void InputValue(float inValue, out float outValue)
        {
            outValue = inValue == 0 ? 32 : inValue;
        }

        [Node]
        public void Time(bool bang, out string outValue)
        {
            outValue = DateTime.UtcNow.ToLongTimeString();
        }

        [Node]
        public void Add(float a, float b, out float result, out int sign)
        {
            result = a + b;
            sign = Math.Sign(result);
        }

        [Node]
        public void Substract(float a, float b, out float result)
        {
            result = a - b;
        }

        [Node]
        public void Multiplty(float a, float b, out float result)
        {
            result = a * b;
        }

        [Node]
        public void Divid(float a, float b, out float result)
        {
            result = a / b;
        }

        [Node(customEditor: typeof(Label))]
        public void ShowMessageBox(object bang, object x)
        {
            this.CurrentProcessingNode.CustomEditor.Text = x?.ToString() ?? "NULL";
           // MessageBox.Show(x.ToString(), "Show Value", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        [Node(true)]
        public void Bang(out Bang bang)
        {
            bang = NodeEditor.Bang.Instance;
        }

        [Node(customEditor: typeof(Label))]
        public void Flipper(Bang bangIn, out Bang bangOut)
        {
            bangOut = bangIn;
            this.CurrentProcessingNode.UserData = this.CurrentProcessingNode.UserData == "true" ? "false" : "true";
            this.CurrentProcessingNode.CustomEditor.Text = " ";
            this.CurrentProcessingNode.CustomEditor.BackColor = this.CurrentProcessingNode.UserData == "true" ? Color.Green : Color.Red;
        }

        [Node(true)]
        public void Counter(Bang bangIn, out Bang bangOut)
        {
            // TODO: paremeterized nodes - params as inputs?
            if (this.CurrentProcessingNode.UserData?.ToString() == "**")
            {
                this.CurrentProcessingNode.UserData = "";
                bangOut = bangIn;
            }
            else
            {
                this.CurrentProcessingNode.UserData = (this.CurrentProcessingNode.UserData?.ToString() ?? "") + "*";
                bangOut = null;
            }
        }

        [Node(true)]
        public void CounterC(Bang bangIn, int count, out Bang bangOut)
        {
            // TODO: paremeterized nodes - params as inputs?
            if (this.CurrentProcessingNode.UserData?.ToString().Length == count)
            {
                this.CurrentProcessingNode.UserData = "";
                bangOut = bangIn;
            }
            else
            {
                this.CurrentProcessingNode.UserData = (this.CurrentProcessingNode.UserData?.ToString() ?? "") + "*";
                bangOut = null;
            }
        }

        [Node(false, invokeOnLoad: true)]
        public void LoadBang(out Bang bang)
        {
            bang = NodeEditor.Bang.Instance;
        }
    }
}
