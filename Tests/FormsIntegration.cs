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

    void ToggleEditMode() => PressKey(Key.E, KeyModifiers.Control);

    void PressKey(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        _control.OnNodesControl_KeyDown(null, new Avalonia.Input.KeyEventArgs() { Key = key, KeyModifiers = modifiers });
        Repaint();
    }

    void ClickNode(NodeVisual node)
    {
        _control.OnNodesControl_MousePressed(PointerUpdateKind.LeftButtonPressed, 1, new Avalonia.Point((int)node.X, (int)(node.Y + SocketVisual.SocketHeight + 5)));
        _control.OnNodesControl_MouseUp(null, null);
        Repaint();
    }

    void AssertOutputValue(NodeVisual node, int outputIndex, object? value) => node.GetSockets().Outputs[outputIndex].Value.Should().Be(value);

    NodeVisual AddNode(string nodeName)
    {
        int origControlsCount = (_control.Content as Canvas).Children.Count;

        _control.OnNodesControl_MousePressed(PointerUpdateKind.LeftButtonPressed, 2, new Avalonia.Point((_graph.Nodes.Count + 1) * 200, (_graph.Nodes.Count + 1) * 200));

        _graph.Nodes.Count(x => (x as NodeVisual)!.Name == NodeVisual.NewSpecialNodeName).Should().Be(1);
        (_control.Content as Canvas).Children.Count.Should().Be(origControlsCount + 1);

        TextEditor textBox = (_control.Content as Canvas).Children.Last() as TextEditor;
        textBox.Should().NotBeNull();
        textBox.Text = nodeName;

        _control.SwapAutocompleteNode(textBox, textBox.Text, _graph.Nodes.Single(x => (x as NodeVisual).Name == NodeVisual.NewSpecialNodeName) as NodeVisual);

        _graph.Nodes.Count(x => (x as NodeVisual).Name == NodeVisual.NewSpecialNodeName).Should().Be(0);
        _graph.Nodes.Count(x => (x as NodeVisual).Name == nodeName).Should().Be(1);

        Repaint();

        return _graph.Nodes.Single(x => (x as NodeVisual).Name == nodeName) as NodeVisual;
    }

    NodeConnection AddEdge(NodeVisual sourceNode, int sourcePort, NodeVisual destinationNode, int destinationPort)
    {
        int beforeCount = _graph.Edges.Count;

        SocketVisual src = sourceNode.GetSockets().Outputs[sourcePort];
        SocketVisual dest = destinationNode.GetSockets().Inputs[destinationPort];

        _control.OnNodesControl_MousePressed(PointerUpdateKind.LeftButtonPressed, 1, new Avalonia.Point((int)src.GetBounds().X, (int)src.GetBounds().Y));
        _control.OnNodesControl_MouseMove(new PointerPoint(null, new Avalonia.Point((int)dest.GetBounds().X, (int)dest.GetBounds().Y), new PointerPointProperties()));
        _control.OnNodesControl_MouseUp(null, null);

        Repaint();

        _graph.Edges.Should().HaveCount(beforeCount + 1);

        return _graph.Edges.Last() as NodeConnection;
    }

    void Repaint() => _control.Render(new Mock<DrawingContext>().Object);
}
