using AnimateForms.Core;
using Avalonia.Headless.NUnit;
using AvaloniaEdit;
using FluentAssertions;
using Moq;
using NodeEditor;
using NUnit.Framework;

namespace Tests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class FormsIntegration
{
    public class TestContext : BasicContext
    {
        public override List<NodeDescriptor> GetNodeDescriptors()
        {
            List<NodeDescriptor> nds = base.GetNodeDescriptors();
            nds.Add(new NodeDescriptor(
                "NumberAndSign",
                (c, i) =>
                {
                    return new object[2] { i[0] ?? 0, Math.Sign((int?)i[0] ?? 0) };
                },
                isInteractive: true,
                displayTextSelector: (c, i) => (i?[0] ?? "0").ToString())
                .WithInput<int>("inValue")
                .WithOutput<int?>("outValue")
                .WithOutput<int?>("sign"));

            return nds;
        }
    }

    private TestContext _context;
    private NodesGraph _graph;
    private NodesControlAv _control;
    private int _feedbackErrors = 0;

    [SetUp]
    public void Setup()
    {
        Animate.Instance = null;
        this._context = new TestContext();
        this._graph = new NodesGraph();
        this._control = new NodesControlAv(null);
        _control.Initialize(_context, _graph);
        Repaint();

        _graph.Nodes.Should().HaveCount(0);

        _context.FeedbackInfo += (string message, NodeVisual nodeVisual, FeedbackType type, object tag, bool breakExecution) =>
        {
            _feedbackErrors += type == FeedbackType.Error ? 1 : 0;
        };
    }

    [AvaloniaTest]
    public void SerializationRoundtrip()
    {
        string filePath = "..\\..\\..\\..\\SharpData\\default.nds";
        byte[] originalFile = File.ReadAllBytes(filePath);

        TestContext context = new TestContext();
        NodesGraph[] graphs = NodesGraph.Deserialize(originalFile, context);
        NodesControlAv control = new NodesControlAv(null);
        control.Initialize(context, graphs[0]);
        control.MainGraph.Nodes.Should().HaveCountGreaterThan(0);
        control.MainGraph.Edges.Should().HaveCountGreaterThan(0);

        byte[] serialized = NodesGraph.Serialize(graphs[0]);
        serialized.Should().BeEquivalentTo(originalFile);

        // File.WriteAllBytes(filePath, serialized);
    }

    [AvaloniaTest]
    public void AddAndConnectControls()
    {
        NodeVisual loadBang = AddNode("LoadBang");
        NodeVisual bang = AddNode("Bang");

        NodeVisual flipper = AddNode("Flipper");
        this._graph.Nodes.Should().HaveCount(3);

        ClickNode(flipper);
        PressKey(Key.Delete);
        this._graph.Nodes.Should().HaveCount(2);

        // Curried node
        NodeVisual counterC = AddNode("CounterC * 3");

        AddEdge(loadBang, 0, counterC, 0);
        AddEdge(bang, 0, counterC, 0);

        ClickNode(bang);
        ClickNode(bang);
        ClickNode(bang);
        AssertOutputValue(counterC, 0, null);

        ToggleEditMode();

        ClickNode(bang);
        AssertOutputValue(counterC, 0, null);

        ClickNode(bang);
        AssertOutputValue(counterC, 0, Bang.Instance);

        ClickNode(bang);
        AssertOutputValue(counterC, 0, null);

        ToggleEditMode();
        ToggleEditMode();

        AssertOutputValue(counterC, 0, null);

        // Retains state from previous run
        ClickNode(bang);
        AssertOutputValue(counterC, 0, Bang.Instance);
    }

    [AvaloniaTest]
    public void KeyboardNavigationAndSelection()
    {
        NodeVisual loadBang = AddNode("LoadBang", new Point(200, 200));
        loadBang.IsSelected.Should().BeTrue();

        NodeVisual bang = AddNode("Bang", new Point(400, 200));
        loadBang.IsSelected.Should().BeFalse();
        bang.IsSelected.Should().BeTrue();

        NodeVisual compare = AddNode("Compare", new Point(400, 400));
        compare.IsSelected.Should().BeTrue();

        // Arrows
        PressKey(Key.Up);
        compare.IsSelected.Should().BeFalse();
        bang.IsSelected.Should().BeTrue();

        bang.BoundingBox.Top.Should().Be(200);

        PressKey(Key.Down, KeyModifiers.Control);
        bang.IsSelected.Should().BeTrue();
        bang.BoundingBox.Top.Should().Be(210);

        PressKey(Key.Left);
        bang.IsSelected.Should().BeFalse();
        loadBang.IsSelected.Should().BeTrue();

        PressKey(Key.Down);
        loadBang.IsSelected.Should().BeFalse();
        compare.IsSelected.Should().BeTrue();

        // Socket selection
        PressKey(Key.D1);
        compare.IsSelected.Should().BeFalse();
        compare.GetSockets().Inputs[0].ActiveHover.Should().BeTrue();
        this._control.DragStartSocket.Should().BeNull();

        PressKey(Key.D2);
        compare.GetSockets().Inputs[0].ActiveHover.Should().BeFalse();
        compare.GetSockets().Inputs[1].ActiveHover.Should().BeTrue();
        this._control.DragStartSocket.Should().BeNull();

        PressKey(Key.D2);
        this._control.DragStartSocket.Should().Be(compare.GetSockets().Inputs[1]);

        PressKey(Key.Escape);
        compare.GetSockets().Inputs[0].ActiveHover.Should().BeFalse();
        compare.GetSockets().Inputs[1].ActiveHover.Should().BeFalse();
        this._control.DragStartSocket.Should().BeNull();
        compare.IsSelected.Should().BeTrue();

        PressKey(Key.Left);
        compare.IsSelected.Should().BeFalse();
        loadBang.IsSelected.Should().BeTrue();

        PressKey(Key.Down);
        loadBang.IsSelected.Should().BeFalse();
        compare.IsSelected.Should().BeTrue();

        PressKey(Key.D1);
        this._control.DragStartSocket.Should().BeNull();

        PressKey(Key.D1);
        this._control.DragStartSocket.Should().Be(compare.GetSockets().Inputs[0]);

        // Deleting nodes, and selection from no selection
        PressKey(Key.Escape);
        compare.IsSelected.Should().BeTrue();

        this._graph.Nodes.Count.Should().Be(3);

        PressKey(Key.Delete);
        this._graph.Nodes.Count.Should().Be(2);
        bang.IsSelected.Should().BeFalse();

        PressKey(Key.Up);
        bang.IsSelected.Should().BeTrue();
    }

    [AvaloniaTest]
    public void KeyboardEditing()
    {
        NodeVisual loadBang = AddNode("LoadBang", new Point(200, 200));
        NodeVisual num = AddNode("NumberAndSign", new Point(400, 200));
        NodeVisual compare = AddNode("Compare", new Point(400, 400));
        compare.IsSelected.Should().BeTrue();

        // Socket selection
        PressKey(Key.D1);
        compare.IsSelected.Should().BeFalse();
        compare.GetSockets().Inputs[0].ActiveHover.Should().BeTrue();
        this._control.DragStartSocket.Should().BeNull();

        PressKey(Key.D1);
        this._control.DragStartSocket.Should().Be(compare.GetSockets().Inputs[0]);

        PressKey(Key.Up);
        compare.IsSelected.Should().BeFalse();
        num.IsSelected.Should().BeFalse();
        this._control.DragStartSocket.Should().Be(compare.GetSockets().Inputs[0]);

        this._graph.Edges.Count.Should().Be(0);

        PressKey(Key.Q);
        num.IsSelected.Should().BeFalse();
        num.GetSockets().Outputs[0].ActiveHover.Should().BeTrue();

        PressKey(Key.W);
        num.GetSockets().Outputs[1].ActiveHover.Should().BeTrue();
        this._control.DragStartSocket.Should().Be(compare.GetSockets().Inputs[0]);

        this._graph.Edges.Count.Should().Be(0);

        PressKey(Key.W);
        num.IsSelected.Should().BeTrue();

        this._graph.Edges.Count.Should().Be(1);
    }

    [AvaloniaTest]
    public void KeyboardEditingCancel()
    {
        NodeVisual loadBang = AddNode("LoadBang", new Point(200, 200));
        NodeVisual num = AddNode("NumberAndSign", new Point(400, 200));
        NodeVisual compare = AddNode("Compare", new Point(400, 400));
        compare.IsSelected.Should().BeTrue();

        PressKey(Key.D1);
        PressKey(Key.D1);
        this._control.DragStartSocket.Should().Be(compare.GetSockets().Inputs[0]);

        PressKey(Key.Up);
        this._control.DragStartSocket.Should().Be(compare.GetSockets().Inputs[0]);

        PressKey(Key.Q);
        num.IsSelected.Should().BeFalse();
        num.GetSockets().Outputs[0].ActiveHover.Should().BeTrue();

        PressKey(Key.Left);
        num.IsSelected.Should().BeFalse();
        num.GetSockets().Outputs[0].ActiveHover.Should().BeFalse();
        loadBang.GetSockets().Outputs[0].ActiveHover.Should().BeFalse();

        PressKey(Key.Q);
        loadBang.IsSelected.Should().BeFalse();
        loadBang.GetSockets().Outputs[0].ActiveHover.Should().BeTrue();

        PressKey(Key.Escape);

        this._control.DragStartSocket.Should().BeNull();
        loadBang.IsSelected.Should().BeFalse();
        compare.IsSelected.Should().BeTrue();

        this._graph.Edges.Count.Should().Be(0);
    }

    [AvaloniaTest]
    public void ConnectionWithTypeMismatch()
    {
        NodeVisual loadBang = AddNode("LoadBang", new Point(200, 200));

        NodeVisual num = AddNode("NumberAndSign", new Point(400, 200));
        num.GetSockets().Outputs[0].Type.Should().Be<int?>();
        
        NodeVisual compare = AddNode("Compare", new Point(400, 400));
        compare.GetSockets().Inputs[1].Type.Should().Be<string>();

        // Socket selection
        PressKey(Key.D2);
        PressKey(Key.D2);
        this._control.DragStartSocket.Should().Be(compare.GetSockets().Inputs[1]);

        PressKey(Key.Up);

        PressKey(Key.D1);
        num.GetSockets().Inputs[0].ActiveHover.Should().BeTrue();

        PressKey(Key.D1);
        num.GetSockets().Inputs[0].IsCompatibleForConnection(this._control.DragStartSocket!).Should().BeFalse();
        num.GetSockets().Inputs[0].ActiveHover.Should().BeTrue();
        this._graph.Edges.Should().HaveCount(0);

        PressKey(Key.Q);
        num.GetSockets().Inputs[0].ActiveHover.Should().BeFalse();
        num.GetSockets().Outputs[0].ActiveHover.Should().BeTrue();
        this._graph.Edges.Should().HaveCount(0);
        num.GetSockets().Inputs[0].IsCompatibleForConnection(this._control.DragStartSocket!).Should().BeFalse();
        num.GetSockets().Inputs[0].IsCompatibleForConnection(loadBang.GetSockets().Outputs[0]).Should().BeTrue();

        PressKey(Key.Q);
        num.GetSockets().Outputs[0].ActiveHover.Should().BeFalse();
        this._graph.Edges.Should().HaveCount(1);

        AddEdge(loadBang, 0, num, 0);
        AddEdge(num, 0, compare, 0);
        AddEdge(num, 0, compare, 2);

        PressKey(Key.E, KeyModifiers.Control);
        compare.GetSockets().Outputs[0].Value.Should().Be(null);
        _feedbackErrors.Should().Be(1);
    }

    [AvaloniaTest]
    public void ConnectionToCurriedPortOverwritesCurry()
    {
        NodeVisual loadBang = AddNode("LoadBang", new Point(200, 200));
        NodeVisual num = AddNode("NumberAndSign 2", new Point(400, 200));
        AddEdge(loadBang, 0, num, 0);

        NodeVisual compare = AddNode("Compare 1 == 2", new Point(400, 400));
        compare.GetSockets().Inputs[0].CurriedValue.Should().Be(1);
        compare.GetSockets().Inputs[1].CurriedValue.Should().Be("==");
        compare.GetSockets().Inputs[2].CurriedValue.Should().Be(2);

        PressKey(Key.D1);
        PressKey(Key.D1);
        compare.GetSockets().Inputs[0].ActiveHover.Should().BeTrue();

        PressKey(Key.Up);

        PressKey(Key.Q);
        PressKey(Key.Q);

        this._graph.Edges.Should().HaveCount(2);

        ToggleEditMode();
        compare.GetSockets().Outputs[0].Value.Should().Be(true);

        ToggleEditMode();

        ClickNode(num);
        PressKey(Key.Delete);

        this._graph.Edges.Should().HaveCount(1);

        ToggleEditMode();
        compare.GetSockets().Outputs[0].Value.Should().Be(false);
    }

    [AvaloniaTest]
    public void KeyboardCreate()
    {
        NodeVisual loadBang = AddNode("LoadBang", new Point(200, 200));
        
        PressKey(Key.Q);
        PressKey(Key.Q);
        this._control.DragStartSocket.Should().Be(loadBang.GetSockets().Outputs[0]);

        // Socket selection
        PressKey(Key.Enter, KeyModifiers.Control);
        this._graph.Nodes.Count.Should().Be(2);
        (this._graph.Nodes.Last() as NodeVisual).IsSelected.Should().BeTrue();
        (this._graph.Nodes.Last() as NodeVisual).Name.Should().Be("*new*");
        this._control.DragStartSocket.Should().NotBeNull();

        AddNode("Compare", mouseTriggered: false);

        this._graph.Nodes.Count.Should().Be(2);
        (this._graph.Nodes.Last() as NodeVisual).IsSelected.Should().BeTrue();
        (this._graph.Nodes.Last() as NodeVisual).Name.Should().Be("Compare");
        (this._graph.Nodes.Last() as NodeVisual).BoundingBox.Left.Should().Be(200);
        (this._graph.Nodes.Last() as NodeVisual).BoundingBox.Top.Should().BeGreaterThan(250);
        this._control.DragStartSocket.Should().NotBeNull();

        PressKey(Key.D1);
        PressKey(Key.D1);
        this._control.DragStartSocket.Should().BeNull();
        this._graph.Edges.Should().HaveCount(1);
    }

    [AvaloniaTest]
    public void CustomEditor()
    {
        NodeVisual bangInput = AddNode("Bang");
        NodeVisual input = AddNode("InputValue" + " 42.0");

        NodeVisual bang = AddNode("Bang");
        NodeVisual messageBox = AddNode("ShowMessageBox");

        messageBox.CustomEditorAv.Should().NotBeNull();
        messageBox.CustomEditorAv.Should().BeOfType<Label>();
        Label messageEditor = messageBox.CustomEditorAv as Label;
        messageEditor.Content.Should().BeNull();

        this._graph.Nodes.Should().HaveCount(4);

        AddEdge(bangInput, 0, input, 0);
        AddEdge(input, 0, messageBox, 1);
        AddEdge(bang, 0, messageBox, 0);

        ToggleEditMode();

        AssertOutputValue(input, 0, null);

        ClickNode(bang);
        messageEditor.Content.Should().Be("NULL");

        ClickNode(bangInput);
        AssertOutputValue(input, 0, 42.0f);
        messageEditor.Content.Should().Be("NULL");

        ClickNode(bang);
        messageEditor.Content.Should().Be("42");
    }

    [AvaloniaTest]
    public void ValueAsBang()
    {
        NodeVisual bangInput = AddNode("Bang");
        NodeVisual input = AddNode("InputValue" + " 42.0");
        NodeVisual messageBox = AddNode("ShowMessageBox");

        AddEdge(bangInput, 0, input, 0);
        AddEdge(input, 0, messageBox, 0);
        AddEdge(input, 0, messageBox, 1);

        Label messageEditor = messageBox.CustomEditorAv as Label;
        messageEditor.Content.Should().BeNull();

        ToggleEditMode();

        messageEditor.Content.Should().BeNull();

        ClickNode(bangInput);
        messageEditor.Content.Should().Be("42");
    }

    [AvaloniaTest]
    public void Feedback()
    {
        NodeVisual bang = AddNode("Bang");
        NodeVisual compare = AddNode("Compare" + " * >> *");

        AddEdge(bang, 0, compare, 0);

        string feeback = null;
        this._context.FeedbackInfo += (string message, NodeVisual nodeVisual, FeedbackType type, object tag, bool breakExecution) =>
        {
            nodeVisual.Should().Be(compare);
            feeback = message;
        };

        bool controlNotified = false;
        _control.OnNodeHint += e => controlNotified = true;

        ToggleEditMode();

        compare.Feedback.Should().Be(FeedbackType.None);

        ClickNode(bang, expectedFeedbackErrors: 1);

        compare.Feedback.Should().Be(FeedbackType.Error);
        feeback.Should().Be("Unknown operator");
        controlNotified = true;
    }

    [AvaloniaTest]
    public void ControlFlowScenario1()
    {
        NodeVisual bang = AddNode("Bang");
        NodeVisual opPlus = AddNode("+");
        NodeVisual inNum1 = AddNode("Number" + " 42");
        NodeVisual inNum2 = AddNode("Number" + " 5");
        NodeVisual outNum = AddNode("Number");

        AddEdge(bang, 0, opPlus, 0);
        AddEdge(inNum1, 0, opPlus, 0);
        AddEdge(inNum2, 0, opPlus, 1);
        AddEdge(opPlus, 0, outNum, 0);

        ToggleEditMode();

        ClickNode(bang);
        AssertOutputValue(outNum, 0, null);

        ClickNode(inNum1);
        AssertOutputValue(outNum, 0, null);

        ClickNode(inNum2);
        AssertOutputValue(outNum, 0, null);

        ClickNode(bang);
        AssertOutputValue(outNum, 0, 47);
    }


    [AvaloniaTest]
    public void SubgraphEditingAndExecution()
    {
        NodeVisual subgraphNode = AddNode("*s* Module1");
        subgraphNode.SubsystemGraph.Should().NotBeNull();
        subgraphNode.Type.Should().Be(NodeVisual.NodeType.Subsystem);

        NodeVisual subgraphRequest = null;
        _control.OnSubgraphOpenRequest += e => subgraphRequest = e;

        ToggleEditMode();

        ClickNode(subgraphNode, clickCount: 2);
        subgraphNode.Should().BeSameAs(subgraphRequest);
        subgraphNode.GetSockets().All.Count().Should().Be(0);

        var originalControl = this._control;
        var originalGraph = this._graph;

        // Open subtraph window
        this._graph = subgraphNode.SubsystemGraph;
        this._control = new NodesControlAv(subgraphRequest);
        _control.Initialize(_context, this._graph);
        Repaint();

        // Add input and output
        NodeVisual subInput = AddNode("*i* firstInput");
        subInput.Name.Should().Be("*i* firstInput");
        subInput.GetSockets().Outputs.Count.Should().Be(1);
        subInput.GetSockets().Inputs.Count.Should().Be(0);
        subInput.Type.Should().Be(NodeVisual.NodeType.SubsystemInlet);

        NodeVisual subOutput = AddNode("*o* firstOutput");
        subOutput.Name.Should().Be("*o* firstOutput");
        subOutput.GetSockets().Outputs.Count.Should().Be(0);
        subOutput.GetSockets().Inputs.Count.Should().Be(1);
        subOutput.Type.Should().Be(NodeVisual.NodeType.SubsystemOutlet);

        AddEdge(subInput, 0, subOutput, 0);

        // Close subgraph window
        this._graph = originalGraph;
        this._control = originalControl;

        // Edit top-level graph and run it
        subgraphNode.GetSockets().All.Count().Should().Be(2);

        ToggleEditMode();

        NodeVisual bang = AddNode("bang");
        bang.SubsystemGraph.Should().BeNull();
        AddEdge(bang, 0, subgraphNode, 0);

        ToggleEditMode();

        AssertOutputValue(subgraphNode, 0, null);

        ClickNode(bang);

        AssertOutputValue(subgraphNode, 0, Bang.Instance);
    }

    void ToggleEditMode()
    {
        PressKey(Key.E, KeyModifiers.Control);
        _feedbackErrors.Should().Be(0);
    }
    void PressKey(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        _control.OnNodesControl_KeyDown(null, new Avalonia.Input.KeyEventArgs() { Key = key, KeyModifiers = modifiers });
        Repaint();
    }

    void ClickNode(NodeVisual node, int clickCount = 1, int expectedFeedbackErrors = 0)
    {
        _control.OnNodesControl_MouseMove(new PointerPoint(null, new Point((int)node.X, (int)(node.Y + SocketVisual.SocketHeight + 5)), new PointerPointProperties()));
        _control.OnNodesControl_MousePressed(PointerUpdateKind.LeftButtonPressed, clickCount);
        _control.OnNodesControl_MouseUp(null, null);
        Repaint();

        _feedbackErrors.Should().Be(expectedFeedbackErrors);
    }

    void AssertOutputValue(NodeVisual node, int outputIndex, object? value)
    {
        node.GetSockets().Outputs[outputIndex].Value.Should().Be(value);
        _feedbackErrors.Should().Be(0);
    }
    NodeVisual AddNode(string nodeName, Point? location = null, bool mouseTriggered = true)
    {
        if (mouseTriggered)
        {
            int origControlsCount = (_control.Content as Canvas).Children.Count;

            _control.OnNodesControl_MouseMove(new PointerPoint(null, location ?? new Point((_graph.Nodes.Count + 1) * 200, (_graph.Nodes.Count + 1) * 200), new PointerPointProperties()));
            _control.OnNodesControl_MousePressed(PointerUpdateKind.LeftButtonPressed, 2);

            _graph.Nodes.Count(x => (x as NodeVisual)!.Name == NodeVisual.NewSpecialNodeName).Should().Be(1);
            (_control.Content as Canvas).Children.Count.Should().Be(origControlsCount + 1);
        }

        TextEditor textBox = (_control.Content as Canvas).Children.Last() as TextEditor;
        textBox.Should().NotBeNull();
        textBox.Text = nodeName;

        int before = _graph.Nodes.Count(x => (x as NodeVisual).Name == nodeName);
        _control.SwapAutocompleteNode(textBox, textBox.Text, _graph.Nodes.Single(x => (x as NodeVisual).Name == NodeVisual.NewSpecialNodeName) as NodeVisual);

        _graph.Nodes.Count(x => (x as NodeVisual).Name == NodeVisual.NewSpecialNodeName).Should().Be(0);
        _graph.Nodes.Count(x => (x as NodeVisual).Name == nodeName).Should().Be(before + 1);

        Repaint();

        return _graph.Nodes.Last(x => (x as NodeVisual).Name == nodeName) as NodeVisual;
    }

    NodeConnection AddEdge(NodeVisual sourceNode, int sourcePort, NodeVisual destinationNode, int destinationPort)
    {
        int beforeCount = _graph.Edges.Count;

        SocketVisual src = sourceNode.GetSockets().Outputs[sourcePort];
        SocketVisual dest = destinationNode.GetSockets().Inputs[destinationPort];

        _control.OnNodesControl_MouseMove(new PointerPoint(null, new Point((int)src.GetBounds().X, (int)src.GetBounds().Y), new PointerPointProperties()));
        _control.OnNodesControl_MousePressed(PointerUpdateKind.LeftButtonPressed, 1);
        _control.OnNodesControl_MouseMove(new PointerPoint(null, new Point((int)dest.GetBounds().X, (int)dest.GetBounds().Y), new PointerPointProperties()));
        _control.OnNodesControl_MouseUp(null, null);

        Repaint();

        _graph.Edges.Should().HaveCount(beforeCount + 1);

        return _graph.Edges.Last() as NodeConnection;
    }

    void Repaint() => _control.Render(new Mock<DrawingContext>().Object);
}
