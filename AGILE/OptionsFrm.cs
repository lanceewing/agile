using System;
using System.Windows.Forms;
using System.IO;

namespace AGILE
{
    /// <summary>
    /// Form to set preferences
    /// </summary>
    public partial class OptionsFrm : Form
    {
        #region Declares, Imports. etc.

        //public static string assemblyEXE = new System.Uri(Assembly.GetExecutingAssembly().CodeBase).AbsolutePath;
        //public static string assemblyPath = Path.GetDirectoryName(assemblyEXE).Replace("%20", " ");
        //public static string assemblyEXEName = Path.GetFileName(assemblyPath);

        private static string xmlEditPath = Properties.Settings.Default.xmlEditPath;
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
            if (File.Exists(Properties.Settings.Default.xmlEditPath))
            {
                xmlEditPath = Properties.Settings.Default.xmlEditPath;
                xmlEditPathTxtBox.Text = xmlEditPath;
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

        /// <summary>
        /// Sets/displays path to selected XML editor
        /// </summary>
        private void xmlEditPathTxtBox_TextChanged(object sender, EventArgs e)
        {
            if (Directory.Exists(xmlEditPathTxtBox.Text))
                xmlEditPath = xmlEditPathTxtBox.Text;
        }

        /// <summary>
        /// Opens browse dialog to select prefered XML editor
        /// </summary>
        private void browseXMLEditBtn_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDlg = new OpenFileDialog();
            openFileDlg.InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            openFileDlg.DefaultExt = "exe";
            //openFileDlg.FileName = ".exe";

            if (openFileDlg.ShowDialog() == DialogResult.Cancel) return;

            if (File.Exists(openFileDlg.FileName))
            {
                xmlEditPath = openFileDlg.FileName;
                xmlEditPathTxtBox.Text = openFileDlg.FileName;
            }
            else
            {
                string xmlEditName = Path.GetFileName(xmlEditPath);
                MessageBox.Show(xmlEditName + "'cannot be found. Please select a valid XML editor.", "XML Editor Not Found!",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region Button Events

        /// <summary>
        /// Apply all settings and close form
        /// </summary>
        private void okBtn_Click(object sender, EventArgs e)
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
        private void cancelBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Apply all settings and keep form open
        /// </summary>
        private void applyBtn_Click(object sender, EventArgs e)
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
            if (File.Exists(xmlEditPathTxtBox.Text))
            {
                xmlEditPath = xmlEditPathTxtBox.Text;
                Properties.Settings.Default.xmlEditPath = xmlEditPath;
            }
            else
            {
                MessageBox.Show("Please select your prefered XML editor folder.", "Warning!", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                // Prevent form from closing on "OK" if path null or invalid
                nullCheckCancel = true;
                return;
            }

            #endregion null checks

            #region Paths

            xmlEditPath = xmlEditPathTxtBox.Text;
            if (File.Exists(xmlEditPath))
                Properties.Settings.Default.xmlEditPath = xmlEditPath;

            #endregion Paths

            Properties.Settings.Default.Save();
        }

        #endregion Extra Methods
    }
}
