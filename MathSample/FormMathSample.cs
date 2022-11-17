using NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MathSample
{
    public partial class FormMathSample : Form
    {
        //Context that will be used for our nodes
        MathContext context;

        public FormMathSample()
        {
            InitializeComponent();
            this.context = new MathContext();
        }

        private void FormMathSample_Load(object sender, EventArgs e)
        {
            //Context assignment
            controlNodeEditor.nodesControl.Context = context;
            controlNodeEditor.nodesControl.OnNodeSelected += NodesControlOnOnNodeContextSelected; 
            
            //Loading sample from file
            controlNodeEditor.nodesControl.Deserialize(File.ReadAllBytes("..\\..\\default.nds"));

            this.FormClosing += FormMathSample_FormClosing;
        }

        private void FormMathSample_FormClosing(object sender, FormClosingEventArgs e)
        {
            File.WriteAllBytes("..\\..\\default.nds", controlNodeEditor.nodesControl.Serialize());
        }

        private void NodesControlOnOnNodeContextSelected(NodeVisual o)
        {
            if (controlNodeEditor.nodesControl.IsRunMode && o.IsInteractive)
            {
                controlNodeEditor.nodesControl.Execute(new Queue<NodeVisual>(new[]{ o }));
            }
        }

        private void FormMathSample_Shown(object sender, EventArgs e)
        {
            controlNodeEditor.Focus();
        }
    }
}
