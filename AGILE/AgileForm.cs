﻿using AGI;
using System;
using System.ComponentModel;
using System.Diagnostics;
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

        private Interpreter interpreter;
        private GameScreen screen;

        private Stopwatch stopWatch;
        private TimeSpan lastTime;
        private TimeSpan deltaTime;

        /// <summary>
        /// The number of TimeSpan Ticks to achieve 60 times a second.
        /// </summary>
        private TimeSpan targetElaspedTime = TimeSpan.FromTicks(166667);

        /// <summary>
        /// Constructor for MekaForm.
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
            this.FormClosing += AgileForm_Closing;

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
        }

        /// <summary>
        /// Invoked when this form is closing. We use this to stop the sound playing thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void AgileForm_Closing(object sender, CancelEventArgs e)
        {
            interpreter.StopSound();
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
