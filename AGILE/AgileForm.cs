using AGI;
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

        private static string xmlEditPath = Properties.Settings.Default.xmlEditPath;

        /// <summary>
        /// The number of TimeSpan Ticks to achieve 60 times a second.
        /// </summary>
        private TimeSpan targetElaspedTime = TimeSpan.FromTicks(166667);

        #endregion Declares, Imports. etc.

        /// <summary>
        /// Constructor for AgileForm.
        /// </summary>
        /// <param name="game">The Game from which we'll get all of the game data.</param>
        public AgileForm(Game game)
        {
            InitializeComponent();

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

            // Create the AGI screen and add to the Form.
            this.screen = new GameScreen();
            this.Controls.Add(screen);

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

            this.Show();
            this.Activate();
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

            AdjustGameScreen();

            #endregion Load Screen Metrics

        }

        /// <summary>
        /// Stops the sound playing thread and saves form settings on close.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AgileForm_Closing(object sender, FormClosingEventArgs e)
        {
            interpreter.ShutdownSound();

            #region Save Screen Metrics

            // Save screen metrics (not for full screen, as this isn't working too well)
            if (!fullScreen)
            {
                Properties.Settings.Default.AgileFormLocation = this.Location;
                Properties.Settings.Default.AgileFormSize = this.Size;
            }

            #endregion Save Screen Metrics

            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Adjusts the GameScreen size based on the new AgileForm size.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AgileForm_Resize(object sender, System.EventArgs e)
        {
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
        }

        #endregion Key events

        #region Context Menu

        /// <summary>
        /// Catches the context menu opening event
        ///     Can be used to alter context menu in real time
        /// </summary>
        private void cntxtMenu_Opening(object sender, CancelEventArgs e) { }

        private void cntxtMenuOpenUserConfig_Click(object sender, EventArgs e)
        {
            string configPath = Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath) + "\\user.config";

            if (!System.IO.File.Exists(xmlEditPath))
            {
                MessageBox.Show("Please set an editor in the options dialog and try again.", "xmlEditor not found!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                OptionsFrm optionsFrm = new OptionsFrm();
                optionsFrm.Show();
                return;
            }

            if (!System.IO.File.Exists(xmlEditPath))
            {
                //MessageBox.Show("Please set an editor in the options dialog and try again.", "xmlEditor not found!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                //OptionsFrm optionsFrm = new OptionsFrm();
                //optionsFrm.Show();
                //return;
            }

            try
            {
                Process.Start(xmlEditPath, configPath);
            }
            catch { }
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
