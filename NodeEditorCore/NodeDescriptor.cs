
namespace NodeEditor
{
    public class NodeDescriptor
    {
        public bool IsFrozen { get; private set; }

        public Func<INodesContext, object[], object[]?> Action { get; }

        public Func<Control, Control>? CustomEditorFactory { get; private set; }

        public string Name { get; }

        /// <summary>
        /// If true, the node is able to be executed during execution process (will have exec input and output socket).
        /// </summary>
        public bool IsInteractive { get; internal set; }

        public bool InvokeOnLoad { get; internal set; }

        public Func<INodesContext?, object[]?, string>? DisplayTextSelector { get; }

        public List<PortDescriptor> Ports { get; private set; } = new List<PortDescriptor>();

        public NodeDescriptor(string name, Func<INodesContext, object[], object[]> action, bool isInteractive = false, bool invokeOnLoad = false, Func<INodesContext?, object[]?, string>? displayTextSelector = null)
        {
            // TODO: keep own state on the instance?
            Name = name;
            Action = action;
            IsInteractive = isInteractive;
            InvokeOnLoad = invokeOnLoad;
            DisplayTextSelector = displayTextSelector;
        }

        public void Freeze()
        {
            IsFrozen = true;
        }

        public NodeDescriptor WithPort(PortDescriptor port)
        {
            if (this.IsFrozen)
            {
                throw new InvalidOperationException("frozen " + this.Name);
            }

            if (port.Index == -1)
            {
                port.Index = this.Ports.Count;
            }

            this.Ports.Add(port);
            this.Ports = this.Ports.OrderBy(x => x.Index).ToList();

            return this;
        }

        public NodeDescriptor WithInput<T>(string name, bool isInput = false, bool isOutput = false, int index = -1) => this.WithPort(new PortDescriptor(name, typeof(T), isInput: true, isOutput: false));

        public NodeDescriptor WithOutput<T>(string name, bool isInput = false, bool isOutput = false, int index = -1) => this.WithPort(new PortDescriptor(name, typeof(T), isInput: false, isOutput: true));

        public NodeDescriptor WithCustomEditor(Func<Control, Control> factory)
        {
            this.CustomEditorFactory = factory;
            return this;
        }
    }

    public class PortDescriptor
    {
        public PortDescriptor(string name, Type type, bool isInput = false, bool isOutput = false, int index = -1)
        {
            Name = name;
            Type = type;
            IsInput = isInput;
            IsOutput = isOutput;
            Index = index;
        }

        public int Index { get; set; }

        public string Name { get; }

        public bool IsInput { get; }

        public bool IsOutput { get; }

        public Type Type { get; }
    }
}
