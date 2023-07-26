using NodeEditor;

namespace MathSample
{
    public partial class FormMathSample
    {
        public MathContext context;
        public NodesGraph mainGraph;
        private NodeVisual owner;

        public FormMathSample(MathContext context, NodesGraph mainGraph, NodeVisual owner)
        {
            this.context = context;
            this.mainGraph = mainGraph;
            this.owner = owner;
        }

        private void FormMathSample_FormClosing(object sender)
        {
            // owner?.ResetSocketsCache();
        }

        private void FormMathSample_Load(object sender, EventArgs e)
        {
            // controlNodeEditor.nodesControl.Initialize(context, mainGraph);
            // 
            // controlNodeEditor.nodesControl.OnSubgraphOpenRequest += NodesControl_OnSubgraphOpenRequest;
        }

        private void NodesControl_OnSubgraphOpenRequest(NodeVisual obj)
        {
            FormMathSample newForm = new FormMathSample(this.context, obj.SubsystemGraph, obj);
            // newForm.Show();
        }

        private void FormMathSample_Shown(object sender, EventArgs e)
        {
            // controlNodeEditor.Focus();
        }
    }
}
