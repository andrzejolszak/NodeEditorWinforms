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
        }

        [Node(true)]
        public void Bang(out Bang bang)
        {
            bang = NodeEditor.Bang.Instance;
        }

        [Node(customEditor: typeof(Label))]
        public void Flipper(Bang bangIn, out Bang bangOut)
        {
            if (this.CurrentProcessingNode.UserData is null)
            {
                this.CurrentProcessingNode.UserData = false;
            }

            bangOut = bangIn;
            this.CurrentProcessingNode.UserData = (bool)this.CurrentProcessingNode.UserData ? false : true;
            this.CurrentProcessingNode.CustomEditor.Text = " ";
            this.CurrentProcessingNode.CustomEditor.BackColor = (bool)this.CurrentProcessingNode.UserData ? Color.Green : Color.Red;
        }

        [Node(true)]
        public void Counter(Bang bangIn, out Bang bangOut)
        {
            if (this.CurrentProcessingNode.UserData is null)
            {
                this.CurrentProcessingNode.UserData = 0;
            }

            this.CurrentProcessingNode.UserData = (int)this.CurrentProcessingNode.UserData + 1;

            if ((int)this.CurrentProcessingNode.UserData == 2)
            {
                this.CurrentProcessingNode.UserData = 0;
                bangOut = bangIn;
            }
            else
            {
                bangOut = null;
            }
        }

        [Node(true)]
        public void CounterC(Bang bangIn, int count, out Bang bangOut)
        {
            if (this.CurrentProcessingNode.UserData is null)
            {
                this.CurrentProcessingNode.UserData = 0;
            }

            this.CurrentProcessingNode.UserData = (int)this.CurrentProcessingNode.UserData + 1;

            if ((int)this.CurrentProcessingNode.UserData == count)
            {
                this.CurrentProcessingNode.UserData = 0;
                bangOut = bangIn;
            }
            else
            {
                bangOut = null;
            }
        }

        [Node]
        public void Compare(int a, string @operator, int b, out bool result)
        {
            result = false;
            switch (@operator)
            {
                case "<": result = a < b; break;
                case "<=": result = a <= b; break;
                case ">": result = a > b; break;
                case ">=": result = a >= b; break;
                case "==": result = a == b; break;
                case "!=": result = a != b; break;

                default: this.FeedbackInfo?.Invoke("Unknown operator", this.CurrentProcessingNode, FeedbackType.Error, null, false); break;
            };
        }

        [Node]
        public void AggrAnd(bool input, Bang resetBang, out bool res)
        {
            if (resetBang != null || this.CurrentProcessingNode.UserData is null)
            {
                this.CurrentProcessingNode.UserData = true;
            }

            this.CurrentProcessingNode.UserData = (bool)this.CurrentProcessingNode.UserData && input;
            res = (bool)this.CurrentProcessingNode.UserData;
        }

        [Node(false, invokeOnLoad: true)]
        public void LoadBang(out Bang bang)
        {
            bang = NodeEditor.Bang.Instance;
        }
    }
}
