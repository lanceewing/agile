using AGI;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AGILE
{
    /// <summary>
    /// AgileForm is a Form for running the AGILE AGI interpreter for a given Game. After 
    /// creating the Interpreter, it uses a Windows Forms Timer to feed the Interpreter
    /// regular ticks at 20 times a second.
    /// </summary>
    public partial class AgileForm : Form
    {
        #region Declares, Imports. etc.

        private Interpreter interpreter;
        private GameScreen screen;

        private Stopwatch stopWatch;
        private TimeSpan lastTime;
        private TimeSpan deltaTime;

        private Boolean fullScreen = false;
        private FormWindowState windowStateBeforeFullscreen;

        private static bool nullXML = false;

        private static string xmlEditor = Properties.Settings.Default.xmlEditor;

        /// <summary>
        /// The number of TimeSpan Ticks to achieve 60 times a second.
        /// </summary>
        private TimeSpan targetElaspedTime = TimeSpan.FromTicks(166667);

        #endregion Declares, Imports. etc.

        /// <summary>
        /// Constructor for AgileForm.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public AgileForm(string[] args)
        {
            InitializeComponent();
            this.screen = new GameScreen();
            this.Controls.Add(screen);
            this.Show();
            this.Activate();
            this.StartGame(this.SelectGame(args));
        }

        /// <summary>
        /// Starts the given AGI Game.
        /// </summary>
        /// <param name="game">The Game from which we'll get all of the game data.</param>
        private void StartGame(Game game)
        {
            // Register the key event handlers for KeyUp, KeyDown, and KeyPress.
            UserInput userInput = new UserInput();
            this.KeyPreview = true;
            this.KeyDown += (s, e) => userInput.KeyDown(e);
            this.KeyUp += (s, e) => userInput.KeyUp(e);
            this.KeyPress += (s, e) => userInput.KeyPressed(e);

            // Update title with version and game name.
            Detection gameDetection = new Detection(game);
            Version appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = $"AGILE v{appVersion.Major}.{appVersion.Minor}.{appVersion.Build}.{appVersion.Revision} | {gameDetection.GameName}";

            // Create the Interpreter to run this Game.
            this.interpreter = new Interpreter(game, userInput, screen.Pixels);

            // Start a timer to call our game loop 60 times a second (1000ms/60 = ~16.67ms). We use 
            // the Timers Timer so that we don't block the UI thread when the Interpreter cycle
            // is in a wait state (e.g. when a window or the menu is being displayed).
            System.Timers.Timer timer = new System.Timers.Timer(1000 / 60);
            timer.Elapsed += InterpreterTick;
            timer.Start();

            // Start a StopWatch so that we can calculate the delta time.
            this.stopWatch = Stopwatch.StartNew();
            this.lastTime = stopWatch.Elapsed;
            this.deltaTime = TimeSpan.Zero;
        }

        /// <summary>
        /// Attempts to load an AGI game from the given game folder.
        /// </summary>
        /// <param name="gameFolder">The folder to attempt to load the AGI game from.</param>
        /// <returns>The Game from which we'll get all of the game data.</returns>
        private Game LoadGame(string gameFolder)
        {
            // Use a dummy TextGraphics instance to render the "Loading" text in grand AGI fashion.
            TextGraphics textGraphics = new TextGraphics(screen.Pixels, null, null);
            try
            {
                textGraphics.DrawString(screen.Pixels, "Loading... Please wait", 72, 88, 15, 0);
                screen.Render();
                this.Refresh();
                return new AGI.Game(gameFolder);
            }
            finally
            {
                textGraphics.ClearLines(0, 24, 0);
                screen.Render();
                this.Refresh();
            }
        }

        /// <summary>
        /// Selects an AGI game to run. Starts by looking for a command line parameter. If there 
        /// is a single command line parameter provided, it will use its value as the directory 
        /// in which to look for the AGI game to run. If the command line parameter is not provided, 
        /// then it looks in the current working directory. If it isn't able to find an AGI game in 
        /// that directory, then it will open a Folder Browser Dialog for you to choose the folder 
        /// that contains the AGI game. 
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The Game from which we'll get all of the game data.</returns>
        private Game SelectGame(string[] args)
        {
            // Start by attempting to run with command line argument or the current folder.
            Boolean stillChoosingGame = true, firstTime = true;
            string gameFolder = (args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
            AGI.Game game = null;
            string prompt = null;

            // Pass path argument for game folder
            foreach (string arg in args)
            {
                if (Directory.Exists(arg))
                {
                    gameFolder = arg;
                    prompt = $"No AGI game was found in {gameFolder}";

                    try
                    {
                        game = LoadGame(gameFolder);
                        stillChoosingGame = false;
                    }
                    catch { }

                    if (game != null)
                    {
                        return game;
                    }
                    else
                    {
                        MessageBox.Show(prompt, "No AGI Game Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                        gameFolder = null;
                    }
                }
            }

            while (stillChoosingGame)
            {
                try
                {
                    game = LoadGame(gameFolder);
                    stillChoosingGame = false;
                }
                catch (Exception)
                {
                    // There isn't an AGI game in the current folder, so ask player to choose a different folder.
                    using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                    {
                        prompt = "Please choose a folder containing an AGI game.";

                        if (!firstTime)
                        {
                            prompt = $"No AGI game was found in {gameFolder}\n\n{prompt}";
                        }

                        folderDialog.ShowNewFolderButton = false;
                        folderDialog.RootFolder = Environment.SpecialFolder.MyComputer;
                        folderDialog.Description = prompt;
                        folderDialog.SelectedPath = !String.IsNullOrEmpty(Properties.Settings.Default.lastBrowsePath) ?
                                Properties.Settings.Default.lastBrowsePath : gameFolder;

                        DialogResult result = folderDialog.ShowDialog(Control.FromHandle(this.Handle));
                        if (result == DialogResult.OK)
                        {
                            Properties.Settings.Default.lastBrowsePath = folderDialog.SelectedPath;
                            Properties.Settings.Default.Save();

                            gameFolder = folderDialog.SelectedPath;
                            firstTime = false;
                        }
                        else if (result == DialogResult.Cancel)
                        {
                            Environment.Exit(0);
                        }
                    }
                }
            }

            return game;
        }

        /// <summary>
        /// Dynamically adjusts the GameScreen size based on the new AgileForm size. Takes into
        /// consideration the current aspect ratio correction setting when doing this. Works for both
        /// fullscreen mode and window mode.
        /// </summary>
        private void AdjustGameScreen()
        {
            if (this.screen != null)
            {
                if (cntxtMenuStretchMode.Checked)
                {
                    // Stretch mode, so fill the window completely (i.e. stretch to fit).
                    screen.Location = new Point(0, 0);
                    screen.Dock = DockStyle.Fill;
                }
                else
                {
                    // Aspect correction on uses 4:3, and off uses 8:5.
                    int ratioWidth = (cntxtMenuAspectCorrectionOn.Checked ? 4 : 8);
                    int ratioHeight = (cntxtMenuAspectCorrectionOn.Checked ? 3 : 5);

                    // Stops GameScreen filling screen (this is mainly to support fullscreen mode)
                    screen.Dock = DockStyle.None;

                    if (Screen.FromControl(this).Bounds.Height >= ((ClientSize.Width / ratioWidth) * ratioHeight))
                    {
                        // GameScreen fills whole width, adjusting height according to aspect ratio.
                        screen.Size = new Size(ClientSize.Width, ((ClientSize.Width / ratioWidth) * ratioHeight));
                    }
                    else
                    {
                        // GameScreen fills whole height, adjusting width according to aspect ratio.
                        screen.Size = new Size(((ClientSize.Height / ratioHeight) * ratioWidth), ClientSize.Height);
                    }

                    // Center the GameScreen (this is mainly to support fullscreen mode)
                    screen.Location = new Point(
                        ClientSize.Width / 2 - screen.Size.Width / 2,
                        ClientSize.Height / 2 - screen.Size.Height / 2);

                    // Crop windown to match GameScreen size, but only if not in full screen of maximised!!
                    if (!fullScreen && (this.WindowState != FormWindowState.Maximized))
                    {
                        ClientSize = this.screen.Size;
                    }
                }
            }
        }

        #region Form Events

        /// <summary>
        /// Get initial settings
        /// </summary>
        private void AgileForm_Load(object sender, EventArgs e)
        {
            #region Load Screen Metrics

            if (!Properties.Settings.Default.AgileFormLocation.IsEmpty)
                this.Location = Properties.Settings.Default.AgileFormLocation;
            else
                this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

            if (!Properties.Settings.Default.AgileFormSize.IsEmpty)
                this.Size = Properties.Settings.Default.AgileFormSize;

            #endregion Load Screen Metrics

            // Get last fullscreen state
            if (Properties.Settings.Default.fullScreen == true)
            {
                ToggleFullscreen();
                cntxtMenuFullScreen.Checked = true;
            }
            else if (Properties.Settings.Default.fullScreen == false)
            {
                cntxtMenuFullScreen.Checked = false;
            }

            // Set aspect correction preference
            cntxtMenuAspectCorrectionOn.Checked = Properties.Settings.Default.aspect;
            cntxtMenuAspectCorrectionOff.Checked = !Properties.Settings.Default.aspect;
            AdjustGameScreen();
        }

        /// <summary>
        /// Stops the sound playing thread and saves form settings on close.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AgileForm_Closing(object sender, FormClosingEventArgs e)
        {
            interpreter.ShutdownSound();

            if (this.fullScreen)
            {
                Properties.Settings.Default.fullScreen = true;
            }
            else
            {
                Properties.Settings.Default.fullScreen = false;

                // Save Screen Metrics if not full screen
                Properties.Settings.Default.AgileFormLocation = this.Location;
                Properties.Settings.Default.AgileFormSize = this.Size;
            }

            // Save aspect correction preference
            Properties.Settings.Default.aspect = cntxtMenuAspectCorrectionOn.Checked;

            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Adjusts the GameScreen size based on the new AgileForm size.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AgileForm_Resize(object sender, System.EventArgs e)
        {
            if (this.WindowState != FormWindowState.Minimized)
                AdjustGameScreen();
        }

        #endregion Form Events

        #region Key events

        /// <summary>
        /// Invoked on key down events. Allows the windows form itself to do something in 
        /// response to a key event rather than the AGI Interpreter.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void AgileForm_KeyDown(object sender, KeyEventArgs e)
        {
            // Both ALT-ENTER and F11 toggle full screen.
            if ((e.KeyData == Keys.F11) || (e.KeyData == (Keys.Alt | Keys.Enter)))
            {
                ToggleFullscreen();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Toggles full screen mode.
        /// </summary>
        private void ToggleFullscreen()
        {
            // Important! We toggle the fullScreen flag first, so that the AdjustGameScreen method
            // has the correct value when it is responding to the changes to maximised window.
            this.fullScreen = !this.fullScreen;

            if (this.fullScreen)
            {
                this.windowStateBeforeFullscreen = this.WindowState;
                this.WindowState = FormWindowState.Normal;
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
            }
            else
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.WindowState = this.windowStateBeforeFullscreen;
            }

            // Keep context menu in sync.
            cntxtMenuFullScreen.Checked = this.fullScreen;
        }

        #endregion Key events

        #region Context Menu

        /// <summary>
        /// Catches the context menu opening event
        ///     Can be used to alter context menu in real time
        /// </summary>
        private void cntxtMenu_Opening(object sender, CancelEventArgs e) { }

        /// <summary>
        /// Switch full screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cntxtMenuFullScreen_Click(object sender, EventArgs e)
        {
            ToggleFullscreen();

            cntxtMenuFullScreen.Checked = this.fullScreen;
        }
        
        /// <summary>
        /// Opens config file for debugging
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cntxtMenuOpenUserConfig_Click(object sender, EventArgs e)
        {
            xmlEditor = Properties.Settings.Default.xmlEditor;

            string configFile = Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath) + "\\user.config";

            //if (Properties.Settings.Default.xmlEditor == null || !System.IO.File.Exists(Properties.Settings.Default.xmlEditor))
            if (!System.IO.File.Exists(Properties.Settings.Default.xmlEditor))
            {
                GetXMLEditor();
            }

            if (System.IO.File.Exists(xmlEditor))
            {
                OpenConfig();
                return;
            }

            if (!System.IO.File.Exists(xmlEditor))
            {
                DialogResult result = MessageBox.Show("Please select an XML editor in the options dialog to proceed.",
                    "XML Editor Not Found!", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (result == DialogResult.OK)
                {
                    OptionsFrm.nullXML = true;

                    OptionsFrm optionsFrm = new OptionsFrm();
                    optionsFrm.Show();
                }
                else
                    return;
            }
        }

        /// <summary>
        /// Opens Options form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cntxtMenuOptions_Click(object sender, EventArgs e)
        {
            OptionsFrm optionsFrm = new OptionsFrm();
            optionsFrm.Show();
        }

        /// <summary>
        /// Turn aspect correctiion on
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="
        private void cntxtMenuAspectCorrectionOn_Click(object sender, EventArgs e)
        {
            cntxtMenuStretchMode.Checked = false;
            cntxtMenuAspectCorrectionOff.Checked = false;
            cntxtMenuAspectCorrectionOn.Checked = true;

            AdjustGameScreen();
        }

        /// <summary>
        /// Turn aspect correctiion off
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cntxtMenuAspectCorrectionOff_Click(object sender, EventArgs e)
        {
            cntxtMenuStretchMode.Checked = false;
            cntxtMenuAspectCorrectionOff.Checked = true;
            cntxtMenuAspectCorrectionOn.Checked = false;

            AdjustGameScreen();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cntxtMenuStretchMode_Click(object sender, EventArgs e)
        {
            cntxtMenuStretchMode.Checked = true;
            cntxtMenuAspectCorrectionOff.Checked = false;
            cntxtMenuAspectCorrectionOn.Checked = false;

            AdjustGameScreen();
        }

        #endregion Context Menu

        /// <summary>
        /// Open user config file
        /// </summary>
        public static void OpenConfig()
        {
            xmlEditor = Properties.Settings.Default.xmlEditor;

            string configFile = Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath) + "\\user.config";

            try
            {
                Process.Start(xmlEditor, configFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Get default XML editor
        /// </summary>
        public static void GetXMLEditor()
        {
            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey("xmlfile\\shell\\open\\command"))
                {
                    if (key != null)
                    {
                        Object obj = key.GetValue("");
                        if (obj != null)
                        {
                            xmlEditor = obj.ToString().Split(new string[] { "\" /verb" }, StringSplitOptions.None)[0];
                            xmlEditor = xmlEditor.Split(new string[] { "\"" }, StringSplitOptions.None)[1];

                            if (File.Exists(xmlEditor))
                            {
                                Properties.Settings.Default.xmlEditor = xmlEditor;
                                Properties.Settings.Default.Save();
                            }
                        }
                    }
                }
            }
            catch { }// (Exception ex) {}
        }

        /// <summary>
        /// Processes the Tick event of the Timer, which we've requested to be triggered 60 
        /// times a second.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void InterpreterTick(object sender, EventArgs e)
        {
            // Calculate the time since the last call.
            TimeSpan currentTime = stopWatch.Elapsed;
            deltaTime += (currentTime - lastTime);
            lastTime = currentTime;

            // We can't be certain that this method is being invoked at exactly 60 times a
            // second, or that a call hasn't been skipped, so we adjust as appropriate based
            // on the delta time and play catch up if needed. This should avoid drift in the
            // AGI clock and keep the animation smooth.
            while (deltaTime > targetElaspedTime)
            {
                deltaTime -= targetElaspedTime;
                try
                {
                    interpreter.Tick();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ex: " + ex.Message);
                }
            }

            // Trigger a redraw of the GameScreen.
            screen.Render();
        }
    }
}
