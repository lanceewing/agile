using AGI;
using IWshRuntimeLibrary;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using static AGI.Resource.Logic;

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

        public static string assemblyEXE = new System.Uri(Assembly.GetExecutingAssembly().CodeBase).AbsolutePath;
        public static string assemblyPath = Path.GetDirectoryName(assemblyEXE).Replace("%20", " ");

        private Interpreter interpreter;
        private GameScreen screen;

        private Stopwatch stopWatch;
        private TimeSpan lastTime;
        private TimeSpan deltaTime;

        private Boolean fullScreen = false;
        private FormWindowState windowStateBeforeFullscreen;

        private static string xmlEditor = Properties.Settings.Default.xmlEditor;

        /// <summary>
        /// The number of TimeSpan Ticks to achieve 60 times a second.
        /// </summary>
        private TimeSpan targetElaspedTime = TimeSpan.FromTicks(166667);

        // Shortcut variables
        private static string gameName = null;
        private static string gameFolder = null;
        private static string icoPath = null;
        private static bool useIcon = false;

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
            this.StartGame(this.SelectGame(args), args);
        }

        /// <summary>
        /// Starts the given AGI Game.
        /// </summary>
        /// <param name="game">The Game from which we'll get all of the game data.</param>
        private void StartGame(Game game, string[] args)
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
            gameName = gameDetection.GameName;

            // Get patch game settings from UI / Application properties
            bool patchGameSetting = Properties.Settings.Default.patchGames;
            // Get patch game settings from command line args
            bool patchGameArg = args.Contains("--patch-game");

            if (patchGameSetting || patchGameArg)
            {
                // Applies patch to the game to skip the starting question(s).
                this.PatchGame(game, gameDetection.GameId, gameDetection.GameName);
            }

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
        /// Patches the given games's Logic scripts, so that the starting question is skipped.
        /// </summary>
        /// <param name="game">Game to patch the Logics for.</param>
        /// <param name="gameId">The detected game ID.</param>
        /// <param name="gameName">The detected game name.</param>
        /// <returns>The patched Game.</returns>
        private Game PatchGame(Game game, String gameId, String gameName)
        {
            foreach (Volume volume in game.Volumes)
            {
                foreach (Resource resource in volume.Logics)
                {
                    Resource.Logic logic = (Resource.Logic)resource;
                    List<Resource.Logic.Action> actions = logic.Actions;

                    switch (gameId)
                    {
                        case "GR":
                            // Gold Rush version 3.0 doesn't have copy protection
                            if (gameName.Contains("3.0"))
                            {
                                break;      
                            }
                            if (resource.Index == 129)
                            {
                                // Changes the new.room(125) to be new.room(73) instead, thus skipping the questions.
                                Resource.Logic.Action action = actions[27];
                                if ((action.Operation.Opcode == 18) && (action.Operands[0].asInt() == 125))
                                {
                                    action.Operands[0] = new Resource.Logic.Operand(Resource.Logic.OperandType.NUM, 73);
                                }
                            }
                            break;

                        case "MH1":
                            if (resource.Index == 159)
                            {
                                // Modifies LOGIC.159 to jump to the code that is run when a successful answer is entered.
                                if ((actions[134].Operation.Opcode == 18) && (actions[134].Operands[0].asInt() == 153))
                                {
                                    actions[0] = new GotoAction(new List<Operand>() { new Operand(OperandType.ADDRESS, actions[132].Address) });
                                    actions[0].Logic = logic;
                                }
                            }
                            break;

                        case "KQ4":
                            if (resource.Index == 0)
                            {
                                // Changes the new.room(140) to be new.room(96) instead, thus skipping the questions.
                                Resource.Logic.Action action = actions[55];
                                if ((action.Operation.Opcode == 18) && (action.Operands[0].asInt() == 140))
                                {
                                    action.Operands[0] = new Resource.Logic.Operand(Resource.Logic.OperandType.NUM, 96);
                                }
                            }
                            break;

                        case "LLLLL":
                            if (resource.Index == 6)
                            {
                                // Modifies LOGIC.6 to jump to the code that is run when all of the trivia questions has been answered correctly.
                                Resource.Logic.Action action = actions[0];                                
                                // Verify that the action is the if-condition to check if the user can enter the game.
                                if (action.Operation.Opcode == 255 && action.Operands.Count == 2)
                                {
                                    actions[0] = new GotoAction(new List<Operand>() { new Operand(OperandType.ADDRESS, actions[1].Address) });
                                    actions[0].Logic = logic;

                                    // Skips the 'Thank you. And now, slip into your leisure suit and prepare to enter the
                                    // "Land of the Lounge Lizards" with "Leisure "Suit Larry!"' message
                                    int printIndex = 9;
                                    Resource.Logic.Action printAction = actions[printIndex];

                                    // Verify it's the print function
                                    if (printAction.Operation.Opcode == 101)
                                    {
                                        // Go to next command in the logic, which is the new.room command
                                        actions[printIndex] = new GotoAction(new List<Operand>() { new Operand(OperandType.ADDRESS, actions[printIndex + 1].Address) });
                                        actions[printIndex].Logic = logic;
                                    }
                                }                               
                            }
                            break;

                        default:
                            break;
                    }
                }
            }

            return game;
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
                if (System.IO.File.Exists(gameFolder + "\\WORDS.TOK"))
                {
                    textGraphics.DrawString(screen.Pixels, "Loading... Please wait", 72, 88, 15, 0);
                    screen.Render();
                    this.Refresh();
                }
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
            gameFolder = (args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
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
                    using (FolderSelectionDialog folderDialog = new FolderSelectionDialog())
                    {
                        prompt = "Please choose a folder containing an AGI game.";

                        if (!firstTime)
                        {
                            prompt = $"No AGI game was found in {gameFolder}\n\n{prompt}";
                        }

                        folderDialog.ShowNewFolderButton = false;
                        folderDialog.Description = prompt;
                        folderDialog.SelectedPath = !String.IsNullOrEmpty(Properties.Settings.Default.lastBrowsePath) ?
                                Properties.Settings.Default.lastBrowsePath : gameFolder;

                        // Open to desktop if lastBrowsePath doesnot exist
                        if (String.IsNullOrEmpty(Properties.Settings.Default.lastBrowsePath) || Properties.Settings.Default.lastBrowsePath == assemblyPath)
                            folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

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
        private void CntxtMenu_Opening(object sender, CancelEventArgs e) { }

        /// <summary>
        /// Opens config file for debugging
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CntxtMenuOpenUserConfig_Click(object sender, EventArgs e)
        {
            //// Open config file folder
            //Process.Start(Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath));
            //    return;

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
        private void CntxtMenuOptions_Click(object sender, EventArgs e)
        {
            OptionsFrm optionsFrm = new OptionsFrm();
            optionsFrm.Show();
        }

        /// <summary>
        /// Switch full screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CntxtMenuFullScreen_Click(object sender, EventArgs e)
        {
            ToggleFullscreen();

            cntxtMenuFullScreen.Checked = this.fullScreen;
        }

        /// <summary>
        /// Turn aspect correctiion on
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="
        private void CntxtMenuAspectCorrectionOn_Click(object sender, EventArgs e)
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
        private void CntxtMenuAspectCorrectionOff_Click(object sender, EventArgs e)
        {
            cntxtMenuStretchMode.Checked = false;
            cntxtMenuAspectCorrectionOff.Checked = true;
            cntxtMenuAspectCorrectionOn.Checked = false;

            AdjustGameScreen();
        }

        /// <summary>
        /// Turn on strech mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CntxtMenuStretchMode_Click(object sender, EventArgs e)
        {
            cntxtMenuStretchMode.Checked = true;
            cntxtMenuAspectCorrectionOff.Checked = false;
            cntxtMenuAspectCorrectionOn.Checked = false;

            AdjustGameScreen();
        }

        /// <summary>
        /// Create Agile shortcut to current game
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CntxtMenuCreateShortcut_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Would you like to select an icon for this shortcut?", "Creating a " + gameName + " shortcut", MessageBoxButtons.YesNoCancel);

            if (result == DialogResult.Yes)
            {
                useIcon = true;
                BrowseForIcon();
            }
            if (result == DialogResult.No)
            {
                useIcon = false;
                icoPath = null;
            }
            if (result == DialogResult.Cancel)
                return;

            gameName = GetSafeName(gameName, "My AGI Game");
            CreateShortcut(gameName, Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Assembly.GetExecutingAssembly().Location);
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

                            if (System.IO.File.Exists(xmlEditor))
                            {
                                Properties.Settings.Default.xmlEditor = xmlEditor;
                                Properties.Settings.Default.Save();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Selects icon for shortcut
        /// </summary>
        private static void BrowseForIcon()
        {
            OpenFileDialog openFileDlg = new OpenFileDialog();
            openFileDlg.Title = "Select an Icon";
            openFileDlg.CheckFileExists = true;
            openFileDlg.CheckPathExists = true;
            openFileDlg.ValidateNames = true;
            openFileDlg.Multiselect = false;
            openFileDlg.DefaultExt = "ico";
            openFileDlg.Filter = "Icon Files|*.ICO|All Files|*.*";
            openFileDlg.RestoreDirectory = true;

            string lastIcoPath = Properties.Settings.Default.lastIcoPath;
            if (Directory.Exists(lastIcoPath))
                openFileDlg.InitialDirectory = lastIcoPath;
            else
                openFileDlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Return if Cancel is pressed
            if (openFileDlg.ShowDialog() == DialogResult.Cancel) return;

            // Get selected icon
            icoPath = openFileDlg.FileName;
            Properties.Settings.Default.lastIcoPath = Path.GetDirectoryName(openFileDlg.FileName);
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Creates Agile destop icon for current game
        /// </summary>
        /// <param name="shortcutName">Name to be assigned to shortcut</param>
        /// <param name="shortcutPath">game folder for shortcut argument</param>
        /// <param name="targetFileLocation">Agile's path</param>
        public static void CreateShortcut(string shortcutName, string shortcutPath, string targetFileLocation)
        {
            string shortcutLocation = Path.Combine(shortcutPath, shortcutName + ".lnk");
            WshShell shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutLocation);

            shortcut.Description = "Start in AGILE";            // The description of the shortcut

            if (useIcon == true)
            {
                if (System.IO.File.Exists(icoPath))
                    shortcut.IconLocation = icoPath;            // The icon of the shortcut
                else
                    MessageBox.Show("Icon not found. No icon will be used.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            shortcut.TargetPath = targetFileLocation;           // The path of the file that will launch when the shortcut is run
            shortcut.Arguments = "\"" + gameFolder + "\"";      // The path of the game folder to be used for Agile argument 
            shortcut.Save();                                    // Save the shortcut
        }

        /// <summary>
        /// Removes invalid characters from game name
        /// </summary>
        /// <param name="inName">Name to remove invalid path characters</param>
        /// <param name="failName">Name to use if inName is void</param>
        /// <returns>File System Safe Name</returns>
        public static string GetSafeName(string inName, string failName) //, string outName)
        {
            // Remove ivalid Charsfile (system safe name)
            if (!String.IsNullOrEmpty(inName))
            {
                inName = inName.Replace(":", "-");
                inName = inName.Replace("*", "");
                inName = inName.Replace("?", "");
                inName = inName.Replace("/", "");
                inName = inName.Replace(@"\", "");
                inName = inName.Replace("|", "");
                inName = inName.Replace("\"", "'");
                inName = inName.Replace("<", "");
                inName = inName.Replace(">", "");
                return inName;
            }
            else
            {
                return inName = failName;
            }
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
