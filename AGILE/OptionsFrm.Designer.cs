namespace AGILE
{
    partial class OptionsFrm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OptionsFrm));
            this.applyBtn = new System.Windows.Forms.Button();
            this.okBtn = new System.Windows.Forms.Button();
            this.cancelBtn = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.browseXMLEditorBtn = new System.Windows.Forms.Button();
            this.xmlEditorTxtBox = new System.Windows.Forms.TextBox();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.runInAgileChkBox = new System.Windows.Forms.CheckBox();
            this.systemXMLDefaultChkBox = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // applyBtn
            // 
            this.applyBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.applyBtn.Location = new System.Drawing.Point(297, 169);
            this.applyBtn.Name = "applyBtn";
            this.applyBtn.Size = new System.Drawing.Size(75, 23);
            this.applyBtn.TabIndex = 1038;
            this.applyBtn.Text = "Apply";
            this.toolTip.SetToolTip(this.applyBtn, "Applies selected settings");
            this.applyBtn.UseVisualStyleBackColor = true;
            this.applyBtn.Click += new System.EventHandler(this.applyBtn_Click);
            // 
            // okBtn
            // 
            this.okBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okBtn.Location = new System.Drawing.Point(135, 169);
            this.okBtn.Name = "okBtn";
            this.okBtn.Size = new System.Drawing.Size(75, 23);
            this.okBtn.TabIndex = 1036;
            this.okBtn.Text = "OK";
            this.toolTip.SetToolTip(this.okBtn, "Applies selected settings and cl;oses options form");
            this.okBtn.UseVisualStyleBackColor = true;
            this.okBtn.Click += new System.EventHandler(this.okBtn_Click);
            // 
            // cancelBtn
            // 
            this.cancelBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelBtn.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelBtn.Location = new System.Drawing.Point(216, 169);
            this.cancelBtn.Name = "cancelBtn";
            this.cancelBtn.Size = new System.Drawing.Size(75, 23);
            this.cancelBtn.TabIndex = 1037;
            this.cancelBtn.Text = "Cancel";
            this.toolTip.SetToolTip(this.cancelBtn, "Closes options form without appling any changes");
            this.cancelBtn.UseVisualStyleBackColor = true;
            this.cancelBtn.Click += new System.EventHandler(this.cancelBtn_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.systemXMLDefaultChkBox);
            this.groupBox1.Controls.Add(this.textBox1);
            this.groupBox1.Controls.Add(this.browseXMLEditorBtn);
            this.groupBox1.Controls.Add(this.xmlEditorTxtBox);
            this.groupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(360, 101);
            this.groupBox1.TabIndex = 1032;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "External XML Editor";
            // 
            // textBox1
            // 
            this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox1.BackColor = System.Drawing.SystemColors.Control;
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox1.HideSelection = false;
            this.textBox1.Location = new System.Drawing.Point(6, 19);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.ShortcutsEnabled = false;
            this.textBox1.Size = new System.Drawing.Size(348, 48);
            this.textBox1.TabIndex = 1000;
            this.textBox1.TabStop = false;
            this.textBox1.Text = "Select external XML editor for manually editing save files.";
            // 
            // browseXMLEditorBtn
            // 
            this.browseXMLEditorBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.browseXMLEditorBtn.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.browseXMLEditorBtn.Location = new System.Drawing.Point(327, 71);
            this.browseXMLEditorBtn.Name = "browseXMLEditorBtn";
            this.browseXMLEditorBtn.Size = new System.Drawing.Size(27, 23);
            this.browseXMLEditorBtn.TabIndex = 0;
            this.browseXMLEditorBtn.Text = "...";
            this.toolTip.SetToolTip(this.browseXMLEditorBtn, "Browse to select default external XML editor");
            this.browseXMLEditorBtn.UseVisualStyleBackColor = true;
            this.browseXMLEditorBtn.Click += new System.EventHandler(this.browseXMLEditorBtn_Click);
            // 
            // xmlEditorTxtBox
            // 
            this.xmlEditorTxtBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.xmlEditorTxtBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.xmlEditorTxtBox.Location = new System.Drawing.Point(6, 73);
            this.xmlEditorTxtBox.Name = "xmlEditorTxtBox";
            this.xmlEditorTxtBox.Size = new System.Drawing.Size(315, 20);
            this.xmlEditorTxtBox.TabIndex = 1;
            this.toolTip.SetToolTip(this.xmlEditorTxtBox, "Sets path to preferred external XML editor");
            this.xmlEditorTxtBox.TextChanged += new System.EventHandler(this.xmlEditorTxtBox_TextChanged);
            // 
            // runInAgileChkBox
            // 
            this.runInAgileChkBox.AutoSize = true;
            this.runInAgileChkBox.Location = new System.Drawing.Point(18, 119);
            this.runInAgileChkBox.Name = "runInAgileChkBox";
            this.runInAgileChkBox.Size = new System.Drawing.Size(233, 17);
            this.runInAgileChkBox.TabIndex = 1039;
            this.runInAgileChkBox.Text = "Add \"Run in Agile\" folder context menu item";
            this.runInAgileChkBox.UseVisualStyleBackColor = true;
            this.runInAgileChkBox.CheckedChanged += new System.EventHandler(this.runInAgileChkBox_CheckedChanged);
            // 
            // systemXMLDefaultChkBox
            // 
            this.systemXMLDefaultChkBox.AutoSize = true;
            this.systemXMLDefaultChkBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.systemXMLDefaultChkBox.Location = new System.Drawing.Point(6, 50);
            this.systemXMLDefaultChkBox.Name = "systemXMLDefaultChkBox";
            this.systemXMLDefaultChkBox.Size = new System.Drawing.Size(115, 17);
            this.systemXMLDefaultChkBox.TabIndex = 1040;
            this.systemXMLDefaultChkBox.Text = "Use system default";
            this.systemXMLDefaultChkBox.UseVisualStyleBackColor = true;
            this.systemXMLDefaultChkBox.CheckedChanged += new System.EventHandler(this.systemXMLDefaultChkBox_CheckedChanged);
            // 
            // OptionsFrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelBtn;
            this.ClientSize = new System.Drawing.Size(384, 204);
            this.Controls.Add(this.runInAgileChkBox);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.applyBtn);
            this.Controls.Add(this.okBtn);
            this.Controls.Add(this.cancelBtn);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(400, 195);
            this.Name = "OptionsFrm";
            this.Text = "Wiki Page Generator Options";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.OptionsFrm_FormClosing);
            this.Load += new System.EventHandler(this.Form_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button applyBtn;
        private System.Windows.Forms.Button okBtn;
        private System.Windows.Forms.Button cancelBtn;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button browseXMLEditorBtn;
        private System.Windows.Forms.TextBox xmlEditorTxtBox;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.CheckBox runInAgileChkBox;
        private System.Windows.Forms.CheckBox systemXMLDefaultChkBox;
    }
}
