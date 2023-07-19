using FluentAssertions;
using Microsoft.Msagl.Core.Layout;
using NodeEditor;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Bitmap = System.Drawing.Bitmap;

namespace Tests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class FormsIntegration
{
    private BasicContext _context;
    private NodesGraph _graph;
    private NodesControl _control;

    [SetUp]
    public void Setup()
    {
        this._context = new BasicContext();
        this._graph = new NodesGraph();
        this._control = new NodesControl();
        _control.Initialize(_context, _graph);
        Repaint();

        _graph.Nodes.Should().HaveCount(0);
    }

    [Test]
    public void SerializationRoundtrip()
    {
        string filePath = "..\\..\\..\\..\\SharpData\\default.nds";
        byte[] originalFile = File.ReadAllBytes(filePath);

        BasicContext context = new BasicContext();
        NodesGraph[] graphs = NodesGraph.Deserialize(originalFile, context);
        NodesControl control = new NodesControl();
        control.Initialize(context, graphs[0]);
        control.MainGraph.Nodes.Should().HaveCountGreaterThan(0);
        control.MainGraph.Edges.Should().HaveCountGreaterThan(0);

        byte[] serialized = NodesGraph.Serialize(graphs[0]);
        serialized.Should().BeEquivalentTo(originalFile);

        // File.WriteAllBytes(filePath, serialized);
    }

    [Test]
    public void AddAndConnectControls()
    {
        NodeVisual loadBang = AddNode(nameof(BasicContext.LoadBang));
        NodeVisual bang = AddNode(nameof(BasicContext.Bang));

        NodeVisual flipper = AddNode(nameof(BasicContext.Flipper));
        this._graph.Nodes.Should().HaveCount(3);

        ClickNode(flipper);
        PressKeys(Keys.Delete);
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

    void ToggleEditMode() => PressKeys(Keys.Control | Keys.E);

    void PressKeys(Keys keys)
    {
        _control.OnNodesControl_KeyDown(null, new KeyEventArgs(keys));
        Repaint();
    }

    void ClickNode(NodeVisual node)
    {
        _control.OnNodesControl_MouseDown(null, new MouseEventArgs(MouseButtons.Left, 1, (int)node.X, (int)(node.Y + SocketVisual.SocketHeight + 5), 0));
        _control.OnNodesControl_MouseUp(null, new MouseEventArgs(MouseButtons.None, 0, (int)node.X, (int)(node.Y + SocketVisual.SocketHeight + 5), 0));
        Repaint();
    }

    void AssertOutputValue(NodeVisual node, int outputIndex, object? value) => node.GetSockets().Outputs[outputIndex].Value.Should().Be(value);

    NodeVisual AddNode(string nodeName)
    {
        int origControlsCount = _control.Controls.Count;

        _control.OnNodesControl_DoubleMouseClick(null, new MouseEventArgs(MouseButtons.Left, 2, (_graph.Nodes.Count + 1) * 200, (_graph.Nodes.Count + 1) * 200, 0));

        _graph.Nodes.Count(x => (x as NodeVisual)!.Name == NodeVisual.NewSpecialNodeName).Should().Be(1);
        _control.Controls.Count.Should().Be(origControlsCount + 1);

        TextBox textBox = _control.Controls[_control.Controls.Count - 1] as TextBox;
        textBox.Should().NotBeNull();
        textBox.Text = nodeName;

        _control.SwapAutocompleteNode(textBox, _graph.Nodes.Single(x => (x as NodeVisual).Name == NodeVisual.NewSpecialNodeName) as NodeVisual);

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

        _control.OnNodesControl_MouseDown(null, new MouseEventArgs(MouseButtons.Left, 1, (int)src.GetBounds().X, (int)src.GetBounds().Y, 0));
        _control.OnNodesControl_MouseMove(null, new MouseEventArgs(MouseButtons.None, 0, (int)dest.GetBounds().X, (int)dest.GetBounds().Y, 0));
        _control.OnNodesControl_MouseUp(null, new MouseEventArgs(MouseButtons.None, 0, (int)dest.GetBounds().X, (int)dest.GetBounds().Y, 0));

        Repaint();

        _graph.Edges.Should().HaveCount(beforeCount + 1);

        return _graph.Edges.Last() as NodeConnection;
    }

    void Repaint() => _control.OnNodesControl_Paint(null, new PaintEventArgs(Graphics.FromImage(new Bitmap(1024, 768)), new Rectangle(0, 0, 1024, 768)));
}
