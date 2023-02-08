namespace NodeEditor
{
    partial class NodesControl
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
            this.SuspendLayout();
            // 
            // NodesControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.DoubleBuffered = true;
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "NodesControl";
            this.Size = new System.Drawing.Size(670, 463);
            this.VisibleChanged += new System.EventHandler(this.OnNodesControl_VisibleChanged);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.OnNodesControl_Paint);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnNodesControl_KeyDown);
            this.MouseClick += new System.Windows.Forms.MouseEventHandler(this.OnNodesControl_MouseClick);
            this.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.OnNodesControl_DoubleMouseClick);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.OnNodesControl_MouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.OnNodesControl_MouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.OnNodesControl_MouseUp);
            this.ResumeLayout(false);

        }

        #endregion
    }
}
