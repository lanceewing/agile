using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static AGILE.ScriptBuffer;
using static AGILE.TextGraphics;

namespace AGILE
{
    /// <summary>
    /// A class or saving and restoring saved games.
    /// </summary>
    class SavedGames
    {
        private const int SAVENAME_LEN = 30;
        private const int NUM_GAMES = 12;
        private const int GAME_INDENT = 3;
        private const char POINTER_CHAR = (char)26;
        private const char ERASE_CHAR = (char)32;

        // Keeps track of whether it is the first time a save/restore is happening in simple mode.
        private bool FirstTime = true;

        // Messages for the various window dialogs that are shown as part of the Save / Restore functionality.
        private string simpleFirstMsg = "Use the arrow keys to move\n     the pointer to your name.\nThen press ENTER\n";
        private string simpleSelectMsg = "   Sorry, this disk is full.\nPosition pointer and press ENTER\n    to overwrite a saved game\nor press ESC and try again \n    with another disk\n";
        private string selectSaveMsg = "Use the arrow keys to select the slot in which you wish to save the game. Press ENTER to save in the slot, ESC to not save a game.";
        private string selectRestoreMsg = "Use the arrow keys to select the game which you wish to restore. Press ENTER to restore the game, ESC to not restore a game.";
        private string newDescriptMsg = "How would you like to describe this saved game?\n\n";
        private string noGamesMsg = "There are no games to\nrestore in\n\n{0}\n\nPress ENTER to continue.";

        // Data type for storing data about a single saved game file.
        class SavedGame
        {
            public int Num;
            public bool Exists;
            public string FileName;
            public long FileTime;
            public string Description;
            public byte[] SavedGameData;
        }

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
        /// Provides methods for drawing text on to the AGI screen.
        /// </summary>
        private TextGraphics textGraphics;

        /// <summary>
        /// The pixels array for the AGI screen on which the background Picture and 
        /// AnimatedObjects will be drawn to.
        /// </summary>
        private int[] pixels;

        /// <summary>
        /// Construtor for SavedGames.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="userInput"></param>
        /// <param name="textGraphics"></param>
        /// <param name="pixels"></param>
        public SavedGames(GameState state, UserInput userInput, TextGraphics textGraphics, int[] pixels)
        {
            this.state = state;
            this.userInput = userInput;
            this.textGraphics = textGraphics;
            this.pixels = pixels;
        }

        /// <summary>
        /// Chooses a saved game to either save to or restore from. The choice is either automatic
        /// such as in the case of simple save, or by the user.
        /// </summary>
        /// <param name="function">'s' for save, 'r' for restore</param>
        /// <returns></returns>
        private SavedGame ChooseGame(char function)
        {
            SavedGame[] game = new SavedGame[NUM_GAMES];
            int gameNum, numGames, mostRecentGame = 0;
            long mostRecentTime = 0;
            bool simpleSave = (state.SimpleName.Length > 0);

            // Create saved game directory for this game if it doesn't yet exist.
            Directory.CreateDirectory(GetSavePath());

            // Look for the game files and get their data and meta data.
            if (function == 's')
            {
                // We're saving a game.
                for (gameNum = 0; gameNum < NUM_GAMES; gameNum++)
                {
                    game[gameNum] = GetGameByNumber(gameNum + 1);

                    if (game[gameNum].Exists && (game[gameNum].FileTime > mostRecentTime))
                    {
                        mostRecentTime = game[gameNum].FileTime;
                        mostRecentGame = gameNum;
                    }
                }

                numGames = NUM_GAMES;
            }
            else
            {
                // We're restoring a game.
                for (gameNum = numGames = 0; gameNum < NUM_GAMES; gameNum++)
                {
                    game[numGames] = GetGameByNumber(gameNum + 1);
                    
                    if (game[numGames].Exists)
                    {
                        if (game[numGames].FileTime > mostRecentTime)
                        {
                            mostRecentTime = game[numGames].FileTime;
                            mostRecentGame = numGames;
                        }

                        // Count how many saved games we currently have.
                        numGames++;
                    }
                }

                if (numGames == 0)
                {
                    if (!simpleSave)
                    {
                        // For normal save, if there are no games to display, tell the user so.
                        textGraphics.WindowPrint(String.Format(noGamesMsg, GetSavePath().Replace("\\", "\\\\")));
                    }

                    // If there are no games to restore, exit at this point.
                    return null;
                }
            }

            if (simpleSave && !FirstTime)
            {
                // See if we have a slot for the current simple name value.
                for (gameNum = 0; gameNum < NUM_GAMES; gameNum++)
                {
                    if (game[gameNum].Description.Equals(state.SimpleName))
                    {
                        return (game[gameNum]);
                    }
                }

                if (function == 's')
                {
                    // For simple save, we automatically find an empty slot for new saved game.
                    for (gameNum = 0; gameNum < NUM_GAMES; gameNum++)
                    {
                        if ((game[gameNum].Description == null) || (game[gameNum].Description.Equals("")))
                        {
                            // Description is automatically set to the SimpleName value if it is set.
                            game[gameNum].Description = state.SimpleName;
                            return (game[gameNum]);
                        }
                    }
                }

                // If none available, fall thru to window.

                // We shouldn't be able to get to this point in restore mode, but just in case, return null.
                if (function == 'r') return null;
            }

            // Compute the height of the window desired and put it up
            int descriptTop = 5;
            int height = numGames + descriptTop;
            TextWindow textWin = textGraphics.WindowNoWait(simpleSave ? (FirstTime ? simpleFirstMsg : simpleSelectMsg) :
                (function == 's') ? selectSaveMsg : selectRestoreMsg,
                height, SAVENAME_LEN + GAME_INDENT + 1, true);

            descriptTop += textWin.Top;
            FirstTime = false;

            // Print the game descriptions within the open window..
            for (gameNum = 0; gameNum < numGames; gameNum++)
            {
                textGraphics.DrawString(this.pixels, String.Format(" - {0}", game[gameNum].Description),
                    textWin.Left * 8, (descriptTop + gameNum) * 8, 0, 15);
            }

            // Put up the pointer, defaulting to most recently saved game, and then let the user start 
            // scrolling around with it to make a choice.
            gameNum = mostRecentGame;
            WritePointer(textWin.Left, descriptTop + gameNum);

            while (true)
            {
                switch (userInput.WaitForKey())
                {
                    case (int)Keys.Enter:
                        if (simpleSave && (function == 'r'))
                        {
                            // If this is a restore in simple save mode, it must be the first one, in which
                            // case we remember the selection in the SimpleName var so that it automatically 
                            // restores next time the user restores.
                            state.SimpleName = game[gameNum].Description;
                        }
                        if (!simpleSave && (function == 's'))
                        {
                            // If this is a save in normal save mode, then we ask the user to confirm/enter 
                            // the description for the save game.
                            if ((game[gameNum].Description = GetWindowStr(newDescriptMsg, game[gameNum].Description)) == null)
                            {
                                // If they have pressed ESC, we return null to indicate not to continue.
                                return null;
                            }
                        }
                        textGraphics.CloseWindow();
                        return (game[gameNum]);

                    case (int)Keys.Escape:
                        textGraphics.CloseWindow();
                        return null;

                    case (int)Keys.Up:
                        ErasePointer(textWin.Left, descriptTop + gameNum);
                        gameNum = (gameNum == 0) ? numGames - 1 : gameNum - 1;
                        WritePointer(textWin.Left, descriptTop + gameNum);
                        break;

                    case (int)Keys.Down:
                        ErasePointer(textWin.Left, descriptTop + gameNum);
                        gameNum = (gameNum == numGames - 1) ? 0 : gameNum + 1;
                        WritePointer(textWin.Left, descriptTop + gameNum);
                        break;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        private SavedGame GetGameByNumber(int num)
        {
            SavedGame theGame = new SavedGame();
            theGame.Num = num;

            // Build full path to the saved game of this number for this game ID.
            theGame.FileName = String.Format("{0}\\{1}SG.{2}", GetSavePath(), state.GameId, num);

            try
            {
                FileStream savedGameFile = File.Open(theGame.FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                theGame.SavedGameData = new byte[savedGameFile.Length];

                // Read entire saved game file in.
                int bytesRead = savedGameFile.Read(theGame.SavedGameData, 0, theGame.SavedGameData.Length);
                if (bytesRead != savedGameFile.Length)
                {
                    savedGameFile.Close();
                    theGame.Description = "";
                    theGame.Exists = false;
                    return theGame;
                }
                savedGameFile.Close();
            }
            catch (FileNotFoundException)
            {
                // There is no saved game file of this name, so return false.
                theGame.Description = "";
                theGame.Exists = false;
                return theGame;
            }
            catch (Exception)
            {
                // Something unexpected happened. Bad file I guess. Return false.
                theGame.Description = "";
                theGame.Exists = false;
                return theGame;
            }

            // Get last modified time as an epoch time, i.e. seconds since start of
            // 1970 (which I guess must have been when the big bang was).
            DateTime dt = File.GetLastWriteTimeUtc(theGame.FileName);
            theGame.FileTime = (int)(dt.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            // 0 - 30(31 bytes) SAVED GAME DESCRIPTION.
            int textEnd = 0;
            while (theGame.SavedGameData[textEnd] != 0) textEnd++;
            string savedGameDescription = System.Text.Encoding.ASCII.GetString(theGame.SavedGameData, 0, textEnd);

            // 33 - 39(7 bytes) Game ID("SQ2", "KQ3", "LLLLL", etc.), NUL padded.
            textEnd = 33;
            while ((theGame.SavedGameData[textEnd] != 0) && ((textEnd - 33) < 7)) textEnd++;
            string gameId = System.Text.Encoding.ASCII.GetString(theGame.SavedGameData, 33, textEnd - 33);

            // If the saved Game ID  doesn't match the current, don't use  this game.
            if (!gameId.Equals(state.GameId))
            {
                theGame.Description = "";
                theGame.Exists = false;
                return theGame;
            }

            // If we get this far, there is a valid saved game with this number for this game.
            theGame.Description = savedGameDescription;
            theGame.Exists = true;
            return theGame;
		}

        /// <summary>
        /// Displays the pointer character at the specified screen position.
        /// </summary>
        /// <param name="col"></param>
        /// <param name="row"></param>
        private void WritePointer(int col, int row)
        {
            textGraphics.DrawChar(this.pixels, (byte)POINTER_CHAR, col * 8, row * 8, 0, 15);
        }

        /// <summary>
        /// Erases the pointer character from the specified screen position.
        /// </summary>
        /// <param name="col"></param>
        /// <param name="row"></param>
        private void ErasePointer(int col, int row)
        {
            textGraphics.DrawChar(this.pixels, (byte)ERASE_CHAR, col * 8, row * 8, 0, 15);
        }

        /// <summary>
        /// Gets a string from the user by opening a window dialog.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="str"></param>
        /// <returns>The entered text.</returns>
        private string GetWindowStr(string msg, string str = "")
        {
            // Open a new window with the message text displayed. 
            TextWindow textWin = textGraphics.WindowNoWait(msg, 0, SAVENAME_LEN+1, true);

            // Clear the input row to black on top of the window.
            textGraphics.ClearRect(textWin.Bottom, textWin.Left, textWin.Bottom, textWin.Right - 1, 0);

            // Get the line of text from the user.
            string line = textGraphics.GetLine(SAVENAME_LEN, (byte)textWin.Bottom, (byte)textWin.Left, str, 15, 0);

            textGraphics.CloseWindow();

	        return line;
        }

        /// <summary>
        /// Gets the full path of the folder to use for reading and writing saved games.
        /// </summary>
        /// <returns></returns>
        private string GetSavePath()
        {
            return Path.Combine(Vista.GetKnownFolderPath(Vista.SavedGames) ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), state.GameId);
        }

        /// <summary>
        /// Saves the GameState of the Interpreter to a saved game file.
        /// </summary>
        public void SaveGameState()
        {
            bool simpleSave = (state.SimpleName.Length > 0);
            SavedGame savedGame = null;

            // Get the saved game file to save.
            if ((savedGame = ChooseGame('s')) == null) return;

            // If it is Simple Save mode then we skip asking them if they want to save.
            if (!simpleSave)
            {
                // Otherwise we prompt the user to confirm.
                string msg = String.Format(
                    "About to save the game\ndescribed as:\n\n{0}\n\nin file:\n{1}\n\n{2}",
                    savedGame.Description, savedGame.FileName.Replace("\\", "\\\\"),
                    "Press ENTER to continue.\nPress ESC to cancel.");
                textGraphics.WindowNoWait(msg, 0, 35, false);
                bool abort = (userInput.WaitAcceptAbort() == UserInput.ABORT);
                textGraphics.CloseWindow();
                if (abort) return;
            }

            // No saved game will ever be as big as 20000, but we put that as a theoretical lid
            // on the size based on rough calculations with all parts set to maximum size. We'll
            // only write the bytes that use when created the file.
            byte[] savedGameData = new byte[20000];
            int pos = 0;

            // 0 - 30(31 bytes) SAVED GAME DESCRIPTION.
            foreach (byte b in Encoding.ASCII.GetBytes(savedGame.Description)) savedGameData[pos++] = b;

            // FIRST PIECE: SAVE VARIABLES
            // [0] 31 - 32(2 bytes) Length of save variables piece. Length depends on AGI interpreter version. We use 0xE1 0x05
            int saveVarsLength = 0x05E1;
            int aniObjsOffset = 33 + saveVarsLength;
            savedGameData[31] = (byte)(saveVarsLength & 0xFF);
            savedGameData[32] = (byte)((saveVarsLength >> 8) & 0xFF);

            // [2] 33 - 39(7 bytes) Game ID("SQ2", "KQ3", "LLLLL", etc.), NUL padded.
            pos = 33;
            foreach (byte b in Encoding.ASCII.GetBytes(state.GameId)) savedGameData[pos++] = b;

            // [9] 40 - 295(256 bytes) Variables, 1 variable per byte
            for (int i = 0; i < 256; i++) savedGameData[40 + i] = state.Vars[i];

            // [265] 296 - 327(32 bytes) Flags, 8 flags per byte
            pos = 296;
            for (int i = 0; i < 256; i+=8)
            {
                savedGameData[pos++] = (byte)(
                    (state.Flags[i + 0] ? 0x80 : 0x00) | (state.Flags[i + 1] ? 0x40 : 0x00) |
                    (state.Flags[i + 2] ? 0x20 : 0x00) | (state.Flags[i + 3] ? 0x10 : 0x00) | 
                    (state.Flags[i + 4] ? 0x08 : 0x00) | (state.Flags[i + 5] ? 0x04 : 0x00) | 
                    (state.Flags[i + 6] ? 0x02 : 0x00) | (state.Flags[i + 7] ? 0x01 : 0x00));
            }

            // [297] 328 - 331(4 bytes) Clock ticks since game started. 1 clock tick == 50ms.
            int saveGameTicks = (int)(state.TotalTicks / 3);
            savedGameData[328] = (byte)(saveGameTicks & 0xFF);
            savedGameData[329] = (byte)((saveGameTicks >> 8) & 0xFF);
            savedGameData[330] = (byte)((saveGameTicks >> 16) & 0xFF);
            savedGameData[331] = (byte)((saveGameTicks >> 24) & 0xFF);

            // [301] 332 - 333(2 bytes) Horizon
            savedGameData[332] = (byte)(state.Horizon & 0xFF);
            savedGameData[333] = (byte)((state.Horizon >> 8) & 0xFF);

            // [303] 334 - 335(2 bytes) Key Dir
            // TODO: Not entirely sure what this is for, so not currently saving this.

            // Currently active block.
            // [305] 336 - 337(2 bytes) Upper left X position for active block.
            savedGameData[336] = (byte)(state.BlockUpperLeftX & 0xFF);
            savedGameData[337] = (byte)((state.BlockUpperLeftX >> 8) & 0xFF);
            // [307] 338 - 339(2 bytes) Upper Left Y position for active block.
            savedGameData[338] = (byte)(state.BlockUpperLeftY & 0xFF);
            savedGameData[339] = (byte)((state.BlockUpperLeftY >> 8) & 0xFF);
            // [309] 340 - 341(2 bytes) Lower Right X position for active block.
            savedGameData[340] = (byte)(state.BlockLowerRightX & 0xFF);
            savedGameData[341] = (byte)((state.BlockLowerRightX >> 8) & 0xFF);
            // [311] 342 - 343(2 bytes) Lower Right Y position for active block.
            savedGameData[342] = (byte)(state.BlockLowerRightY & 0xFF);
            savedGameData[343] = (byte)((state.BlockLowerRightY >> 8) & 0xFF);

            // [313] 344 - 345(2 bytes) Player control (1) / Program control (0)
            savedGameData[344] = (byte)(state.UserControl ? 1 : 0);
            // [315] 346 - 347(2 bytes) Current PICTURE number
            savedGameData[346] = (byte)state.CurrentPicture.Index;
            // [317] 348 - 349(2 bytes) Blocking flag (1 = true, 0 = false)
            savedGameData[348] = (byte)(state.Blocking ? 1 : 0);

            // [319] 350 - 351(2 bytes) Max drawn. Always set to 15. Maximum number of animated objects that can be drawn at a time. Set by old max.drawn command in AGI v2.001.
            savedGameData[350] = (byte)state.MaxDrawn;
            // [321] 352 - 353(2 bytes) Script size. Set by script.size. Max number of script event items. Default is 50.
            savedGameData[352] = (byte)state.ScriptBuffer.ScriptSize;
            // [323] 354 - 355(2 bytes) Current number of script event entries.
            savedGameData[354] = (byte)state.ScriptBuffer.ScriptEntries;

            // [325] 356 - 555(200 or 160 bytes) ? Key to controller map (4 bytes each). Earlier versions had less entries.
            pos = 356;
            foreach (var entry in state.KeyToControllerMap)
            {
                if (entry.Key != 0)
                {
                    int keyCode = userInput.ReverseKeyCodeMap[entry.Key];
                    int controllerNum = entry.Value;
                    savedGameData[pos++] = (byte)(keyCode & 0xFF);
                    savedGameData[pos++] = (byte)((keyCode >> 8) & 0xFF);
                    savedGameData[pos++] = (byte)(controllerNum & 0xFF);
                    savedGameData[pos++] = (byte)((controllerNum >> 8) & 0xFF);
                }
            }

            // [525] 556 - 1515(480 or 960 bytes) 12 or 24 strings, each 40 bytes long. For 2.4XX to 2.9XX, it was 24 strings.
            for (int i = 0; i < Defines.NUMSTRINGS; i++)
            {
                pos = 556 + (i * Defines.STRLENGTH);
                if ((state.Strings[i] != null) && (state.Strings[i].Length > 0)) {
                    foreach (byte b in Encoding.ASCII.GetBytes(state.Strings[i])) savedGameData[pos++] = b;
                }
            }

            // [1485] 1516(2 bytes) Foreground colour
            savedGameData[1516] = (byte)state.ForegroundColour;

            // TODO: Need to fix the foreground and background colour storage.

            // [1487] 1518(2 bytes) Background colour
            //int backgroundColour = (savedGameData[postStringsOffset + 2] + (savedGameData[postStringsOffset + 3] << 8));
            // TODO: Interpreter doesn't yet properly handle AGI background colour.

            // [1489] 1520(2 bytes) Text Attribute value (combined foreground/background value)
            //int textAttribute = (savedGameData[postStringsOffset + 4] + (savedGameData[postStringsOffset + 5] << 8));

            // [1491] 1522(2 bytes) Accept input = 1, Prevent input = 0
            savedGameData[1522] = (byte)(state.AcceptInput ? 1 : 0);

            // [1493] 1524(2 bytes) User input row on the screen
            savedGameData[1524] = (byte)state.InputLineRow;

            // [1495] 1526(2 bytes) Cursor character
            savedGameData[1526] = (byte)state.CursorCharacter;

            // [1497] 1528(2 bytes) Show status line = 1, Don't show status line = 0
            savedGameData[1528] = (byte)(state.ShowStatusLine ? 1 : 0);

            // [1499] 1530(2 bytes) Status line row on the screen
            savedGameData[1530] = (byte)state.StatusLineRow;

            // [1501] 1532(2 bytes) Picture top row on the screen
            savedGameData[1532] = (byte)state.PictureRow;

            // [1503] 1534(2 bytes) Picture bottom row on the screen
            savedGameData[1534] = (byte)(state.PictureRow + 21);

            // [1505] 1536(2 bytes) Stores a pushed position within the script event list
            // Note: Depends on interpreter version. 2.4xx and below didn't have push.script/pop.script, so they didn't have this saved game field.
            savedGameData[1536] = (byte)(state.ScriptBuffer.SavedScript);

            // SECOND PIECE: ANIMATED OBJECT STATE
            // 1538 - 1539(2 bytes) Length of piece
            // Each ANIOBJ entry is 0x2B in length, i.e. 43 bytes.
            int aniObjectsLength = ((state.Objects.NumOfAnimatedObjects + 1) * 0x2B);
            savedGameData[aniObjsOffset + 0] = (byte)(aniObjectsLength & 0xFF);
            savedGameData[aniObjsOffset + 1] = (byte)((aniObjectsLength >> 8) & 0xFF);
            
            for (int i=0; i < (state.Objects.NumOfAnimatedObjects + 1); i++)
            {
                int aniObjOffset = aniObjsOffset + 2 + (i * 0x2B);
                AnimatedObject aniObj = state.AnimatedObjects[i];

                //UBYTE movefreq;     /* number of animation cycles between motion  */    e.g.   01
                savedGameData[aniObjOffset + 0] = aniObj.StepTime;
                //UBYTE moveclk;      /* number of cycles between moves of object   */    e.g.   01
                savedGameData[aniObjOffset + 1] = aniObj.StepTimeCount;
                //UBYTE num;          /* object number                              */    e.g.   00
                savedGameData[aniObjOffset + 2] = aniObj.ObjectNumber;
                //COORD x;            /* current x coordinate                       */    e.g.   6e 00 (0x006e = )
                savedGameData[aniObjOffset + 3] = (byte)(aniObj.X & 0xFF);
                savedGameData[aniObjOffset + 4] = (byte)((aniObj.X >> 8) & 0xFF);
                //COORD y;            /* current y coordinate                       */    e.g.   64 00 (0x0064 = )
                savedGameData[aniObjOffset + 5] = (byte)(aniObj.Y & 0xFF);
                savedGameData[aniObjOffset + 6] = (byte)((aniObj.Y >> 8) & 0xFF);
                //UBYTE view;         /* current view number                        */    e.g.   00
                savedGameData[aniObjOffset + 7] = aniObj.CurrentView;
                //VIEW* viewptr;      /* pointer to current view                    */    e.g.   17 6b (0x6b17 = ) IGNORE.
                //UBYTE loop;         /* current loop in view                       */    e.g.   00
                savedGameData[aniObjOffset + 10] = aniObj.CurrentLoop;
                //UBYTE loopcnt;      /* number of loops in view                    */    e.g.   04
                if (aniObj.View != null) savedGameData[aniObjOffset + 11] = aniObj.NumberOfLoops;
                //LOOP* loopptr;      /* pointer to current loop                    */    e.g.   24 6b (0x6b24 = ) IGNORE
                //UBYTE cel;          /* current cell in loop                       */    e.g.   00
                savedGameData[aniObjOffset + 14] = aniObj.CurrentCel;
                //UBYTE celcnt;       /* number of cells in current loop            */    e.g.   06
                if (aniObj.View != null) savedGameData[aniObjOffset + 15] = aniObj.NumberOfCels;
                //CEL* celptr;        /* pointer to current cell                    */    e.g.   31 6b (0x6b31 = ) IGNORE
                //CEL* prevcel;       /* pointer to previous cell                   */    e.g.   31 6b (0x6b31 = ) IGNORE
                //STRPTR save;        /* pointer to background save area            */    e.g.   2f 9c (0x9c2f = ) IGNORE
                //COORD prevx;        /* previous x coordinate                      */    e.g.   6e 00 (0x006e = )
                savedGameData[aniObjOffset + 22] = (byte)(aniObj.PrevX & 0xFF);
                savedGameData[aniObjOffset + 23] = (byte)((aniObj.PrevX >> 8) & 0xFF);
                //COORD prevy;        /* previous y coordinate                      */    e.g.   64 00 (0x0064 = )
                savedGameData[aniObjOffset + 24] = (byte)(aniObj.PrevY & 0xFF);
                savedGameData[aniObjOffset + 25] = (byte)((aniObj.PrevY >> 8) & 0xFF);
                //COORD xsize;        /* x dimension of current cell                */    e.g.   06 00 (0x0006 = )
                if (aniObj.View != null) savedGameData[aniObjOffset + 26] = (byte)(aniObj.XSize & 0xFF);
                if (aniObj.View != null) savedGameData[aniObjOffset + 27] = (byte)((aniObj.XSize >> 8) & 0xFF);
                //COORD ysize;        /* y dimension of current cell                */    e.g.   20 00 (0x0020 = )
                if (aniObj.View != null) savedGameData[aniObjOffset + 28] = (byte)(aniObj.YSize & 0xFF);
                if (aniObj.View != null) savedGameData[aniObjOffset + 29] = (byte)((aniObj.YSize >> 8) & 0xFF);
                //UBYTE stepsize;     /* distance object can move                   */    e.g.   01
                savedGameData[aniObjOffset + 30] = aniObj.StepSize;
                //UBYTE cyclfreq;     /* time interval between cells of object      */    e.g.   01
                savedGameData[aniObjOffset + 31] = aniObj.CycleTime;
                //UBYTE cycleclk;     /* counter for determining when object cycles */    e.g.   01
                savedGameData[aniObjOffset + 32] = aniObj.CycleTimeCount;
                //UBYTE dir;          /* object direction                           */    e.g.   00
                savedGameData[aniObjOffset + 33] = aniObj.Direction;
                //UBYTE motion;       /* object motion type                         */    e.g.   00
                // #define	WANDER	1		/* random movement */
                // #define	FOLLOW	2		/* follow an object */
                // #define	MOVETO	3		/* move to a given coordinate */
                savedGameData[aniObjOffset + 34] = (byte)aniObj.MotionType;
                //UBYTE cycle;        /* cell cycling type                          */    e.g.   00
                // #define NORMAL	0		/* normal repetative cycling of object */
                // #define ENDLOOP	1		/* animate to end of loop and stop */
                // #define RVRSLOOP	2		/* reverse of ENDLOOP */
                // #define REVERSE	3		/* cycle continually in reverse */
                savedGameData[aniObjOffset + 35] = (byte)aniObj.CycleType;
                //UBYTE pri;          /* priority of object                         */    e.g.   09
                savedGameData[aniObjOffset + 36] = aniObj.Priority;

                //UWORD control;      /* object control flag (bit mapped)           */    e.g.   53 40 (0x4053 = )
                int controlBits =
                    (aniObj.Drawn ? 0x0001 : 0x00) |
                    (aniObj.IgnoreBlocks ? 0x0002 : 0x00) |
                    (aniObj.FixedPriority ? 0x0004 : 0x00) |
                    (aniObj.IgnoreHorizon ? 0x0008 : 0x00) |
                    (aniObj.Update ? 0x0010 : 0x00) |
                    (aniObj.Cycle ? 0x0020 : 0x00) |
                    (aniObj.Animated ? 0x0040 : 0x00) |
                    (aniObj.Blocked ? 0x0080 : 0x00) |
                    (aniObj.StayOnWater ? 0x0100 : 0x00) |
                    (aniObj.IgnoreObjects ? 0x0200 : 0x00) |
                    (aniObj.Repositioned ? 0x0400 : 0x00) |
                    (aniObj.StayOnLand ? 0x0800 : 0x00) |
                    (aniObj.NoAdvance ? 0x1000 : 0x00) |
                    (aniObj.FixedLoop ? 0x2000 : 0x00) |
                    (aniObj.Stopped ? 0x4000 : 0x00);
                savedGameData[aniObjOffset + 37] = (byte)(controlBits & 0xFF);
                savedGameData[aniObjOffset + 38] = (byte)((controlBits >> 8) & 0xFF);

                //UBYTE parms[4];     /* space for various motion parameters        */    e.g.   00 00 00 00
                savedGameData[aniObjOffset + 39] = (byte)aniObj.MotionParam1;
                savedGameData[aniObjOffset + 40] = (byte)aniObj.MotionParam2;
                savedGameData[aniObjOffset + 41] = (byte)aniObj.MotionParam3;
                savedGameData[aniObjOffset + 42] = (byte)aniObj.MotionParam4;
            }

            // THIRD PIECE: OBJECTS
            // Almost an exact copy of the OBJECT file, but with the 3 byte header removed, and room
            // numbers reflecting the current location of each object.
            byte[] objectData = state.Objects.Encode();
            int objectsOffset = aniObjsOffset + 2 + aniObjectsLength;
            int objectsLength = objectData.Length - 3;
            savedGameData[objectsOffset + 0] = (byte)(objectsLength & 0xFF);
            savedGameData[objectsOffset + 1] = (byte)((objectsLength >> 8) & 0xFF);
            pos = objectsOffset + 2;
            for (int i=3; i<objectData.Length; i++)
            {
                savedGameData[pos++] = objectData[i];
            }

            // FOURTH PIECE: SCRIPT BUFFER EVENTS
            // A transcript of events leading to the current state in the current room.
            int scriptsOffset = objectsOffset + 2 + objectsLength;
            byte[] scriptEventData = state.ScriptBuffer.Encode();
            int scriptsLength = scriptEventData.Length;
            savedGameData[scriptsOffset + 0] = (byte)(scriptsLength & 0xFF);
            savedGameData[scriptsOffset + 1] = (byte)((scriptsLength >> 8) & 0xFF);
            pos = scriptsOffset + 2;
            for (int i = 0; i < scriptEventData.Length; i++)
            {
                savedGameData[pos++] = scriptEventData[i];
            }

            // FIFTH PIECE: SCAN OFFSETS
            int scanOffsetsOffset = scriptsOffset + 2 + scriptsLength;
            int loadedLogicCount = 0;
            // There is a scan offset for each loaded logic.
            foreach (ScriptBufferEvent e in state.ScriptBuffer.Events) if (e.type == ScriptBufferEventType.LoadLogic) loadedLogicCount++;
            // The scan offset data contains the offsets for loaded logics plus a 4 byte header, 4 bytes for logic 0, and 4 byte trailer.
            int scanOffsetsLength = (loadedLogicCount * 4) + 12;
            savedGameData[scanOffsetsOffset + 0] = (byte)(scanOffsetsLength & 0xFF);
            savedGameData[scanOffsetsOffset + 1] = (byte)((scanOffsetsLength >> 8) & 0xFF);
            pos = scanOffsetsOffset + 2;
            // The scan offsets start with 00 00 00 00.
            savedGameData[pos++] = 0;
            savedGameData[pos++] = 0;
            savedGameData[pos++] = 0;
            savedGameData[pos++] = 0;
            // And this is then always followed by an entry for Logic 0
            savedGameData[pos++] = 0;
            savedGameData[pos++] = 0;
            savedGameData[pos++] = (byte)(state.ScanStart[0] & 0xFF);
            savedGameData[pos++] = (byte)((state.ScanStart[0] >> 8) & 0xFF);
            // The scan offsets for the rest are stored in the order in which the logics were loaded.
            foreach (ScriptBufferEvent e in state.ScriptBuffer.Events)
            {
                if (e.type == ScriptBufferEventType.LoadLogic) {
                    int logicNum = e.resourceNumber;
                    int scanOffset = state.ScanStart[logicNum];
                    savedGameData[pos++] = (byte)(logicNum & 0xFF);
                    savedGameData[pos++] = (byte)((logicNum >> 8) & 0xFF);
                    savedGameData[pos++] = (byte)(scanOffset & 0xFF);
                    savedGameData[pos++] = (byte)((scanOffset >> 8) & 0xFF);
                }
            }
            // The scan offset section ends with FF FF 00 00.
            savedGameData[pos++] = 0xFF;
            savedGameData[pos++] = 0xFF;
            savedGameData[pos++] = 0;
            savedGameData[pos++] = 0;

            // Write out the saved game data to the file.
            try
            {
                using (FileStream savedGameFile = File.Open(savedGame.FileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                {
                    savedGameFile.Write(savedGameData, 0, pos);
                }
            }
            catch (Exception)
            {
                this.textGraphics.Print("Error in saving game.\nPress ENTER to continue.");
            }
        }

        /// <summary>
        /// Restores the GameState of the Interpreter from a saved game file.
        /// </summary>
        /// <returns>true if a game was restored; otherwise false</returns>
        public bool RestoreGameState()
        {
            bool simpleSave = (state.SimpleName.Length > 0);
            SavedGame savedGame = null;

            // Get the saved game file to restore.
            if ((savedGame = ChooseGame('r')) == null) return false;

            // If it is Simple Save mode then we skip asking them if they want to restore.
            if (!simpleSave)
            {
                // Otherwise we prompt the user to confirm.
                string msg = String.Format(
                    "About to restore the game\ndescribed as:\n\n{0}\n\nfrom file:\n{1}\n\n{2}",
                    savedGame.Description, savedGame.FileName.Replace("\\", "\\\\"), 
                    "Press ENTER to continue.\nPress ESC to cancel.");
                textGraphics.WindowNoWait(msg, 0, 35, false);
                bool abort = (userInput.WaitAcceptAbort() == UserInput.ABORT);
                textGraphics.CloseWindow();
                if (abort) return false;
            }

            byte[] savedGameData = savedGame.SavedGameData;

            // 0 - 30(31 bytes) SAVED GAME DESCRIPTION.
            int textEnd = 0;
            while (savedGameData[textEnd] != 0) textEnd++;
            string savedGameDescription = System.Text.Encoding.ASCII.GetString(savedGameData, 0, textEnd);

            // FIRST PIECE: SAVE VARIABLES
            // [0] 31 - 32(2 bytes) Length of save variables piece. Length depends on AGI interpreter version. [e.g. (0xE1 0x05) for some games, (0xDB 0x03) for some] 
            int saveVarsLength = savedGameData[31] + (savedGameData[32] << 8);
            int aniObjsOffset = 33 + saveVarsLength;

            // [2] 33 - 39(7 bytes) Game ID("SQ2", "KQ3", "LLLLL", etc.), NUL padded.
            textEnd = 33;
            while ((savedGameData[textEnd] != 0) && ((textEnd - 33) < 7)) textEnd++;
            string gameId = System.Text.Encoding.ASCII.GetString(savedGameData, 33, textEnd - 33);
            if (!gameId.Equals(state.GameId)) return false;

            // If we're sure that this saved game file is for this game, then continue.
            state.Init();
            textGraphics.ClearLines(0, 24, 0);

            // [9] 40 - 295(256 bytes) Variables, 1 variable per byte
            for (int i=0; i<256; i++) state.Vars[i] = savedGameData[40 + i];

            // [265] 296 - 327(32 bytes) Flags, 8 flags per byte
            for (int i=0; i<256; i++) state.Flags[i] = (savedGameData[(i >> 3) + 296] & (0x80 >> (i & 0x07))) > 0;

            // [297] 328 - 331(4 bytes) Clock ticks since game started. 1 clock tick == 50ms.
            state.TotalTicks = (savedGameData[328] + (savedGameData[329] << 8) + (savedGameData[330] << 16) + (savedGameData[331] << 24)) * 3;

            // [301] 332 - 333(2 bytes) Horizon
            state.Horizon = (savedGameData[332] + (savedGameData[333] << 8));

            // [303] 334 - 335(2 bytes) Key Dir
            // TODO: Not entirely sure what this is for.
            int keyDir = (savedGameData[334] + (savedGameData[335] << 8));

            // Currently active block.
            // [305] 336 - 337(2 bytes) Upper left X position for active block.
            state.BlockUpperLeftX = (short)(savedGameData[336] + (savedGameData[337] << 8));
            // [307] 338 - 339(2 bytes) Upper Left Y position for active block.
            state.BlockUpperLeftY = (short)(savedGameData[338] + (savedGameData[339] << 8));
            // [309] 340 - 341(2 bytes) Lower Right X position for active block.
            state.BlockLowerRightX = (short)(savedGameData[340] + (savedGameData[341] << 8));
            // [311] 342 - 343(2 bytes) Lower Right Y position for active block.
            state.BlockLowerRightY = (short)(savedGameData[342] + (savedGameData[343] << 8));

            // [313] 344 - 345(2 bytes) Player control (1) / Program control (0)
            state.UserControl = (savedGameData[344] + (savedGameData[345] << 8)) == 1;
            // [315] 346 - 347(2 bytes) Current PICTURE number
            state.CurrentPicture = null; // Will be set via load.pic script entry later on.
            // [317] 348 - 349(2 bytes) Blocking flag (1 = true, 0 = false)
            state.Blocking = (savedGameData[348] + (savedGameData[349] << 8)) == 1;

            // [319] 350 - 351(2 bytes) Max drawn. Always set to 15. Maximum number of animated objects that can be drawn at a time. Set by old max.drawn command in AGI v2.001.
            state.MaxDrawn = (savedGameData[350] + (savedGameData[351] << 8));
            // [321] 352 - 353(2 bytes) Script size. Set by script.size. Max number of script event items. Default is 50.
            state.ScriptBuffer.SetScriptSize(savedGameData[352] + (savedGameData[353] << 8));
            // [323] 354 - 355(2 bytes) Current number of script event entries.
            int scriptEntryCount = (savedGameData[354] + (savedGameData[355] << 8));

            // [325] 356 - 555(200 or 160 bytes) ? Key to controller map (4 bytes each)
            int keyMapSize = (saveVarsLength < 1000 ? 40 : 50);   // TODO: This is a version check hack. Need a better way.
            for (int i = 0; i < keyMapSize; i++)
            {
                int keyMapOffset = i << 2;
                int keyCode = (savedGameData[356 + keyMapOffset] + (savedGameData[357 + keyMapOffset] << 8));
                int controllerNum = (savedGameData[358 + keyMapOffset] + (savedGameData[359 + keyMapOffset] << 8));
                if (!((keyCode == 0) && (controllerNum == 0)) && userInput.KeyCodeMap.ContainsKey(keyCode))
                {
                    int interKeyCode = userInput.KeyCodeMap[keyCode];
                    if (state.KeyToControllerMap.ContainsKey(interKeyCode))
                    {
                        state.KeyToControllerMap.Remove(interKeyCode);
                    }
                    state.KeyToControllerMap.Add(userInput.KeyCodeMap[keyCode], controllerNum);
                }
            }

            // For the saved game formats we support (2.4XX to 2.9XX), the keymap always starts at 356.
            int postKeyMapOffset = 356 + (keyMapSize << 2);

            // [525] 556 - 1515(480 or 960 bytes) 12 or 24 strings, each 40 bytes long
            int numOfStrings = (saveVarsLength < 1000 ? 12 : 24);  // TODO: This is a version check hack. Need a better way.
            for (int i = 0; i < numOfStrings; i++)
            {
                int stringOffset = postKeyMapOffset + (i * Defines.STRLENGTH);
                textEnd = stringOffset;
                while ((savedGameData[textEnd] != 0) && ((textEnd - stringOffset) < Defines.STRLENGTH)) textEnd++;
                state.Strings[i] = System.Text.Encoding.ASCII.GetString(savedGameData, stringOffset, textEnd - stringOffset);
            }

            int postStringsOffset = postKeyMapOffset + (numOfStrings * Defines.STRLENGTH);

            // [1485] 1516(2 bytes) Foreground colour
            state.ForegroundColour = (savedGameData[postStringsOffset + 0] + (savedGameData[postStringsOffset + 1] << 8));

            // [1487] 1518(2 bytes) Background colour
            int backgroundColour = (savedGameData[postStringsOffset + 2] + (savedGameData[postStringsOffset + 3] << 8));
            // TODO: Interpreter doesn't yet properly handle AGI background colour.

            // [1489] 1520(2 bytes) Text Attribute value (combined foreground/background value)
            int textAttribute = (savedGameData[postStringsOffset + 4] + (savedGameData[postStringsOffset + 5] << 8));
            
            // [1491] 1522(2 bytes) Accept input = 1, Prevent input = 0
            state.AcceptInput = (savedGameData[postStringsOffset + 6] + (savedGameData[postStringsOffset + 7] << 8)) == 1;

            // [1493] 1524(2 bytes) User input row on the screen
            state.InputLineRow = (savedGameData[postStringsOffset + 8] + (savedGameData[postStringsOffset + 9] << 8));

            // [1495] 1526(2 bytes) Cursor character
            state.CursorCharacter = (char)(savedGameData[postStringsOffset + 10] + (savedGameData[postStringsOffset + 11] << 8));

            // [1497] 1528(2 bytes) Show status line = 1, Don't show status line = 0
            state.ShowStatusLine = (savedGameData[postStringsOffset + 12] + (savedGameData[postStringsOffset + 13] << 8)) == 1;

            // [1499] 1530(2 bytes) Status line row on the screen
            state.StatusLineRow = (savedGameData[postStringsOffset + 14] + (savedGameData[postStringsOffset + 15] << 8));

            // [1501] 1532(2 bytes) Picture top row on the screen
            state.PictureRow = (savedGameData[postStringsOffset + 16] + (savedGameData[postStringsOffset + 17] << 8));

            // [1503] 1534(2 bytes) Picture bottom row on the screen
            // Note: Not needed by this intepreter.
            int picBottom = (savedGameData[postStringsOffset + 18] + (savedGameData[postStringsOffset + 19] << 8));

            if ((postStringsOffset + 20) < aniObjsOffset)
            {
                // [1505] 1536(2 bytes) Stores a pushed position within the script event list
                // Note: Depends on interpreter version. 2.4xx and below didn't have push.script/pop.script, so they didn't have this saved game field.
                state.ScriptBuffer.SavedScript = (savedGameData[postStringsOffset + 20] + (savedGameData[postStringsOffset + 21] << 8));
            }

            // SECOND PIECE: ANIMATED OBJECT STATE
            // 17 aniobjs = 0x02DB length, 18 aniobjs = 0x0306, 20 aniobjs = 0x035C, 21 aniobjs = 0x0387, 91 = 0x0F49] 2B, 2B, 2B, 2B, 2B
            // 1538 - 1539(2 bytes) Length of piece (ANIOBJ should divide evenly in to this length)
            int aniObjectsLength = (savedGameData[aniObjsOffset + 0] + (savedGameData[aniObjsOffset + 1] << 8));
            // Each ANIOBJ entry is 0x2B in length, i.e. 43 bytes.
            // 17 aniobjs = 0x02DB length, 18 aniobjs = 0x0306, 20 aniobjs = 0x035C, 21 aniobjs = 0x0387, 91 = 0x0F49] 2B, 2B, 2B, 2B, 2B
            int numOfAniObjs = (aniObjectsLength / 0x2B);

            for (int i = 0; i < numOfAniObjs; i++)
            {
                int aniObjOffset = aniObjsOffset + 2 + (i * 0x2B);
                AnimatedObject aniObj = state.AnimatedObjects[i];
                aniObj.Reset();

                // Each ANIOBJ entry is 0x2B in length, i.e. 43 bytes.
                // Example: KQ1 - ego - starting position in room 1
                // 01 01 00 6e 00 64 00 00 17 6b 00 04 24 6b 00 06
                // 31 6b 31 6b 2f 9c 6e 00 64 00 06 00 20 00 01 01
                // 01 00 00 00 09 53 40 00 00 00 00
                
                //UBYTE movefreq;     /* number of animation cycles between motion  */    e.g.   01
                aniObj.StepTime = savedGameData[aniObjOffset + 0];
                //UBYTE moveclk;      /* number of cycles between moves of object   */    e.g.   01
                aniObj.StepTimeCount = savedGameData[aniObjOffset + 1];
                //UBYTE num;          /* object number                              */    e.g.   00
                aniObj.ObjectNumber = savedGameData[aniObjOffset + 2];
                //COORD x;            /* current x coordinate                       */    e.g.   6e 00 (0x006e = )
                aniObj.X = (byte)(savedGameData[aniObjOffset + 3] + (savedGameData[aniObjOffset + 4] << 8));
                //COORD y;            /* current y coordinate                       */    e.g.   64 00 (0x0064 = )
                aniObj.Y = (byte)(savedGameData[aniObjOffset + 5] + (savedGameData[aniObjOffset + 6] << 8));
                //UBYTE view;         /* current view number                        */    e.g.   00
                aniObj.CurrentView = savedGameData[aniObjOffset + 7];
                //VIEW* viewptr;      /* pointer to current view                    */    e.g.   17 6b (0x6b17 = ) IGNORE.
                //UBYTE loop;         /* current loop in view                       */    e.g.   00
                aniObj.CurrentLoop = savedGameData[aniObjOffset + 10];
                //UBYTE loopcnt;      /* number of loops in view                    */    e.g.   04                IGNORE
                //LOOP* loopptr;      /* pointer to current loop                    */    e.g.   24 6b (0x6b24 = ) IGNORE
                //UBYTE cel;          /* current cell in loop                       */    e.g.   00
                aniObj.CurrentCel = savedGameData[aniObjOffset + 14];
                //UBYTE celcnt;       /* number of cells in current loop            */    e.g.   06                IGNORE
                //CEL* celptr;        /* pointer to current cell                    */    e.g.   31 6b (0x6b31 = ) IGNORE
                //CEL* prevcel;       /* pointer to previous cell                   */    e.g.   31 6b (0x6b31 = ) 
                if (aniObj.View != null) aniObj.PreviousCel = aniObj.Cel;
                //STRPTR save;        /* pointer to background save area            */    e.g.   2f 9c (0x9c2f = ) IGNORE
                //COORD prevx;        /* previous x coordinate                      */    e.g.   6e 00 (0x006e = )
                aniObj.PrevX = (byte)(savedGameData[aniObjOffset + 22] + (savedGameData[aniObjOffset + 23] << 8));
                //COORD prevy;        /* previous y coordinate                      */    e.g.   64 00 (0x0064 = )
                aniObj.PrevY = (byte)(savedGameData[aniObjOffset + 24] + (savedGameData[aniObjOffset + 25] << 8));
                //COORD xsize;        /* x dimension of current cell                */    e.g.   06 00 (0x0006 = ) IGNORE
                //COORD ysize;        /* y dimension of current cell                */    e.g.   20 00 (0x0020 = ) IGNORE
                //UBYTE stepsize;     /* distance object can move                   */    e.g.   01
                aniObj.StepSize = savedGameData[aniObjOffset + 30];
                //UBYTE cyclfreq;     /* time interval between cells of object      */    e.g.   01
                aniObj.CycleTime = savedGameData[aniObjOffset + 31];
                //UBYTE cycleclk;     /* counter for determining when object cycles */    e.g.   01
                aniObj.CycleTimeCount = savedGameData[aniObjOffset + 32];
                //UBYTE dir;          /* object direction                           */    e.g.   00
                aniObj.Direction = savedGameData[aniObjOffset + 33];
                //UBYTE motion;       /* object motion type                         */    e.g.   00
                // #define	WANDER	1		/* random movement */
                // #define	FOLLOW	2		/* follow an object */
                // #define	MOVETO	3		/* move to a given coordinate */
                aniObj.MotionType = (MotionType)savedGameData[aniObjOffset + 34];
                //UBYTE cycle;        /* cell cycling type                          */    e.g.   00
                // #define NORMAL	0		/* normal repetative cycling of object */
                // #define ENDLOOP	1		/* animate to end of loop and stop */
                // #define RVRSLOOP	2		/* reverse of ENDLOOP */
                // #define REVERSE	3		/* cycle continually in reverse */
                aniObj.CycleType = (CycleType)savedGameData[aniObjOffset + 35];
                //UBYTE pri;          /* priority of object                         */    e.g.   09
                aniObj.Priority = savedGameData[aniObjOffset + 36];
                //UWORD control;      /* object control flag (bit mapped)           */    e.g.   53 40 (0x4053 = )
                int controlBits = (savedGameData[aniObjOffset + 37] + (savedGameData[aniObjOffset + 38] << 8));
                /* object control bits */
                // DRAWN     0x0001  /* 1 -> object is drawn on screen */
                aniObj.Drawn = ((controlBits & 0x0001) > 0);
                // IGNRBLK   0x0002  /* 1 -> object ignores blocks */
                aniObj.IgnoreBlocks = ((controlBits & 0x0002) > 0);
                // FIXEDPRI  0x0004  /* 1 -> object has fixed priority */
                aniObj.FixedPriority = ((controlBits & 0x0004) > 0);
                // IGNRHRZ   0x0008  /* 1 -> object ignores the horizon */
                aniObj.IgnoreHorizon = ((controlBits & 0x0008) > 0);
                // UPDATE    0x0010  /* 1 -> update the object */
                aniObj.Update = ((controlBits & 0x0010) > 0);
                // CYCLE     0x0020  /* 1 -> cycle the object */
                aniObj.Cycle = ((controlBits & 0x0020) > 0);
                // ANIMATED  0x0040  /* 1 -> object can move */
                aniObj.Animated = ((controlBits & 0x0040) > 0);
                // BLOCKED   0x0080  /* 1 -> object is blocked */
                aniObj.Blocked = ((controlBits & 0x0080) > 0);
                // PRICTRL1  0x0100  /* 1 -> object must be on 'water' priority */
                aniObj.StayOnWater = ((controlBits & 0x0100) > 0);
                // IGNROBJ   0x0200  /* 1 -> object won't collide with objects */
                aniObj.IgnoreObjects = ((controlBits & 0x0200) > 0);
                // REPOS     0x0400  /* 1 -> object being reposn'd in this cycle */
                aniObj.Repositioned = ((controlBits & 0x0400) > 0);
                // PRICTRL2  0x0800  /* 1 -> object must not be entirely on water */
                aniObj.StayOnLand = ((controlBits & 0x0800) > 0);
                // NOADVANC  0x1000  /* 1 -> don't advance object's cel in this loop */
                aniObj.NoAdvance = ((controlBits & 0x1000) > 0);
                // FIXEDLOOP 0x2000  /* 1 -> object's loop is fixed */
                aniObj.FixedLoop = ((controlBits & 0x2000) > 0);
                // STOPPED   0x4000  /* 1 -> object did not move during last animation cycle */
                aniObj.Stopped = ((controlBits & 0x4000) > 0);
                //UBYTE parms[4];     /* space for various motion parameters        */    e.g.   00 00 00 00
                aniObj.MotionParam1 = savedGameData[aniObjOffset + 39];
                aniObj.MotionParam2 = savedGameData[aniObjOffset + 40];
                aniObj.MotionParam3 = savedGameData[aniObjOffset + 41];
                aniObj.MotionParam4 = savedGameData[aniObjOffset + 42];
                // If motion type is follow, then force a re-initialisation of the follow path.
                if (aniObj.MotionType == MotionType.Follow) aniObj.MotionParam3 = -1;
            }

            // THIRD PIECE: OBJECTS
            // Almost an exact copy of the OBJECT file, but with the 3 byte header removed, and room
            // numbers reflecting the current location of each object.
            int objectsOffset = aniObjsOffset + 2 + aniObjectsLength;
            int objectsLength = (savedGameData[objectsOffset + 0] + (savedGameData[objectsOffset + 1] << 8));
            // The NumOfAnimatedObjects, as stored in OBJECT, should be 1 less than the number of animated object slots
            // (due to add.to.pic slot), otherwise this number increments by 1 on every save followed by restore.
            state.Objects.NumOfAnimatedObjects = (byte)(numOfAniObjs - 1);
            int numOfObjects = (savedGameData[objectsOffset + 2] + (savedGameData[objectsOffset + 3] << 8)) / 3;
            // Set the saved room number of each Object. 
            for (int objectNum = 0, roomPos = objectsOffset + 4; objectNum < numOfObjects; objectNum++, roomPos += 3)
            {
                state.Objects[objectNum].Room = savedGameData[roomPos];
            }

            // FOURTH PIECE: SCRIPT BUFFER EVENTS
            // A transcript of events leading to the current state in the current room.
            int scriptsOffset = objectsOffset + 2 + objectsLength;
            int scriptsLength = (savedGameData[scriptsOffset + 0] + (savedGameData[scriptsOffset + 1] << 8));
            // Each script entry is two unsigned bytes long:
            // UBYTE action;
            // UBYTE who;
            //
            // Action byte is a code defined as follows:
            // S_LOADLOG       0
            // S_LOADVIEW      1
            // S_LOADPIC       2
            // S_LOADSND       3
            // S_DRAWPIC       4
            // S_ADDPIC        5
            // S_DSCRDPIC      6
            // S_DSCRDVIEW     7
            // S_OVERLAYPIC    8
            //
            // Example: 
            // c8 00 Length
            // 00 01 load.logic  0x01
            // 01 00 load.view   0x00
            // 00 66 load.logic  0x66
            // 01 4b load.view   0x4B
            // 01 57 load.view   0x57
            // 01 6e load.view   0x6e
            // 02 01 load.pic    0x01
            // 04 01 draw.pic    0x01
            // 06 01 discard.pic 0x01
            // 00 65 load.logic  0x65
            // 01 6b load.view   0x6B
            // 01 61 load.view   0x61
            // 01 5d load.view   0x5D
            // 01 46 load.view   0x46
            // 03 0d load.sound  0x0D
            // etc...
            state.ScriptBuffer.InitScript();
            for (int i = 0; i < scriptEntryCount; i++)
            {
                int scriptOffset = scriptsOffset + 2 + (i * 2);
                int action = savedGameData[scriptOffset + 0];
                int resourceNum = savedGameData[scriptOffset + 1];
                byte[] data = null;
                if (action == (int)ScriptBuffer.ScriptBufferEventType.AddToPic)
                {
                    // The add.to.pics are stored in the saved game file across 8 bytes, i.e. 4 separate script 
                    // entries (that is also how the original AGI interpreter stored it in memory). 
                    // What we do though is store these in an additional data array associated with
                    // the script event since utilitising multiple event entries is a bit of a hack
                    // really. I can understand why they did it though.
                    data = new byte[] {
                        savedGameData[scriptOffset + 2], savedGameData[scriptOffset + 3], savedGameData[scriptOffset + 4],
                        savedGameData[scriptOffset + 5], savedGameData[scriptOffset + 6], savedGameData[scriptOffset + 7]
                    };

                    // Increase i to account for the fact that we've processed an additional 3 slots.
                    i += 3;
                }
                state.ScriptBuffer.RestoreScript((ScriptBuffer.ScriptBufferEventType)action, resourceNum, data);
            }

            // FIFTH PIECE: SCAN OFFSETS
            // Note: Not every logic can set a scan offset, as there is a max of 30. But only
            // loaded logics can have this set and I'd imagine you'd run out of memory before 
            // loading that many logics at once. 
            int scanOffsetsOffset = scriptsOffset + 2 + scriptsLength;
            int scanOffsetsLength = (savedGameData[scanOffsetsOffset + 0] + (savedGameData[scanOffsetsOffset + 1] << 8));
            int numOfScanOffsets = (scanOffsetsLength / 4);
            // Each entry is 4 bytes long, made up of 2 16-bit words:
            // COUNT num;                                    /* logic number         */
            // COUNT ofs;                                    /* offset to scan start */ 
            //
            // Example:
            // 18 00 
            // 00 00 00 00  Start of list. Seems to always be 4 zeroes.
            // 00 00 00 00  Logic 0 - Offset 0
            // 01 00 00 00  Logic 1 - Offset 0
            // 66 00 00 00  Logic 102 - Offset 0
            // 65 00 00 00  Logic 101 - Offset 0
            // ff ff 00 00  End of list
            //
            // Quick Analysis of the above:
            // * Only logics that are current loaded are in the scan offset list, i.e. they're removed when the room changes.
            // * The order logics appear in this list is the order that they are loaded.
            // * Logics disappear from this list when they are unloaded (on new.room).
            // * The new.room command unloads all logics except for logic 0, so it never leaves this list.
            for (int i = 0; i < 256; i++) state.ScanStart[i] = 0;
            for (int i = 1; i < numOfScanOffsets; i++)
            {
                int scanOffsetOffset = scanOffsetsOffset + 2 + (i * 4);
                int logicNumber = (savedGameData[scanOffsetOffset + 0] + (savedGameData[scanOffsetOffset + 1] << 8));
                if (logicNumber < 256)
                {
                    state.ScanStart[logicNumber] = (savedGameData[scanOffsetOffset + 2] + (savedGameData[scanOffsetOffset + 3] << 8));
                }
            }

            state.Flags[Defines.RESTORE] = true;

            // Return true to say that we have successfully restored a saved game file.
            return true;
        }

        // The following code was provided by Kawa from a post at the sciprogramming.com forums.
        // http://sciprogramming.com/community/index.php?topic=1702.msg10657#msg10657

        public static class Vista
        {
            public static bool IsVista = (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 6);

            public static readonly Guid SavedGames = new Guid("4C5C32FF-BB9D-43b0-B5B4-2D72E54EAAA4");

            public static string GetKnownFolderPath(Guid target)
            {
                if (!IsVista)
                    return null;

                string ret = null;
                IntPtr pPath;
                try
                {
                    if (SafeNativeMethods.SHGetKnownFolderPath(SavedGames, 0, IntPtr.Zero, out pPath) == 0)
                    {
                        ret = System.Runtime.InteropServices.Marshal.PtrToStringUni(pPath);
                        System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pPath);
                    }
                    return ret;
                }
                catch (DllNotFoundException)
                {
                    return null;
                }
            }
        }

        internal static class SafeNativeMethods
        {
            [DllImport("shell32.dll")]
            public static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);
        }
    }
}
