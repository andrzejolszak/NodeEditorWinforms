using AnimateForms.Core;
using NodeEditor;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static AnimateForms.Core.Animate;

namespace MathSample
{
    public partial class FormMathSample : Form
    {
        public MathContext context;
        public NodesGraph mainGraph;
        private NodeVisual owner;

        public FormMathSample(MathContext context, NodesGraph mainGraph, NodeVisual owner)
        {
            InitializeComponent();
            this.context = context;
            this.mainGraph = mainGraph;
            this.owner = owner;
            this.FormClosing += FormMathSample_FormClosing;
        }

        private void FormMathSample_FormClosing(object sender, FormClosingEventArgs e)
        {
            owner?.ResetSocketsCache();
        }

        private void FormMathSample_Load(object sender, EventArgs e)
        {
            controlNodeEditor.nodesControl.Initialize(context, mainGraph);

            controlNodeEditor.nodesControl.OnSubgraphOpenRequest += NodesControl_OnSubgraphOpenRequest;
        }

        private void NodesControl_OnSubgraphOpenRequest(NodeVisual obj)
        {
            FormMathSample newForm = new FormMathSample(this.context, obj.SubsystemGraph, obj);
            newForm.Show();
        }

        private void FormMathSample_Shown(object sender, EventArgs e)
        {
            controlNodeEditor.Focus();
        }
    }
}
