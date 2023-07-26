using Avalonia.Controls.Shapes;
using Avalonia.Headless.NUnit;
using AvaloniaEdit;
using FluentAssertions;
using Moq;
using NodeEditor;
using NUnit.Framework;
using System.Diagnostics.Metrics;

namespace Tests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class FormsIntegration
{
    private BasicContext _context;
    private NodesGraph _graph;
    private NodesControlAv _control;

    [SetUp]
    public void Setup()
    {
        this._context = new BasicContext();
        this._graph = new NodesGraph();
        this._control = new NodesControlAv(null);
        _control.Initialize(_context, _graph);
        Repaint();

        _graph.Nodes.Should().HaveCount(0);
    }

    [AvaloniaTest]
    public void SerializationRoundtrip()
    {
        string filePath = "..\\..\\..\\..\\SharpData\\default.nds";
        byte[] originalFile = File.ReadAllBytes(filePath);

        BasicContext context = new BasicContext();
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
        NodeVisual loadBang = AddNode(nameof(BasicContext.LoadBang));
        NodeVisual bang = AddNode(nameof(BasicContext.Bang));

        NodeVisual flipper = AddNode(nameof(BasicContext.Flipper));
        this._graph.Nodes.Should().HaveCount(3);

        ClickNode(flipper);
        PressKey(Key.Delete);
        this._graph.Nodes.Should().HaveCount(2);

        // Curried node
        NodeVisual counterC = AddNode(nameof(BasicContext.CounterC) + " * 3");

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
    public void CustomEditor()
    {
        NodeVisual bangInput = AddNode(nameof(BasicContext.Bang));
        NodeVisual input = AddNode(nameof(BasicContext.InputValue) + " 42.0");

        NodeVisual bang = AddNode(nameof(BasicContext.Bang));
        NodeVisual messageBox = AddNode(nameof(BasicContext.ShowMessageBox));

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
        NodeVisual bangInput = AddNode(nameof(BasicContext.Bang));
        NodeVisual input = AddNode(nameof(BasicContext.InputValue) + " 42.0");
        NodeVisual messageBox = AddNode(nameof(BasicContext.ShowMessageBox));

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
        NodeVisual bang = AddNode(nameof(BasicContext.Bang));
        NodeVisual compare = AddNode(nameof(BasicContext.Compare) + " * >> *");

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

        ClickNode(bang);

        compare.Feedback.Should().Be(FeedbackType.Error);
        feeback.Should().Be("Unknown operator");
        controlNotified = true;
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

    void ToggleEditMode() => PressKey(Key.E, KeyModifiers.Control);

    void PressKey(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        _control.OnNodesControl_KeyDown(null, new Avalonia.Input.KeyEventArgs() { Key = key, KeyModifiers = modifiers });
        Repaint();
    }

    void ClickNode(NodeVisual node, int clickCount = 1)
    {
        _control.OnNodesControl_MouseMove(new PointerPoint(null, new Point((int)node.X, (int)(node.Y + SocketVisual.SocketHeight + 5)), new PointerPointProperties()));
        _control.OnNodesControl_MousePressed(PointerUpdateKind.LeftButtonPressed, clickCount);
        _control.OnNodesControl_MouseUp(null, null);
        Repaint();
    }

    void AssertOutputValue(NodeVisual node, int outputIndex, object? value) => node.GetSockets().Outputs[outputIndex].Value.Should().Be(value);

    NodeVisual AddNode(string nodeName)
    {
        int origControlsCount = (_control.Content as Canvas).Children.Count;

        _control.OnNodesControl_MouseMove(new PointerPoint(null, new Point((_graph.Nodes.Count + 1) * 200, (_graph.Nodes.Count + 1) * 200), new PointerPointProperties()));
        _control.OnNodesControl_MousePressed(PointerUpdateKind.LeftButtonPressed, 2);

        _graph.Nodes.Count(x => (x as NodeVisual)!.Name == NodeVisual.NewSpecialNodeName).Should().Be(1);
        (_control.Content as Canvas).Children.Count.Should().Be(origControlsCount + 1);

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
