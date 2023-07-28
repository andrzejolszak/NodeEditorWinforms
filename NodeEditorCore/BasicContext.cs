
namespace NodeEditor
{
    public class BasicContext : INodesContext
    {
        public NodeVisual CurrentProcessingNode { get; set; }

        public Bang? CurrentTriggerBang { get; set; }

        public event Action<string, NodeVisual, FeedbackType, object, bool> FeedbackInfo;

        public void RaiseExecutionError(string message)
        {
            this.FeedbackInfo?.Invoke(message, this.CurrentProcessingNode, FeedbackType.Error, null, true);
        }

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

        [Node(customEditor: typeof(Label))]
        public void ShowMessageBox(object bang, object x)
        {
            if (this.CurrentProcessingNode.CustomEditorAv is not null)
                (this.CurrentProcessingNode.CustomEditorAv as Label).Content = x?.ToString() ?? "NULL";
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

            if (this.CurrentProcessingNode.CustomEditorAv is not null)
            {
                (this.CurrentProcessingNode.CustomEditorAv as Label).Content = " ";
                (this.CurrentProcessingNode.CustomEditorAv as Label).Background = (bool)this.CurrentProcessingNode.UserData ? Brushes.Green : Brushes.Red;
            }
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

        [Node(true, hideName: true)]
        public void Number(int inValue, out int? outValue)
        {
            outValue = inValue;
        }

        [Node]
        public void Op(int? a, string @operator, int? b, out int? result)
        {
            result = null;
            switch (@operator)
            {
                case "+": result = a + b; break;
                case "-": result = a - b; break;
                case "*": result = a * b; break;
                case "/": result = a / b; break;

                default: this.FeedbackInfo?.Invoke("Unknown operator", this.CurrentProcessingNode, FeedbackType.Error, null, false); break;
            };
        }

        [Node(alias: "+")]
        public void Plus(int? a, int? b, out int? result)
        {
            result = a + b;
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
