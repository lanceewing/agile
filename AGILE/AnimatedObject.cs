using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AGI.Resource;
using static AGI.Resource.View;

using Marshal = System.Runtime.InteropServices.Marshal;

namespace AGILE
{
    /// <summary>
    /// The AnimatedObject class is one of the core classes in the AGI interpreter. An instance of
    /// this class holds the state of an animated object on the screen. Many of the action commands
    /// change the state within an instance of AnimatedObject, and the interpreter makes use of 
    /// the instances of this class stored within the animated object table to perform an animation
    /// cycle.
    /// </summary>
    class AnimatedObject : IComparable<AnimatedObject>
    {
        /// <summary>
        /// Number of animate cycles between moves of the AnimatedObject. Set by step.time action command.
        /// </summary>
        public byte StepTime { get; set; }

        /// <summary>
        /// Count down from StepTime for determining when the AnimatedObject will move. Initially set 
        /// by step.time and it then counts down from there on each animate cycle, resetting back to 
        /// the StepTime value when it hits zero.
        /// </summary>
        public byte StepTimeCount { get; set; }

        /// <summary>
        /// The index of this AnimatedObject in the animated object table. Set to -1 for add.to.pic objects.
        /// </summary>
        public byte ObjectNumber { get; set; }

        /// <summary>
        /// Current X position of this AnimatedObject.
        /// </summary>
        public short X { get; set; }

        /// <summary>
        /// Current Y position of this AnimatedObject.
        /// </summary>
        public short Y { get; set; }

        /// <summary>
        /// The current view number for this AnimatedObject.
        /// </summary>
        public byte CurrentView { get; set; }

        /// <summary>
        /// The View currently being used by this AnimatedObject.
        /// </summary>
        public View View { get { return state.Views[CurrentView]; } }

        /// <summary>
        /// The current loop number within the View.
        /// </summary>
        public byte CurrentLoop { get; set; }

        /// <summary>
        /// The number of loops in the View.
        /// </summary>
        public byte NumberOfLoops { get { return (byte)View.Loops.Count; } }

        /// <summary>
        /// The Loop that is currently cycling for this AnimatedObject.
        /// </summary>
        public Loop Loop { get { return (Loop)View.Loops[CurrentLoop]; } }

        /// <summary>
        /// The current cell number within the loop.
        /// </summary>
        public byte CurrentCel { get; set; }

        /// <summary>
        /// The number of cels in the current loop.
        /// </summary>
        public byte NumberOfCels { get { return (byte)Loop.Cels.Count; } }

        /// <summary>
        /// The Cel currently being displayed.
        /// </summary>
        public Cel Cel { get { return (Cel)Loop.Cels[CurrentCel]; } }

        /// <summary>
        /// The previous Cel that was displayed.
        /// </summary>
        public Cel PreviousCel { get; set; }

        /// <summary>
        /// The background save area for this AnimatedObject.
        /// </summary>
        public SaveArea SaveArea { get; set; }

        /// <summary>
        /// Previous X position.
        /// </summary>
        public short PrevX { get; set; }

        /// <summary>
        /// Previous Y position.
        /// </summary>
        public short PrevY { get; set; }

        /// <summary>
        /// X dimension of the current cel.
        /// </summary>
        public short XSize { get { return (short)Cel.Screen.Bitmap.Width; } }

        /// <summary>
        /// Y dimesion of the current cel.
        /// </summary>
        public short YSize { get { return (short)Cel.Screen.Bitmap.Height; } }

        /// <summary>
        /// Distance that this AnimatedObject will move on each move.
        /// </summary>
        public byte StepSize { get; set; }

        /// <summary>
        /// The number of animate cycles between changing to the next cel in the current 
        /// loop. Set by the cycle.time action command.
        /// </summary>
        public byte CycleTime { get; set; }

        /// <summary>
        /// Count down from CycleTime for determining when the AnimatedObject will cycle to the next
        /// cel in the loop. Initially set by cycle.time and it then counts down from there on each
        /// animate cycle, resetting back to the CycleTime value when it hits zero.
        /// </summary>
        public byte CycleTimeCount { get; set; }

        /// <summary>
        /// The AnimatedObject's direction.
        /// </summary>
        public byte Direction { get; set; }

        /// <summary>
        /// The AnimatedObject's motion type.
        /// </summary>
        public MotionType MotionType { get; set; }

        /// <summary>
        /// The AnimatedObject's cycling type.
        /// </summary>
        public CycleType CycleType { get; set; }

        /// <summary>
        /// The priority band value for this AnimatedObject.
        /// </summary>
        public byte Priority { get; set; }

        /// <summary>
        /// The control colour of the box around the base of add.to.pic objects. Not application
        /// to normal AnimatedObjects.
        /// </summary>
        public byte ControlBoxColour { get; set; }

        /// <summary>
        /// true if AnimatedObject is drawn on the screen; otherwise false;
        /// </summary>
        public bool Drawn { get; set; }

        /// <summary>
        /// true if the AnimatedObject should ignore blocks; otherwise false. Ignoring blocks
        /// means that it can pass black priority one lines and also script blocks. Set to true
        /// by the ignore.blocks action command. Set to false by the observe.blocks action 
        /// command.
        /// </summary>
        public bool IgnoreBlocks { get; set; }

        /// <summary>
        /// true if the AnimatedObject has fixed priority; otherwise false. Set to true by the
        /// set.priority action command. Set to false by the release.priority action command.
        /// </summary>
        public bool FixedPriority { get; set; }

        /// <summary>
        /// true if the AnimatedObject should ignore the horizon; otherwise false. Set to true 
        /// by the ignore.horizon action command. Set to false by the observe.horizon action
        /// command.
        /// </summary>
        public bool IgnoreHorizon { get; set; }

        /// <summary>
        /// true if the AnimatedObject should be updated; otherwise false.
        /// </summary>
        public bool Update { get; set; }

        /// <summary>
        /// true if the AnimatedObject should be cycled; otherwise false.
        /// </summary>
        public bool Cycle { get; set; }

        /// <summary>
        /// true if the AnimatedObject can move; otherwise false.
        /// </summary>
        public bool Animated { get; set; }

        /// <summary>
        /// true if the AnimatedObject is blocked; otherwise false.
        /// </summary>
        public bool Blocked { get; set; }

        /// <summary>
        /// true if the AnimatedObject must stay entirely on water; otherwise false.
        /// </summary>
        public bool StayOnWater { get; set; }

        /// <summary>
        /// true if the AnimatedObject must not be entirely on water; otherwise false.
        /// </summary>
        public bool StayOnLand { get; set; }

        /// <summary>
        /// true if the AnimatedObject is ignoring collisions with other AnimatedObjects; otherwise false.
        /// </summary>
        public bool IgnoreObjects { get; set; }

        /// <summary>
        /// true if the AnimatedObject is being repositioned in this cycle; otherwise false.
        /// </summary>
        public bool Repositioned { get; set; }

        /// <summary>
        /// true if the AnimatedObject should not have the cel advanced in this loop; otherwise false.
        /// </summary>
        public bool NoAdvance { get; set; }

        /// <summary>
        /// true if the AnimatedObject should not have the loop fixed; otherwise false. Having 
        /// the loop fixed means that it will not adjust according to the direction. Set to 
        /// true by the fix.loop action command. Set to false by the release.loop action command.
        /// </summary>
        public bool FixedLoop { get; set; }

        /// <summary>
        /// true if the AnimatedObject did not move in the last animation cycle; otherwise false.
        /// </summary>
        public bool Stopped { get; set; }

        /// <summary>
        /// Miscellaneous motion parameter 1. Used by Wander, MoveTo, and Follow.
        /// </summary>
        public short MotionParam1 { get; set; }

        /// <summary>
        /// Miscellaneous motion parameter 2.
        /// </summary>
        public short MotionParam2 { get; set; }

        /// <summary>
        /// Miscellaneous motion parameter 3.
        /// </summary>
        public short MotionParam3 { get; set; }

        /// <summary>
        /// Miscellaneous motion parameter 4.
        /// </summary>
        public short MotionParam4 { get; set; }

        /// <summary>
        /// The GameState class holds all of the data and state for the Game currently 
        /// being run by the interpreter.
        /// </summary>
        private GameState state;

        /// <summary>
        ///  Constructor for AnimatedObject.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="objectNum"></param>
        public AnimatedObject(GameState state, int objectNum)
        {
            this.state = state;
            this.ObjectNumber = (byte)objectNum;
            this.SaveArea = new SaveArea();
            Reset(true);
        }

        /// <summary>
        /// Resets the AnimatedObject back to its initial state.
        /// </summary>
        public void Reset(bool fullReset = false)
        {
            Animated = false;
            Drawn = false;
            Update = true;

            PreviousCel = null;
            SaveArea.VisBackPixels = null;
            SaveArea.PriBackPixels = null;

            StepSize = 1;
            CycleTime = 1;
            CycleTimeCount = 1;
            StepTime = 1;
            StepTimeCount = 1;

            // A full reset is to go back to the initial state, whereas a normal reset is
            // simply for changing rooms.
            if (fullReset)
            {
                this.Blocked = false;
                this.ControlBoxColour = 0;
                this.CurrentCel = 0;
                this.CurrentLoop = 0;
                this.CurrentView = 0;
                this.Cycle = false;
                this.CycleType = CycleType.Normal;
                this.Direction = 0;
                this.FixedLoop = false;
                this.FixedPriority = false;
                this.IgnoreBlocks = false;
                this.IgnoreHorizon = false;
                this.IgnoreObjects = false;
                this.MotionParam1 = 0;
                this.MotionParam2 = 0;
                this.MotionParam3 = 0;
                this.MotionParam4 = 0;
                this.MotionType = MotionType.Normal;
                this.NoAdvance = false;
                this.PrevX = this.X = 0;
                this.PrevY = this.Y = 0;
                this.Priority = 0;
                this.Repositioned = false;
                this.StayOnLand = false;
                this.StayOnWater = false;
                this.Stopped = false;
            }
        }

        /// <summary>
        /// Updates the AnimatedObject's Direction based on its current MotionType.
        /// </summary>
        public void UpdateDirection()
        {
            if (Animated && Update && Drawn && (StepTimeCount == 1))
            {
                switch (MotionType)
                {
                    case MotionType.Wander:
                        Wander();
                        break;

                    case MotionType.Follow:
                        Follow();
                        break;

                    case MotionType.MoveTo:
                        MoveTo();
                        break;
                }

                // If no blocks are in effect, clear the 'blocked' flag.  Otherwise,
                // if object must observe blocks, check for blocking.
                if (!state.Blocking)
                {
                    Blocked = false;
                }
                else if (!IgnoreBlocks && (Direction != 0))
                {
                    CheckBlock();
                }
            }
        }

        /// <summary>
        /// Starts the Wander motion for this AnimatedObject.
        /// </summary>
        public void StartWander()
        {
            if (this == state.Ego)
            {
                state.UserControl = false;
            }
            this.MotionType = MotionType.Wander;
            this.Update = true;
        }

        /// <summary>
        /// If the AnimatedObject has stopped, but the motion type is Wander, then this
        /// method picks a random direction and distance.
        /// 
        /// Note: MotionParam1 is used to track the distance.
        /// </summary>
        private void Wander()
        {
            // Wander uses general purpose motion parameter 1 for the distance.
            if ((MotionParam1-- == 0) || Stopped)
            {
                Direction = (byte)state.Random.Next(9);

                // If the AnimatedObject is ego, then set the EGODIR var.
                if (ObjectNumber == 0)
                {
                    state.Vars[Defines.EGODIR] = Direction;
                }

                MotionParam1 = (byte)state.Random.Next(Defines.MINDIST, Defines.MAXDIST + 1);
            }
        }

        /// <summary>
        /// New Direction matrix to support the MoveDirection method.
        /// </summary>
        private static readonly byte[,] newdir = { {8, 1, 2}, {7, 0, 3}, {6, 5, 4} };

        /// <summary>
        /// Return the direction from (oldx, oldy) to (newx, newy).  If the object is within
        /// 'delta' of the position in both directions, return 0
        /// </summary>
        /// <param name="oldx"></param>
        /// <param name="oldy"></param>
        /// <param name="newx"></param>
        /// <param name="newy"></param>
        /// <param name="delta"></param>
        /// <returns></returns>
        private byte MoveDirection(short oldx, short oldy, short newx, short newy, short delta)
        {
	        return (newdir[DirectionIndex(newy - oldy, delta), DirectionIndex(newx - oldx, delta)]);
        }

        /// <summary>
        /// Return 0, 1, or 2 depending on whether the difference between coords, d,
        /// indicates that the coordinate should decrease, stay the same, or increase.
        /// The return value is used as one of the indeces into 'newdir' above.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="delta"></param>
        /// <returns>0, 1, or 2, as described in the summary above.</returns>
        private byte DirectionIndex(int d, short delta)
        {
            byte index = 0;

            if (d <= -delta)
            {
                index = 0;
            }
            else if (d >= delta)
            {
                index = 2;
            }
            else
            {
                index = 1;
            }

            return index;
        }

        /// <summary>
        /// Move this AnimatedObject towards ego.
        /// 
        /// MotionParam1 (endDist): Distance from ego which is considered to be completion of the motion.
        /// MotionParam2 (endFlag): Flag to set on completion of the motion
        /// MotionParam3 (randDist): Distance to move in current direction (for random search)
        /// </summary>
        private void Follow()
        {
            int maxDist = 0;

            // Get coordinates of center of object's & ego's bases.
            short ecx = (short)(state.Ego.X + (state.Ego.XSize / 2));
            short ocx = (short)(this.X + (this.XSize / 2));

            // Get direction from object's center to ego's center.
            byte dir = MoveDirection(ocx, this.Y, ecx, state.Ego.Y, MotionParam1);

            // If the direction is zero, the object and ego have collided, so signal completion.
            if (dir == 0)
            {
                this.Direction = 0;
                this.MotionType = MotionType.Normal;
                this.state.Flags[this.MotionParam2] = true;
                return;
            }

            // If the object has not moved since last time, assume it is blocked and
            // move in a random direction for a random distance no greater than the
            // distance between the object and ego

            // NOTE: randDist = -1 indicates that this is initialization, and thus
            // we don't care about the previous position
            if (this.MotionParam3 == -1)
            {
                this.MotionParam3 = 0;
            }
            else if (this.Stopped)
            {
                // Make sure that the object goes in some direction.
                Direction = (byte)state.Random.Next(1, 9);

                // Average the x and y distances to the object for movement limit.
                maxDist = (Math.Abs(ocx - ecx) + Math.Abs(this.Y - state.Ego.Y)) / 2 + 1;

                // Make sure that the distance is at least the object stepsize.
                if (maxDist <= this.StepSize)
                {
                    this.MotionParam3 = this.StepSize;
                }
                else
                {
                    this.MotionParam3 = (short)state.Random.Next(this.StepSize, maxDist);
                }

                return;
            }

            // If 'randDist' is non-zero, keep moving the object in the current direction.
            if (this.MotionParam3 != 0)
            {
                if ((this.MotionParam3 -= this.StepSize) < 0)
                {
                    // Down with the random movement.
                    this.MotionParam3 = 0;
                }
                return;
            }

            // Otherwise, just move the object towards ego.  Whew...
            this.Direction = dir;
        }

        /// <summary>
        /// Starts a Follow ego motion for this AnimatedObject.
        /// </summary>
        /// <param name="dist">Distance from ego which is considered to be completion of the motion.</param>
        /// <param name="completionFlag">The number of the flag to set when the motion is completed.</param>
        public void StartFollowEgo(byte dist, byte completionFlag)
        {
            this.MotionType = MotionType.Follow;

            // Distance from ego which is considered to be completion of the motion is the larger of 
            // the object's StepSize and the dist parameter.
            this.MotionParam1 = (dist > this.StepSize ? dist : this.StepSize);
            this.MotionParam2 = completionFlag;
            this.MotionParam3 = -1;                  // 'follow' routine expects this.
            state.Flags[completionFlag] = false;     // Flag to set at completion.
            this.Update = true;
        }

        /// <summary>
        /// Move this AnimatedObject toward the target (xt, yt) position, as defined below:
        /// 
        /// MotionParam1 (xt): Target X coordinate.
        /// MotionParam2 (yt): Target Y coordinate.
        /// MotionParam3 (oldStep): Old stepsize for this AnimatedObject.
        /// MotionParam4 (endFlag): Flag to set when this AnimatedObject reaches the target position.
        /// </summary>
        public void MoveTo()
        {
            // Get the direction to move.
            this.Direction = MoveDirection(this.X, this.Y, this.MotionParam1, this.MotionParam2, this.StepSize);

            // If this AnimatedObject is ego, set var[EGODIR]
            if (this.ObjectNumber == 0)
            {
                this.state.Vars[Defines.EGODIR] = this.Direction;
            }

            // If 0, signal completion.
            if (this.Direction == 0)
            {
                EndMoveObj();
            }
        }

        /// <summary>
        /// Starts the MoveTo motion for this AnimatedObject.
        /// </summary>
        /// <param name="x">The x position to move to.</param>
        /// <param name="y">The y position to move to.</param>
        /// <param name="stepSize">The step size to use for the motion. If 0, then the current StepSize value for this AnimatedObject is used.</param>
        /// <param name="completionFlag">The flag number to set when the motion has completed.</param>
        public void StartMoveObj(byte x, byte y, byte stepSize, byte completionFlag)
        {
            this.MotionType = MotionType.MoveTo;
            this.MotionParam1 = x;
            this.MotionParam2 = y;
            this.MotionParam3 = this.StepSize;
            if (stepSize != 0)
            {
                this.StepSize = stepSize;
            }
            this.MotionParam4 = completionFlag;
            state.Flags[completionFlag] = false;
            this.Update = true;
            if (this == state.Ego)
            {
                state.UserControl = false;
            }
            this.MoveTo();
        }

        /// <summary>
        /// Ends the MoveTo motion for this AnimatedObject.
        /// </summary>
        private void EndMoveObj()
        {
            // Restore old step size.
            this.StepSize = (byte)this.MotionParam3;

            // Set flag indicating completion.
            this.state.Flags[this.MotionParam4] = true;

            // Set it back to normal motion.
            this.MotionType = MotionType.Normal;

            // If this AnimatedObject is ego, then give back user control.
            if (this.ObjectNumber == 0)
            {
                state.UserControl = true;
                state.Vars[Defines.EGODIR] = 0;
            }
        }

        /// <summary>
        /// A block is in effect and the object must observe blocks. Check to see
        /// if the object can move in its current direction.
        /// </summary>
        private void CheckBlock()
        {
            bool objInBlock;
            short ox, oy;

            // Get obj coord into temp vars and determine if the object is
            // currently within the block.
            ox = this.X;
            oy = this.Y;

            objInBlock = InBlock(ox, oy);

            // Get object coordinate after moving.
            switch (this.Direction)
            {
                case 1:
                    oy -= this.StepSize;
                    break;

                case 2:
                    ox += this.StepSize;
                    oy -= this.StepSize;
                    break;

                case 3:
                    ox += this.StepSize;
                    break;

                case 4:
                    ox += this.StepSize;
                    oy += this.StepSize;
                    break;

                case 5:
                    oy += this.StepSize;
                    break;

                case 6:
                    ox -= this.StepSize;
                    oy += this.StepSize;
                    break;

                case 7:
                    ox -= this.StepSize;
                    break;

                case 8:
                    ox -= this.StepSize;
                    oy -= this.StepSize;
                    break;
            }

            // If moving the object will not change its 'in block' status, let it move.
            if (objInBlock == InBlock(ox, oy))
            {
                this.Blocked = false;
            }
            else
            {
                this.Blocked = true;
                this.Direction = 0;

                // When Ego is the blocked object also set ego's direction to zero.
                if (this.ObjectNumber == 0)
                {
                    state.Vars[Defines.EGODIR] = 0;
                }
            }
        }

        /// <summary>
        /// Tests if the currently active block contains the given X/Y position. Ths method should
        /// not be called unless a block has been set.
        /// </summary>
        /// <param name="x">The X position to test.</param>
        /// <param name="y">The Y position to test.</param>
        /// <returns></returns>
        private bool InBlock(short x, short y)
        {
            return (x > state.BlockUpperLeftX && x < state.BlockLowerRightX && y > state.BlockUpperLeftY && y < state.BlockLowerRightY);
        }

        private static short[] xs = { 0, 0, 1, 1, 1, 0, -1, -1, -1 };
        private static short[] ys = { 0, -1, -1, 0, 1, 1, 1, 0, -1 };

        /// <summary>
        /// Updates this AnimatedObject's position on the screen according to its current state.
        /// </summary>
        public void UpdatePosition()
        {
            if (Animated && Update && Drawn) {
                // Decrement the move clock for this object.  Don't move the object unless
                // the clock has reached 0.
                if ((StepTimeCount != 0) && (--StepTimeCount != 0)) return;

                // Reset the move clock.
                StepTimeCount = StepTime;

                // Clear border collision flag.
                byte border = 0;

                short ox = this.X;
                short px = this.X;
                short oy = this.Y;
                short py = this.Y;
                byte od = 0;
                byte os = 0;

                // If object has not been repositioned, move it.
                if (!this.Repositioned) {
                    od = this.Direction;
                    os = this.StepSize;
                    ox += (short)(xs[od] * os);
                    oy += (short)(ys[od] * os);
                }

                // Check for object border collision.
                if (ox < Defines.MINX)
                {
                    ox = Defines.MINX;
                    border = Defines.LEFT;
                }
                else if (ox + this.XSize > Defines.MAXX + 1)
                {
                    ox = (short)(Defines.MAXX + 1 - this.XSize);
                    border = Defines.RIGHT;
                }
                if (oy - this.YSize < Defines.MINY - 1)
                {
                    oy = (short)(Defines.MINY - 1 + this.YSize);
                    border = Defines.TOP;
                }
                else if (oy > Defines.MAXY)
                {
                    oy = Defines.MAXY;
                    border = Defines.BOTTOM;
                }
                else if (!IgnoreHorizon && (oy <= state.Horizon))
                {
                    oy = (short)(state.Horizon + 1);
                    border = Defines.TOP;
                }

                // Update X and Y to the new position.
                this.X = ox;
                this.Y = oy;

                // If object can't be in this position, then move back to previous
                // position and clear the border collision flag
                if (Collide() || !CanBeHere())
                {
                    this.X = px;
                    this.Y = py;
                    border = 0;

                    // Make sure that this position is OK
                    FindPosition();
                }

                // If the object hit the border, set the appropriate flags.
                if (border > 0)
                {
                    if (this.ObjectNumber == 0)
                    {
                        state.Vars[Defines.EGOEDGE] = border;
                    }
                    else
                    {
                        state.Vars[Defines.OBJHIT] = this.ObjectNumber;
                        state.Vars[Defines.OBJEDGE] = border;
                    }

                    // If the object was on a 'moveobj', set the move as finished.
                    if (this.MotionType == MotionType.MoveTo)
                    {
                        EndMoveObj();
                    }
                }

                // If object was not to be repositioned, it can be repositioned from now on.
                this.Repositioned = false;
            }
        }

        /// <summary>
        /// Return true if the object's position puts it on the screen; false otherwise.
        /// </summary>
        /// <returns>true if the object's position puts it on the screen; false otherwise.</returns>
        private bool GoodPosition()
        {
            return ((this.X >= Defines.MINX) && ((this.X + this.XSize) <= Defines.MAXX + 1) && 
                ((this.Y - this.YSize) >= Defines.MINY - 1) && (this.Y <= Defines.MAXY) &&
                (this.IgnoreHorizon || this.Y > state.Horizon));
        }

        /// <summary>
        /// Find a position for this AnimatedObject where it does not collide with any
        /// unappropriate objects or priority regions.  If the object can't be in
        /// its current position, then start scanning in a spiral pattern for a position
        /// at which it can be placed.
        /// </summary>
        public void FindPosition()
        {
            // Place Y below horizon if it is above it and is not ignoring the horizon.
            if ((this.Y <= state.Horizon) && !this.IgnoreHorizon)
            {
                this.Y = (short)(state.Horizon + 1);
            }

            // If current position is OK, return.
            if (GoodPosition() && !Collide() && CanBeHere())
            {
                return;
            }

            // Start scan.
            int legLen = 1, legDir = 0, legCnt = 1;

            while (!GoodPosition() || Collide() || !CanBeHere())
            {
                switch (legDir)
                {
                    case 0:         // Move left.
                        --this.X;

                        if (--legCnt == 0)
                        {
                            legDir = 1;
                            legCnt = legLen;
                        }
                        break;

                    case 1:         // Move down.
                        ++this.Y;

                        if (--legCnt == 0)
                        {
                            legDir = 2;
                            legCnt = ++legLen;
                        }
                        break;

                    case 2:         // Move right.
                        ++this.X;

                        if (--legCnt == 0)
                        {
                            legDir = 3;
                            legCnt = legLen;
                        }
                        break;

                    case 3:         // Move up.
                        --this.Y;

                        if (--legCnt == 0)
                        {
                            legDir = 0;
                            legCnt = ++legLen;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Checks if this AnimatedObject has collided with another AnimatedObject.
        /// </summary>
        /// <returns>true if collided with another AnimatedObject; otherwise false.</returns>
        private bool Collide()
        {
            // If AnimatedObject is ignoring objects this return false.
            if (this.IgnoreObjects)
            {
                return false;
            }

            foreach (AnimatedObject otherObj in state.AnimatedObjects)
            {
                // Collision with another object if:
                //	- other object is animated and drawn
                //	- other object is not ignoring objects
                //	- other object is not this object
                //	- the two objects have overlapping baselines
                if (otherObj.Animated && otherObj.Drawn && 
                    !otherObj.IgnoreObjects && 
                    (this.ObjectNumber != otherObj.ObjectNumber) && 
                    (this.X + this.XSize >= otherObj.X) && 
                    (this.X <= otherObj.X + otherObj.XSize))

                    // At this point, the two objects have overlapping
                    // x coordinates. A collision has occurred if they have
                    // the same y coordinate or if the object in question has
                    // moved across the other object in the last animation cycle
                    if ((this.Y == otherObj.Y) || 
                        (this.Y > otherObj.Y && this.PrevY < otherObj.PrevY) || 
                        (this.Y < otherObj.Y && this.PrevY > otherObj.PrevY))
                    {
                        return true;
                    }
            }

            return false;
        }

        /// <summary>
        /// For the given y value, calculates what the priority value should be.
        /// </summary>
        /// <param name="y"></param>
        /// <returns></returns>
        private byte CalculatePriority(int y)
        {
            return (byte)(y < state.PriorityBase ? Defines.BACK_MOST_PRIORITY : (byte)(((y - state.PriorityBase) / ((168.0 - state.PriorityBase) / 10.0f)) + 5));
        }

        /// <summary>
        /// Return the effective Y for this Animated Object, which is Y if the priority is not fixed, or if it
        /// is fixed then is the value corresponding to the start of the fixed priority band.
        /// </summary>
        /// <returns></returns>
        private short EffectiveY()
        {
            // IMPORTANT: When in fixed priority mode, it uses the "top" of the priority band, not the bottom, i.e. the "start" is the top.
            return (FixedPriority ? (short)(state.PriorityBase + Math.Ceiling(((168.0 - state.PriorityBase) / 10.0f) * (Priority - Defines.BACK_MOST_PRIORITY - 1))) : Y);
        }

        /// <summary>
        /// Checks if this AnimatedObject can be in its current position according to
        /// the control lines. Normally this method would be invoked immediately after
        /// setting its position to a newly calculated position.
        /// 
        /// There are a number of side effects to calling this method, and in fact 
        /// it is responsible for performing these updates:
        /// 
        /// - It sets the priority value for the current Y position.
        /// - It sets the on.water flag, if applicable.
        /// - It sets the hit.special flag, if applicable.
        /// </summary>
        /// <returns>true if it can be in the current position; otherwise false.</returns>
        private bool CanBeHere()
        {
            bool canBeHere = true;
            bool entirelyOnWater = false;
            bool hitSpecial = false;

            // If the priority is not fixed, calculate the priority based on current Y position.
            if (!this.FixedPriority)
            {
                // NOTE: The following table only applies to games that don't support the ability to change the PriorityBase.
                // Priority Band   Y range
                // ------------------------
                //       4 -
                //       5          48 - 59
                //       6          60 - 71
                //       7          72 - 83
                //       8          84 - 95
                //       9          96 - 107
                //      10         108 - 119
                //      11         120 - 131
                //      12         132 - 143
                //      13         144 - 155
                //      14         156 - 167
                //      15            168
                // ------------------------
                this.Priority = CalculatePriority(Y);
            }

            // Priority 15 skips the whole base line testing. None of the control lines
            // have any affect.
            if (this.Priority != 15)
            {
                // Start by assuming we're on water. Will be set false if it turns out we're not.
                entirelyOnWater = true;

                // Loop over the priority screen pixels for the area overed by this
                // object's base line.
                int startPixelPos = (Y * 160) + X;
                int endPixelPos = startPixelPos + XSize;

                for (int pixelPos = startPixelPos; pixelPos < endPixelPos; pixelPos++) {
                    // Get the priority screen priority value for this pixel of the base line.
                    int priority = state.ControlPixels[pixelPos];

                    if (priority != 3)
                    {
                        // This pixel is not water (i.e. not 3), so it can't be entirely on water.
                        entirelyOnWater = false;

                        if (priority == 0)
                        {
                            // Permanent block.
                            canBeHere = false;
                            break;
                        }
                        else if (priority == 1)
                        {
                            // Blocks if the AnimatedObject isn't ignoring blocks.
                            if (!IgnoreBlocks)
                            {
                                canBeHere = false;
                                break;
                            }
                        }
                        else if (priority == 2)
                        {
                            hitSpecial = true;
                        }
                    }
                }

                if (entirelyOnWater)
                {
                    if (this.StayOnLand)
                    {
                        // Must not be entirely on water, so can't be here.
                        canBeHere = false;
                    }
                }
                else
                {
                    if (this.StayOnWater)
                    {
                        canBeHere = false;
                    }
                }
            }

            // If the object is ego then we need to determine the on.water and hit.special flag values.
            if (this.ObjectNumber == 0)
            {
                state.Flags[Defines.ONWATER] = entirelyOnWater;
                state.Flags[Defines.HITSPEC] = hitSpecial;
            }

            return canBeHere;
        }

        // Object views -- Same, Right, Left, Front, Back.
        private const byte S = 4;
        private const byte R = 0;
        private const byte L = 1;
        private const byte F = 2;
        private const byte B = 3;
        private static byte[] twoLoop = { S, S, R, R, R, S, L, L, L };
        private static byte[] fourLoop = { S, B, R, R, R, F, L, L, L };

        /// <summary>
        /// Updates the loop and cel numbers based on the AnimatedObjects current state.
        /// </summary>
        public void UpdateLoopAndCel()
        {
            byte newLoop = 0;

            if (Animated && Update && Drawn)
            {
                // Get the appropriate loop based on the current direction.
                newLoop = S;

                if (!FixedLoop)
                {
                    if (NumberOfLoops == 2 || NumberOfLoops == 3)
                    {
                        newLoop = twoLoop[Direction];
                    }
                    else if (NumberOfLoops == 4)
                    {
                        newLoop = fourLoop[Direction];
                    }
                    else if ((NumberOfLoops > 4) && (state.GameId.Equals("KQ4")))
                    {
                        // Main Ego View (0) in KQ4 has 5 loops, but is expected to automatically change
                        // loop in sync with the Direction, in the same way as if it had only 4 loops.
                        newLoop = fourLoop[Direction];
                    }
                }

                // If the object is to move in this cycle and the loop has changed, point to the new loop.
                if ((StepTimeCount == 1) && (newLoop != S) && (CurrentLoop != newLoop))
                {
                    SetLoop(newLoop);
                }

                // If it is time to cycle the object, advance it's cel.
                if (Cycle && (CycleTimeCount > 0) && (--CycleTimeCount == 0))
                {
                    AdvanceCel();

                    CycleTimeCount = CycleTime;
                }
            }
        }

        /// <summary>
        /// Determine which cel of an object to display next.
        /// </summary>
        public void AdvanceCel()
        {
            byte theCel;
            byte lastCel;

            if (NoAdvance)
            {
                NoAdvance = false;
                return;
            }

            // Advance to the next cel in the loop.
            theCel = CurrentCel;
            lastCel = (byte)(NumberOfCels - 1);

            switch (CycleType)
            {
                case CycleType.Normal:
                    // Move to the next sequential cel.
                    if (++theCel > lastCel)
                    {
                        theCel = 0;
                    }
                    break;

                case CycleType.EndLoop:
                    // Advance to the end of the loop, set flag in parms[0] when done
                    if (theCel >= lastCel || ++theCel == lastCel)
                    {
                        state.Flags[MotionParam1] = true;
                        Cycle = false;
                        Direction = 0;
                        CycleType = CycleType.Normal;
                    }
                    break;

                case CycleType.ReverseLoop:
                    // Move backwards, celwise, until beginning of loop, then set flag.
                    if (theCel == 0 || --theCel == 0)
                    {
                        state.Flags[MotionParam1] = true;
                        Cycle = false;
                        Direction = 0;
                        CycleType = CycleType.Normal;
                    }
                    break;

                case CycleType.Reverse:
                    // Cycle continually, but from end of loop to beginning.
                    if (theCel > 0)
                    {
                        --theCel;
                    }
                    else
                    {
                        theCel = lastCel;
                    }
                    break;
            }

            // Get pointer to the new cel and set cel dimensions.
            SetCel(theCel);
        }

        /// <summary>
        /// Adds this AnimatedObject as a permanent part of the current picture. If the priority parameter
        /// is 0, the object's priority is that of the priority band in which it is placed; otherwise it
        /// will be set to the specified priority value. If the controlBoxColour parameter is below 4, 
        /// then a control line box is added to the control screen of the specified control colour value,
        /// which extends from the object's baseline to the bottom of the next lowest priority band. If
        /// this control box priority is set to 0, then obviously this would prevent animated objects from
        /// walking through it. The other 3 control colours have their normal behaviours as well. The
        /// add.to.pic objects ignore all control lines, all base lines of other objects, and the "block"
        /// if one is active...   i.e. it can go anywhere in the picture. Once added, it is not animated
        /// and cannot be erased ecept by drawing something over it. It effectively becomes part of the 
        /// picture.
        /// </summary>
        /// <param name="viewNum"></param>
        /// <param name="loopNum"></param>
        /// <param name="celNum"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="priority"></param>
        /// <param name="controlBoxColour"></param>
        /// <param name="pixels"></param>
        public void AddToPicture(byte viewNum, byte loopNum, byte celNum, byte x, byte y, byte priority, byte controlBoxColour, int[] pixels)
        {
            // Add the add.to.pic details to the script event buffer.
            state.ScriptBuffer.AddScript(ScriptBuffer.ScriptBufferEventType.AddToPic, 0, new byte[] {
                viewNum, loopNum, celNum, x, y, (byte)(priority | (controlBoxColour << 4))
            });

            // Set the view, loop, and cel to those specified.
            SetView(viewNum);
            SetLoop(loopNum);
            SetCel(celNum);

            // Set PreviousCel to current Cel for Show call.
            this.PreviousCel = this.Cel;

            // Place the add.to.pic at the specified position. This may not be fully within the
            // screen bounds, so a call below to FindPosition is made to resolve this.
            this.X = this.PrevX = x;
            this.Y = this.PrevY = y;

            // In order to make use of FindPosition, we set these flags to disable certain parts
            // of the FindPosition functionality that don't apply to add.to.pic objects.
            this.IgnoreHorizon = true;
            this.FixedPriority = true;
            this.IgnoreObjects = true;

            // And we set the priority temporarily to 15 so that when FindPosition is doing its thing,
            // the control lines will be ignored, as they have no effect on add.to.pic objects.
            this.Priority = 15;

            // Now we call FindPosition to adjust the object's position if it has been placed either 
            // partially or fully outside of the picture area.
            FindPosition();

            // Having checked and (if appropriate) adjusted the position, we can now work out what the
            // object priority should be.
            if (priority == 0)
            {
                // If the specified priority is 0, it means that the priority should be calculated 
                // from the object's Y position as would normally happen if its priority is not fixed.
                this.Priority = CalculatePriority(Y);
            }
            else
            {
                // Otherwise it will be set to the specified value.
                this.Priority = priority;
            }

            this.ControlBoxColour = controlBoxColour;

            // Draw permanently to the CurrentPicture, including the control box.
            Draw(state.CurrentPicture);

            // Restore backgrounds, add add.to.pic to VisualPixels, then redraw AnimatedObjects and show updated area.
            state.RestoreBackgrounds();
            Draw();
            state.DrawObjects();
            Show(pixels);
        }

        /// <summary>
        /// Set the Cel of this AnimatedObject to the given cel number.
        /// </summary>
        /// <param name="celNum">The cel number within the current Loop to set the Cel to.</param>
        public void SetCel(byte celNum)
        {
            // Set the cel number. 
            this.CurrentCel = celNum;

            // The border collision can only be performed if a valid combination of loops and cels has been set.
            if ((this.CurrentLoop < this.NumberOfLoops) && (this.CurrentCel < this.NumberOfCels))
            {
                // Make sure that the new cel size doesn't cause a border collision.
                if (this.X + this.XSize > Defines.MAXX + 1)
                {
                    // Don't let the object move.
                    this.Repositioned = true;
                    this.X = (short)(Defines.MAXX - this.XSize);
                }

                if (this.Y - this.YSize < Defines.MINY - 1)
                {
                    this.Repositioned = true;
                    this.Y = (short)(Defines.MINY - 1 + this.YSize);

                    if (this.Y <= state.Horizon && !this.IgnoreHorizon)
                    {
                        this.Y = (short)(state.Horizon + 1);
                    }
                }
            }
        }

        /// <summary>
        /// Set the loop of this AnimatedObject to the given loop number.
        /// </summary>
        /// <param name="loopNum">The loop number within the current View to set the Loop to.</param>
        public void SetLoop(byte loopNum)
        {
            this.CurrentLoop = loopNum;

            // If the current cel # is greater than the cel count for this loop, set
            // it to 0, otherwise leave it alone. Sometimes the loop number is set before
            // the associated view number is set. We allow for this in the check below.
            if ((this.CurrentLoop >= this.NumberOfLoops) || (this.CurrentCel >= this.NumberOfCels))
            {
                this.CurrentCel = 0;
            }

            this.SetCel(this.CurrentCel);
        }

        /// <summary>
        /// Set the number of the View for this AnimatedObject to use.
        /// </summary>
        /// <param name="viewNum">The number of the View for this AnimatedObject to use.</param>
        public void SetView(byte viewNum)
        {
            this.CurrentView = viewNum;

            // If the current loop is greater than the number of loops for the view,
            // set the loop number to 0.  Otherwise, leave it alone.
            SetLoop(CurrentLoop >= NumberOfLoops? (byte)0 : CurrentLoop);
        }

        /// <summary>
        /// Performs an animate.obj on this AnimatedObject.
        /// </summary>
        public void Animate()
        {
            if (!Animated)
            {
                // Most flags are reset to false.
                this.IgnoreBlocks = false;
                this.FixedPriority = false;
                this.IgnoreHorizon = false;
                this.Cycle = false;
                this.Blocked = false;
                this.StayOnLand = false;
                this.StayOnWater = false;
                this.IgnoreObjects = false;
                this.Repositioned = false;
                this.NoAdvance = false;
                this.FixedLoop = false;
                this.Stopped = false;

                // But these ones are specifying set to true.
                this.Animated = true;
                this.Update = true;
                this.Cycle = true;

                this.MotionType = MotionType.Normal;
                this.CycleType = CycleType.Normal;
                this.Direction = 0;
            }
        }

        /// <summary>
        /// Repositions the object by the deltaX and deltaY values.
        /// </summary>
        /// <param name="deltaX">Delta for the X position (signed, where negative is to the left)</param>
        /// <param name="deltaY">Delta for the Y position (signed, where negative is to the top)</param>
        public void Reposition(sbyte deltaX, sbyte deltaY)
        {
            this.Repositioned = true;

            if ((deltaX < 0) && (this.X < -deltaX))
            {
                this.X = 0;
            }
            else
            {
                this.X = (short)(this.X + deltaX);
            }

            if ((deltaY < 0) && (this.Y < -deltaY))
            {
                this.Y = 0;
            }
            else
            {
                this.Y = (short)(this.Y + deltaY);
            }

            // Make sure that this position is OK
            FindPosition();
        }

        /// <summary>
        /// Calculates the distance between this AnimatedObject and the given AnimatedObject.
        /// </summary>
        /// <param name="aniObj">The AnimatedObject to calculate the distance to.</param>
        /// <returns></returns>
        public byte Distance(AnimatedObject aniObj)
        {
            if (!this.Drawn || !aniObj.Drawn)
            {
                return Defines.MAXVAR;
            }
            else
            {
                int dist = Math.Abs((this.X + this.XSize / 2) - (aniObj.X + aniObj.XSize / 2)) + Math.Abs(this.Y - aniObj.Y);
                return (byte)((dist > 254) ? 254 : dist);
            }
        }

        /// <summary>
        /// Draws this AnimatedObject to the pixel arrays of the given Picture. This is intended for use by 
        /// add.to.pic objects, which is a specialist static type of AnimatedObject that becomes a permanent
        /// part of the Picture.
        /// </summary>
        /// <param name="picture"></param>
        public void Draw(Picture picture)
        {
            Bitmap visualBitmap = picture.Screen.VisualBitmap;
            Bitmap priorityBitmap = picture.Screen.PriorityBitmap;

            // Copy cell pixel data in to a byte array that we can directly access in a safe way.
            Bitmap cellBitmap = this.Cel.Screen.Bitmap;
            int cellWidth = cellBitmap.Width;
            int cellHeight = cellBitmap.Height;
            BitmapData cellBitmapData = cellBitmap.LockBits(new Rectangle(0, 0, cellWidth, cellHeight), ImageLockMode.ReadWrite, cellBitmap.PixelFormat);
            byte[] cellPixels = new byte[cellBitmapData.Stride * cellHeight];
            Marshal.Copy(cellBitmapData.Scan0, cellPixels, 0, cellPixels.Length);
            cellBitmap.UnlockBits(cellBitmapData);

            // Likewise copy visual pixels to a 160x168 byte array.
            BitmapData visualBitmapData = visualBitmap.LockBits(new Rectangle(0, 0, visualBitmap.Width, visualBitmap.Height), ImageLockMode.ReadWrite, visualBitmap.PixelFormat);
            byte[] visualPixels = new byte[visualBitmapData.Stride * visualBitmapData.Height];
            Marshal.Copy(visualBitmapData.Scan0, visualPixels, 0, visualPixels.Length);

            // And copy priority pixels to a 160x168 byte array.
            BitmapData priorityBitmapData = priorityBitmap.LockBits(new Rectangle(0, 0, priorityBitmap.Width, priorityBitmap.Height), ImageLockMode.ReadWrite, priorityBitmap.PixelFormat);
            byte[] priorityPixels = new byte[priorityBitmapData.Stride * priorityBitmapData.Height];
            Marshal.Copy(priorityBitmapData.Scan0, priorityPixels, 0, priorityPixels.Length);

            // Get the transparency colour index. We'll use this to ignore pixels this colour.
            byte transIndex = this.Cel.TransparentColor.ColorIndex;

            // Calculate starting position within the pixel arrays.
            int aniObjTop = ((this.Y - cellHeight) + 1);
            int screenPos = (aniObjTop * 160) + this.X;
            int screenLineAdd = 160 - cellWidth;

            int cellPos = 0;
            int cellLineAdd = (cellBitmapData.Stride - cellWidth);

            // Iterate over each of the pixels and decide if the priority screen allows the pixel
            // to be drawn or not when adding them in to the VisualPixels and PriorityPixels arrays. 
            for (int y = 0; y < cellHeight; y++, screenPos += screenLineAdd, cellPos += cellLineAdd)
            {
                for (int x = 0; x < cellWidth; x++, screenPos++, cellPos++)
                {
                    // Check that the pixel is within the bounds of the AGI picture area.
                    if (((aniObjTop + y) >= 0) && ((aniObjTop + y) < 168) && ((this.X + x) >= 0) && ((this.X + x) < 160))
                    {
                        // Get the priority colour index for this position from the priority screen.
                        int priorityIndex = priorityPixels[screenPos];

                        // If this AnimatedObject's priority is greater or equal to the priority screen value
                        // for this pixel's position, then we'll draw it.
                        if (this.Priority >= priorityIndex)
                        {
                            // Get the colour index from the Cell bitmap pixels.
                            byte colourIndex = cellPixels[cellPos];

                            // If the colourIndex is not the transparent index, then we'll draw the pixel.
                            if (colourIndex != transIndex)
                            {
                                visualPixels[screenPos] = colourIndex;
                                //  Replace the priority pixel only if the existing one is not a special priority pixel (0, 1, 2)
                                if (priorityIndex > 2)
                                {
                                    priorityPixels[screenPos] = this.Priority;
                                }
                            }
                        }
                    }
                }
            }

            // Draw the control box.
            if (ControlBoxColour <= 3)
            {
                // Calculate the height of the box.
                int yy = this.Y;
                byte priorityHeight = 0;
                byte objPriorityForY = CalculatePriority(this.Y);
                do
                {
                    priorityHeight++;
                    if (yy <= 0) break;
                    yy--;
                }
                while (CalculatePriority(yy) == objPriorityForY);
                byte height = (byte)(YSize > priorityHeight ? priorityHeight : YSize);

                // Draw bottom line.
                for (int i = 0; i < XSize; i++)
                {
                    priorityPixels[(this.Y * 160) + this.X + i] = ControlBoxColour;
                }

                if (height > 1)
                {
                    // Draw both sides.
                    for (int i = 1; i < height; i++)
                    {
                        priorityPixels[((this.Y - i) * 160) + this.X] = ControlBoxColour;
                        priorityPixels[((this.Y - i) * 160) + this.X + XSize - 1] = ControlBoxColour;
                    }

                    // Draw top line.
                    for (int i = 1; i < XSize - 1; i++)
                    {
                        priorityPixels[((this.Y - (height - 1)) * 160) + this.X + i] = ControlBoxColour;
                    }
                }
            }

            // Copy the modified visual and priority pixel arrays back in to the Bitmaps.
            Marshal.Copy(visualPixels, 0, visualBitmapData.Scan0, visualPixels.Length);
            Marshal.Copy(priorityPixels, 0, priorityBitmapData.Scan0, priorityPixels.Length);
            visualBitmap.UnlockBits(visualBitmapData);
            priorityBitmap.UnlockBits(priorityBitmapData);
        }

        /// <summary>
        /// Draws this AnimatedObject to the VisualPixels pixels array.
        /// </summary>
        public void Draw()
        {
            // Start by copying the cell pixel data in to a byte array that we can directly access in a safe way.
            Bitmap cellBitmap = this.Cel.Screen.Bitmap;
            int cellWidth = cellBitmap.Width;
            int cellHeight = cellBitmap.Height;
            BitmapData cellBitmapData = cellBitmap.LockBits(new Rectangle(0, 0, cellWidth, cellHeight), ImageLockMode.ReadWrite, cellBitmap.PixelFormat);
            byte[] cellPixels = new byte[cellBitmapData.Stride * cellHeight];
            Marshal.Copy(cellBitmapData.Scan0, cellPixels, 0, cellPixels.Length);
            cellBitmap.UnlockBits(cellBitmapData);

            // Get the transparency colour index. We'll use this to ignore pixels this colour.
            byte transIndex = this.Cel.TransparentColor.ColorIndex;

            // Calculate starting screen offset. AGI pixels are 2x1 within the picture area.
            int aniObjTop = ((this.Y - cellHeight) + 1);
            int screenPos = (aniObjTop * 320) + (this.X * 2);
            int screenLineAdd = 320 - (cellWidth << 1);

            // Calculate starting position within the priority screen.
            int priorityPos = (aniObjTop * 160) + this.X;
            int priorityLineAdd = 160 - cellWidth;

            // Position within the cell pixels depends on whether it is mirrored or not.
            bool mirrored = (this.Cel.IsMirrored && (this.Cel.MirrorOf != this.CurrentLoop));
            int cellPos = (mirrored ? cellWidth - 1 : 0);
            int cellXAdd = (mirrored ? -1 : 1);
            int cellYAdd = (cellBitmapData.Stride + (mirrored ? (cellWidth) : -cellWidth));

            // Allocate new background pixel array for the current cell size.
            this.SaveArea.VisBackPixels = new int[cellWidth, cellHeight];
            this.SaveArea.PriBackPixels = new int[cellWidth, cellHeight];
            this.SaveArea.X = this.X;
            this.SaveArea.Y = this.Y;

            // Iterate over each of the pixels and decide if the priority screen allows the pixel
            // to be drawn or not. Deliberately tried to avoid multiplication within the loops.
            for (int y = 0; y < cellHeight; y++, screenPos += screenLineAdd, priorityPos += priorityLineAdd, cellPos += cellYAdd)
            {
                for (int x = 0; x < cellWidth; x++, screenPos += 2, priorityPos++, cellPos += cellXAdd)
                {
                    // Check that the pixel is within the bounds of the AGI picture area.
                    if (((aniObjTop + y) >= 0) && ((aniObjTop + y) < 168) && ((this.X + x) >= 0) && ((this.X + x) < 160))
                    {
                        // Store the background pixel. Should be the same colour in both pixels.
                        this.SaveArea.VisBackPixels[x, y] = state.VisualPixels[screenPos];
                        this.SaveArea.PriBackPixels[x, y] = state.PriorityPixels[priorityPos];

                        // Get the priority colour index for this position from the priority screen.
                        int priorityIndex = state.PriorityPixels[priorityPos];

                        // If this AnimatedObject's priority is greater or equal to the priority screen value
                        // for this pixel's position, then we'll draw it.
                        if (this.Priority >= priorityIndex)
                        {
                            // Get the colour index from the Cell bitmap pixels.
                            int colourIndex = cellPixels[cellPos];

                            // If the colourIndex is not the transparent index, then we'll draw the pixel.
                            if (colourIndex != transIndex)
                            {
                                // Get the ARGB value from the AGI Color Palette.
                                int colorArgb = AGI.Color.Palette[colourIndex].ToArgb();

                                // Draw two pixels (due to AGI picture pixels being 2x1).
                                state.VisualPixels[screenPos] = colorArgb;
                                state.VisualPixels[screenPos + 1] = colorArgb;

                                // Priority screen is only stored 160x168 though.
                                state.PriorityPixels[priorityPos] = this.Priority;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Restores the current background pixels to the previous position of this AnimatedObject.
        /// </summary>
        public void RestoreBackPixels()
        {
            if ((SaveArea.VisBackPixels != null) && (SaveArea.PriBackPixels != null))
            {
                int saveWidth = SaveArea.VisBackPixels.GetLength(0);
                int saveHeight = SaveArea.VisBackPixels.GetLength(1);
                int aniObjTop = ((SaveArea.Y - saveHeight) + 1);
                int screenPos = (aniObjTop * 320) + (SaveArea.X * 2);
                int screenLineAdd = 320 - (saveWidth << 1);
                int priorityPos = (aniObjTop * 160) + SaveArea.X;
                int priorityLineAdd = 160 - saveWidth;

                for (int y = 0; y < saveHeight; y++, screenPos += screenLineAdd, priorityPos += priorityLineAdd)
                {
                    for (int x = 0; x < saveWidth; x++, screenPos += 2, priorityPos++)
                    {
                        if (((aniObjTop + y) >= 0) && ((aniObjTop + y) < 168) && ((SaveArea.X + x) >= 0) && ((SaveArea.X + x) < 160))
                        {
                            state.VisualPixels[screenPos] = SaveArea.VisBackPixels[x, y];
                            state.VisualPixels[screenPos + 1] = SaveArea.VisBackPixels[x, y];
                            state.PriorityPixels[priorityPos] = SaveArea.PriBackPixels[x, y];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Shows the AnimatedObject by blitting the bounds of its current and previous cels to the screen 
        /// pixels. The include the previous cel so that we pick up the restoration of the save area.
        /// </summary>
        /// <param name="pixels">The screen pixels to blit the AnimatedObject to.</param>
        public void Show(int[] pixels)
        {
            // We will only render an AnimatedObject to the screen if the picture is currently visible.
            if (state.PictureVisible)
            {
                // Work out the rectangle that covers the previous and current cells.
                int prevCelWidth = (this.PreviousCel != null ? this.PreviousCel.Screen.Bitmap.Width : this.XSize);
                int prevCelHeight = (this.PreviousCel != null? this.PreviousCel.Screen.Bitmap.Height : this.YSize);
                int prevX = (this.PreviousCel != null ? this.PrevX : this.X);
                int prevY = (this.PreviousCel != null ? this.PrevY : this.Y);
                int leftmostX = Math.Min(prevX, this.X);
                int rightmostX = Math.Max(prevX + prevCelWidth, this.X + this.XSize) - 1;
                int topmostY = Math.Min(prevY - prevCelHeight, this.Y - this.YSize) + 1;
                int bottommostY = Math.Max(prevY, this.Y);

                // We no longer need the PreviousCel, so point it at the new one.
                this.PreviousCel = this.Cel;

                int height = (bottommostY - topmostY) + 1;
                int width = ((rightmostX - leftmostX) + 1) * 2;
                int picturePos = (topmostY * 320) + (leftmostX * 2);
                int pictureLineAdd = 320 - width;
                int screenPos = picturePos + (state.PictureRow * 8 * 320);

                for (int y = 0; y < height; y++, picturePos += pictureLineAdd, screenPos += pictureLineAdd)
                {
                    for (int x = 0; x < width; x++, screenPos++, picturePos++)
                    {
                        if (((topmostY + y) >= 0) && ((topmostY + y) < 168) && ((leftmostX + x) >= 0) && ((leftmostX + x) < 320) && (screenPos >= 0) && (screenPos < pixels.Length))
                        {
                            try
                            {
                                pixels[screenPos] = state.VisualPixels[picturePos];
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("ex: " + ex.Message);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Used to sort by drawing order when drawing AnimatedObjects to the screen. When 
        /// invoked, it compares the other AnimatedObject with this one and says which is in
        /// front and which is behind. Since we want to draw those with lowest priority first, 
        /// and if their priority is equal then lowest Y, then this is what determines whether
        /// we return a negative value, equal, or greater.
        /// </summary>
        /// <param name="other">The other AnimatedObject to compare this one to.</param>
        /// <returns></returns>
        public int CompareTo(AnimatedObject other)
        {
            if (this.Priority < other.Priority)
            {
                return -1;
            }
            else if (this.Priority > other.Priority)
            {
                return 1;
            }
            else
            {
                if (this.EffectiveY() < other.EffectiveY())
                {
                    return -1;
                }
                else if (this.EffectiveY() > other.EffectiveY())
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Gets the core status of the object in the status string format used by the AGI
        /// debug mode.
        /// </summary>
        /// <returns></returns>
        public string GetStatusStr()
        {
            return String.Format(
                "Object {0}:\nx: {1}  xsize: {2}\ny: {3}  ysize: {4}\npri: {5}\nstepsize: {6}",
                ObjectNumber, X, XSize, Y, YSize, Priority, StepSize);
        }
    }

    /// <summary>
    /// Holds data about an AnimatedObject's background save area.
    /// </summary>
    public class SaveArea
    {
        public short X { get; set; }
        public short Y { get; set; }
        public int[,] VisBackPixels { get; set; }
        public int[,] PriBackPixels { get; set; }
    }

    /// <summary>
    /// An enum that defines the types of motion that an AnimatedObject can have.
    /// </summary>
    public enum MotionType
    {
        /// <summary>
        /// AnimatedObject is using the normal motion.
        /// </summary>
        Normal,

        /// <summary>
        /// AnimatedObject randomly moves around the screen.
        /// </summary>
        Wander,

        /// <summary>
        /// AnimatedObject follows another AnimatedObject.
        /// </summary>
        Follow,

        /// <summary>
        /// AnimatedObject is moving to a given coordinate.
        /// </summary>
        MoveTo
    }

    /// <summary>
    /// An enum that defines the type of cel cycling that an AnimatedObject can have.
    /// </summary>
    public enum CycleType
    {
        /// <summary>
        /// Normal repetitive cycling of the AnimatedObject.
        /// </summary>
        Normal,

        /// <summary>
        /// Cycle to the end of the loop and then stop.
        /// </summary>
        EndLoop,

        /// <summary>
        /// Cycle in reverse order to the start of the loop and then stop.
        /// </summary>
        ReverseLoop,

        /// <summary>
        /// Cycle continually in reverse.
        /// </summary>
        Reverse
    }
}
