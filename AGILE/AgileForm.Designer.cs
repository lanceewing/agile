namespace AGILE
{
    partial class AgileForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AgileForm));
            this.cntxtMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.cntxtMenuOpenUserConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.cntxtMenuFullScreen = new System.Windows.Forms.ToolStripMenuItem();
            this.cntxtMenuAspectCorrectionOn = new System.Windows.Forms.ToolStripMenuItem();
            this.cntxtMenuAspectCorrectionOff = new System.Windows.Forms.ToolStripMenuItem();
            this.cntxtMenuStretchMode = new System.Windows.Forms.ToolStripMenuItem();
            this.cntxtMenuOptions = new System.Windows.Forms.ToolStripMenuItem();
            this.cntxtMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // cntxtMenu
            // 
            this.cntxtMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cntxtMenuOpenUserConfig,
            this.cntxtMenuOptions,
            this.cntxtMenuFullScreen,
            this.cntxtMenuAspectCorrectionOn,
            this.cntxtMenuAspectCorrectionOff,
            this.cntxtMenuStretchMode});
            this.cntxtMenu.Name = "cntxtMenu";
            this.cntxtMenu.Size = new System.Drawing.Size(190, 158);
            this.cntxtMenu.Opening += new System.ComponentModel.CancelEventHandler(this.cntxtMenu_Opening);
            // 
            // cntxtMenuOpenUserConfig
            // 
            this.cntxtMenuOpenUserConfig.Name = "cntxtMenuOpenUserConfig";
            this.cntxtMenuOpenUserConfig.Size = new System.Drawing.Size(189, 22);
            this.cntxtMenuOpenUserConfig.Text = "Open User.&config";
            this.cntxtMenuOpenUserConfig.Click += new System.EventHandler(this.cntxtMenuOpenUserConfig_Click);
            // 
            // cntxtMenuFullScreen
            // 
            this.cntxtMenuFullScreen.CheckOnClick = true;
            this.cntxtMenuFullScreen.Name = "cntxtMenuFullScreen";
            this.cntxtMenuFullScreen.Size = new System.Drawing.Size(189, 22);
            this.cntxtMenuFullScreen.Text = "Full Screen";
            this.cntxtMenuFullScreen.Click += new System.EventHandler(this.cntxtMenuFullScreen_Click);
            // 
            // cntxtMenuAspectCorrectionOn
            // 
            this.cntxtMenuAspectCorrectionOn.CheckOnClick = true;
            this.cntxtMenuAspectCorrectionOn.Name = "cntxtMenuAspectCorrectionOn";
            this.cntxtMenuAspectCorrectionOn.Size = new System.Drawing.Size(189, 22);
            this.cntxtMenuAspectCorrectionOn.Text = "Aspect Correction On";
            this.cntxtMenuAspectCorrectionOn.Click += new System.EventHandler(this.cntxtMenuAspectCorrectionOn_Click);
            // 
            // cntxtMenuAspectCorrectionOff
            // 
            this.cntxtMenuAspectCorrectionOff.Checked = true;
            this.cntxtMenuAspectCorrectionOff.CheckOnClick = true;
            this.cntxtMenuAspectCorrectionOff.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cntxtMenuAspectCorrectionOff.Name = "cntxtMenuAspectCorrectionOff";
            this.cntxtMenuAspectCorrectionOff.Size = new System.Drawing.Size(189, 22);
            this.cntxtMenuAspectCorrectionOff.Text = "Aspect Correction Off";
            this.cntxtMenuAspectCorrectionOff.Click += new System.EventHandler(this.cntxtMenuAspectCorrectionOff_Click);
            // 
            // cntxtMenuStretchMode
            // 
            this.cntxtMenuStretchMode.CheckOnClick = true;
            this.cntxtMenuStretchMode.Name = "cntxtMenuStretchMode";
            this.cntxtMenuStretchMode.Size = new System.Drawing.Size(189, 22);
            this.cntxtMenuStretchMode.Text = "Stretch Mode";
            this.cntxtMenuStretchMode.Click += new System.EventHandler(this.cntxtMenuStretchMode_Click);
            // 
            // cntxtMenuOptions
            // 
            this.cntxtMenuOptions.Name = "cntxtMenuOptions";
            this.cntxtMenuOptions.Size = new System.Drawing.Size(189, 22);
            this.cntxtMenuOptions.Text = "Open &Options";
            this.cntxtMenuOptions.Click += new System.EventHandler(this.cntxtMenuOptions_Click);
            // 
            // AgileForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Desktop;
            this.ClientSize = new System.Drawing.Size(960, 600);
            this.ContextMenuStrip = this.cntxtMenu;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "AgileForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "AGILE";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.AgileForm_Closing);
            this.Load += new System.EventHandler(this.AgileForm_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.AgileForm_KeyDown);
            this.Resize += new System.EventHandler(this.AgileForm_Resize);
            this.cntxtMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip cntxtMenu;
        private System.Windows.Forms.ToolStripMenuItem cntxtMenuOpenUserConfig;
        private System.Windows.Forms.ToolStripMenuItem cntxtMenuAspectCorrectionOn;
        private System.Windows.Forms.ToolStripMenuItem cntxtMenuAspectCorrectionOff;
        private System.Windows.Forms.ToolStripMenuItem cntxtMenuStretchMode;
        private System.Windows.Forms.ToolStripMenuItem cntxtMenuFullScreen;
        private System.Windows.Forms.ToolStripMenuItem cntxtMenuOptions;
    }
}

