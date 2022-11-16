namespace SampleCommon
{
    partial class ControlNodeEditor
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panel = new System.Windows.Forms.Panel();
            this.nodesControl = new NodeEditor.NodesControl();
            this.panel.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel
            // 
            this.panel.AutoScroll = true;
            this.panel.Controls.Add(this.nodesControl);
            this.panel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel.Location = new System.Drawing.Point(0, 0);
            this.panel.Name = "panel";
            this.panel.Size = new System.Drawing.Size(622, 485);
            this.panel.TabIndex = 0;
            // 
            // nodesControl
            // 
            this.nodesControl.BackgroundImage = global::SampleCommon.Properties.Resources.grid;
            this.nodesControl.Context = null;
            this.nodesControl.Location = new System.Drawing.Point(0, 0);
            this.nodesControl.Name = "nodesControl";
            this.nodesControl.Size = new System.Drawing.Size(5000, 5000);
            this.nodesControl.TabIndex = 0;
            this.nodesControl.KeyDown += new System.Windows.Forms.KeyEventHandler(this.nodesControl_KeyDown);
            // 
            // ControlNodeEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel);
            this.Name = "ControlNodeEditor";
            this.Size = new System.Drawing.Size(622, 485);
            this.panel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Panel panel;
        public NodeEditor.NodesControl nodesControl;
    }
}
