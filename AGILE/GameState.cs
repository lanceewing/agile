using AGI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static AGI.Resource;

namespace AGILE
{
    /// <summary>
    /// The GameState class holds all of the data and state for the Game currently 
    /// being run by the interpreter.
    /// </summary>
    class GameState
    {
        /// <summary>
        /// The Game whose data we are interpreting.
        /// </summary>
        private Game game;

        public Logic[] Logics { get; }
        public Picture[] Pictures { get; }
        public View[] Views { get; }
        public Sound[] Sounds { get; }
        public Objects Objects { get; set; }
        public Words Words { get; }

        /// <summary>
        /// Scan start values for each Logic. Index is the Logic number. We normally start 
        /// scanning the Logic at position 0, but this can be set to another  value via the
        /// set.scan.start AGI command. Note that only loaded logics can have their scan 
        /// offset set. When they are unloaded, their scan offset is forgotten. Logic 0 is
        /// always loaded, so its scan start is never forgotten.
        /// </summary>
        public int[] ScanStart { get; set; }

        public bool[] Controllers { get; set; }
        public byte[] Vars { get; set; }
        public bool[] Flags { get; set; }
        public string[] Strings { get; set; }
        public AnimatedObject[] AnimatedObjects { get; }
        public AnimatedObject Ego { get { return AnimatedObjects[0]; } }

        /// <summary>
        /// The List of animated objects that currently have the DRAWN and UPDATE flags set.
        /// </summary>
        public List<AnimatedObject> UpdateObjectList { get; set; }

        /// <summary>
        /// The List of animated objects that have the DRAWN flag set but not the UPDATE flag.
        /// </summary>
        public List<AnimatedObject> StoppedObjectList { get; set; }

        /// <summary>
        /// A Map between a key event code and the matching controller number.
        /// </summary>
        public Dictionary<int, int> KeyToControllerMap { get; set; }

        /// <summary>
        /// For making random decisions.
        /// </summary>
        public Random Random = new Random();

        /// <summary>
        /// The Picture that is currently drawn, i.e. the last one for which a draw.pic() 
        /// command was executed. This will be a clone of an instance in the Pictures array,
        /// which may have subsequently had an overlay drawn on top of it.
        /// </summary>
        public Picture CurrentPicture { get; set; }

        /// <summary>
        /// The pixel array for the visual data for the current Picture, where the values
        /// are the ARGB values. The dimensions of this are 320x168, i.e. two pixels per 
        /// AGI pixel. Makes it easier to copy to the main pixels array when required.
        /// </summary>
        public int[] VisualPixels { get; set; }

        /// <summary>
        /// The pixel array for the priority data for the current Picture, where the values
        /// are from 4 to 15 (i.e. they are not ARGB values). The dimensions of this one
        /// are 160x168 as its usage is non-visual.
        /// </summary>
        public int[] PriorityPixels { get; set; }

        /// <summary>
        /// The pixel array for the control line data for the current Picture, where the
        /// values are from 0 to 4 (i.e. not ARGB values). The dimensions of this one
        /// are 160x168 as its usage is non-visual.
        /// </summary>
        public int[] ControlPixels { get; set; }

        /// <summary>
        /// Whether or not the picture is currently visible. This is set to true after a
        /// show.pic call. The draw.pic and overlay.pic commands both set it to false. It's
        /// value is used to determine whether to render the AnimatedObjects.
        /// </summary>
        public bool PictureVisible { get; set; }

        public bool AcceptInput { get; set; }
        public bool UserControl { get; set;  }
        public bool GraphicsMode { get; set; }
        public bool ShowStatusLine { get; set; }
        public int StatusLineRow { get; set; }
        public int PictureRow { get; set; }
        public int InputLineRow { get; set; }
        public int Horizon { get; set;  }
        public int TextAttribute { get; set; }
        public int ForegroundColour { get; set; }
        public int BackgroundColour { get; set; }
        public char CursorCharacter { get; set; }
        public long TotalTicks { get; set; }
        public long AnimationTicks { get; set; }
        public bool GamePaused { get; set; }
        public int CurrentLogNum { get; set; }
        public StringBuilder CurrentInput { get; set; }
        public string LastInput { get; set; }
        public string GameId { get; set; }
        public string Version { get; set; }
        public int MaxDrawn { get; set; }
        public int PriorityBase { get; set; }
        public string SimpleName { get; set; }
        public byte CurrentRoom { get; set; }
        public bool MenuEnabled { get; set; }
        public bool MenuOpen { get; set; }
        public bool HoldKey { get; set; }

        /// <summary>
        /// The List of recognised words from the current user input line.
        /// </summary>
        public List<string> RecognisedWords { get; set; }

        /// <summary>
        /// Indicates that a block has been set.
        /// </summary>
        public bool Blocking { get; set; }

        public short BlockUpperLeftX { get; set; }
        public short BlockUpperLeftY { get; set; }
        public short BlockLowerRightX { get; set; }
        public short BlockLowerRightY { get; set; }

        /// <summary>
        /// Contains a transcript of events leading to the current state in the current room.
        /// </summary>
        public ScriptBuffer ScriptBuffer { get; set; }

        /// <summary>
        /// Returns true if the AGI game files are V3; otherwise false.
        /// </summary>
        public bool IsAGIV3 { get { return (game.v3GameSig != null); } }

        /// <summary>
        /// Constructor for GameState.
        /// </summary>
        /// <param name="game">The Game from which we'll get all of the game data.</param>
        public GameState(Game game)
        {
            this.game = game;
            this.Vars = new byte[Defines.NUMVARS];
            this.Flags = new bool[Defines.NUMFLAGS];
            this.Strings = new string[Defines.NUMSTRINGS];
            this.Controllers = new bool[Defines.NUMCONTROL];
            this.ScanStart = new int[256];
            this.Logics = new Logic[256];
            this.Pictures = new Picture[256];
            this.Views = new View[256];
            this.Sounds = new Sound[256];
            this.Objects = new Objects(game.Objects);
            this.Words = game.Words;
            this.MaxDrawn = 15;
            this.PriorityBase = 48;
            this.CurrentInput = new StringBuilder();
            this.LastInput = "";
            this.SimpleName = "";
            this.GameId = (game.v3GameSig != null? game.v3GameSig : "UNKNOWN");
            this.Version = (game.version.Equals("Unknown")? "2.917" : game.version);
            this.MenuEnabled = true;
            this.HoldKey = false;
            this.KeyToControllerMap = new Dictionary<int, int>();
            this.RecognisedWords = new List<string>();
            this.ScriptBuffer = new ScriptBuffer(this);

            this.VisualPixels = new int[320 * 168];
            this.PriorityPixels = new int[160 * 168];
            this.ControlPixels = new int[160 * 168];

            // Create and initialise all of the AnimatedObject entries.
            this.AnimatedObjects = new AnimatedObject[Defines.NUMANIMATED];
            for (int i=0; i < Defines.NUMANIMATED; i++)
            {
                this.AnimatedObjects[i] = new AnimatedObject(this, i);
            }

            this.UpdateObjectList = new List<AnimatedObject>();
            this.StoppedObjectList = new List<AnimatedObject>();

            // Store resources in arrays for easy lookup.
            foreach (Volume volume in game.Volumes)
            {
                foreach (Resource logic in volume.Logics) Logics[logic.Index] = (Logic)logic;
                foreach (Resource picture in volume.Pictures) Pictures[picture.Index] = (Picture)picture;
                foreach (Resource view in volume.Views) Views[view.Index] = (View)view;
                foreach (Resource sound in volume.Sounds) Sounds[sound.Index] = (Sound)sound;
            }

            // Logic 0 is always marked as loaded. It never gets unloaded.
            Logics[0].IsLoaded = true;
        }

        /// <summary>
        /// Performs the initialisation of the state of the game being interpreted. Usually called whenever
        /// the game starts or restarts.
        /// </summary>
        public void Init()
        {
            ClearVars();
            Vars[Defines.MACHINE_TYPE] = 0;  // IBM PC
            Vars[Defines.MONITOR_TYPE] = 3;  // EGA
            Vars[Defines.INPUTLEN] = Defines.MAXINPUT + 1;
            Vars[Defines.NUM_VOICES] = 3;

            // The game would usually set this, but no harm doing it here (2 = NORMAL).
            Vars[Defines.ANIMATION_INT] = 2;

            // Set to the maximum memory amount as recognised by AGI.
            Vars[Defines.MEMLEFT] = 255;

            ClearFlags();
            Flags[Defines.HAS_NOISE] = true;
            Flags[Defines.INITLOGS] = true;
            Flags[Defines.SOUNDON] = true;

            // Set the text attribute to default (black on white), and display the input line.
            ForegroundColour = 15;
            BackgroundColour = 0;

            Horizon = Defines.HORIZON;
            UserControl = true;
            Blocking = false;

            ClearVisualPixels();
            GraphicsMode = true;
            AcceptInput = false;
            ShowStatusLine = false;
            CurrentLogNum = 0;
            CurrentInput.Clear();
            LastInput = "";
            SimpleName = "";
            KeyToControllerMap.Clear();
            MenuEnabled = true;
            HoldKey = false;

            foreach (AnimatedObject aniObj in AnimatedObjects)
            {
                aniObj.Reset(true);
            }

            StoppedObjectList.Clear();
            UpdateObjectList.Clear();

            this.Objects = new Objects(game.Objects);
        }

        /// <summary>
        /// Resets the four resources types back to their new room state. The main reason for doing
        /// this is to support the script event buffer.
        /// </summary>
        public void ResetResources()
        {
            for (int i = 0; i < 256; i++)
            {
                // For Logics and Views, number 0 is never unloaded.
                if (i > 0)
                {
                    if (Logics[i] != null) Logics[i].IsLoaded = false;
                }
                if (Views[i] != null) Views[i].IsLoaded = false;
                if (Pictures[i] != null) Pictures[i].IsLoaded = false;
                if (Sounds[i] != null) Sounds[i].IsLoaded = false;
            }
        }

        /// <summary>
        /// Restores all of the background save areas for the most recently drawn AnimatedObjects.
        /// </summary>
        /// <param name="restoreList"></param>
        public void RestoreBackgrounds(List<AnimatedObject> restoreList = null)
        {
            if (restoreList == null)
            {
                // If no list specified, then restore update list then stopped list.
                RestoreBackgrounds(UpdateObjectList);
                RestoreBackgrounds(StoppedObjectList);
            }
            else
            {
                // Restore the backgrounds of the previous drawn cels for each AnimatedObject.
                foreach (AnimatedObject aniObj in restoreList.AsEnumerable().Reverse())
                {
                    aniObj.RestoreBackPixels();
                }
            }
        }

        /// <summary>
        /// Draws all of the drawn AnimatedObjects in their priority / Y position order. This method
        /// does not actually render the objects to the screen but rather to the "back" screen, or 
        /// "off" screen version of the visual screen.
        /// </summary>
        /// <param name="objectDrawList"></param>
        public void DrawObjects(List<AnimatedObject> objectDrawList = null)
        {
            if (objectDrawList == null)
            {
                // If no list specified, then draw stopped list then update list.
                DrawObjects(MakeStoppedObjectList());
                DrawObjects(MakeUpdateObjectList());
            }
            else
            {
                // Draw the AnimatedObjects to screen in priority order.
                foreach (AnimatedObject aniObj in objectDrawList)
                {
                    aniObj.Draw();
                }
            }
        }

        /// <summary>
        /// Shows all AnimatedObjects by blitting the bounds of their current cel to the screen 
        /// pixels. Also updates the Stopped flag and previous position as per the original AGI 
        /// interpreter behaviour.
        /// </summary>
        /// <param name="pixels">The screen pixels to blit the AnimatedObjects to.</param>
        /// <param name="objectShowList"></param>
        public void ShowObjects(int[] pixels, List<AnimatedObject> objectShowList = null)
        {
            if (objectShowList == null)
            {
                // If no list specified, then draw stopped list then update list.
                ShowObjects(pixels, StoppedObjectList);
                ShowObjects(pixels, UpdateObjectList);
            }
            else
            {
                foreach (AnimatedObject aniObj in objectShowList)
                {
                    aniObj.Show(pixels);

                    // Check if the AnimatedObject moved this cycle and if it did then set the flags accordingly. The
                    // position of an AnimatedObject is updated only when the StepTimeCount hits 0, at which point it 
                    // reloads from StepTime. So if the values are equal, this is a step time reload cycle and therefore
                    // the AnimatedObject's position would have been updated and it is appropriate to update Stopped flag.
                    if (aniObj.StepTimeCount == aniObj.StepTime)
                    {
                        if ((aniObj.X == aniObj.PrevX) && (aniObj.Y == aniObj.PrevY))
                        {
                            aniObj.Stopped = true;
                        }
                        else
                        {
                            aniObj.PrevX = aniObj.X;
                            aniObj.PrevY = aniObj.Y;
                            aniObj.Stopped = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns a List of the AnimatedObjects to draw, in the order in which they should be
        /// drawn. It gets the list of candidate AnimatedObjects from the given GameState and 
        /// then for each object that is in a Drawn state, it adds them to the list to be draw
        /// and then sorts that list by a combination of Y position and priority state, which
        /// results in the List to be drawn in the order they should be drawn. The updating param
        /// determines what the value of the Update flag should be in order to include an object
        /// in the list.
        /// </summary>
        /// <param name="objsToDraw"></param>
        /// <param name="updating">The value of the UPDATE flag to check for when adding to list</param>
        /// <returns></returns>
        public List<AnimatedObject> MakeObjectDrawList(List<AnimatedObject> objsToDraw, bool updating)
        {
            objsToDraw.Clear();

            foreach (AnimatedObject aniObj in this.AnimatedObjects)
            {
                if (aniObj.Drawn && (aniObj.Update == updating))
                {
                    objsToDraw.Add(aniObj);
                }
            }

            // Sorts them by draw order.
            objsToDraw.Sort();

            return objsToDraw;
        }

        /// <summary>
        /// Recreates and then returns the list of animated objects that are currently
        /// being updated, in draw order.
        /// </summary>
        /// <returns></returns>
        public List<AnimatedObject> MakeUpdateObjectList()
        {
            return MakeObjectDrawList(UpdateObjectList, true);
        }

        /// <summary>
        /// Recreates and the returns the list of animated objects that are currently
        /// not being updated, in draw order.
        /// </summary>
        /// <returns></returns>
        public List<AnimatedObject> MakeStoppedObjectList()
        {
            return MakeObjectDrawList(StoppedObjectList, false);
        }

        /// <summary>
        /// Clears the VisualPixels screen to it's initial black state.
        /// </summary>
        public void ClearVisualPixels()
        {
            for (int i=0; i < this.VisualPixels.Length; i++)
            {
                this.VisualPixels[i] = AGI.Color.Palette[0].ToArgb();
            }
        }

        /// <summary>
        /// Clears all of the AGI variables to be zero.
        /// </summary>
        public void ClearVars()
        {
            for (int i = 0; i < Defines.NUMVARS; i++)
            {
                Vars[i] = 0;
            }
        }

        /// <summary>
        /// Clears all of the AGI flags to be false.
        /// </summary>
        public void ClearFlags()
        {
            for (int i = 0; i < Defines.NUMFLAGS; i++)
            {
                Flags[i] = false;
            }
        }

        /// <summary>
        /// Clears all of the AGI controllers to be false.
        /// </summary>
        public void ClearControllers()
        {
            for (int i = 0; i < Defines.NUMCONTROL; i++)
            {
                Controllers[i] = false;
            }
        }
    }
}
