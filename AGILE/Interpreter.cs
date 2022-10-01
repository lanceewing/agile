using AGI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static AGI.Resource;

namespace AGILE
{
    /// <summary>
    /// Interpreter is the core class in the MEKA AGI interpreter. It controls the overall interpreter cycle.
    /// </summary>
    class Interpreter
    {
        /// <summary>
        /// The GameState class holds all of the data and state for the Game currently 
        /// being run by the interpreter.
        /// </summary>
        private GameState state;

        /// <summary>
        /// Holds the data and state for user input events, such as keyboard and mouse input.
        /// </summary>
        private UserInput userInput;

        /// <summary>
        /// The pixels array for the AGI screen on which the background Picture and 
        /// AnimatedObjects will be drawn to.
        /// </summary>
        private int[] pixels;

        /// <summary>
        /// Provides methods for drawing text on to the AGI screen.
        /// </summary>
        private TextGraphics textGraphics;

        /// <summary>
        /// Direct reference to AnimatedObject number one, i.e. ego, the main character.
        /// </summary>
        private AnimatedObject ego;

        /// <summary>
        /// Performs the execution of the LOGIC scripts.
        /// </summary>
        private Commands commands;

        /// <summary>
        /// Responsible for displaying the menu system.
        /// </summary>
        private Menu menu;

        /// <summary>
        /// Responsible for parsing the user input line to match known words.
        /// </summary>
        private Parser parser;

        /// <summary>
        /// Responsible for playing Sound resources.
        /// </summary>
        private SoundPlayer soundPlayer;

        /// <summary>
        /// Indicates that a thread is currently executing the Tick, i.e. a single interpretation 
        /// cycle. This flag exists because there are some AGI commands that wait for something to 
        /// happen before continuing. For example, a print window will stay up for a defined timeout
        /// period or until a key is pressed. In such cases, the thread can be in the Tick method 
        /// for the duration of what would normally be many Ticks. 
        /// </summary>
        private volatile bool inTick;

        /// <summary>
        /// Constructor for Interpreter.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="userInput"></param>
        /// <param name="pixels"></param>
        public Interpreter(Game game, UserInput userInput, int[] pixels)
        {
            this.state = new GameState(game);
            this.userInput = userInput;
            this.pixels = pixels;
            this.textGraphics = new TextGraphics(pixels, state, userInput);
            this.parser = new Parser(state);
            this.soundPlayer = new SoundPlayer(state);
            this.menu = new Menu(state, textGraphics, pixels, userInput);
            this.commands = new Commands(pixels, state, userInput, textGraphics, parser, soundPlayer, menu);
            this.ego = state.Ego;
            this.state.Init();
            this.textGraphics.UpdateInputLine();
        }

        /// <summary>
        /// Updates the internal AGI game clock. This method is invoked once a second.
        /// </summary>
        private void UpdateGameClock()
        {
            if (++state.Vars[Defines.SECONDS] >= 60)
            {
                // One minute has passed.
                if (++state.Vars[Defines.MINUTES] >= 60)
                { 
                    // One hour has passed.
                    if (++state.Vars[Defines.HOURS] >= 24)
                    {
                        // One day has passed.
                        state.Vars[Defines.DAYS]++;
                        state.Vars[Defines.HOURS] = 0;
                    }

                    state.Vars[Defines.MINUTES] = 0;
                }

                state.Vars[Defines.SECONDS] = 0;
            }
        }

        /// <summary>
        /// Executes a single AGI interpreter tick, or cycle. This method is invoked 60 times a
        /// second, but the rate at which the logics are run and the animation updated is determined
        /// by the animation interval variable.
        /// </summary>
        public void Tick()
        {
            // Regardless of whether we're already in a Tick, we keep counting the number of Ticks.
            state.TotalTicks++;

            // Tick is called 60 times a second, so every 60th call, the second clock ticks. We 
            // deliberately do this outside of the main Tick block because some scripts wait for 
            // the clock to reach a certain clock value, which will never happen if the block isn't
            // updated outside of the Tick block.
            if ((state.TotalTicks % 60) == 0)
            {
                UpdateGameClock();
            }

            // Only one thread can be running the core interpreter cycle at a time.
            if (!inTick)
            {
                inTick = true;

                // Proceed only if the animation tick count has reached the set animation interval x 3.
                if (++state.AnimationTicks < (state.Vars[Defines.ANIMATION_INT] * 3))
                {
                    inTick = false;
                    return;
                }

                // Reset animation tick count.
                state.AnimationTicks = 0;

                // Clear controllers and get user input.
                ProcessUserInput();

                // Update input line text on every cycle.
                textGraphics.UpdateInputLine(false);

                // If ego is under program control, override user input as to his direction.
                if (!state.UserControl)
                {
                    state.Vars[Defines.EGODIR] = ego.Direction;
                }
                else
                {
                    ego.Direction = state.Vars[Defines.EGODIR];
                }

                // Calculate the direction in which objects will move, based on their MotionType. We do
                // this here, i.e. call UpdateObjectDirections() before starting the logic scan, to
                // allow ego's direction to be known to the logics even when ego is on a move.obj().
                UpdateObjectDirections();

                // Continue scanning LOGIC 0 while the return value is above 0, indicating a room change.
                while (NewRoom(commands.ExecuteLogic(0))) ;

                // Set ego's direction from the variable.
                ego.Direction = state.Vars[Defines.EGODIR];

                // Update the status line, in case the score or sound status have changed.
                textGraphics.UpdateStatusLine();

                state.Vars[Defines.OBJHIT] = 0;
                state.Vars[Defines.OBJEDGE] = 0;

                // Clear the restart, restore, & init logics flags.
                state.Flags[Defines.INITLOGS] = false;
                state.Flags[Defines.RESTART] = false;
                state.Flags[Defines.RESTORE] = false;

                // If in graphics mode, animate the AnimatedObjects.
                if (state.GraphicsMode)
                {
                    AnimateObjects();
                }

                // If there is an open text window, we render it now.
                if (textGraphics.IsWindowOpen())
                {
                    textGraphics.DrawWindow();
                }

                // Store what the key states were in this cycle before leaving.
                for (int i = 0; i < 256; i++) userInput.OldKeys[i] = userInput.Keys[i];

                inTick = false;
            }
        }

        /// <summary>
        /// Stops the sound thread if it is currently running.
        /// </summary>
        public void StopSound()
        {
            soundPlayer.StopSound();
        }

        /// <summary>
        /// If the room has changed, then performs all the necessary updates to vars, flags, 
        /// animated objects, controllers, and other state to prepare for entry in to the 
        /// next room. If the room hasn't changed, it returns false up front and does nothing
        /// else.
        /// </summary>
        /// <param name="roomNum"></param>
        /// <returns>true if the room has changed; otherwise false.</returns>
        private bool NewRoom(byte roomNum)
        {
            // Has the room changed?
            if (roomNum == state.CurrentRoom) return false;

            // Simulate a slow room change if there is a text window open.
            if (textGraphics.IsWindowOpen()) Thread.Sleep(1000);

            // Turn off sound.
            soundPlayer.StopSound();
            soundPlayer.ClearCache();

            // Clear the script event buffer ready for next room.
            state.ScriptBuffer.InitScript();
            state.ScriptBuffer.ScriptOn();

            // Resets the Logics, Views, Pictures and Sounds back to new room state.
            state.ResetResources();

            // Carry over ego's view number.
            // TODO: For some reason in MH2, the ego View can be null at this point. Needs investigation to determine why.
            if (ego.View != null)
            {
                state.Vars[Defines.CURRENT_EGO] = (byte)ego.View.Index;
            }

            // Reset state for all animated objects.
            foreach (AnimatedObject aniObj in state.AnimatedObjects) aniObj.Reset();

            // Current room logic is loaded automatically on room change and not directly by load.logic
            Logic logic = state.Logics[roomNum];
            logic.IsLoaded = true;
            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.LoadLogic, logic.Index);

            // If ego collided with a border, set his position in the new room to
            // the appropriate edge of the screen.
            switch (state.Vars[Defines.EGOEDGE])
            {
                case Defines.TOP:
                    ego.Y = Defines.MAXY;
                    break;

                case Defines.RIGHT:
                    ego.X = Defines.MINX;
                    break;

                case Defines.BOTTOM:
                    ego.Y = Defines.HORIZON + 1;
                    break;

                case Defines.LEFT:
                    ego.X = (short)(Defines.MAXX + 1 - ego.XSize);
                    break;
            }

            // Change the room number. Note that some games, e.g. MH2, change the CURROOM VAR directly, 
            // which is why we also track the CurrentRoom in a separate state variable. We can't rely
            // on the AGI VAR that stores the current room.
            state.Vars[Defines.PREVROOM] = state.CurrentRoom;
            state.Vars[Defines.CURROOM] = state.CurrentRoom = roomNum;

            // Set flags and vars as appropriate for a new room.
            state.Vars[Defines.OBJHIT] = 0;
            state.Vars[Defines.OBJEDGE] = 0;
            state.Vars[Defines.UNKNOWN_WORD] = 0;
            state.Vars[Defines.EGOEDGE] = 0;
            state.Flags[Defines.INPUT] = false;
            state.Flags[Defines.INITLOGS] = true;
            state.UserControl = true;
            state.Blocking = false;
            state.Horizon = Defines.HORIZON;
            state.ClearControllers();

            // Draw the status line background if applicable.
            if (state.ShowStatusLine) textGraphics.ClearLines(state.StatusLineRow, state.StatusLineRow, 15);

            // Return true to indicate to the scan loop to rescan.
            return true;
        }

        /// <summary>
        /// Animates each of the AnimatedObjects that are currently on the screen. This 
        /// involves the cell cycling, the movement, and the drawing to the screen.
        /// </summary>
        private void AnimateObjects()
        {
            // Ask each AnimatedObject to update its loop and cell number if required.
            foreach (AnimatedObject aniObj in state.AnimatedObjects)
            {
                aniObj.UpdateLoopAndCel();
            }

            state.Vars[Defines.EGOEDGE] = 0;
            state.Vars[Defines.OBJHIT] = 0;
            state.Vars[Defines.OBJEDGE] = 0;

            // Restore the backgrounds of the previous drawn cels for each AnimatedObject.
            state.RestoreBackgrounds(state.UpdateObjectList);

            // Ask each AnimatedObject to move if it needs to.
            foreach (AnimatedObject aniObj in state.AnimatedObjects)
            {
                aniObj.UpdatePosition();
            }

            // Draw the AnimatedObjects to screen in priority order.
            state.DrawObjects(state.MakeUpdateObjectList());
            state.ShowObjects(pixels, state.UpdateObjectList);

            // Clear the 'must be on water or land' bits for ego.
            state.Ego.StayOnLand = false;
            state.Ego.StayOnWater = false;
        }

        /// <summary>
        /// Asks every AnimatedObject to calculate their direction based on their current state.
        /// </summary>
        private void UpdateObjectDirections()
        {
            foreach (AnimatedObject aniObj in state.AnimatedObjects)
            {
                aniObj.UpdateDirection();
            }
        }

        /// <summary>
        /// Processes the user's input.
        /// </summary>
        private void ProcessUserInput()
        {
            state.ClearControllers();
            state.Flags[Defines.INPUT] = false;
            state.Flags[Defines.HADMATCH] = false;
            state.Vars[Defines.UNKNOWN_WORD] = 0;
            state.Vars[Defines.LAST_CHAR] = 0;

            // If opening of the menu was "triggered" in the last cycle, we open it now before processing the rest of the input.
            if (state.MenuOpen)
            {
                menu.MenuInput();
            }

            // F12 shows the priority and control screens.
            if (userInput.Keys[(int)Keys.F12] && !userInput.OldKeys[(int)Keys.F12])
            {
                while (userInput.Keys[(int)Keys.F12]);
                commands.ShowPriorityScreen();
            }

            // Handle arrow keys.
            if (state.UserControl)
            {
                if (state.HoldKey)
                {
                    // In "hold key" mode, the ego direction directly reflects the direction key currently being held down.
                    byte direction = 0;
                    if (userInput.Keys[(int)Keys.Up]) direction = 1;
                    if (userInput.Keys[(int)Keys.PageUp]) direction = 2;
                    if (userInput.Keys[(int)Keys.Right]) direction = 3;
                    if (userInput.Keys[(int)Keys.PageDown]) direction = 4;
                    if (userInput.Keys[(int)Keys.Down]) direction = 5;
                    if (userInput.Keys[(int)Keys.End]) direction = 6;
                    if (userInput.Keys[(int)Keys.Left]) direction = 7;
                    if (userInput.Keys[(int)Keys.Home]) direction = 8;
                    state.Vars[Defines.EGODIR] = direction;
                }
                else
                {
                    // Whereas in "release key" mode, the direction key press will toggle movement in that direction.
                    byte direction = 0;
                    if (userInput.Keys[(int)Keys.Up] && !userInput.OldKeys[(int)Keys.Up]) direction = 1;
                    if (userInput.Keys[(int)Keys.PageUp] && !userInput.OldKeys[(int)Keys.PageUp]) direction = 2;
                    if (userInput.Keys[(int)Keys.Right] && !userInput.OldKeys[(int)Keys.Right]) direction = 3;
                    if (userInput.Keys[(int)Keys.PageDown] && !userInput.OldKeys[(int)Keys.PageDown]) direction = 4;
                    if (userInput.Keys[(int)Keys.Down] && !userInput.OldKeys[(int)Keys.Down]) direction = 5;
                    if (userInput.Keys[(int)Keys.End] && !userInput.OldKeys[(int)Keys.End]) direction = 6;
                    if (userInput.Keys[(int)Keys.Left] && !userInput.OldKeys[(int)Keys.Left]) direction = 7;
                    if (userInput.Keys[(int)Keys.Home] && !userInput.OldKeys[(int)Keys.Home]) direction = 8;
                    if (direction > 0)
                    {
                        state.Vars[Defines.EGODIR] = (state.Vars[Defines.EGODIR] == direction ? (byte)0 : direction);
                    }
                }
            }

            // Check all waiting characters.
            int ch;
            while ((ch = userInput.GetKey()) > 0)
            {
                // Check controller matches. They take precedence.
                if (state.KeyToControllerMap.ContainsKey(ch))
                {
                    state.Controllers[state.KeyToControllerMap[ch]] = true;
                }
                else if ((ch >= (0x80000 + 'A')) && (ch <= (0x80000 + 'Z')) && (state.KeyToControllerMap.ContainsKey(0x80000 + Char.ToLower((char)(ch - 0x80000)))))
                {
                    // We map the lower case alpha chars in the key map, so check for upper case and convert to
                    // lower when setting controller state. This allows for if the user has CAPS LOCK on.
                    state.Controllers[state.KeyToControllerMap[0x80000 + Char.ToLower((char)(ch - 0x80000))]] = true;
                }
                else if ((ch & 0xF0000) == 0x80000)  // Standard char from a keypress event.
                {
                    state.Vars[Defines.LAST_CHAR] = (byte)(ch & 0xff);

                    if (state.AcceptInput)
                    {
                        // Handle normal characters for user input line.
                        if ((state.Strings[0].Length + (state.CursorCharacter > 0 ? 1 : 0) + state.CurrentInput.Length) < Defines.MAXINPUT)
                        {
                            state.CurrentInput.Append((char)(ch & 0xff));
                        }
                    }
                }
                else if ((ch & 0xFF) == ch)   // Unmodified Keys value, i.e. there is no modifier.
                {
                    state.Vars[Defines.LAST_CHAR] = (byte)(ch & 0xff);

                    if (state.AcceptInput)
                    {
                        // Handle enter and backspace for user input line.
                        switch ((Keys)ch)
                        {
                            case Keys.Enter:
                                if (state.CurrentInput.Length > 0)
                                {
                                    parser.Parse(state.CurrentInput.ToString());
                                    state.LastInput = state.CurrentInput.ToString();
                                    state.CurrentInput.Clear();
                                }
                                while (userInput.Keys[(int)Keys.Enter]) { /* Wait until ENTER released */ }
                                break;

                            case Keys.Back:
                                if (state.CurrentInput.Length > 0)
                                {
                                    state.CurrentInput.Remove(state.CurrentInput.Length - 1, 1);
                                }
                                break;
                        }
                    }
                }
            }
        }
    }
}
