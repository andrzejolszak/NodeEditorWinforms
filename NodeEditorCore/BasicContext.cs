﻿
using static SkiaSharp.SKPath;

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

        public virtual List<NodeDescriptor> GetNodeDescriptors()
        {
            List<NodeDescriptor> descriptors = new List<NodeDescriptor>();

            descriptors.Add(new NodeDescriptor(
                "InputValue",
                (c, i) =>
                {
                    float? i0 = (float?)i[0];
                    return new object[] { i0 == null ? -1 : i0 };
                })
                .WithInput<float>("inValue")
                .WithOutput<float>("outValue"));

            descriptors.Add(new NodeDescriptor(
                "Time",
                (c, i) =>
                {
                    object[] res = new object[1];
                    res[0] = DateTime.UtcNow.ToLongTimeString();
                    return res;
                })
                .WithInput<bool>("bang")
                .WithOutput<string>("outValue"));

            descriptors.Add(new NodeDescriptor(
                "ShowMessageBox",
                (c, i) =>
                {
                    if (c.CurrentProcessingNode.CustomEditorAv is not null)
                        (c.CurrentProcessingNode.CustomEditorAv as Label).Content = i[1]?.ToString() ?? "NULL";

                    return null;
                })
                .WithCustomEditor(x => new Label())
                .WithInput<object>("bang")
                .WithInput<object>("x"));

            descriptors.Add(new NodeDescriptor(
                "Bang",
                (c, i) =>
                {
                    return new object[1] { NodeEditor.Bang.Instance };
                },
                isInteractive: true)
                .WithOutput<Bang>("bang"));

            descriptors.Add(new NodeDescriptor(
                "LoadBang",
                (c, i) =>
                {
                    return new object[1] { Bang.Instance };
                },
                invokeOnLoad: true)
                .WithOutput<Bang>("bang"));

            descriptors.Add(new NodeDescriptor(
                "Flipper",
                (c, i) =>
                {
                    if (c.CurrentProcessingNode.UserData is null)
                    {
                        c.CurrentProcessingNode.UserData = false;
                    }

                    c.CurrentProcessingNode.UserData = (bool)c.CurrentProcessingNode.UserData ? false : true;

                    if (c.CurrentProcessingNode.CustomEditorAv is not null)
                    {
                        (c.CurrentProcessingNode.CustomEditorAv as Label).Content = " ";
                        (c.CurrentProcessingNode.CustomEditorAv as Label).Background = (bool)c.CurrentProcessingNode.UserData ? Brushes.Green : Brushes.Red;
                    }

                    return i;
                })
                .WithCustomEditor(x => new Label())
                .WithInput<Bang>("bangIn")
                .WithOutput<Bang>("bangOut"));

            descriptors.Add(new NodeDescriptor(
                "Counter",
                (c, i) =>
                {
                    if (c.CurrentProcessingNode.UserData is null)
                    {
                        c.CurrentProcessingNode.UserData = 0;
                    }

                    c.CurrentProcessingNode.UserData = (int)c.CurrentProcessingNode.UserData + 1;

                    if ((int)c.CurrentProcessingNode.UserData == 2)
                    {
                        c.CurrentProcessingNode.UserData = 0;
                        return i;
                    }
                    else
                    {
                        return null;
                    }
                },
                isInteractive: true)
                .WithInput<Bang>("bangIn")
                .WithOutput<Bang>("bangOut"));

            descriptors.Add(new NodeDescriptor(
                "CounterC",
                (c, i) =>
                {
                    if (c.CurrentProcessingNode.UserData is null)
                    {
                        c.CurrentProcessingNode.UserData = 0;
                    }

                    c.CurrentProcessingNode.UserData = (int)c.CurrentProcessingNode.UserData + 1;

                    if ((int)c.CurrentProcessingNode.UserData == (int)i[1])
                    {
                        c.CurrentProcessingNode.UserData = 0;
                        return new object[1] { i[0] };
                    }
                    else
                    {
                        return null;
                    }
                },
                isInteractive: true)
                .WithInput<Bang>("bangIn")
                .WithInput<int>("count")
                .WithOutput<Bang>("bangOut"));

            descriptors.Add(new NodeDescriptor(
                "Compare",
                (c, i) =>
                {
                    bool result = false;
                    int? a = (int?)i[0];
                    int? b = (int?)i[2];
                    switch ((string)i[1])
                    {
                        case "<": result = a < b; break;
                        case "<=": result = a <= b; break;
                        case ">": result = a > b; break;
                        case ">=": result = a >= b; break;
                        case "==": result = a == b; break;
                        case "!=": result = a != b; break;

                        default: this.FeedbackInfo?.Invoke("Unknown operator", c.CurrentProcessingNode, FeedbackType.Error, null, false); break;
                    }

                    return new object[1] { result };
                })
                .WithInput<int?>("a")
                .WithInput<string>("operator")
                .WithInput<int?>("b")
                .WithOutput<bool>("result"));

            descriptors.Add(new NodeDescriptor(
                "Number",
                (c, i) =>
                {
                    return i;
                },
                isInteractive: true,
                displayTextSelector: (c, i) => (i?[0] ?? "0").ToString())
                .WithInput<int>("inValue")
                .WithOutput<int?>("outValue"));

            descriptors.Add(new NodeDescriptor(
                "Op",
                (c, i) =>
                {
                    int? result = null;
                    int? a = (int?)i[0];
                    int? b = (int?)i[2];
                    switch ((string)i[1])
                    {
                        case "+": result = a + b; break;
                        case "-": result = a - b; break;
                        case "*": result = a * b; break;
                        case "/": result = a / b; break;

                        default: this.FeedbackInfo?.Invoke("Unknown operator", c.CurrentProcessingNode, FeedbackType.Error, null, false); break;
                    }

                    return new object[1] { result };
                })
                .WithInput<int?>("a")
                .WithInput<string>("operator")
                .WithInput<int?>("b")
                .WithOutput<int?>("result"));

            descriptors.Add(new NodeDescriptor(
                "+",
                (c, i) =>
                {
                    return new object[1] { (int?)i[0] + (int?)i[1] };
                })
                .WithInput<int?>("a")
                .WithInput<int?>("b")
                .WithOutput<int?>("result"));

            descriptors.Add(new NodeDescriptor(
                "AggrAnd",
                (c, i) =>
                {
                    if (i[1] != null || c.CurrentProcessingNode.UserData is null)
                    {
                        c.CurrentProcessingNode.UserData = true;
                    }

                    c.CurrentProcessingNode.UserData = (bool)c.CurrentProcessingNode.UserData && (bool)i[0];
                    bool res = (bool)c.CurrentProcessingNode.UserData;

                    return new object[1] { res };
                })
                .WithInput<bool>("input")
                .WithInput<Bang>("resetBang")
                .WithOutput<bool>("res"));

            descriptors.ForEach(x => x.Freeze());

            return descriptors;
        }
    }
}
