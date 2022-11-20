using System;
using System.Windows.Forms;
using System.IO;
using Microsoft.Win32;
using System.Reflection;

namespace AGILE
{
    /// <summary>
    /// Form to set preferences
    /// </summary>
    public partial class OptionsFrm : Form
    {
        #region Declares, Imports. etc.

        public static bool? useSystemXMLDefault = Properties.Settings.Default.useSystemXMLDefault;
        public static bool? patchGames = Properties.Settings.Default.patchGames;
        private static string xmlEditor = Properties.Settings.Default.xmlEditor;
        private static string currentXMLEditor = null;
        public static bool nullXML = false;
        public static bool nullCheckCancel = false;
        //AgileForm agileForm = (AgileForm)Application.OpenForms["AgileForm"];

        #endregion Declares, Imports. etc.

        #region Constructor

        public OptionsFrm()
        {
            InitializeComponent();
        }

        #endregion Constructor

        #region Form Events

        /// <summary>
        /// Load saved settings
        /// </summary>
        private void Form_Load(object sender, EventArgs e)
        {
            #region Load Screen Metrics

            if (!Properties.Settings.Default.OptionsFrmLocation.IsEmpty)
                this.Location = Properties.Settings.Default.OptionsFrmLocation;
            else
                this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

            #endregion Load Screen Metrics

            // Populate controls
            currentXMLEditor = Properties.Settings.Default.xmlEditor;

            // Get if default XML editor is to be used
            if (useSystemXMLDefault.HasValue)
            {
                systemXMLDefaultChkBox.Checked = Properties.Settings.Default.useSystemXMLDefault;
                SystemXMLDefaultChkBox_CheckedChanged(null, null);
            }
            else if (File.Exists(Properties.Settings.Default.xmlEditor))
            {
                xmlEditor = Properties.Settings.Default.xmlEditor;
                xmlEditorTxtBox.Text = xmlEditor;
            }

            // Deselect text in xmlEditorTxtBox
            xmlEditorTxtBox.Select(0, 0);

            // Get status of directory shellex 
            RegistryKey rkSubKey = Registry.ClassesRoot.OpenSubKey("Directory\\shell\\AGILE", false);
            if (rkSubKey == null)
                runInAgileChkBox.Checked = false;
            else
                runInAgileChkBox.Checked = true;

            // Get value of patchGames
            if (patchGames.HasValue)
            {
                patchGameChkBox.Checked = Properties.Settings.Default.patchGames;
            }
        }

        /// <summary>
        /// Save form states
        /// </summary>
        private void OptionsFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            #region Save Screen Metrics

            // Save screen metrics
            Properties.Settings.Default.OptionsFrmLocation = this.Location;

            #endregion Save Screen Metrics

            Properties.Settings.Default.Save();
        }

        #endregion Form Events

        #region Paths

        /// <summary>
        /// Sets/displays if Agile is to use the system default XML editor or user specified
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SystemXMLDefaultChkBox_CheckedChanged(object sender, EventArgs e)
        {
            xmlEditorTxtBox.Enabled = !systemXMLDefaultChkBox.Checked;
            browseXMLEditorBtn.Enabled = !systemXMLDefaultChkBox.Checked;

            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey("xmlfile\\shell\\open\\command"))
                {
                    if (key != null)
                    {
                        Object o = key.GetValue("");
                        string xml = key.GetValue("").ToString();
                        if (!String.IsNullOrEmpty(xml))
                            xml = xml.Replace("\"", null);
                        xml = xml.Split(new string[] { @".exe" }, StringSplitOptions.None)[0] + ".exe";
                        if (File.Exists(xml))
                            xmlEditorTxtBox.Text = xml;
                        else
                            xmlEditorTxtBox.Text = Properties.Settings.Default.xmlEditor;
                    }
                }
            }
            catch //*(Exception ex)*/
            { }

        }

        /// <summary>
        /// Sets/displays path to selected XML editor
        /// </summary>
        private void XMLEditorTxtBox_TextChanged(object sender, EventArgs e)
        {
            if (Directory.Exists(xmlEditorTxtBox.Text))
                xmlEditor = xmlEditorTxtBox.Text;
        }

        /// <summary>
        /// Opens browse dialog to select prefered XML editor
        /// </summary>
        private void BrowseXMLEditorBtn_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDlg = new OpenFileDialog();
            openFileDlg.InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            openFileDlg.DefaultExt = "exe";
            //openFileDlg.FileName = ".exe";

            if (openFileDlg.ShowDialog() == DialogResult.Cancel) return;

            if (File.Exists(openFileDlg.FileName))
            {
                xmlEditor = openFileDlg.FileName;
                xmlEditorTxtBox.Text = openFileDlg.FileName;
            }
            else
            {
                string xmlEditName = Path.GetFileName(xmlEditor);
                MessageBox.Show(xmlEditName + "'cannot be found. Please select a valid XML editor.", "XML Editor Not Found!",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion Paths

        #region Button Events

        /// <summary>
        /// Apply all settings and close form
        /// </summary>
        private void OKBtn_Click(object sender, EventArgs e)
        {
            Apply();

            // Close only if no null fields
            if (nullCheckCancel == false)
            {
                this.Close();
                nullCheckCancel = false;
            }
            else
                nullCheckCancel = false;
        }

        /// <summary>
        /// Close form without saving changes
        /// </summary>
        private void CancelBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Apply all settings and keep form open
        /// </summary>
        private void ApplyBtn_Click(object sender, EventArgs e)
        {
            Apply();
        }

        #endregion Button Events

        #region Extra Methods

        /// <summary>
        /// Applys all settings
        /// </summary>
        private void Apply()
        {
            #region null checks

            // Check selected all projects path
            if (File.Exists(xmlEditorTxtBox.Text))
            {
                xmlEditor = xmlEditorTxtBox.Text;
                Properties.Settings.Default.xmlEditor = xmlEditor;
            }
            else
            {
                MessageBox.Show("Please select your prefered XML editor.", "Warning!", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                // Prevent form from closing on "OK" if path null or invalid
                nullCheckCancel = true;
                return;
            }

            #endregion null checks

            Properties.Settings.Default.useSystemXMLDefault = systemXMLDefaultChkBox.Checked;

            #region Paths

            xmlEditor = xmlEditorTxtBox.Text;
            if (File.Exists(xmlEditor))
                Properties.Settings.Default.xmlEditor = xmlEditor;

            #endregion Paths

            #region Set directory context menu

            string appEXE = Assembly.GetEntryAssembly().Location;

            // Set Registry entry to associate project files with the Game Archival Tool
            if (runInAgileChkBox.Checked == true)
            {
                try
                {
                    Registry.SetValue(@"HKEY_CLASSES_ROOT\Directory\shell\AGILE", "", "Run with AGILE");
                    Registry.SetValue(@"HKEY_CLASSES_ROOT\Directory\shell\AGILE", "Icon", Assembly.GetEntryAssembly().Location);

                    Registry.SetValue(@"HKEY_CLASSES_ROOT\Directory\shell\AGILE\command", "", @"""" + appEXE + @"""" + @" ""--working-dir"" ""%1""");
                }
                catch { }
            }
            else
            {
                try
                {
                    Registry.ClassesRoot.DeleteSubKeyTree(@"HKEY_CLASSES_ROOT\Directory\shell\AGILE");
                }
                catch { }
            }

            #endregion Set directory context menu

            Properties.Settings.Default.patchGames = patchGameChkBox.Checked;

            Properties.Settings.Default.Save();

            // Open user config if options was called from trying to open config without XML editor being specified
            if (currentXMLEditor != xmlEditorTxtBox.Text)
                AgileForm.OpenConfig();
        }

        #endregion Extra Methods
    }
}
