using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static AGI.Resource;
using static AGI.Resource.Logic;

using Math = System.Math;
using Marshal = System.Runtime.InteropServices.Marshal;
using static AGILE.ScriptBuffer;
using System.Diagnostics;
using System.Threading;

namespace AGILE
{
    class Commands
    {
        /// <summary>
        /// The GameState class holds all of the data and state for the Game currently 
        /// being run by the interpreter.
        /// </summary>
        private GameState state;

        /// <summary>
        /// Holds the data and state for the user input, i.e. keyboard and mouse input.
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
        /// Responsible for parsing the user input line to match known words
        /// </summary>
        private Parser parser;

        /// <summary>
        /// Responsible for displaying the inventory screen.
        /// </summary>
        private Inventory inventory;

        /// <summary>
        /// Responsible for displaying the menu system.
        /// </summary>
        private Menu menu;

        /// <summary>
        /// Responsible for saving and restoring saved game files.
        /// </summary>
        private SavedGames savedGames;

        /// <summary>
        /// Responsible for playing Sound resources.
        /// </summary>
        private SoundPlayer soundPlayer;

        /// <summary>
        /// Constructor for Commands.
        /// </summary>
        /// <param name="pixels"></param>
        /// <param name="state"></param>
        /// <param name="userInput"></param>
        /// <param name="textGraphics"></param>
        /// <param name="parser"></param>
        /// <param name="soundPlayer"></param>
        /// <param name="menu"></param>
        public Commands(int[] pixels, GameState state, UserInput userInput, TextGraphics textGraphics, Parser parser, SoundPlayer soundPlayer, Menu menu)
        {
            this.pixels = pixels;
            this.state = state;
            this.userInput = userInput;
            this.textGraphics = textGraphics;
            this.parser = parser;
            this.menu = menu;
            this.inventory = new Inventory(state, userInput, textGraphics, pixels);
            this.savedGames = new SavedGames(state, userInput, textGraphics, pixels);
            this.soundPlayer = soundPlayer;
        }

        /// <summary>
        /// Draws the AGI Picture identified by the given picture number.
        /// </summary>
        /// <param name="pictureNum">The number of the picture to draw.</param>
        /// <returns></returns>
        private void DrawPicture(int pictureNum)
        {
            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.DrawPic, pictureNum);
            state.RestoreBackgrounds();

            // By encoding and then decoding, we create a copy of the Picture.
            Picture picture = new Picture(state.Pictures[pictureNum].Encode());

            // Now clear the draw the whole Picture from the beginning on clear Bitmaps.
            picture.Screen.Clear();
            picture.Screen.DrawCommands(picture.CommandStack);

            state.CurrentPicture = picture;

            UpdatePixelArrays();

            state.DrawObjects();

            state.PictureVisible = false;
        }

        /// <summary>
        /// Updates the Visual, Priority and Control pixel arrays with the bitmaps from the
        /// current Picture.
        /// </summary>
        private void UpdatePixelArrays()
        {
            Picture picture = state.CurrentPicture;

            Bitmap visualBitmap = picture.Screen.VisualBitmap;
            Bitmap priorityBitmap = picture.Screen.PriorityBitmap;

            // Copy visual pixels to a 160x168 byte array.
            BitmapData visualBitmapData = visualBitmap.LockBits(new Rectangle(0, 0, visualBitmap.Width, visualBitmap.Height), ImageLockMode.ReadWrite, visualBitmap.PixelFormat);
            byte[] visualPixels = new byte[visualBitmapData.Stride * visualBitmapData.Height];
            Marshal.Copy(visualBitmapData.Scan0, visualPixels, 0, visualPixels.Length);
            visualBitmap.UnlockBits(visualBitmapData);

            // Copy the pixels to our VisualPixels array, doubling each one as we go.
            for (int i = 0, ii = 0; i < (160 * 168); i++, ii += 2)
            {
                int argbColor = AGI.Color.Palette[visualPixels[i]].ToArgb(); ;
                state.VisualPixels[ii + 0] = argbColor;
                state.VisualPixels[ii + 1] = argbColor;
            }

            SplitPriorityPixels();
        }

        /// <summary>
        /// Overlays an AGI Picture identified by the given picture number over the current picture.
        /// </summary>
        /// <param name="pictureNum"></param>
        private void OverlayPicture(int pictureNum)
        {
            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.OverlayPic, pictureNum);
            state.RestoreBackgrounds();

            // Draw the overlay picture on top of the current picture.
            Picture overlayPicture = state.Pictures[pictureNum];
            state.CurrentPicture.Screen.DrawCommands(overlayPicture.CommandStack);

            UpdatePixelArrays();

            state.DrawObjects();

            ShowVisualPixels();

            state.PictureVisible = false;
        }

        /// <summary>
        /// For the current picture, sets the relevant pixels in the PriorityPixels and 
        /// ControlPixels arrays in  the GameState. It determines the priority information for 
        /// pixels that are overdrawn by control lines by the same method used in Sierra's 
        /// interpreter. To quote the original AGI specs: "Control pixels still have a visual 
        /// priority from 4 to 15. To accomplish this, AGI scans directly down the control 
        /// priority until it finds some 'non-control' priority".
        /// </summary>
        private void SplitPriorityPixels()
        {
            Picture picture = state.CurrentPicture;
            Bitmap priorityBitmap = picture.Screen.PriorityBitmap;

            // Copy priority pixels to a 160x168 byte array.
            BitmapData priorityBitmapData = priorityBitmap.LockBits(new Rectangle(0, 0, priorityBitmap.Width, priorityBitmap.Height), ImageLockMode.ReadWrite, priorityBitmap.PixelFormat);
            byte[] priorityPixels = new byte[priorityBitmapData.Stride * priorityBitmapData.Height];
            Marshal.Copy(priorityBitmapData.Scan0, priorityPixels, 0, priorityPixels.Length);
            priorityBitmap.UnlockBits(priorityBitmapData);

            //
            for (int x = 0; x < 160; x++)
            {
                for (int y = 0; y < 168; y++)
                {
                    // Shift left 7 + shift level 5 is a trick to avoid multiplying by 160.
                    int index = (y << 7) + (y << 5) + x;
                    byte data = priorityPixels[index];

                    if (data == 3)
                    {
                        state.PriorityPixels[index] = 3;
                        state.ControlPixels[index] = data;
                    }
                    else if (data < 3)
                    {
                        state.ControlPixels[index] = data;

                        int dy = y + 1;
                        bool priFound = false;

                        while (!priFound && (dy < 168))
                        {
                            data = priorityPixels[(dy << 7) + (dy << 5) + x];

                            if (data > 2)
                            {
                                priFound = true;
                                state.PriorityPixels[index] = data;
                            }
                            else
                            {
                                dy++;
                            }
                        }
                    }
                    else
                    {
                        state.ControlPixels[index] = 4;
                        state.PriorityPixels[index] = data;
                    }
                }
            }
        }

        /// <summary>
        /// Shows the current priority pixels and control pixels to screen.
        /// </summary>
        public void ShowPriorityScreen()
        {
            int[] backPixels = new int[pixels.Length];
            System.Buffer.BlockCopy(pixels, 0, backPixels, 0, sizeof(int) * pixels.Length);

            for (int i = 0, ii = (8 * state.PictureRow) * 320; i < (160 * 168); i++, ii += 2)
            {
                int priColorIndex = state.PriorityPixels[i];
                int ctrlColorIndex = state.ControlPixels[i];
                int argbColor = AGI.Color.Palette[ctrlColorIndex <= 3 ? ctrlColorIndex : priColorIndex].ToArgb();
                pixels[ii + 0] = argbColor;
                pixels[ii + 1] = argbColor;
            }

            userInput.WaitForKey(true);

            System.Buffer.BlockCopy(backPixels, 0, pixels, 0, sizeof(int) * pixels.Length);
        }

        /// <summary>
        /// Blits the current VisualPixels array to the screen pixels array.
        /// </summary>
        private void ShowVisualPixels()
        {
            // Perform the copy to the pixels array of the VisualPixels. This is where the PictureRow comes in to effect.
            System.Buffer.BlockCopy(state.VisualPixels, 0, this.pixels, (8 * state.PictureRow) * 320 * sizeof(int), sizeof(int) * state.VisualPixels.Length);
        }

        /// <summary>
        /// Implements the show.pic command. Blits the current VisualPixels array to the screen pixels 
        /// array. If there is an open window, it will be closed by default.
        /// </summary>
        /// <param name="closeWindow">Skips the closing of open windows if set to false.</param>
        private void ShowPicture(bool closeWindow = true)
        {
            if (closeWindow)
            {
                // It is possible to leave the window up from the previous room, so we force a close.
                state.Flags[Defines.LEAVE_WIN] = false;
                textGraphics.CloseWindow(false);
            }

            // Perform the copy to the pixels array of the VisualPixels
            ShowVisualPixels();

            // Remember that the picture is now being displayed to the user.
            state.PictureVisible = true;
        }

        /// <summary>
        /// Executes the shake.screen command. Implementation is based on the scummvm code.
        /// </summary>
        /// <param name="repeatCount">The number of times to do the shake routine.</param>
        private void ShakeScreen(int repeatCount)
        {
            int shakeCount = (repeatCount * 8);
            int backgroundArgb = AGI.Color.Palette[0].ToArgb();
            int[] backPixels = new int[pixels.Length];

            System.Buffer.BlockCopy(pixels, 0, backPixels, 0, sizeof(int) * pixels.Length);

            for (int shakeNumber = 0; shakeNumber < shakeCount; shakeNumber++)
            {
                if ((shakeNumber & 1) == 1)
                {
                    System.Buffer.BlockCopy(backPixels, 0, pixels, 0, sizeof(int) * pixels.Length);
                }
                else
                {
                    for (int y = 0, screenPos = 0; y < 200; y++)
                    {
                        for (int x = 0; x < 320; x++, screenPos++)
                        {
                            if ((x < 8) || (y < 4))
                            {
                                this.pixels[screenPos] = backgroundArgb;
                            }
                            else
                            {
                                this.pixels[screenPos] = backPixels[screenPos - 1288];
                            }
                        }
                    }
                }
                Thread.Sleep(66);
            }

            System.Buffer.BlockCopy(backPixels, 0, pixels, 0, sizeof(int) * pixels.Length);
        }

        /// <summary>
        /// Replays the events that happened in the ScriptBuffer. This would usually be called
        /// immediately after restoring a saved game file, to do things such as add the add.to.pics,
        /// draw the picture, show the picture, etc.
        /// </summary>
        private void ReplayScriptEvents()
        {
            // Mainly for the AddToPicture method, since that adds script events if active.
            state.ScriptBuffer.ScriptOff();

            foreach (ScriptBufferEvent scriptBufferEvent in state.ScriptBuffer.Events)
            {
                switch (scriptBufferEvent.type)
                {
                    case ScriptBufferEventType.AddToPic:
                        {
                            AnimatedObject picObj = new AnimatedObject(state, -1);
                            picObj.AddToPicture(
                                scriptBufferEvent.data[0], scriptBufferEvent.data[1], scriptBufferEvent.data[2],
                                scriptBufferEvent.data[3], scriptBufferEvent.data[4], (byte)(scriptBufferEvent.data[5] & 0x0F),
                                (byte)((scriptBufferEvent.data[5] >> 4) & 0x0F), pixels);
                            SplitPriorityPixels();
                        }
                        break;

                    case ScriptBufferEventType.DiscardPic:
                        {
                            Picture pic = state.Pictures[scriptBufferEvent.resourceNumber];
                            if (pic != null) pic.IsLoaded = false;
                        }
                        break;

                    case ScriptBufferEventType.DiscardView:
                        {
                            View view = state.Views[scriptBufferEvent.resourceNumber];
                            if (view != null) view.IsLoaded = false;
                        }
                        break;

                    case ScriptBufferEventType.DrawPic:
                        {
                            DrawPicture(scriptBufferEvent.resourceNumber);
                        }
                        break;

                    case ScriptBufferEventType.LoadLogic:
                        {
                            Logic logic = state.Logics[scriptBufferEvent.resourceNumber];
                            if (logic != null) logic.IsLoaded = true;
                        }
                        break;

                    case ScriptBufferEventType.LoadPic:
                        {
                            Picture pic = state.Pictures[scriptBufferEvent.resourceNumber];
                            if (pic != null) pic.IsLoaded = true;
                        }
                        break;

                    case ScriptBufferEventType.LoadSound:
                        {
                            Sound sound = state.Sounds[scriptBufferEvent.resourceNumber];
                            if (sound != null)
                            {
                                soundPlayer.LoadSound(sound);
                                sound.IsLoaded = true;
                            }
                        }
                        break;

                    case ScriptBufferEventType.LoadView:
                        {
                            View view = state.Views[scriptBufferEvent.resourceNumber];
                            if (view != null) view.IsLoaded = true;
                        }
                        break;

                    case ScriptBufferEventType.OverlayPic:
                        {
                            Picture overlayPicture = state.Pictures[scriptBufferEvent.resourceNumber];
                            state.CurrentPicture.Screen.DrawCommands(overlayPicture.CommandStack);
                        }
                        break;
                }
            }

            state.ScriptBuffer.ScriptOn();
        }

        /// <summary>
        /// Evaluates the given Condition.
        /// </summary>
        /// <param name="condition">The Condition to evaluate.</param>
        /// <returns>The result of evaluating the Condition; either true or false. </returns>
        private bool IsConditionTrue(Condition condition)
        {
            bool result = false;

            switch (condition.Operation.Opcode)
            {
                case 1: // equaln
                    {
                        result = (state.Vars[condition.Operands[0].asByte()] == condition.Operands[1].asByte());
                    }
                    break;

                case 2: // equalv
                    {
                        result = (state.Vars[condition.Operands[0].asByte()] == state.Vars[condition.Operands[1].asByte()]);
                    }
                    break;

                case 3: // lessn
                    {
                        result = (state.Vars[condition.Operands[0].asByte()] < condition.Operands[1].asByte());
                    }
                    break;

                case 4: // lessv
                    {
                        result = (state.Vars[condition.Operands[0].asByte()] < state.Vars[condition.Operands[1].asByte()]);
                    }
                    break;

                case 5: // greatern
                    {
                        result = (state.Vars[condition.Operands[0].asByte()] > condition.Operands[1].asByte());
                    }
                    break;

                case 6: // greaterv
                    {
                        result = (state.Vars[condition.Operands[0].asByte()] > state.Vars[condition.Operands[1].asByte()]);
                    }
                    break;

                case 7: // isset
                    {
                        result = state.Flags[condition.Operands[0].asByte()];
                    }
                    break;

                case 8: // issetv
                    {
                        result = state.Flags[state.Vars[condition.Operands[0].asByte()]];
                    }
                    break;

                case 9: // has
                    {
                        result = (state.Objects[condition.Operands[0].asByte()].Room == Defines.CARRYING);
                    }
                    break;

                case 10: // obj.in.room
                    {
                        result = (state.Objects[condition.Operands[0].asByte()].Room == state.Vars[condition.Operands[1].asByte()]);
                    }
                    break;

                case 11: // posn
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[condition.Operands[0].asByte()];
                        int x1 = condition.Operands[1].asByte();
                        int y1 = condition.Operands[2].asByte();
                        int x2 = condition.Operands[3].asByte();
                        int y2 = condition.Operands[4].asByte();
                        result = ((aniObj.X >= x1) && (aniObj.Y >= y1) && (aniObj.X <= x2) && (aniObj.Y <= y2));
                    }
                    break;

                case 12: // controller
                    {
                        result = state.Controllers[condition.Operands[0].asByte()];
                    }
                    break;

                case 13: // have.key
                    {
                        int key = state.Vars[Defines.LAST_CHAR];
                        if (key == 0)
                        {
                            key = userInput.GetKey();
                        }
                        if (key > 0)
                        {
                            state.Vars[Defines.LAST_CHAR] = (byte)key;
                        }
                        result = (key != 0);
                    }
                    break;

                case 14: // said
                    {
                        result = parser.Said(condition.Operands[0].asInts());
                    }
                    break;

                case 15: // compare.strings
                    {
                        // Compare two strings. Ignore case, whitespace, and punctuation.
                        string str1 = Regex.Replace(state.Strings[condition.Operands[0].asByte()].ToLower(), "[ \t.,;:\'!-]", string.Empty);
                        string str2 = Regex.Replace(state.Strings[condition.Operands[1].asByte()].ToLower(), "[ \t.,;:\'!-]", string.Empty);
                        result = str1.Equals(str2);
                    }
                    break;

                case 16: // obj.in.box
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[condition.Operands[0].asByte()];
                        int x1 = condition.Operands[1].asByte();
                        int y1 = condition.Operands[2].asByte();
                        int x2 = condition.Operands[3].asByte();
                        int y2 = condition.Operands[4].asByte();
                        result = ((aniObj.X >= x1) && (aniObj.Y >= y1) && ((aniObj.X + aniObj.XSize - 1) <= x2) && (aniObj.Y <= y2));
                    }
                    break;

                case 17: // center.posn
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[condition.Operands[0].asByte()];
                        int x1 = condition.Operands[1].asByte();
                        int y1 = condition.Operands[2].asByte();
                        int x2 = condition.Operands[3].asByte();
                        int y2 = condition.Operands[4].asByte();
                        result = ((aniObj.X + (aniObj.XSize / 2) >= x1) && (aniObj.Y >= y1) && (aniObj.X + (aniObj.XSize / 2) <= x2) && (aniObj.Y <= y2));
                    }
                    break;

                case 18: // right.posn
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[condition.Operands[0].asByte()];
                        int x1 = condition.Operands[1].asByte();
                        int y1 = condition.Operands[2].asByte();
                        int x2 = condition.Operands[3].asByte();
                        int y2 = condition.Operands[4].asByte();
                        result = (((aniObj.X + aniObj.XSize - 1) >= x1) && (aniObj.Y >= y1) && ((aniObj.X + aniObj.XSize - 1) <= x2) && (aniObj.Y <= y2));
                    }
                    break;

                case 0xfc: // OR
                    {
                        result = false;
                        foreach (Condition orCondition in condition.Operands[0].asConditions())
                        {
                            if (IsConditionTrue(orCondition))
                            {
                                result = true;
                                break;
                            }
                        }
                    }
                    break;

                case 0xfd: // NOT
                    {
                        result = !IsConditionTrue(condition.Operands[0].asCondition());
                    }
                    break;
            }

            return result;
        }

        /// <summary>
        /// Executes the given Action command.
        /// </summary>
        /// <param name="action">The Action command to execute.</param>
        /// <param name="newRoom"></param>
        /// <param name="exit"></param>
        /// <returns>The index of the next Action to execute.</returns>
        private int ExecuteAction(Action action, ref byte newRoom, ref bool exit)
        {
            // Normally the next Action will be the next one in the Actions list, but this
            // can be overwritten by the If and Goto actions.
            int nextActionNum = action.Logic.AddressToActionIndex[action.Address] + 1;

            switch (action.Operation.Opcode)
            {
                case 0: // return
                    {
                        exit = true;
                    }
                    return 0;

                case 1: // increment
                    {
                        byte varNum = action.Operands[0].asByte();
                        if (state.Vars[varNum] < 255) state.Vars[varNum]++;
                    }
                    break;

                case 2: // decrement
                    {
                        byte varNum = action.Operands[0].asByte();
                        if (state.Vars[varNum] > 0) state.Vars[varNum]--;
                    }
                    break;

                case 3: // assignn
                    {
                        byte varNum = action.Operands[0].asByte();
                        byte value = action.Operands[1].asByte();
                        state.Vars[varNum] = value;
                    }
                    break;

                case 4: // assignv
                    {
                        byte varNum1 = action.Operands[0].asByte();
                        byte varNum2 = action.Operands[1].asByte();
                        state.Vars[varNum1] = state.Vars[varNum2];
                    }
                    break;

                case 5: // addn
                    {
                        byte varNum = action.Operands[0].asByte();
                        byte value = action.Operands[1].asByte();
                        state.Vars[varNum] += value;
                    }
                    break;

                case 6: // addv
                    {
                        byte varNum1 = action.Operands[0].asByte();
                        byte varNum2 = action.Operands[1].asByte();
                        state.Vars[varNum1] += state.Vars[varNum2];
                    }
                    break;

                case 7: // subn
                    {
                        byte varNum = action.Operands[0].asByte();
                        byte value = action.Operands[1].asByte();
                        state.Vars[varNum] -= value;
                    } 
                    break;

                case 8: // subv
                    {
                        byte varNum1 = action.Operands[0].asByte();
                        byte varNum2 = action.Operands[1].asByte();
                        state.Vars[varNum1] -= state.Vars[varNum2];
                    }
                    break;

                case 9: // lindirectv
                    {
                        byte varNum1 = action.Operands[0].asByte();
                        byte varNum2 = action.Operands[1].asByte();
                        state.Vars[state.Vars[varNum1]] = state.Vars[varNum2];
                    }
                    break;

                case 10: // rindirect
                    {
                        byte varNum1 = action.Operands[0].asByte();
                        byte varNum2 = action.Operands[1].asByte();
                        state.Vars[varNum1] = state.Vars[state.Vars[varNum2]];
                    }
                    break;

                case 11: // lindirectn
                    {
                        byte varNum = action.Operands[0].asByte();
                        byte value = action.Operands[1].asByte();
                        state.Vars[state.Vars[varNum]] = value;
                    }
                    break;

                case 12: // set
                    {
                        state.Flags[action.Operands[0].asByte()] = true;
                    }
                    break;

                case 13: // reset
                    {
                        state.Flags[action.Operands[0].asByte()] = false;
                    }
                    break;

                case 14: // toggle
                    {
                        byte flagNum = action.Operands[0].asByte();
                        state.Flags[flagNum] = !state.Flags[flagNum];
                    }
                    break;

                case 15: // set.v
                    {
                        state.Flags[state.Vars[action.Operands[0].asByte()]] = true;
                    }
                    break;

                case 16: // reset.v
                    {
                        state.Flags[state.Vars[action.Operands[0].asByte()]] = false;
                    }
                    break;

                case 17: // toggle.v
                    {
                        byte flagNum = state.Vars[action.Operands[0].asByte()];
                        state.Flags[flagNum] = !state.Flags[flagNum];
                    }
                    break;

                case 18: // new.room
                    {
                        newRoom = action.Operands[0].asByte();
                        exit = true;
                    }
                    return 0;

                case 19: // new.room.v
                    {
                        newRoom = state.Vars[action.Operands[0].asByte()];
                        exit = true;
                    }
                    return 0;

                case 20: // load.logics
                    {
                        // All logics are already loaded in this interpreter, so nothing to do as such
                        // other than to remember it was "loaded".
                        Logic logic = state.Logics[action.Operands[0].asByte()];
                        if ((logic != null) && !logic.IsLoaded)
                        {
                            logic.IsLoaded = true;
                            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.LoadLogic, logic.Index);
                        }
                    }
                    break;

                case 21: // load.logics.v
                    {
                        // All logics are already loaded in this interpreter, so nothing to do as such
                        // other than to remember it was "loaded".
                        Logic logic = state.Logics[state.Vars[action.Operands[0].asByte()]];
                        if ((logic != null) && !logic.IsLoaded)
                        {
                            logic.IsLoaded = true;
                            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.LoadLogic, logic.Index);
                        }
                    }
                    break;

                case 22: // call
                    {
                        if ((newRoom = ExecuteLogic(action.Operands[0].asByte())) != state.CurrentRoom)
                        {
                            exit = true;
                            return 0;
                        }
                    }
                    break;

                case 23: // call.v
                    {
                        if ((newRoom = ExecuteLogic(state.Vars[action.Operands[0].asByte()])) != state.CurrentRoom)
                        {
                            exit = true;
                            return 0;
                        }
                    }
                    break;

                case 24: // load.pic
                    {
                        // All pictures are already loaded in this interpreter, so nothing to do as such
                        // other than to remember it was "loaded".
                        Picture pic = state.Pictures[state.Vars[action.Operands[0].asByte()]];
                        if ((pic != null) && !pic.IsLoaded)
                        {
                            pic.IsLoaded = true;
                            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.LoadPic, pic.Index);
                        }
                    }
                    break;

                case 25: // draw.pic
                    {
                        DrawPicture(state.Vars[action.Operands[0].asByte()]);
                    }
                    break;

                case 26: // show.pic
                    {
                        ShowPicture();
                    }
                    break;

                case 27: // discard.pic
                    {
                        // All pictures are kept loaded in this interpreter, so nothing to do as such
                        // other than to remember it was "unloaded".
                        Picture pic = state.Pictures[state.Vars[action.Operands[0].asByte()]];
                        if ((pic != null) && pic.IsLoaded)
                        {
                            pic.IsLoaded = false;
                            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.DiscardPic, pic.Index);
                        }
                    }
                    break;

                case 28: // overlay.pic
                    {
                        OverlayPicture(state.Vars[action.Operands[0].asByte()]);
                    }
                    break;

                case 29: // show.pri.screen
                    {
                        ShowPriorityScreen();
                    }
                    break;

                case 30: // load.view
                    {
                        // All views are already loaded in this interpreter, so nothing to do as such
                        // other than to remember it was "loaded".
                        View view = state.Views[action.Operands[0].asByte()];
                        if ((view != null) && !view.IsLoaded)
                        {
                            view.IsLoaded = true;
                            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.LoadView, view.Index);
                        }
                    }
                    break;

                case 31: // load.view.v
                    {
                        // All views are already loaded in this interpreter, so nothing to do as such
                        // other than to remember it was "loaded".
                        View view = state.Views[state.Vars[action.Operands[0].asByte()]];
                        if ((view != null) && !view.IsLoaded)
                        {
                            view.IsLoaded = true;
                            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.LoadView, view.Index);
                        }
                    }
                    break;

                case 32: // discard.view
                    {
                        // All views are kept loaded in this interpreter, so nothing to do as such
                        // other than to remember it was "unloaded".
                        View view = state.Views[action.Operands[0].asByte()];
                        if ((view != null) && view.IsLoaded)
                        {
                            view.IsLoaded = false;
                            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.DiscardView, view.Index);
                        }
                    }
                    break;

                case 33: // animate.obj
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.Animate();
                    }
                    break;

                case 34: // unanimate.all
                    {
                        state.RestoreBackgrounds();
                        foreach (AnimatedObject aniObj in state.AnimatedObjects)
                        {
                            aniObj.Animated = false;
                            aniObj.Drawn = false;
                        }
                    }
                    break;

                case 35: // draw
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        if (!aniObj.Drawn)
                        {
                            aniObj.Update = true;
                            aniObj.FindPosition();
                            aniObj.PrevX = aniObj.X;
                            aniObj.PrevY = aniObj.Y;
                            aniObj.PreviousCel = aniObj.Cel;
                            state.RestoreBackgrounds(state.UpdateObjectList);
                            aniObj.Drawn = true;
                            state.DrawObjects(state.MakeUpdateObjectList());
                            aniObj.Show(pixels);
                            aniObj.NoAdvance = false;
                        }
                    }
                    break;

                case 36: // erase
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        state.RestoreBackgrounds(state.UpdateObjectList);
                        if (!aniObj.Update)
                        {
                            state.RestoreBackgrounds(state.StoppedObjectList);
                        }
                        aniObj.Drawn = false;
                        if (!aniObj.Update)
                        {
                            state.DrawObjects(state.MakeStoppedObjectList());
                        }
                        state.DrawObjects(state.MakeUpdateObjectList());
                        aniObj.Show(pixels);
                    }
                    break;

                case 37: // position
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.X = aniObj.PrevX = action.Operands[1].asByte();
                        aniObj.Y = aniObj.PrevY = action.Operands[2].asByte();
                    }
                    break;

                case 38: // position.v
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.X = aniObj.PrevX = state.Vars[action.Operands[1].asByte()];
                        aniObj.Y = aniObj.PrevY = state.Vars[action.Operands[2].asByte()];
                    }
                    break;

                case 39: // get.posn
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        state.Vars[action.Operands[1].asByte()] = (byte)aniObj.X;
                        state.Vars[action.Operands[2].asByte()] = (byte)aniObj.Y;
                    }
                    break;

                case 40: // reposition
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.Reposition((sbyte)state.Vars[action.Operands[1].asByte()], (sbyte)state.Vars[action.Operands[2].asByte()]);
                    }
                    break;

                case 41: // set.view
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.SetView(action.Operands[1].asByte());
                    }
                    break;

                case 42: // set.view.v
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.SetView(state.Vars[action.Operands[1].asByte()]);
                    }
                    break;

                case 43: // set.loop
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.SetLoop(action.Operands[1].asByte());
                    }
                    break;

                case 44: // set.loop.v
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.SetLoop(state.Vars[action.Operands[1].asByte()]);
                    }
                    break;

                case 45: // fix.loop
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.FixedLoop = true;
                    }
                    break;

                case 46: // release.loop
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.FixedLoop = false;
                    }
                    break;

                case 47: // set.cel
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.SetCel(action.Operands[1].asByte());
                        aniObj.NoAdvance = false;
                    }
                    break;

                case 48: // set.cel.v
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.SetCel(state.Vars[action.Operands[1].asByte()]);
                        aniObj.NoAdvance = false;
                    }
                    break;

                case 49: // last.cel
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        state.Vars[action.Operands[1].asByte()] = (byte)(aniObj.NumberOfCels - 1);
                    }
                    break;

                case 50: // current.cel
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        state.Vars[action.Operands[1].asByte()] = aniObj.CurrentCel;
                    }
                    break;

                case 51: // current.loop
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        state.Vars[action.Operands[1].asByte()] = aniObj.CurrentLoop;
                    }
                    break;

                case 52: // current.view
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        state.Vars[action.Operands[1].asByte()] = aniObj.CurrentView;
                    }
                    break;

                case 53: // number.of.loops
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        state.Vars[action.Operands[1].asByte()] = aniObj.NumberOfLoops;
                    }
                    break;

                case 54: // set.priority
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.FixedPriority = true;
                        aniObj.Priority = action.Operands[1].asByte();
                    }
                    break;

                case 55: // set.priority.v
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.FixedPriority = true;
                        aniObj.Priority = state.Vars[action.Operands[1].asByte()];
                    }
                    break;

                case 56: // release.priority
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.FixedPriority = false;
                    }
                    break;

                case 57: // get.priority
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        state.Vars[action.Operands[1].asByte()] = aniObj.Priority;
                    }
                    break;

                case 58: // stop.update
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        if (aniObj.Update)
                        {
                            state.RestoreBackgrounds();
                            aniObj.Update = false;
                            state.DrawObjects();
                        }
                    }
                    break;

                case 59: // start.update
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        if (!aniObj.Update)
                        {
                            state.RestoreBackgrounds();
                            aniObj.Update = true;
                            state.DrawObjects();
                        }
                    }
                    break;

                case 60: // force.update
                    {
                        // Although this command has a parameter, it seems to get ignored. Instead
                        // every AnimatedObject is redrawn and blitted to the screen.
                        state.RestoreBackgrounds();
                        state.DrawObjects();
                        state.ShowObjects(pixels);
                    }
                    break;

                case 61: // ignore.horizon
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.IgnoreHorizon = true;
                    }
                    break;

                case 62: // observe.horizon
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.IgnoreHorizon = false;
                    }
                    break;

                case 63: // set.horizon
                    {
                        state.Horizon = action.Operands[0].asByte();
                    }
                    break;

                case 64: // object.on.water
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.StayOnWater = true;
                    }
                    break;

                case 65: // object.on.land
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.StayOnLand = true;
                    }
                    break;

                case 66: // object.on.anything
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.StayOnLand = false;
                        aniObj.StayOnWater = false;
                    }
                    break;

                case 67: // ignore.objs
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.IgnoreObjects = true;
                    }
                    break;

                case 68: // observe.objs
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.IgnoreObjects = false;
                    }
                    break;

                case 69: // distance
                    {
                        AnimatedObject aniObj1 = state.AnimatedObjects[action.Operands[0].asByte()];
                        AnimatedObject aniObj2 = state.AnimatedObjects[action.Operands[1].asByte()];
                        state.Vars[action.Operands[2].asByte()] = aniObj1.Distance(aniObj2);
                    }
                    break;

                case 70: // stop.cycling
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.Cycle = false;
                    }
                    break;

                case 71: // start.cycling
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.Cycle = true;
                    }
                    break;

                case 72: // normal.cycle
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.CycleType = CycleType.Normal;
                        aniObj.Cycle = true;
                    }
                    break;

                case 73: // end.of.loop
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        byte flagNum = action.Operands[1].asByte();
                        aniObj.CycleType = CycleType.EndLoop;
                        aniObj.Update = true;
                        aniObj.Cycle = true;
                        aniObj.NoAdvance = true;
                        aniObj.MotionParam1 = flagNum;
                        state.Flags[flagNum] = false;
                    }
                    break;

                case 74: // reverse.cycle
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.CycleType = CycleType.Reverse;
                        aniObj.Cycle = true;
                    }
                    break;

                case 75: // reverse.loop
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        byte flagNum = action.Operands[1].asByte();
                        aniObj.CycleType = CycleType.ReverseLoop;
                        aniObj.Update = true;
                        aniObj.Cycle = true;
                        aniObj.NoAdvance = true;
                        aniObj.MotionParam1 = flagNum;
                        state.Flags[flagNum] = false;
                    }
                    break;

                case 76: // cycle.time
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.CycleTimeCount = aniObj.CycleTime = state.Vars[action.Operands[1].asByte()];
                    }
                    break;

                case 77: // stop.motion
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.Direction = 0;
                        aniObj.MotionType = MotionType.Normal;
                        if (aniObj == state.Ego)
                        {
                            state.Vars[Defines.EGODIR] = 0;
                            state.UserControl = false;
                        }
                    }
                    break;

                case 78: // start.motion
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.MotionType = MotionType.Normal;
                        if (aniObj == state.Ego)
                        {
                            state.Vars[Defines.EGODIR] = 0;
                            state.UserControl = true;
                        }
                    }
                    break;

                case 79: // step.size
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.StepSize = state.Vars[action.Operands[1].asByte()];
                    }
                    break;

                case 80: // step.time
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.StepTimeCount = aniObj.StepTime = state.Vars[action.Operands[1].asByte()];
                    }
                    break;

                case 81: // move.obj
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.StartMoveObj(
                            action.Operands[1].asByte(), action.Operands[2].asByte(), 
                            action.Operands[3].asByte(), action.Operands[4].asByte());
                    }
                    break;

                case 82: // move.obj.v
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.StartMoveObj(
                            state.Vars[action.Operands[1].asByte()], state.Vars[action.Operands[2].asByte()],
                            state.Vars[action.Operands[3].asByte()], action.Operands[4].asByte());
                    }
                    break;

                case 83: // follow.ego
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.StartFollowEgo(action.Operands[1].asByte(), action.Operands[2].asByte());
                    }
                    break;

                case 84: // wander
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.StartWander();
                    }
                    break;

                case 85: // normal.motion
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.MotionType = MotionType.Normal;
                    }
                    break;

                case 86: // set.dir
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.Direction = state.Vars[action.Operands[1].asByte()];
                    }
                    break;

                case 87: // get.dir
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        state.Vars[action.Operands[1].asByte()] = aniObj.Direction;
                    }
                    break;

                case 88: // ignore.blocks
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.IgnoreBlocks = true;
                    }
                    break;

                case 89: // observe.blocks
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.IgnoreBlocks = false;
                    }
                    break;

                case 90: // block
                    {
                        state.Blocking = true;
                        state.BlockUpperLeftX = action.Operands[0].asByte();
                        state.BlockUpperLeftY = action.Operands[1].asByte();
                        state.BlockLowerRightX = action.Operands[2].asByte();
                        state.BlockLowerRightY = action.Operands[3].asByte();
                    }
                    break;

                case 91: // unblock
                    {
                        state.Blocking = false;
                    }
                    break;

                case 92: // get
                    {
                        state.Objects[action.Operands[0].asByte()].Room = Defines.CARRYING;
                    }
                    break;

                case 93: // get.v
                    {
                        state.Objects[state.Vars[action.Operands[0].asByte()]].Room = Defines.CARRYING;
                    }
                    break;

                case 94: // drop
                    {
                        state.Objects[action.Operands[0].asByte()].Room = Defines.LIMBO;
                    }
                    break;

                case 95: // put
                    {
                        state.Objects[action.Operands[0].asByte()].Room = state.Vars[action.Operands[1].asByte()];
                    }
                    break;

                case 96: // put.v
                    {
                        state.Objects[state.Vars[action.Operands[0].asByte()]].Room = state.Vars[action.Operands[1].asByte()];
                    }
                    break;

                case 97: // get.room.v
                    {
                        state.Vars[action.Operands[1].asByte()] = state.Objects[state.Vars[action.Operands[0].asByte()]].Room;
                    }
                    break;

                case 98: // load.sound
                    {
                        // All sounds are already loaded in this interpreter, so nothing to do as such
                        // other than to remember it was "loaded".
                        int soundNum = action.Operands[0].asByte();
                        Sound sound = state.Sounds[soundNum];
                        if ((sound != null) && !sound.IsLoaded)
                        {
                            soundPlayer.LoadSound(sound);
                            sound.IsLoaded = true;
                            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.LoadSound, sound.Index);
                        }
                    }
                    break;

                case 99: // sound
                    {
                        int soundNum = action.Operands[0].asByte();
                        int endFlag = action.Operands[1].asByte();
                        state.Flags[endFlag] = false;
                        Sound sound = state.Sounds[soundNum];
                        if ((sound != null) && (sound.IsLoaded))
                        {
                            this.soundPlayer.PlaySound(sound, endFlag);
                        }
                    }
                    break;

                case 100: // stop.sound
                    {
                        this.soundPlayer.StopSound();
                    }
                    break;

                case 101: // print
                    {
                        this.textGraphics.Print(action.Logic.Messages[action.Operands[0].asByte()]);
                    }
                    break;

                case 102: // print.v
                    {
                        this.textGraphics.Print(action.Logic.Messages[state.Vars[action.Operands[0].asByte()]]);
                    }
                    break;

                case 103: // display
                    {
                        int row = action.Operands[0].asByte();
                        int col = action.Operands[1].asByte();
                        string message = action.Logic.Messages[action.Operands[2].asByte()];
                        this.textGraphics.Display(message, row, col);
                    }
                    break;

                case 104: // display.v
                    {
                        int row = state.Vars[action.Operands[0].asByte()];
                        int col = state.Vars[action.Operands[1].asByte()];
                        string message = action.Logic.Messages[state.Vars[action.Operands[2].asByte()]];
                        this.textGraphics.Display(message, row, col);
                    }
                    break;

                case 105: // clear.lines
                    {
                        int colour = textGraphics.MakeBackgroundColour(action.Operands[2].asByte());
                        textGraphics.ClearLines(action.Operands[0].asByte(), action.Operands[1].asByte(), colour);
                    }
                    break;

                case 106: // text.screen
                    {
                        textGraphics.TextScreen();
                    }
                    break;

                case 107: // graphics
                    {
                        textGraphics.GraphicsScreen();
                    }
                    break;

                case 108: // set.cursor.char
                    {
                        string cursorStr = action.Logic.Messages[action.Operands[0].asByte()];
                        state.CursorCharacter = (cursorStr.Length > 0? cursorStr[0] : (char)0);
                    }
                    break;

                case 109: // set.text.attribute
                    {
                        textGraphics.SetTextAttribute(action.Operands[0].asByte(), action.Operands[1].asByte());
                    }
                    break;

                case 110: // shake.screen
                    {
                        ShakeScreen(action.Operands[0].asByte());
                    }
                    break;

                case 111: // configure.screen
                    {
                        state.PictureRow = action.Operands[0].asByte();
                        state.InputLineRow = action.Operands[1].asByte();
                        state.StatusLineRow = action.Operands[2].asByte();
                    }
                    break;

                case 112: // status.line.on
                    {
                        state.ShowStatusLine = true;
                        textGraphics.ClearLines(state.StatusLineRow, state.StatusLineRow, 15);
                        textGraphics.UpdateStatusLine();
                    }
                    break;

                case 113: // status.line.off
                    {
                        state.ShowStatusLine = false;
                        textGraphics.ClearLines(state.StatusLineRow, state.StatusLineRow, 0);
                    }
                    break;

                case 114: // set.string
                    {
                        state.Strings[action.Operands[0].asByte()] = action.Logic.Messages[action.Operands[1].asByte()];
                    }
                    break;

                case 115: // get.string
                    {
                        textGraphics.GetString(action.Operands[0].asByte(), action.Logic.Messages[action.Operands[1].asByte()],
                            action.Operands[2].asByte(), action.Operands[3].asByte(), action.Operands[4].asByte());
                    }
                    break;

                case 116: // word.to.string
                    {
                        state.Strings[action.Operands[0].asByte()] = state.RecognisedWords[action.Operands[1].asByte()];
                    }
                    break;

                case 117: // parse
                    {
                        parser.ParseString(action.Operands[0].asByte());
                    }
                    break;

                case 118: // get.num
                    {
                        state.Vars[action.Operands[1].asByte()] = textGraphics.GetNum(action.Logic.Messages[action.Operands[0].asByte()]);
                    }
                    break;

                case 119: // prevent.input
                    {
                        state.AcceptInput = false;
                        textGraphics.UpdateInputLine();
                    }
                    break;

                case 120: // accept.input
                    {
                        state.AcceptInput = true;
                        textGraphics.UpdateInputLine();
                    }
                    break;

                case 121: // set.key
                    {
                        int keyCode = (action.Operands[0].asByte() + (action.Operands[1].asByte() << 8));
                        if (userInput.KeyCodeMap.ContainsKey(keyCode))
                        {
                            int controllerNum = action.Operands[2].asByte();
                            int interKeyCode = userInput.KeyCodeMap[keyCode];
                            if (state.KeyToControllerMap.ContainsKey(interKeyCode))
                            {
                                state.KeyToControllerMap.Remove(interKeyCode);
                            }
                            state.KeyToControllerMap.Add(userInput.KeyCodeMap[keyCode], controllerNum);
                        }
                    }
                    break;

                case 122: // add.to.pic
                    {
                        AnimatedObject picObj = new AnimatedObject(state, -1);
                        picObj.AddToPicture(
                            action.Operands[0].asByte(), action.Operands[1].asByte(), action.Operands[2].asByte(), 
                            action.Operands[3].asByte(), action.Operands[4].asByte(), action.Operands[5].asByte(), 
                            action.Operands[6].asByte(), pixels);
                        SplitPriorityPixels();
                        picObj.Show(pixels);
                    }
                    break;

                case 123: // add.to.pic.v
                    {
                        AnimatedObject picObj = new AnimatedObject(state, -1);
                        picObj.AddToPicture(
                            state.Vars[action.Operands[0].asByte()], state.Vars[action.Operands[1].asByte()], 
                            state.Vars[action.Operands[2].asByte()], state.Vars[action.Operands[3].asByte()], 
                            state.Vars[action.Operands[4].asByte()], state.Vars[action.Operands[5].asByte()],
                            state.Vars[action.Operands[6].asByte()], pixels);
                        SplitPriorityPixels();
                    }
                    break;

                case 124: // status
                    {
                        inventory.ShowInventoryScreen();
                    }
                    break;

                case 125: // save.game
                    {
                        savedGames.SaveGameState();
                    }
                    break;

                case 126: // restore.game
                    {
                        if (savedGames.RestoreGameState())
                        {
                            soundPlayer.Reset();
                            menu.EnableAllMenus();
                            ReplayScriptEvents();
                            ShowPicture(false);
                            newRoom = state.CurrentRoom = state.Vars[Defines.CURROOM];
                            textGraphics.UpdateStatusLine();
                            exit = true;
                        }
                    }
                    break;

                case 127: // init.disk
                    {
                        // No need to implement this. 
                    }
                    break;

                case 128: // restart.game
                    {
                        if (state.Flags[Defines.NO_PRMPT_RSTRT] || textGraphics.WindowPrint("Press ENTER to restart\nthe game.\n\nPress ESC to continue\nthis game."))
                        {
                            soundPlayer.Reset();
                            state.Init();
                            state.Flags[Defines.RESTART] = true;
                            menu.EnableAllMenus();
                            textGraphics.ClearLines(0, 24, 0);
                            exit = true;
                            newRoom = 0;
                        }
                    }
                    break;

                case 129: // show.obj
                    {
                        inventory.ShowInventoryObject(action.Operands[0].asByte());
                    }
                    break;

                case 130: // random.num
                    {
                        int minVal = action.Operands[0].asByte();
                        int maxVal = action.Operands[1].asByte();
                        state.Vars[action.Operands[2].asByte()] = (byte)((state.Random.Next(0, 255) % (maxVal - minVal + 1)) + minVal);
                    }
                    break;

                case 131: // program.control
                    {
                        state.UserControl = false;
                    }
                    break;

                case 132: // player.control
                    {
                        state.UserControl = true;
                        state.Ego.MotionType = MotionType.Normal;
                    }
                    break;

                case 133: // obj.status.v
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[state.Vars[action.Operands[0].asByte()]];
                        textGraphics.WindowPrint(aniObj.GetStatusStr());
                    }
                    break;

                case 134: // quit
                    {
                        int quitAction = action.Operands[0].asByte();
                        if ((quitAction == 1) || textGraphics.WindowPrint("Press ENTER to quit.\nPress ESC to keep playing."))
                        {
                            soundPlayer.Shutdown();
                            System.Windows.Forms.Application.Exit();
                        }
                    }
                    break;

                case 135: // show.mem
                    {
                        // No need to implement this.
                    }
                    break;

                case 136: // pause
                    {
                        // Note: In the original AGI interpreter, pause stopped sound rather than pause
                        soundPlayer.StopSound();
                        this.textGraphics.Print("      Game paused.\nPress Enter to continue.");
                    }
                    break;

                case 137: // echo.line
                    {
                        if (state.CurrentInput.Length < state.LastInput.Length)
                        {
                            state.CurrentInput.Append(state.LastInput.Substring(state.CurrentInput.Length));
                        }
                    }
                    break;

                case 138: // cancel.line
                    {
                        state.CurrentInput.Clear();
                    }
                    break;

                case 139: // init.joy
                    {
                        // No need to implement this.
                    }
                    break;

                case 140: // toggle.monitor
                    {
                        // No need to implement this.
                    }
                    break;

                case 141: // version
                    {
                        this.textGraphics.Print("Adventure Game Interpreter\n      Version " + state.Version);
                    }
                    break;

                case 142: // script.size
                    {
                        state.ScriptBuffer.SetScriptSize(action.Operands[0].asByte());
                    }
                    break;

                // --------------------------------------------------------------------------------------------------
                // ---- AGI version 2.001 in effect ended here. It did have a 143 and 144 but they were different ---
                // --------------------------------------------------------------------------------------------------

                case 143: // set.game.id (was max.drawn in AGI v2.001)
                    {
                        state.GameId = action.Logic.Messages[action.Operands[0].asByte()];
                    }
                    break;

                case 144: // log
                    {
                        // No need to implement this.
                    }
                    break;

                case 145: // set.scan.start
                    {
                        state.ScanStart[action.Logic.Index] = action.ActionNumber + 1;
                    }
                    break;

                case 146: // reset.scan.start
                    {
                        state.ScanStart[action.Logic.Index] = 0;
                    }
                    break;

                case 147: // reposition.to
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.X = action.Operands[1].asByte();
                        aniObj.Y = action.Operands[2].asByte();
                        aniObj.Repositioned = true;
                        aniObj.FindPosition();         // Make sure that this position is OK.
                    }
                    break;

                case 148: // reposition.to.v
                    {
                        AnimatedObject aniObj = state.AnimatedObjects[action.Operands[0].asByte()];
                        aniObj.X = state.Vars[action.Operands[1].asByte()];
                        aniObj.Y = state.Vars[action.Operands[2].asByte()];
                        aniObj.Repositioned = true;
                        aniObj.FindPosition();         // Make sure that this position is OK.
                    }
                    break;

                case 149: // trace.on
                    {
                        // No need to implement this.
                    }
                    break;

                case 150: // trace.info
                    {
                        // No need to implement this.
                    }
                    break;

                case 151: // print.at
                    {
                        string message = action.Logic.Messages[action.Operands[0].asByte()];
                        int row = action.Operands[1].asByte();
                        int col = action.Operands[2].asByte();
                        int width = action.Operands[3].asByte();
                        this.textGraphics.PrintAt(message, row, col, width);
                    }
                    break;

                case 152: // print.at.v
                    {
                        string message = action.Logic.Messages[state.Vars[action.Operands[0].asByte()]];
                        int row = action.Operands[1].asByte();
                        int col = action.Operands[2].asByte();
                        int width = action.Operands[3].asByte();
                        this.textGraphics.PrintAt(message, row, col, width);
                    }
                    break;

                case 153: // discard.view.v
                    {
                        // All views are kept loaded in this interpreter, so nothing to do as such
                        // other than to remember it was "unloaded".
                        View view = state.Views[state.Vars[action.Operands[0].asByte()]];
                        if ((view != null) && view.IsLoaded)
                        {
                            view.IsLoaded = false;
                            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.DiscardView, view.Index);
                        }
                    }
                    break;

                case 154: // clear.text.rect
                    {
                        int top = action.Operands[0].asByte();
                        int left = action.Operands[1].asByte();
                        int bottom = action.Operands[2].asByte();
                        int right = action.Operands[3].asByte();
                        int colour = textGraphics.MakeBackgroundColour(action.Operands[4].asByte());
                        textGraphics.ClearRect(top, left, bottom, right, colour);
                    }
                    break;

                case 155: // set.upper.left
                    {
                        // Only used on the Apple. No need to implement.
                    }
                    break;

                // --------------------------------------------------------------------------------------------------
                // ---- AGI version 2.089 ends with command 155 above, i.e before the menu system was introduced ----
                // --------------------------------------------------------------------------------------------------

                case 156: // set.menu
                    {
                        menu.SetMenu(action.Logic.Messages[action.Operands[0].asByte()]);
                    }
                    break;

                case 157: // set.menu.item
                    {
                        string menuItemName = action.Logic.Messages[action.Operands[0].asByte()];
                        byte controllerNum = action.Operands[1].asByte();
                        menu.SetMenuItem(menuItemName, controllerNum);
                    }
                    break;

                case 158: // submit.menu
                    {
                        menu.SubmitMenu();
                    }
                    break;

                case 159: // enable.item
                    {
                        menu.EnableItem(action.Operands[0].asByte());
                    }
                    break;

                case 160: // disable.item
                    {
                        menu.DisableItem(action.Operands[0].asByte());
                    }
                    break;

                case 161: // menu.input
                    {
                        state.MenuOpen = true;
                    }
                    break;

                // -------------------------------------------------------------------------------------------------
                // ---- AGI version 2.272 ends with command 161 above, i.e after the menu system was introduced ----
                // -------------------------------------------------------------------------------------------------

                case 162: // show.obj.v
                    {
                        inventory.ShowInventoryObject(state.Vars[action.Operands[0].asByte()]);
                    }
                    break;

                case 163: // open.dialogue
                    {
                        // Appears to be something specific to monochrome. No need to implement.
                    }
                    break;

                case 164: // close.dialogue
                    {
                        // Appears to be something specific to monochrome. No need to implement.
                    }
                    break;

                case 165: // mul.n
                    {
                        byte varNum = action.Operands[0].asByte();
                        byte value = action.Operands[1].asByte();
                        state.Vars[varNum] *= value;
                    }
                    break;

                case 166: // mul.v
                    {
                        byte varNum1 = action.Operands[0].asByte();
                        byte varNum2 = action.Operands[1].asByte();
                        state.Vars[varNum1] *= state.Vars[varNum2];
                    }
                    break;

                case 167: // div.n
                    {
                        byte varNum = action.Operands[0].asByte();
                        byte value = action.Operands[1].asByte();
                        state.Vars[varNum] /= value;
                    }
                    break;

                case 168: // div.v
                    {
                        byte varNum1 = action.Operands[0].asByte();
                        byte varNum2 = action.Operands[1].asByte();
                        state.Vars[varNum1] /= state.Vars[varNum2];
                    }
                    break;

                case 169: // close.window
                    {
                        textGraphics.CloseWindow();
                    }
                    break;

                case 170: // set.simple (i.e. simpleName variable for saved games)
                    {
                        state.SimpleName = action.Logic.Messages[action.Operands[0].asByte()];
                    }
                    break;

                case 171: // push.script
                    {
                        state.ScriptBuffer.PushScript();
                    }
                    break;

                case 172: // pop.script
                    {
                        state.ScriptBuffer.PopScript();
                    }
                    break;

                case 173: // hold.key
                    {
                        state.HoldKey = true;
                    }
                    break;

                // --------------------------------------------------------------------------------------------------
                // ---- AGI version 2.915/2.917 ends with command 173 above                                      ----
                // --------------------------------------------------------------------------------------------------

                case 174: // set.pri.base
                    {
                        state.PriorityBase = action.Operands[0].asByte();
                    }
                    break;

                case 175: // discard.sound
                    {
                        // Note: Interpreter 2.936 doesn't persist discard sound to the script event buffer.
                    }
                    break;

                // --------------------------------------------------------------------------------------------------
                // ---- AGI version 2.936 ends with command 175 above                                            ----
                // --------------------------------------------------------------------------------------------------

                case 176: // hide.mouse
                    {
                        // This command isn't supported by PC versions of original AGI Interpreter.
                    }
                    break;

                case 177: // allow.menu
                    {
                        state.MenuEnabled = (action.Operands[0].asByte() != 0);
                    }
                    break;

                case 178: // show.mouse
                    {
                        // This command isn't supported by PC versions of original AGI Interpreter.
                    }
                    break;

                case 179: // fence.mouse
                    {
                        // This command isn't supported by PC versions of original AGI Interpreter.
                    }
                    break;

                case 180: // mouse.posn
                    {
                        // This command isn't supported by PC versions of original AGI Interpreter.
                    }
                    break;

                case 181: // release.key
                    {
                        state.HoldKey = false;
                    }
                    break;

                case 182: // adj.ego.move.to.x.y
                    {
                        // This command isn't supported by PC versions of original AGI Interpreter.
                    }
                    break;

                case 0xfe: // Unconditional branch: else, goto.
                    {
                        nextActionNum = ((GotoAction)action).GetDestinationActionIndex();
                    }
                    break;

                case 0xff: // Conditional branch: if.
                    {
                        foreach (Condition condition in action.Operands[0].asConditions())
                        {
                            if (!IsConditionTrue(condition))
                            {
                                nextActionNum = ((IfAction)action).GetDestinationActionIndex();
                                break;
                            }
                        }
                    }
                    break;

                default:    // Error has occurred
                    break;
            }

            return nextActionNum;
        }

        /// <summary>
        /// Executes the Logic identified by the given logic number.
        /// </summary>
        /// <param name="logicNum">The number of the Logic to execute.</param>
        /// <returns>If new.room was invoked, the new room number; otherwise the current room number.</returns>
        public byte ExecuteLogic(int logicNum)
        {
            // Remember the previous Logic number.
            int previousLogNum = state.CurrentLogNum;

            // Store the new Logic number in the state so that actions will know this.
            state.CurrentLogNum = logicNum;

            // Prepare to start executing the Logic.
            Logic logic = state.Logics[logicNum];
            int actionNum = state.ScanStart[logicNum];
            byte newRoom = state.CurrentRoom;
            bool exit = false;

            // Continually execute the Actions in the Logic until one of them tells us to exit.
            do actionNum = ExecuteAction(logic.Actions[actionNum], ref newRoom, ref exit); while (!exit);

            // Restore the previous Logic number before we leave.
            state.CurrentLogNum = previousLogNum;

            // If new.room was not one of the Actions executed, then newRoom will still have the current
            // room value; otherwise it will have the number of the new room.
            return newRoom;
        }
    }
}
