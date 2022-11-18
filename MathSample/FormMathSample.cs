using NodeEditor;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace MathSample
{
    public partial class FormMathSample : Form
    {
        public MathContext context;
        public NodesGraph mainGraph;
        public NodesGraph[] allGraphs;

        public FormMathSample(MathContext context, NodesGraph mainGraph, NodesGraph[] allGraphs)
        {
            InitializeComponent();
            this.context = context;
            this.mainGraph = mainGraph;
            this.allGraphs = allGraphs;
        }

        private void FormMathSample_Load(object sender, EventArgs e)
        {
            //Context assignment
            controlNodeEditor.nodesControl.Context = context;
            controlNodeEditor.nodesControl.OnNodeSelected += NodesControlOnOnNodeContextSelected; 
            
            //Loading sample from file
            controlNodeEditor.nodesControl.MainGraph = mainGraph;
            controlNodeEditor.nodesControl.AllGraphs = this.allGraphs.ToDictionary(x => x.GUID);
            controlNodeEditor.nodesControl.Controls.Clear();
            controlNodeEditor.nodesControl.Controls.AddRange(mainGraph.Nodes.Where(x => x.CustomEditor != null).Select(x => x.CustomEditor).ToArray());
            controlNodeEditor.nodesControl.Refresh();

            controlNodeEditor.nodesControl.OnSubgraphOpenRequest += NodesControl_OnSubgraphOpenRequest;
        }

        private void NodesControl_OnSubgraphOpenRequest(NodeVisual obj)
        {
            FormMathSample newForm = new FormMathSample(this.context, obj.SubsystemGraph, allGraphs.Except<NodesGraph>(new[] { this.mainGraph }).ToArray());
            newForm.Parent = this;
            newForm.Show();
        }

        private void NodesControlOnOnNodeContextSelected(NodeVisual o)
        {
            if (controlNodeEditor.nodesControl.IsRunMode && o.IsInteractive)
            {
                controlNodeEditor.nodesControl.Execute(new Stack<NodeVisual>(new[]{ o }));
            }
        }

        private void FormMathSample_Shown(object sender, EventArgs e)
        {
            controlNodeEditor.Focus();
        }
    }
}
