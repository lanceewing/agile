using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AGILE
{
    /// <summary>
    /// Provides methods for drawing text on to the AGI screen.
    /// </summary>
    class TextGraphics
    {
        private const int WINTOP = 1;
        private const int WINBOT = 20;
        private const int WINWIDTH = 30;
        private const int VMARGIN = 5;
        private const int HMARGIN = 5;
        private const int CHARWIDTH = 4;        /* in our coordinates */
        private const int CHARHEIGHT = 8;
        private const int INVERSE = 0x8f;       /* inverse video, i.e. black on white */
        private const int UNASSIGNED = -1;

        /// <summary>
        /// Stores details about the currently displayed text window.
        /// </summary>
        public class TextWindow
        {
            // Mandatory items required by OpenWindow.
            public int Position { get; set; }
            public int Dimensions { get; set; }
            public int X { get { return ((((Position >> 8) & 0xFF) << 1)); } }
            public int Y { get { return ((Position & 0xFF) - (((Dimensions >> 8) & 0xFF) - 1) + 8); } }
            public int Width { get { return ((Dimensions & 0xFF) << 1); } }
            public int Height { get { return ((Dimensions >> 8) & 0xFF); } }
            public int BackgroundColour { get; set; }
            public int BorderColour { get; set; }

            // Items set by OpenWindow.
            public int[,] BackPixels { get; set; }

            // Items always set by WindowNoWait.
            public int Top { get; set; }
            public int Left { get; set; }
            public int Bottom { get; set; }
            public int Right { get; set; }
            public string[] TextLines { get; set; }
            public int TextColour { get; set; }

            // Items optionally set by WindowNoWait.
            public AnimatedObject AniObj { get; set; }

            public TextWindow(
                int position, int dimensions, int backgroundColour, int borderColour, int top = 0, int left = 0, 
                int bottom = 0, int right = 0, string[] textLines = null, int textColour = 0, AnimatedObject aniObj = null)
            {
                this.Position = position;
                this.Dimensions = dimensions;
                this.BackgroundColour = backgroundColour;
                this.BorderColour = borderColour;
                this.Top = top;
                this.Left = left;
                this.Bottom = bottom;
                this.Right = right;
                this.TextLines = textLines;
                this.TextColour = textColour;
                this.AniObj = aniObj;
            }
        }

        /// <summary>
        /// Stores details about the currently displayed text window.
        /// </summary>
        private TextWindow openWindow;

        private int winWidth = -1;
        private int winULRow = -1;
        private int winULCol = -1;
        private int maxLength;

        private char escapeChar = '\\';         /* the escape character */

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
        /// The pixels array for the AGI screen, in which the text will be drawn.
        /// </summary>
        private int[] pixels;

        /// <summary>
        /// The pixels array for storing the GameScreen pixels when a window or text screen is being displayed.
        /// </summary>
        private int[] savePixels;

        /// <summary>
        /// Constructor for TextGraphics.
        /// </summary>
        /// <param name="pixels">The GameScreen pixels. This is what TextGraphics draws windows (and indirectly menus) to.</param>
        /// <param name="state">The GameState class holds all of the data and state for the Game currently running.</param>
        /// <param name="userInput">Holds the data and state for the user input, i.e. keyboard and mouse input.</param>
        public TextGraphics(int[] pixels, GameState state, UserInput userInput)
        {
            this.state = state;
            this.userInput = userInput;
            this.pixels = pixels;
            this.savePixels = new int[pixels.Length];
            this.openWindow = null;
            this.ClearLines(0, 24, 0);
        }

        /// <summary>
        /// Sets the text colour attributes used when drawing text characters.
        /// </summary>
        /// <param name="foregroundColour"></param>
        /// <param name="backgroundColour"></param>
        public void SetTextAttribute(byte foregroundColour, byte backgroundColour)
        {
            state.ForegroundColour = foregroundColour;
            state.BackgroundColour = MakeBackgroundColour(backgroundColour);
            state.TextAttribute = MakeTextAttribute(foregroundColour, backgroundColour);
        }

        /// <summary>
        /// Return the requested text attribute in it's internal representation.
        /// </summary>
        /// <param name="foregroundColour"></param>
        /// <param name="backgroundColour"></param>
        /// <returns></returns>
        private int MakeTextAttribute(byte foregroundColour, byte backgroundColour)
        {
            if (!state.GraphicsMode)
            {
                // For text mode, put background in high nibble, fore in low.
                return (((backgroundColour << 4) | foregroundColour) & 0xFF);
            }
            else
            {
                // In graphics mode, if back is not black, approximate with inverse text (black on white).
                return (backgroundColour == 0? foregroundColour : INVERSE);
            }
        }

        /// <summary>
        /// Return the internal representation for the requested background color.
        /// </summary>
        /// <param name="backgroundColour"></param>
        /// <returns>The internal representation for the requested background color.</returns>
        public int MakeBackgroundColour(byte backgroundColour)
        {
            if (state.GraphicsMode && (backgroundColour != 0))
            {
                // In graphics if back is not black, approximate with inverse text (black on white).
                return (0xff);  /* mask off inverse */
            }
            else
            {
                // This is rather strange, but for clear.lines and clear.text.rect, in text mode the
                // background colour is black regardless of the colour parameter value.
                return (0);
            }
        }

        /// <summary>
        /// Clears the lines from the specified top line to the specified bottom line using the
        /// given background colour.
        /// </summary>
        /// <param name="top"></param>
        /// <param name="bottom"></param>
        /// <param name="backgroundColour"></param>
        public void ClearLines(int top, int bottom, int backgroundColour)
        {
            int startPos = top * 8 * 320;
            int endPos = ((bottom + 1) * 8 * 320) - 1;
            int colour = AGI.Color.Palette[backgroundColour & 0x0F].ToArgb();

            for (int i=startPos; i <= endPos; i++)
            {
                this.pixels[i] = colour;
            }
        }

        /// <summary>
        /// Clears a text rectangle as specified by the top, left, bottom and right values. The top and
        /// bottom values are rows in the text grid and the left and right and columns in the text grid.
        /// </summary>
        /// <param name="top"></param>
        /// <param name="left"></param>
        /// <param name="bottom"></param>
        /// <param name="right"></param>
        /// <param name="backgroundColour"></param>
        public void ClearRect(int top, int left, int bottom, int right, int backgroundColour)
        {
            int backgroundArgb = AGI.Color.Palette[backgroundColour & 0x0F].ToArgb();
            int height = ((bottom - top) + 1) * 8;
            int width = ((right - left) + 1) * 8;
            int startY = (top * 8);
            int startX = (left * 8);
            int startScreenPos = ((startY * 320) + startX);
            int screenYAdd = 320 - width;

            for (int y = 0, screenPos = startScreenPos; y < height; y++, screenPos += screenYAdd)
            {
                for (int x = 0; x < width; x++, screenPos++)
                {
                    this.pixels[screenPos] = backgroundArgb;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="backgroundColour"></param>
        public void TextScreen(int backgroundColour = UNASSIGNED)
        {
            state.GraphicsMode = false;

            if (backgroundColour == UNASSIGNED)
            {
                SetTextAttribute((byte)state.ForegroundColour, (byte)state.BackgroundColour);
                // Note that the original AGI interpreter uses the background from the TextAttribute 
                // value rather than the current BackgroundColour.
                backgroundColour = ((state.TextAttribute >> 4) & 0x0F);
            }

            // Clear the whole screen to the background colour.
            ClearLines(0, 24, backgroundColour);
        }

        /// <summary>
        /// 
        /// </summary>
        public void GraphicsScreen()
        {
            state.GraphicsMode = true;

            SetTextAttribute((byte)state.ForegroundColour, (byte)state.BackgroundColour);

            // Clear whole screen to black.
            ClearLines(0, 24, 0);

            // Copy VisualPixels to game screen.
            System.Buffer.BlockCopy(state.VisualPixels, 0, this.pixels, (8 * state.PictureRow) * 320 * sizeof(int), sizeof(int) * state.VisualPixels.Length);

            UpdateStatusLine();
            UpdateInputLine();
        }

        /// <summary>
        /// Draws a character to the AGI screen. Depending on the usage, this may either be done
        /// to the VisualPixels or directly to the GameScreen pixels. Windows and menu text is 
        /// drawn directly to the GameScreen pixels, but Display action commands are drawn to the
        /// VisualPixels array.
        /// </summary>
        /// <param name="pixels">The pixel array to draw the character to.</param>
        /// <param name="charNum">The ASCII code number of the character to draw.</param>
        /// <param name="x">The X position of the character.</param>
        /// <param name="y">The Y position of the character.</param>
        /// <param name="foregroundColour">The foreground colour of the character.</param>
        /// <param name="backgroundColour">The background colour of the character.</param>
        /// <param name="halfTone">If true then character are only half drawn.</param>
        public void DrawChar(int[] pixels, byte charNum, int x, int y, int foregroundColour, int backgroundColour, bool halfTone = false)
        {
            for (int byteNum = 0; byteNum < 8; byteNum++)
            {
                int fontByte = IBM_BIOS_FONT[(charNum << 3) + byteNum];
                bool halfToneState = ((byteNum % 2) == 0);

                for (int bytePos = 7; bytePos >= 0; bytePos--)
                {
                    if (!halfTone || halfToneState)
                    {
                        if ((fontByte & (1 << bytePos)) != 0)
                        {
                            pixels[((y + byteNum) * 320) + x + (7 - bytePos)] = AGI.Color.Palette[foregroundColour].ToArgb();
                        }
                        else
                        {
                            pixels[((y + byteNum) * 320) + x + (7 - bytePos)] = AGI.Color.Palette[backgroundColour].ToArgb();
                        }
                    }

                    halfToneState = !halfToneState;
                }
            }
        }

        /// <summary>
        /// Draws the given string to the AGI screen, at the given x/y position, in the given colours.
        /// </summary>
        /// <param name="pixels">The pixel array to draw the character to.</param>
        /// <param name="text">The text to draw to the screen.</param>
        /// <param name="x">The X position of the text.</param>
        /// <param name="y">The Y position of the text.</param>
        /// <param name="foregroundColour">Optional foreground colour. Defaults to currently active foreground colour if not specified.</param>
        /// <param name="backgroundColour">Optional background colour. Defaults to currently active background colour if not specified.</param>
        /// <param name="halfTone">If true then character are only half drawn.</param>
        public void DrawString(int[]pixels, string text, int x, int y, int foregroundColour = UNASSIGNED, int backgroundColour = UNASSIGNED, bool halfTone = false)
        {
            // This method is used as both a general text drawing method, for things like the menu 
            // and inventory, and also for the print and display commands. The print and display
            // commands will operate using the currently set text attribute, foreground and background
            // values. The more general use cases would pass in the exact colours that they want to
            // use, no questions asked.

            // Foreground colour.
            if (foregroundColour == UNASSIGNED)
            {
                if (state.GraphicsMode)
                {
                    // In graphics mode, if background is not black, foreground is black; otherwise as is.
                    foregroundColour = (state.BackgroundColour == 0? state.ForegroundColour : 0);
                }
                else
                {
                    // In text mode, we use the text attribute foreground colour as is.
                    foregroundColour = (state.TextAttribute & 0x0F);
                }
            }

            // Background colour.
            if (backgroundColour == UNASSIGNED)
            {
                if (state.GraphicsMode)
                {
                    // In graphics mode, background can only be black or white.
                    backgroundColour = (state.BackgroundColour == 0 ? 0 : 15);
                }
                else
                {
                    // In text mode, we use the text attribute background colour as is.
                    backgroundColour = ((state.TextAttribute >> 4) & 0x0F);
                }
            }

            byte[] textBytes = Encoding.ASCII.GetBytes(text);

            for (int charPos = 0; charPos < textBytes.Length; charPos++)
            {
                DrawChar(pixels, textBytes[charPos], x + (charPos * 8), y, foregroundColour, backgroundColour, halfTone);
            }
        }

        /// <summary>
        /// Display the given string at the given row and col. This method renders only the text and 
        /// does not pop up a message window.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        public void Display(string str, int row, int col)
        {
            // Expand references and split on new lines.
            string[] lines = BuildMessageLines(str, Defines.TEXTCOLS + 1, col);

            for (int i = 0; i < lines.Length; i++)
            {
                DrawString(this.pixels, lines[i], col * 8, (row + i) * 8);

                // For subsequent lines, we start at column 0 and ignore what was passed in.
                col = 0;
            }
        }

        /// <summary>
        /// Print the given string in an AGI message window.
        /// </summary>
        /// <param name="str">The text to include in the message window.</param>
        public void Print(string str)
        {
            WindowPrint(str);
        }

        /// <summary>
        /// Print the given string in an AGI message window, the window positioned at the given row
        /// and col, and of the given width.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="width"></param>
        public void PrintAt(string str, int row, int col, int width)
        {
            winULRow = row;
            winULCol = col;

            if ((winWidth = width) == 0)
            {
                winWidth = WINWIDTH;
            }

            WindowPrint(str);

            winWidth = winULRow = winULCol = -1;
        }

        /// <summary>
        /// Updates the status line with the score and sound status.
        /// </summary>
        public void UpdateStatusLine()
        {
            if (state.ShowStatusLine)
            {
                ClearLines(state.StatusLineRow, state.StatusLineRow, 15);

                StringBuilder scoreStatus = new StringBuilder();
                scoreStatus.Append(" Score:");
                scoreStatus.Append(state.Vars[Defines.SCORE]);
                scoreStatus.Append(" of ");
                scoreStatus.Append(state.Vars[Defines.MAXSCORE]);
                DrawString(this.pixels, scoreStatus.ToString().PadRight(30, ' '), 0, state.StatusLineRow * 8, 0, 15);

                StringBuilder soundStatus = new StringBuilder();
                soundStatus.Append("Sound:");
                soundStatus.Append(state.Flags[Defines.SOUNDON] ? "on" : "off");
                DrawString(this.pixels, soundStatus.ToString().PadRight(10, ' '), 30 * 8, state.StatusLineRow * 8, 0, 15);
            }
        }

        /// <summary>
        /// Updates the user input line based on current state.
        /// </summary>
        public void UpdateInputLine(bool clearWhenNotEnabled = true)
        {
            if (state.GraphicsMode)
            {
                if (state.AcceptInput)
                {
                    // Input line has the prompt string at the start, then the user input.
                    StringBuilder inputLine = new StringBuilder();
                    if (state.Strings[0] != null)
                    {
                        inputLine.Append(ExpandReferences(state.Strings[0]));
                    }
                    inputLine.Append(state.CurrentInput.ToString());
                    if (state.CursorCharacter > 0)
                    {
                        // Cursor character is optional. There isn't one at the start of the game.
                        inputLine.Append(state.CursorCharacter);
                    }

                    DrawString(this.pixels, inputLine.ToString().PadRight(Defines.MAXINPUT, ' '), 0, state.InputLineRow * 8);
                }
                else if (clearWhenNotEnabled)
                {
                    // If not accepting input, clear the prompt and text input.
                    ClearLines(state.InputLineRow, state.InputLineRow, 0);
                }
            }
        }

        /// <summary>
        /// Prints the message as a prompt at column 0 of the current input row, then allows the user to
        /// enter some text. The entered text will have everything other than digits stripped from it, then 
        /// it is converted into a number and returned.
        /// </summary>
        /// <param name="message">The message to display to the player instructing them what to enter.</param>
        /// <returns>The entered number as a byte, or 0 if it can't be converted.</returns>
        public byte GetNum(string message)
        {
            ClearLines(state.InputLineRow, state.InputLineRow, 0);

            // Show the prompt message to the user at the specified position.
            Display(message, state.InputLineRow, 0);

            // Get a line of text from the user.
            string line = GetLine(4, (byte)state.InputLineRow, (byte)message.Length);

            // Strip out everything that isn't a digit. A little more robust than the original AGI interpreter.
            string digitsInLine = new string(line.Where(c => char.IsDigit(c)).ToArray());

            UpdateInputLine();

            return (byte)(digitsInLine.Length > 0? Int32.Parse(digitsInLine) : 0);
        }

        /// <summary>
        /// Prints the message as a prompt at the given screen position, then allows the user to enter
        /// the string for string number.
        /// </summary>
        /// <param name="strNum">The number of the user string to put the entered value in to.</param>
        /// <param name="message">A message to display to the player instructing them what to enter.</param>
        /// <param name="row">The row to display the message at.</param>
        /// <param name="col">The column to display the message at.</param>
        /// <param name="length">The maximum length of the string to get.</param>
        public void GetString(int strNum, string message, byte row, byte col, byte length)
        {
            // The string cannot be longer than the maximum length for a user string.
            length = (byte)(length > Defines.STRLENGTH? Defines.STRLENGTH : length);

            // Show the prompt message to the user at the specified position.
            Display(message, row, col);

            // Position the input area immediately after the message.
            col += (byte)message.Length;

            // Get a line of text from the user.
            string line = GetLine(length, row, col);

            // If it is not null, i.e. the user didn't hit ESC, then store in user string.
            if (line != null) state.Strings[strNum] = line;
        }

        /// <summary>
        /// Gets a line of user input, echoing the prompt char and entered text at the specified position.
        /// </summary>
        /// <param name="length">The maximum length of the line of text to get.</param>
        /// <param name="row">The row on the screen to position the text entry field.</param>
        /// <param name="col">The column on the screen to position the start of the text entry field.</param>
        /// <param name="str">The value to initialise the text entry field with; defaults to empty.</param>
        /// <param name="foregroundColour">The foreground colour of the text in the text entry field.</param>
        /// <param name="backgroundColour">The background colour of the text in the text entry field.</param>
        /// <returns>The entered string if ENTER was hit, otherwise null if ESC was hit.</returns>
        public string GetLine(int length, byte row, byte col, string str = "", int foregroundColour = -1, int backgroundColour = -1)
        {
            StringBuilder line = new StringBuilder(str);

            // The string cannot be longer than the maximum length for a GetLine call.
            length = (byte)(length > Defines.GLSIZE ? Defines.GLSIZE : length);

            // Process entered keys until either ENTER or ESC is pressed.
            while (true)
            {
                // Show the currently entered text.
                DrawString(this.pixels, (line.ToString() + state.CursorCharacter), col * 8, row * 8, foregroundColour, backgroundColour);

                int key = userInput.WaitForKey(false);

                if ((key & 0xF0000) == 0x80000)  // Standard char from a keypress event.
                {
                    // If we haven't reached the max length, add the char to the line of text.
                    if (line.Length < length) line.Append((char)(key & 0xff));
                }
                else if (key == (int)Keys.Escape)
                {
                    // Exits without returning any entered text.
                    return null;
                }
                else if (key == (int)Keys.Enter)
                {
                    // If ENTER is hit, we break out of the loop and return the entered line of text.
                    while (userInput.Keys[(int)Keys.Enter]) { /* Wait until ENTER released */ }
                    break;
                }
                else if (key == (int)Keys.Back)
                {
                    // Removes one from the end of the currently entered input.
                    if (line.Length > 0) line.Remove(line.Length - 1, 1);

                    // Render Line with a space overwritting the previous position of the cursor.
                    DrawString(this.pixels, (line.ToString() + state.CursorCharacter + " "), col * 8, row * 8, foregroundColour, backgroundColour);
                }
            }

            return line.ToString();
        }

        /// <summary>
        /// Print the string 'str' in a window on the screen and wait for ACCEPT or ABORT 
        /// before disposing of it.Return TRUE for ACCEPT, FALSE for ABORT.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="aniObj">Optional AnimatedObject to draw when the window is opened.</param>
        /// <returns></returns>
        public bool WindowPrint(string str, AnimatedObject aniObj = null)
        {
            bool retVal;
            long timeOut;

            // Display the window.
            WindowNoWait(str, 0, 0, false, aniObj);

            // If we're to leave the window up, just return.
            if (state.Flags[Defines.LEAVE_WIN] == true)
            {
                state.Flags[Defines.LEAVE_WIN] = false;
                return true;
            }

            // Get the response.
            if (state.Vars[Defines.PRINT_TIMEOUT] == 0)
            {
                retVal = (userInput.WaitAcceptAbort() == UserInput.ACCEPT);
            }
            else
            {
                // The timeout value is given in half seconds and the TotalTicks in 1/60ths of a second.
                timeOut = state.TotalTicks + state.Vars[Defines.PRINT_TIMEOUT] * 30;

                while ((state.TotalTicks < timeOut) && (userInput.CheckAcceptAbort() == -1)) Thread.Sleep(1);

                retVal = true;

                state.Vars[Defines.PRINT_TIMEOUT] = 0;
            }

            // Close the window.
            CloseWindow();

            return retVal;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="height"></param>
        /// <param name="width"></param>
        /// <param name="fixedSize"></param>
        /// <param name="aniObj">Optional AnimatedObject to draw when the window is opened.</param>
        /// <returns></returns>
        public TextWindow WindowNoWait(string str, int height, int width, bool fixedSize, AnimatedObject aniObj = null)
        {
            string[] lines;
            int numLines = 0;

            if (openWindow != null)
            {
                CloseWindow();
            }

            if ((winWidth == -1) && (width == 0))
            {
                width = WINWIDTH;
            }
            else if (winWidth != -1)
            {
                width = winWidth;
            }

            while (true)
            {
                // First make a formatting pass through the message, getting maximum line length and number of lines.
                lines = BuildMessageLines(str, width);
                numLines = lines.Length;

                if (fixedSize)
                {
                    maxLength = width;
                    if (height != 0)
                    {
                        numLines = height;
                    }
                }

                if (numLines > (WINBOT - WINTOP))
                {
                    str = String.Format("Message too verbose:\n\n\"{0}...\"\n\nPress ESC to continue.", str.Substring(0, 20));
                }
                else
                {
                    break;
                }
            }

            int top = (winULRow == -1 ? WINTOP + (WINBOT - WINTOP - numLines) / 2 : winULRow) + state.PictureRow;
            int bottom = top + numLines - 1;
            int left = (winULCol == -1 ? (Defines.TEXTCOLS - maxLength) / 2 : winULCol);
            int right = left + maxLength;

            // Compute window size and position and put them into the appropriate bytes of the words.
            int windowDim = ((numLines * CHARHEIGHT + 2 * VMARGIN) << 8) | (maxLength * CHARWIDTH + 2 * HMARGIN);
            int windowPos = ((left * CHARWIDTH - HMARGIN) << 8) | (bottom * CHARHEIGHT + VMARGIN - 1);

            // Open the window, white with a red border and black text.
            return OpenWindow(new TextWindow(windowPos, windowDim, 15, 4, top, left, bottom, right, lines, 0, aniObj));
        }

        /// <summary>
        /// Builds the array of message lines to be included in a message window. The str parameter
        /// provides the message text, which may contain special % command references that need 
        /// expanding first. After that substitution, the resulting message text is split up on to
        /// lines that are no longer than the given width, words wrapping down a line if required.
        /// </summary>
        /// <param name="str">The message text to expand references and split in to lines.</param>
        /// <param name="width">The maximum width that a message line can be.</param>
        /// <param name="startColumn">Optional starting column value; defaults to 0.</param>
        /// <returns></returns>
        private string[] BuildMessageLines(string str, int width, int startColumn = 0)
        {
            List<String> lines = new List<String>();

            maxLength = 0;

            if (str != null)
            {
                // Recursively expand/substitute references to other strings.
                string processedMessage = ExpandReferences(str);

                // Now that we have the processed message text, split it in to lines.
                StringBuilder currentLine = new StringBuilder();

                // Pad the first line with however many spaces required to begin at starting column.
                if (startColumn > 0) currentLine.Append("".PadRight(startColumn));

                for (int i = 0; i < processedMessage.Length; i++)
                {
                    int addLines = (i == (processedMessage.Length - 1)) ? 1 : 0;

                    if (processedMessage[i] == 0x0A)
                    {
                        addLines++;
                    }
                    else
                    {
                        // Add the character to the current line.
                        currentLine.Append(processedMessage[i]);

                        // If the current line has reached the width, then word wrap.
                        if (currentLine.Length >= width)
                        {
                            WrapWord(currentLine, ref i);

                            addLines = 1;
                        }
                    }

                    while (addLines-- > 0)
                    {
                        if ((startColumn > 0) && (lines.Count == 0))
                        {
                            // Remove the extra padding that we added at the start of first line.
                            currentLine.Remove(0, startColumn);
                            startColumn = 0;
                        }

                        lines.Add(currentLine.ToString());

                        if (currentLine.Length > maxLength)
                        {
                            maxLength = currentLine.Length;
                        }

                        currentLine.Clear();
                    }
                }
            }

            return lines.ToArray();
        }

        /// <summary>
        /// Winds back the given StringBuilder to the last word separate (i.e. space) and adjusts the
        /// pos index value so that the word that overlapped the max line length is wrapped to the
        /// next line.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="pos"></param>
        private void WrapWord(StringBuilder str, ref int pos)
        {
            for (int i = str.Length - 1; i >= 0; i--)
            {
                if (str[i] == ' ')
                {
                    pos -= (str.Length - i - 1);
                    str.Remove(i, str.Length - i);
                    return;
                }
            }
        }

        /// <summary>
        /// Scans the given string from the given position for a consecutive sequence of digits. When
        /// the end is reached, the string of digits is converted in to numeric form and returned. Any
        /// characters before the given position, and after the end of the sequence of digits, is 
        /// ignored.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="startPos"></param>
        /// <returns></returns>
        private int NumberFromString(string str, ref int pos)
        {
            int startPos = pos;
            while ((pos < str.Length) && (str[pos] >= '0') && (str[pos] <= '9')) pos++;
            return Int32.Parse(str.Substring(startPos, (pos-- - startPos)));
        }

        /// <summary>
        /// Expands the special commands that reference other types of text, such as 
        /// object names, words, other messages, etc.
        /// 
        /// Messages are strings of fewer than 255 characters which may contain 
        /// the following special commands:
        /// 
        ///   \         Take the next character(except '\n' below) literally
        ///   \n        Begin a new line
        ///   %wn       Include word number n from the parsed line (1 &lt; = n &lt;= 255)
        ///   %sn       Include user defined string number n (0 &lt;= n &lt;= 255)
        ///   %mn       Include message number n from this room (0 &lt;= n &lt;= 255)
        ///   %gn       Include global message number n from room 0 (0 &lt;= n &lt;= 255)
        ///   %vn|m     Print the value of var #n. If the optional '|m' is present, print in a field of width m with leading zeros.
        ///   %on       Print the name of the object whose number is in var number n.
        ///   
        /// </summary>
        /// <param name="str">The string to expand the references of.</param>
        /// <returns></returns>
        private string ExpandReferences(string str)
        {
            StringBuilder output = new StringBuilder();

            // Iterate over each character in the message string looking for % codes.
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == escapeChar)
                {
                    // The '\' character escapes the next character (e.g. \%)
                    output.Append(str[++i]);
                }
                else if (str[i] == '%')
                {
                    int num, width;

                    i++;

                    switch (str[i++])
                    {
                        case 'v':
                            num = NumberFromString(str, ref i);
                            if ((i < (str.Length - 1)) && (str[i + 1] == '|'))
                            {
                                i += 2;
                                width = NumberFromString(str, ref i);
                                output.Append(state.Vars[num].ToString().PadLeft(width, '0'));
                            }
                            else
                            {
                                output.Append(state.Vars[num]);
                            }
                            break;

                        case 'm':
                            num = NumberFromString(str, ref i);
                            output.Append(state.Logics[state.CurrentLogNum].Messages[num]);
                            break;

                        case 'g':
                            num = NumberFromString(str, ref i);
                            output.Append(state.Logics[0].Messages[num]);
                            break;

                        case 'w':
                            num = NumberFromString(str, ref i);
                            if (num <= state.RecognisedWords.Count)
                            {
                                output.Append(state.RecognisedWords[num - 1]);
                            }
                            break;

                        case 's':
                            num = NumberFromString(str, ref i);
                            output.Append(state.Strings[num]);
                            break;

                        case 'o':
                            num = NumberFromString(str, ref i);
                            output.Append(state.Objects[num].Name);
                            break;

                        default: // ignore the second character.
                            break;
                    }
                }
                else if (i == 0 && str[i] == '?')
                {
                    // kq2 has some message that starts with a `?`. Replace that with space
                    output.Append(' ');
                }
                else
                {
                    // Default is simply to append the character.
                    output.Append(str[i]);
                }
            }

            // Recursive part to make sure all % formatting codes are dealt with.
            if (output.ToString().Contains("%"))
            {
                return ExpandReferences(output.ToString());
            }
            else
            {
                return output.ToString();
            }
        }

        /// <summary>
        /// Opens an AGI window on the game screen.
        /// </summary>
        /// <param name="textWindow"></param>
        /// <returns>The same TextWindow with the BackPixels populated.</returns>
        public TextWindow OpenWindow(TextWindow textWindow)
        {
            DrawWindow(textWindow);

            // Remember this as the currently open window.
            this.openWindow = textWindow;

            return textWindow;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="textWindow"></param>
        public void DrawWindow(TextWindow textWindow = null)
        {
            // Defaults to the currently open window if one was not provided by the caller.
            textWindow = (textWindow == null ? openWindow : textWindow);

            if (textWindow != null)
            {
                int backgroundArgb = AGI.Color.Palette[textWindow.BackgroundColour].ToArgb();
                int borderArgb = AGI.Color.Palette[textWindow.BorderColour].ToArgb();
                int startScreenPos = (textWindow.Y * 320) + textWindow.X;
                int screenYAdd = (320 - textWindow.Width);

                // The first time that DrawWindow is invoke for a TextWindow, we store the back pixels.
                bool storeBackPixels = (textWindow.BackPixels == null);
                if (storeBackPixels) textWindow.BackPixels = new int[textWindow.Width, textWindow.Height];

                // Draw a box in the background colour and store the pixels that were behind it.
                for (int y = 0, screenPos = startScreenPos; y < textWindow.Height; y++, screenPos += screenYAdd)
                {
                    for (int x = 0; x < textWindow.Width; x++, screenPos++)
                    {
                        // Store the pixel currently at this position (if applicable).
                        if (storeBackPixels) textWindow.BackPixels[x, y] = this.pixels[screenPos];

                        // Overwrite the pixel with the window's background colour.
                        this.pixels[screenPos] = backgroundArgb;
                    }
                }

                // Draw a line just in a bit from the edge of the box in the border colour.
                for (int x = 0, screenPos = (startScreenPos + 320 + 2); x < (textWindow.Width - 4); x++, screenPos++)
                {
                    this.pixels[screenPos] = borderArgb;
                }
                for (int x = 0, screenPos = (startScreenPos + (320 * (textWindow.Height - 2) + 2)); x < (textWindow.Width - 4); x++, screenPos++)
                {
                    this.pixels[screenPos] = borderArgb;
                }
                for (int y = 1, screenPos = (startScreenPos + 640 + 2); y < (textWindow.Height - 2); y++, screenPos += 320)
                {
                    this.pixels[screenPos] = borderArgb;
                    this.pixels[screenPos + 1] = borderArgb;
                    this.pixels[screenPos + (textWindow.Width - 6)] = borderArgb;
                    this.pixels[screenPos + (textWindow.Width - 5)] = borderArgb;
                }

                // Draw the text lines (if applicable).
                if (textWindow.TextLines != null)
                {
                    // Draw the text black on white.
                    for (int i = 0; i < textWindow.TextLines.Length; i++)
                    {
                        DrawString(this.pixels, textWindow.TextLines[i], (textWindow.Left << 3), ((textWindow.Top + i) << 3), textWindow.TextColour, textWindow.BackgroundColour);
                    }
                }

                // Draw the embedded AnimatedObject (if applicable). Supports inventory item description windows.
                if (textWindow.AniObj != null)
                {
                    textWindow.AniObj.Draw();
                    textWindow.AniObj.Show(pixels);
                }
            }
        }

        /// <summary>
        /// Checks if there is a text window currently open.
        /// </summary>
        /// <returns>true if there is a window open; otherwise false.</returns>
        public bool IsWindowOpen()
        {
            return (this.openWindow != null);
        }

        /// <summary>
        /// Closes the current message window.
        /// </summary>
        /// <param name="restoreBackPixels">Whether to restore back pixels or not (defaults to true)</param>
        public void CloseWindow(bool restoreBackPixels = true)
        {
            if (this.openWindow != null)
            {
                if (restoreBackPixels)
                {
                    int startScreenPos = (openWindow.Y * 320) + openWindow.X;
                    int screenYAdd = (320 - openWindow.Width);

                    // Copy each of the stored background pixels back in to their original places.
                    for (int y = 0, screenPos = startScreenPos; y < openWindow.Height; y++, screenPos += screenYAdd)
                    {
                        for (int x = 0; x < openWindow.Width; x++, screenPos++)
                        {
                            this.pixels[screenPos] = openWindow.BackPixels[x, y];
                        }
                    }
                }

                // Clear the currently open window variable.
                this.openWindow = null;
            }
        }

        /// <summary>
        /// The raw bitmap data for the original IBM PC/PCjr BIOS 8x8 font.
        /// </summary>
        private static readonly byte[] IBM_BIOS_FONT =
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x7E, 0x81, 0xA5, 0x81, 0xBD, 0x99, 0x81, 0x7E,
            0x7E, 0xFF, 0xDB, 0xFF, 0xC3, 0xE7, 0xFF, 0x7E,
            0x6C, 0xFE, 0xFE, 0xFE, 0x7C, 0x38, 0x10, 0x00,
            0x10, 0x38, 0x7C, 0xFE, 0x7C, 0x38, 0x10, 0x00,
            0x38, 0x7C, 0x38, 0xFE, 0xFE, 0x7C, 0x38, 0x7C,
            0x10, 0x10, 0x38, 0x7C, 0xFE, 0x7C, 0x38, 0x7C,
            0x00, 0x00, 0x18, 0x3C, 0x3C, 0x18, 0x00, 0x00,
            0xFF, 0xFF, 0xE7, 0xC3, 0xC3, 0xE7, 0xFF, 0xFF,
            0x00, 0x3C, 0x66, 0x42, 0x42, 0x66, 0x3C, 0x00,
            0xFF, 0xC3, 0x99, 0xBD, 0xBD, 0x99, 0xC3, 0xFF,
            0x0F, 0x07, 0x0F, 0x7D, 0xCC, 0xCC, 0xCC, 0x78,
            0x3C, 0x66, 0x66, 0x66, 0x3C, 0x18, 0x7E, 0x18,
            0x3F, 0x33, 0x3F, 0x30, 0x30, 0x70, 0xF0, 0xE0,
            0x7F, 0x63, 0x7F, 0x63, 0x63, 0x67, 0xE6, 0xC0,
            0x99, 0x5A, 0x3C, 0xE7, 0xE7, 0x3C, 0x5A, 0x99,
            0x80, 0xE0, 0xF8, 0xFE, 0xF8, 0xE0, 0x80, 0x00,
            0x02, 0x0E, 0x3E, 0xFE, 0x3E, 0x0E, 0x02, 0x00,
            0x18, 0x3C, 0x7E, 0x18, 0x18, 0x7E, 0x3C, 0x18,
            0x66, 0x66, 0x66, 0x66, 0x66, 0x00, 0x66, 0x00,
            0x7F, 0xDB, 0xDB, 0x7B, 0x1B, 0x1B, 0x1B, 0x00,
            0x3E, 0x63, 0x38, 0x6C, 0x6C, 0x38, 0xCC, 0x78,
            0x00, 0x00, 0x00, 0x00, 0x7E, 0x7E, 0x7E, 0x00,
            0x18, 0x3C, 0x7E, 0x18, 0x7E, 0x3C, 0x18, 0xFF,
            0x18, 0x3C, 0x7E, 0x18, 0x18, 0x18, 0x18, 0x00,
            0x18, 0x18, 0x18, 0x18, 0x7E, 0x3C, 0x18, 0x00,
            0x00, 0x18, 0x0C, 0xFE, 0x0C, 0x18, 0x00, 0x00,
            0x00, 0x30, 0x60, 0xFE, 0x60, 0x30, 0x00, 0x00,
            0x00, 0x00, 0xC0, 0xC0, 0xC0, 0xFE, 0x00, 0x00,
            0x00, 0x24, 0x66, 0xFF, 0x66, 0x24, 0x00, 0x00,
            0x00, 0x18, 0x3C, 0x7E, 0xFF, 0xFF, 0x00, 0x00,
            0x00, 0xFF, 0xFF, 0x7E, 0x3C, 0x18, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x30, 0x78, 0x78, 0x30, 0x30, 0x00, 0x30, 0x00,
            0x6C, 0x6C, 0x6C, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x6C, 0x6C, 0xFE, 0x6C, 0xFE, 0x6C, 0x6C, 0x00,
            0x30, 0x7C, 0xC0, 0x78, 0x0C, 0xF8, 0x30, 0x00,
            0x00, 0xC6, 0xCC, 0x18, 0x30, 0x66, 0xC6, 0x00,
            0x38, 0x6C, 0x38, 0x76, 0xDC, 0xCC, 0x76, 0x00,
            0x60, 0x60, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x18, 0x30, 0x60, 0x60, 0x60, 0x30, 0x18, 0x00,
            0x60, 0x30, 0x18, 0x18, 0x18, 0x30, 0x60, 0x00,
            0x00, 0x66, 0x3C, 0xFF, 0x3C, 0x66, 0x00, 0x00,
            0x00, 0x30, 0x30, 0xFC, 0x30, 0x30, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x30, 0x30, 0x60,
            0x00, 0x00, 0x00, 0xFC, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x30, 0x30, 0x00,
            0x06, 0x0C, 0x18, 0x30, 0x60, 0xC0, 0x80, 0x00,
            0x7C, 0xC6, 0xCE, 0xDE, 0xF6, 0xE6, 0x7C, 0x00,
            0x30, 0x70, 0x30, 0x30, 0x30, 0x30, 0xFC, 0x00,
            0x78, 0xCC, 0x0C, 0x38, 0x60, 0xCC, 0xFC, 0x00,
            0x78, 0xCC, 0x0C, 0x38, 0x0C, 0xCC, 0x78, 0x00,
            0x1C, 0x3C, 0x6C, 0xCC, 0xFE, 0x0C, 0x1E, 0x00,
            0xFC, 0xC0, 0xF8, 0x0C, 0x0C, 0xCC, 0x78, 0x00,
            0x38, 0x60, 0xC0, 0xF8, 0xCC, 0xCC, 0x78, 0x00,
            0xFC, 0xCC, 0x0C, 0x18, 0x30, 0x30, 0x30, 0x00,
            0x78, 0xCC, 0xCC, 0x78, 0xCC, 0xCC, 0x78, 0x00,
            0x78, 0xCC, 0xCC, 0x7C, 0x0C, 0x18, 0x70, 0x00,
            0x00, 0x30, 0x30, 0x00, 0x00, 0x30, 0x30, 0x00,
            0x00, 0x30, 0x30, 0x00, 0x00, 0x30, 0x30, 0x60,
            0x18, 0x30, 0x60, 0xC0, 0x60, 0x30, 0x18, 0x00,
            0x00, 0x00, 0xFC, 0x00, 0x00, 0xFC, 0x00, 0x00,
            0x60, 0x30, 0x18, 0x0C, 0x18, 0x30, 0x60, 0x00,
            0x78, 0xCC, 0x0C, 0x18, 0x30, 0x00, 0x30, 0x00,
            0x7C, 0xC6, 0xDE, 0xDE, 0xDE, 0xC0, 0x78, 0x00,
            0x30, 0x78, 0xCC, 0xCC, 0xFC, 0xCC, 0xCC, 0x00,
            0xFC, 0x66, 0x66, 0x7C, 0x66, 0x66, 0xFC, 0x00,
            0x3C, 0x66, 0xC0, 0xC0, 0xC0, 0x66, 0x3C, 0x00,
            0xF8, 0x6C, 0x66, 0x66, 0x66, 0x6C, 0xF8, 0x00,
            0xFE, 0x62, 0x68, 0x78, 0x68, 0x62, 0xFE, 0x00,
            0xFE, 0x62, 0x68, 0x78, 0x68, 0x60, 0xF0, 0x00,
            0x3C, 0x66, 0xC0, 0xC0, 0xCE, 0x66, 0x3E, 0x00,
            0xCC, 0xCC, 0xCC, 0xFC, 0xCC, 0xCC, 0xCC, 0x00,
            0x78, 0x30, 0x30, 0x30, 0x30, 0x30, 0x78, 0x00,
            0x1E, 0x0C, 0x0C, 0x0C, 0xCC, 0xCC, 0x78, 0x00,
            0xE6, 0x66, 0x6C, 0x78, 0x6C, 0x66, 0xE6, 0x00,
            0xF0, 0x60, 0x60, 0x60, 0x62, 0x66, 0xFE, 0x00,
            0xC6, 0xEE, 0xFE, 0xFE, 0xD6, 0xC6, 0xC6, 0x00,
            0xC6, 0xE6, 0xF6, 0xDE, 0xCE, 0xC6, 0xC6, 0x00,
            0x38, 0x6C, 0xC6, 0xC6, 0xC6, 0x6C, 0x38, 0x00,
            0xFC, 0x66, 0x66, 0x7C, 0x60, 0x60, 0xF0, 0x00,
            0x78, 0xCC, 0xCC, 0xCC, 0xDC, 0x78, 0x1C, 0x00,
            0xFC, 0x66, 0x66, 0x7C, 0x6C, 0x66, 0xE6, 0x00,
            0x78, 0xCC, 0xE0, 0x70, 0x1C, 0xCC, 0x78, 0x00,
            0xFC, 0xB4, 0x30, 0x30, 0x30, 0x30, 0x78, 0x00,
            0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xFC, 0x00,
            0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0x78, 0x30, 0x00,
            0xC6, 0xC6, 0xC6, 0xD6, 0xFE, 0xEE, 0xC6, 0x00,
            0xC6, 0xC6, 0x6C, 0x38, 0x38, 0x6C, 0xC6, 0x00,
            0xCC, 0xCC, 0xCC, 0x78, 0x30, 0x30, 0x78, 0x00,
            0xFE, 0xC6, 0x8C, 0x18, 0x32, 0x66, 0xFE, 0x00,
            0x78, 0x60, 0x60, 0x60, 0x60, 0x60, 0x78, 0x00,
            0xC0, 0x60, 0x30, 0x18, 0x0C, 0x06, 0x02, 0x00,
            0x78, 0x18, 0x18, 0x18, 0x18, 0x18, 0x78, 0x00,
            0x10, 0x38, 0x6C, 0xC6, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF,
            0x30, 0x30, 0x18, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x78, 0x0C, 0x7C, 0xCC, 0x76, 0x00,
            0xE0, 0x60, 0x60, 0x7C, 0x66, 0x66, 0xDC, 0x00,
            0x00, 0x00, 0x78, 0xCC, 0xC0, 0xCC, 0x78, 0x00,
            0x1C, 0x0C, 0x0C, 0x7C, 0xCC, 0xCC, 0x76, 0x00,
            0x00, 0x00, 0x78, 0xCC, 0xFC, 0xC0, 0x78, 0x00,
            0x38, 0x6C, 0x60, 0xF0, 0x60, 0x60, 0xF0, 0x00,
            0x00, 0x00, 0x76, 0xCC, 0xCC, 0x7C, 0x0C, 0xF8,
            0xE0, 0x60, 0x6C, 0x76, 0x66, 0x66, 0xE6, 0x00,
            0x30, 0x00, 0x70, 0x30, 0x30, 0x30, 0x78, 0x00,
            0x0C, 0x00, 0x0C, 0x0C, 0x0C, 0xCC, 0xCC, 0x78,
            0xE0, 0x60, 0x66, 0x6C, 0x78, 0x6C, 0xE6, 0x00,
            0x70, 0x30, 0x30, 0x30, 0x30, 0x30, 0x78, 0x00,
            0x00, 0x00, 0xCC, 0xFE, 0xFE, 0xD6, 0xC6, 0x00,
            0x00, 0x00, 0xF8, 0xCC, 0xCC, 0xCC, 0xCC, 0x00,
            0x00, 0x00, 0x78, 0xCC, 0xCC, 0xCC, 0x78, 0x00,
            0x00, 0x00, 0xDC, 0x66, 0x66, 0x7C, 0x60, 0xF0,
            0x00, 0x00, 0x76, 0xCC, 0xCC, 0x7C, 0x0C, 0x1E,
            0x00, 0x00, 0xDC, 0x76, 0x66, 0x60, 0xF0, 0x00,
            0x00, 0x00, 0x7C, 0xC0, 0x78, 0x0C, 0xF8, 0x00,
            0x10, 0x30, 0x7C, 0x30, 0x30, 0x34, 0x18, 0x00,
            0x00, 0x00, 0xCC, 0xCC, 0xCC, 0xCC, 0x76, 0x00,
            0x00, 0x00, 0xCC, 0xCC, 0xCC, 0x78, 0x30, 0x00,
            0x00, 0x00, 0xC6, 0xD6, 0xFE, 0xFE, 0x6C, 0x00,
            0x00, 0x00, 0xC6, 0x6C, 0x38, 0x6C, 0xC6, 0x00,
            0x00, 0x00, 0xCC, 0xCC, 0xCC, 0x7C, 0x0C, 0xF8,
            0x00, 0x00, 0xFC, 0x98, 0x30, 0x64, 0xFC, 0x00,
            0x1C, 0x30, 0x30, 0xE0, 0x30, 0x30, 0x1C, 0x00,
            0x18, 0x18, 0x18, 0x00, 0x18, 0x18, 0x18, 0x00,
            0xE0, 0x30, 0x30, 0x1C, 0x30, 0x30, 0xE0, 0x00,
            0x76, 0xDC, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x10, 0x38, 0x6C, 0xC6, 0xC6, 0xFE, 0x00,
            0x78, 0xCC, 0xC0, 0xCC, 0x78, 0x18, 0x0C, 0x78,
            0x00, 0xCC, 0x00, 0xCC, 0xCC, 0xCC, 0x7E, 0x00,
            0x1C, 0x00, 0x78, 0xCC, 0xFC, 0xC0, 0x78, 0x00,
            0x7E, 0xC3, 0x3C, 0x06, 0x3E, 0x66, 0x3F, 0x00,
            0xCC, 0x00, 0x78, 0x0C, 0x7C, 0xCC, 0x7E, 0x00,
            0xE0, 0x00, 0x78, 0x0C, 0x7C, 0xCC, 0x7E, 0x00,
            0x30, 0x30, 0x78, 0x0C, 0x7C, 0xCC, 0x7E, 0x00,
            0x00, 0x00, 0x78, 0xC0, 0xC0, 0x78, 0x0C, 0x38,
            0x7E, 0xC3, 0x3C, 0x66, 0x7E, 0x60, 0x3C, 0x00,
            0xCC, 0x00, 0x78, 0xCC, 0xFC, 0xC0, 0x78, 0x00,
            0xE0, 0x00, 0x78, 0xCC, 0xFC, 0xC0, 0x78, 0x00,
            0xCC, 0x00, 0x70, 0x30, 0x30, 0x30, 0x78, 0x00,
            0x7C, 0xC6, 0x38, 0x18, 0x18, 0x18, 0x3C, 0x00,
            0xE0, 0x00, 0x70, 0x30, 0x30, 0x30, 0x78, 0x00,
            0xC6, 0x38, 0x6C, 0xC6, 0xFE, 0xC6, 0xC6, 0x00,
            0x30, 0x30, 0x00, 0x78, 0xCC, 0xFC, 0xCC, 0x00,
            0x1C, 0x00, 0xFC, 0x60, 0x78, 0x60, 0xFC, 0x00,
            0x00, 0x00, 0x7F, 0x0C, 0x7F, 0xCC, 0x7F, 0x00,
            0x3E, 0x6C, 0xCC, 0xFE, 0xCC, 0xCC, 0xCE, 0x00,
            0x78, 0xCC, 0x00, 0x78, 0xCC, 0xCC, 0x78, 0x00,
            0x00, 0xCC, 0x00, 0x78, 0xCC, 0xCC, 0x78, 0x00,
            0x00, 0xE0, 0x00, 0x78, 0xCC, 0xCC, 0x78, 0x00,
            0x78, 0xCC, 0x00, 0xCC, 0xCC, 0xCC, 0x7E, 0x00,
            0x00, 0xE0, 0x00, 0xCC, 0xCC, 0xCC, 0x7E, 0x00,
            0x00, 0xCC, 0x00, 0xCC, 0xCC, 0x7C, 0x0C, 0xF8,
            0xC3, 0x18, 0x3C, 0x66, 0x66, 0x3C, 0x18, 0x00,
            0xCC, 0x00, 0xCC, 0xCC, 0xCC, 0xCC, 0x78, 0x00,
            0x18, 0x18, 0x7E, 0xC0, 0xC0, 0x7E, 0x18, 0x18,
            0x38, 0x6C, 0x64, 0xF0, 0x60, 0xE6, 0xFC, 0x00,
            0xCC, 0xCC, 0x78, 0xFC, 0x30, 0xFC, 0x30, 0x30,
            0xF8, 0xCC, 0xCC, 0xFA, 0xC6, 0xCF, 0xC6, 0xC7,
            0x0E, 0x1B, 0x18, 0x3C, 0x18, 0x18, 0xD8, 0x70,
            0x1C, 0x00, 0x78, 0x0C, 0x7C, 0xCC, 0x7E, 0x00,
            0x38, 0x00, 0x70, 0x30, 0x30, 0x30, 0x78, 0x00,
            0x00, 0x1C, 0x00, 0x78, 0xCC, 0xCC, 0x78, 0x00,
            0x00, 0x1C, 0x00, 0xCC, 0xCC, 0xCC, 0x7E, 0x00,
            0x00, 0xF8, 0x00, 0xF8, 0xCC, 0xCC, 0xCC, 0x00,
            0xFC, 0x00, 0xCC, 0xEC, 0xFC, 0xDC, 0xCC, 0x00,
            0x3C, 0x6C, 0x6C, 0x3E, 0x00, 0x7E, 0x00, 0x00,
            0x38, 0x6C, 0x6C, 0x38, 0x00, 0x7C, 0x00, 0x00,
            0x30, 0x00, 0x30, 0x60, 0xC0, 0xCC, 0x78, 0x00,
            0x00, 0x00, 0x00, 0xFC, 0xC0, 0xC0, 0x00, 0x00,
            0x00, 0x00, 0x00, 0xFC, 0x0C, 0x0C, 0x00, 0x00,
            0xC3, 0xC6, 0xCC, 0xDE, 0x33, 0x66, 0xCC, 0x0F,
            0xC3, 0xC6, 0xCC, 0xDB, 0x37, 0x6F, 0xCF, 0x03,
            0x18, 0x18, 0x00, 0x18, 0x18, 0x18, 0x18, 0x00,
            0x00, 0x33, 0x66, 0xCC, 0x66, 0x33, 0x00, 0x00,
            0x00, 0xCC, 0x66, 0x33, 0x66, 0xCC, 0x00, 0x00,
            0x22, 0x88, 0x22, 0x88, 0x22, 0x88, 0x22, 0x88,
            0x55, 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55, 0xAA,
            0xDB, 0x77, 0xDB, 0xEE, 0xDB, 0x77, 0xDB, 0xEE,
            0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18,
            0x18, 0x18, 0x18, 0x18, 0xF8, 0x18, 0x18, 0x18,
            0x18, 0x18, 0xF8, 0x18, 0xF8, 0x18, 0x18, 0x18,
            0x36, 0x36, 0x36, 0x36, 0xF6, 0x36, 0x36, 0x36,
            0x00, 0x00, 0x00, 0x00, 0xFE, 0x36, 0x36, 0x36,
            0x00, 0x00, 0xF8, 0x18, 0xF8, 0x18, 0x18, 0x18,
            0x36, 0x36, 0xF6, 0x06, 0xF6, 0x36, 0x36, 0x36,
            0x36, 0x36, 0x36, 0x36, 0x36, 0x36, 0x36, 0x36,
            0x00, 0x00, 0xFE, 0x06, 0xF6, 0x36, 0x36, 0x36,
            0x36, 0x36, 0xF6, 0x06, 0xFE, 0x00, 0x00, 0x00,
            0x36, 0x36, 0x36, 0x36, 0xFE, 0x00, 0x00, 0x00,
            0x18, 0x18, 0xF8, 0x18, 0xF8, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0xF8, 0x18, 0x18, 0x18,
            0x18, 0x18, 0x18, 0x18, 0x1F, 0x00, 0x00, 0x00,
            0x18, 0x18, 0x18, 0x18, 0xFF, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0xFF, 0x18, 0x18, 0x18,
            0x18, 0x18, 0x18, 0x18, 0x1F, 0x18, 0x18, 0x18,
            0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00,
            0x18, 0x18, 0x18, 0x18, 0xFF, 0x18, 0x18, 0x18,
            0x18, 0x18, 0x1F, 0x18, 0x1F, 0x18, 0x18, 0x18,
            0x36, 0x36, 0x36, 0x36, 0x37, 0x36, 0x36, 0x36,
            0x36, 0x36, 0x37, 0x30, 0x3F, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x3F, 0x30, 0x37, 0x36, 0x36, 0x36,
            0x36, 0x36, 0xF7, 0x00, 0xFF, 0x00, 0x00, 0x00,
            0x00, 0x00, 0xFF, 0x00, 0xF7, 0x36, 0x36, 0x36,
            0x36, 0x36, 0x37, 0x30, 0x37, 0x36, 0x36, 0x36,
            0x00, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0x00, 0x00,
            0x36, 0x36, 0xF7, 0x00, 0xF7, 0x36, 0x36, 0x36,
            0x18, 0x18, 0xFF, 0x00, 0xFF, 0x00, 0x00, 0x00,
            0x36, 0x36, 0x36, 0x36, 0xFF, 0x00, 0x00, 0x00,
            0x00, 0x00, 0xFF, 0x00, 0xFF, 0x18, 0x18, 0x18,
            0x00, 0x00, 0x00, 0x00, 0xFF, 0x36, 0x36, 0x36,
            0x36, 0x36, 0x36, 0x36, 0x3F, 0x00, 0x00, 0x00,
            0x18, 0x18, 0x1F, 0x18, 0x1F, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x1F, 0x18, 0x1F, 0x18, 0x18, 0x18,
            0x00, 0x00, 0x00, 0x00, 0x3F, 0x36, 0x36, 0x36,
            0x36, 0x36, 0x36, 0x36, 0xFF, 0x36, 0x36, 0x36,
            0x18, 0x18, 0xFF, 0x18, 0xFF, 0x18, 0x18, 0x18,
            0x18, 0x18, 0x18, 0x18, 0xF8, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x1F, 0x18, 0x18, 0x18,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF,
            0xF0, 0xF0, 0xF0, 0xF0, 0xF0, 0xF0, 0xF0, 0xF0,
            0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F,
            0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x76, 0xDC, 0xC8, 0xDC, 0x76, 0x00,
            0x00, 0x78, 0xCC, 0xF8, 0xCC, 0xF8, 0xC0, 0xC0,
            0x00, 0xFC, 0xCC, 0xC0, 0xC0, 0xC0, 0xC0, 0x00,
            0x00, 0xFE, 0x6C, 0x6C, 0x6C, 0x6C, 0x6C, 0x00,
            0xFC, 0xCC, 0x60, 0x30, 0x60, 0xCC, 0xFC, 0x00,
            0x00, 0x00, 0x7E, 0xD8, 0xD8, 0xD8, 0x70, 0x00,
            0x00, 0x66, 0x66, 0x66, 0x66, 0x7C, 0x60, 0xC0,
            0x00, 0x76, 0xDC, 0x18, 0x18, 0x18, 0x18, 0x00,
            0xFC, 0x30, 0x78, 0xCC, 0xCC, 0x78, 0x30, 0xFC,
            0x38, 0x6C, 0xC6, 0xFE, 0xC6, 0x6C, 0x38, 0x00,
            0x38, 0x6C, 0xC6, 0xC6, 0x6C, 0x6C, 0xEE, 0x00,
            0x1C, 0x30, 0x18, 0x7C, 0xCC, 0xCC, 0x78, 0x00,
            0x00, 0x00, 0x7E, 0xDB, 0xDB, 0x7E, 0x00, 0x00,
            0x06, 0x0C, 0x7E, 0xDB, 0xDB, 0x7E, 0x60, 0xC0,
            0x38, 0x60, 0xC0, 0xF8, 0xC0, 0x60, 0x38, 0x00,
            0x78, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0x00,
            0x00, 0xFC, 0x00, 0xFC, 0x00, 0xFC, 0x00, 0x00,
            0x30, 0x30, 0xFC, 0x30, 0x30, 0x00, 0xFC, 0x00,
            0x60, 0x30, 0x18, 0x30, 0x60, 0x00, 0xFC, 0x00,
            0x18, 0x30, 0x60, 0x30, 0x18, 0x00, 0xFC, 0x00,
            0x0E, 0x1B, 0x1B, 0x18, 0x18, 0x18, 0x18, 0x18,
            0x18, 0x18, 0x18, 0x18, 0x18, 0xD8, 0xD8, 0x70,
            0x30, 0x30, 0x00, 0xFC, 0x00, 0x30, 0x30, 0x00,
            0x00, 0x76, 0xDC, 0x00, 0x76, 0xDC, 0x00, 0x00,
            0x38, 0x6C, 0x6C, 0x38, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x18, 0x18, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x18, 0x00, 0x00, 0x00,
            0x0F, 0x0C, 0x0C, 0x0C, 0xEC, 0x6C, 0x3C, 0x1C,
            0x78, 0x6C, 0x6C, 0x6C, 0x6C, 0x00, 0x00, 0x00,
            0x70, 0x18, 0x30, 0x60, 0x78, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x3C, 0x3C, 0x3C, 0x3C, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };
    }
}
