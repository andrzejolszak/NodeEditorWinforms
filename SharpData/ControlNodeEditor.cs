using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SampleCommon
{
    public partial class ControlNodeEditor : UserControl
    {
        public ControlNodeEditor()
        {
            InitializeComponent();
        }

        private void nodesControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.E)
            {
                nodesControl.ToggleRunMode();
            }

            if (e.Control && e.KeyCode == Keys.R)
            {
                nodesControl.ToggleEdgeRouting();
            }
        }
    }
}
