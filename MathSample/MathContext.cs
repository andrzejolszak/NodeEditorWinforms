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
        public readonly Action<NodeVisual> RunNode;

        public NodeVisual CurrentProcessingNode { get; set; }

        public event Action<string, NodeVisual, FeedbackType, object, bool> FeedbackInfo;

        public event Action<bool> RunModeToggled;

        public HashSet<Control> CustomEditors = new HashSet<Control>();

        public MathContext(Action<NodeVisual> runNode)
        {
            this.RunNode = runNode;
        }

        [Node("Value", "Input", "Basic", "Allows to output a simple value.")]
        public void InputValue(float inValue, out float outValue)
        {
            outValue = inValue;
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

        [Node("Show Value","Helper","Basic","Shows input value in the message box")]
        public void ShowMessageBox(object bang, object x)
        {
            MessageBox.Show(x.ToString(), "Show Value", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        [Node("Bang", "Helper","Basic","Starts execution", true)]
        public void Bang(out bool bang)
        {
            bang = true;
        }

        [Node("LoadBang", "Helper", "Basic", "Starts execution", false, typeof(LoadBang))]
        public void LoadBang(out bool bang)
        {
            bang = true;
        }

        public void OnRunModeToggled(bool isRunMode)
        {
            this.RunModeToggled?.Invoke(isRunMode);
        }
    }

    public class LoadBang : Label
    {
        public LoadBang()
        {
            this.Text = "LoadBang";
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            var t = this.Tag as (NodeVisual, INodesContext)?;
            (t.Value.Item2 as MathContext).RunModeToggled += LoadBang_RunModeToggled;
        }

        private void LoadBang_RunModeToggled(bool isRunMode)
        {
            if (!isRunMode)
            {
                return;
            }

            var t = this.Tag as (NodeVisual, INodesContext)?;
            (t.Value.Item2 as MathContext).RunNode(t.Value.Item1);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            var t = this.Tag as (NodeVisual, INodesContext)?;
            (t.Value.Item2 as MathContext).RunModeToggled -= LoadBang_RunModeToggled;
        }

        private void LoadBang_VisibleChanged(object sender, EventArgs e)
        {
            if (this.Visible)
            {
                // (t.Value.Item2 as MathContext).;
            }
        }
    }
}
