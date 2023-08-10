
namespace NodeEditor
{
    public class NodeDescriptor
    {
        public bool IsFrozen { get; private set; }

        public Func<INodesContext, object[], object[]?> Action { get; }

        public string Name { get; }

        /// <summary>
        /// If true, the node is able to be executed during execution process (will have exec input and output socket).
        /// </summary>
        public bool IsInteractive { get; internal set; }

        /// <summary>
        /// Given type should be subclass of System.Windows.Forms.Control, and represents what will be displayed in the middle of the node.
        /// </summary>
        public Type CustomEditor { get; }

        /// <summary>
        /// Width of single node
        /// </summary>
        public int Width { get; internal set; }

        /// <summary>
        /// Height of single node
        /// </summary>
        public int Height { get; internal set; }

        public bool InvokeOnLoad { get; internal set; }

        public string Alias { get; }

        public bool HideName { get; }

        public List<PortDescriptor> Ports { get; private set; } = new List<PortDescriptor>();

        public NodeDescriptor(string name, Func<INodesContext, object[], object[]> action, bool isInteractive = false, Type customEditor = null, int width = -1, int height = -1, bool invokeOnLoad = false, string alias = null, bool hideName = false)
        {
            Name = name;
            Action = action;
            IsInteractive = isInteractive;
            CustomEditor = customEditor;
            Width = width;
            Height = height;
            InvokeOnLoad = invokeOnLoad;
            Alias = alias;
            HideName = hideName;
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
