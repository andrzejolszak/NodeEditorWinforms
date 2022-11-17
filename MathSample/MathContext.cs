using System;
using System.Collections.Generic;
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

        [Node("Value", "Input", "Basic", "Allows to output a simple value.")]
        public void InputValue(float inValue, out float outValue)
        {
            outValue = inValue == 0 ? 32 : inValue;
        }

        [Node("Time", "Input", "Basic", "Allows to output a simple value.")]
        public void Time(bool bang, out string outValue)
        {
            outValue = DateTime.UtcNow.ToLongTimeString();
        }

        [Node("Add","Operators","Basic","Adds two input values.")]
        public void Add(float a, float b, out float result, out int sign)
        {
            result = a + b;
            sign = Math.Sign(result);
        }

        [Node("Substract", "Operators", "Basic", "Substracts two input values.")]
        public void Substract(float a, float b, out float result)
        {
            result = a - b;
        }

        [Node("Multiply", "Operators", "Basic", "Multiplies two input values.")]
        public void Multiplty(float a, float b, out float result)
        {
            result = a * b;
        }

        [Node("Divide", "Operators", "Basic", "Divides two input values.")]
        public void Divid(float a, float b, out float result)
        {
            result = a / b;
        }

        [Node("Show Value","Helper","Basic","Shows input value in the message box", customEditor: typeof(Label))]
        public void ShowMessageBox(object bang, object x)
        {
            this.CurrentProcessingNode.CustomEditor.Text = x?.ToString() ?? "NULL";
           // MessageBox.Show(x.ToString(), "Show Value", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        [Node("Bang", "Helper","Basic","Starts execution", true)]
        public void Bang(out Bang bang)
        {
            bang = NodeEditor.Bang.Instance;
        }

        [Node("LoadBang", "Helper", "Basic", "Starts execution", false, invokeOnLoad: true)]
        public void LoadBang(out Bang bang)
        {
            bang = NodeEditor.Bang.Instance;
        }
    }
}
