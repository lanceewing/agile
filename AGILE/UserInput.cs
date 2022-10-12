using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AGILE
{
    class UserInput
    {
        public const int ACCEPT = 0;
        public const int ABORT = 1;

        private const char ESC = (char)0x1b;

        /// <summary>
        /// A queue of all key presses that the user has made.
        /// </summary>
        public ConcurrentQueue<int> KeyPressQueue { get; set; }

        /// <summary>
        /// Current state of every key on the keyboard.
        /// </summary>
        public bool[] Keys { get; set; }

        /// <summary>
        /// Stores the state of every key on the previous cycle.
        /// </summary>
        public bool[] OldKeys { get; set; }

        /// <summary>
        /// Stores the current state of the key modifiers (i.e. Alt, Control, Shift)
        /// </summary>
        public int Modifiers { get; set; }

        /// <summary>
        /// A Map between IBM PC key codes as understood by the PC AGI interpreter and the C# Key codes.
        /// </summary>
        public Dictionary<int, int> KeyCodeMap { get; set; }

        public Dictionary<int, int> ReverseKeyCodeMap { get; set; }

        /// <summary>
        /// Constructor for Events.
        /// </summary>
        public UserInput()
        {
            this.Keys = new bool[256];
            this.OldKeys = new bool[256];
            this.KeyPressQueue = new ConcurrentQueue<int>();
            this.KeyCodeMap = CreateKeyConversionMap();
            this.ReverseKeyCodeMap = new Dictionary<int, int>();
            foreach (var entry in this.KeyCodeMap)
            {
                if (!ReverseKeyCodeMap.ContainsKey(entry.Value) && (entry.Value != 0))
                {
                    ReverseKeyCodeMap.Add(entry.Value, entry.Key);
                }
            }
        }

        /// <summary>
        /// Handles the key down event.
        /// </summary>
        /// <param name="e"></param>
        public void KeyDown(KeyEventArgs e)
        {
            this.Keys[(int)e.KeyCode & 0xFF] = true;
            this.Modifiers = (int)e.Modifiers;
            this.KeyPressQueue.Enqueue((int)e.KeyData);

            // F10 is a special key in Windows apps that shifts focus to the window menu. This
            // results in only every second F10 key event ending up being seen by AGILE. So to
            // prevent this, we suppress the keypress event when the key is F10 (see github Issue #2)
            if (e.KeyCode == System.Windows.Forms.Keys.F10)
            {
                e.SuppressKeyPress = true;
            }
        }

        /// <summary>
        /// Handles the key up event.
        /// </summary>
        /// <param name="e"></param>
        public void KeyUp(KeyEventArgs e)
        {
            this.Keys[(int)e.KeyCode & 0xFF] = false;
            this.Modifiers = (int)e.Modifiers;
        }

        /// <summary>
        /// Handles the key pressed event.
        /// </summary>
        /// <param name="e"></param>
        public void KeyPressed(KeyPressEventArgs e)
        {
            if ((e.KeyChar >= ' ') && (e.KeyChar <= '~'))
            {
                KeyPressQueue.Enqueue(0x80000 | (int)e.KeyChar);
            }
        }

        /// <summary>
        /// Wait for and return either ACCEPT or ABORT.
        /// </summary>
        /// <returns></returns>
        public int WaitAcceptAbort()
        {
            int action;
            int ignore;

            // Ignore anything currently on the key press queue.
            while (KeyPressQueue.TryDequeue(out ignore)) ;

            // Now wait for the the next key.
            while ((action = CheckAcceptAbort()) == -1) Thread.Sleep(1);

            return action;
        }

        /// <summary>
        /// Waits for the next key to be pressed then returns the value.
        /// </summary>
        /// <param name="clearQueue">Whether to clear what is on the queue before waiting.</param>
        /// <returns>The key that was pressed.</returns>
        public int WaitForKey(bool clearQueue = true)
        {
            int ignore;
            int key;

            if (clearQueue)
            {
                // Ignore anything currently on the key press queue.
                while (KeyPressQueue.TryDequeue(out ignore)) ;
            }

            // Now wait for the the next key.
            while ((key = GetKey()) == 0) Thread.Sleep(1);

            return key;
        }

        /// <summary>
        /// Check if either ACCEPT or ABORT has been selected. Return the value if so, -1 otherwise.
        /// </summary>
        /// <returns></returns>
        public int CheckAcceptAbort()
        {
            int c;

            if ((c = GetKey()) == '\r')
            {
                return ACCEPT;
            }
            else if (c == ESC)
            {
                return ABORT;
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// Gets a key from the key queue. Return 0 if none available.
        /// </summary>
        /// <returns></returns>
        public int GetKey()
        {
            int c;

            if (KeyPressQueue.TryDequeue(out c))
            {
                return c;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Creates the Map between key codes as understood by the PC AGI interpreter and the C# Key codes.
        /// </summary>
        /// <returns></returns>
        private Dictionary<int, int> CreateKeyConversionMap()
        {
            Dictionary<int, int> controllerMap = new Dictionary<int, int>();

            controllerMap.Add(9, (int)System.Windows.Forms.Keys.Tab);
            controllerMap.Add(27, (int)System.Windows.Forms.Keys.Escape);
            controllerMap.Add(13, (int)System.Windows.Forms.Keys.Enter);

            // Function keys.
            controllerMap.Add((59 << 8) + 0, (int)System.Windows.Forms.Keys.F1);
            controllerMap.Add((60 << 8) + 0, (int)System.Windows.Forms.Keys.F2);
            controllerMap.Add((61 << 8) + 0, (int)System.Windows.Forms.Keys.F3);
            controllerMap.Add((62 << 8) + 0, (int)System.Windows.Forms.Keys.F4);
            controllerMap.Add((63 << 8) + 0, (int)System.Windows.Forms.Keys.F5);
            controllerMap.Add((64 << 8) + 0, (int)System.Windows.Forms.Keys.F6);
            controllerMap.Add((65 << 8) + 0, (int)System.Windows.Forms.Keys.F7);
            controllerMap.Add((66 << 8) + 0, (int)System.Windows.Forms.Keys.F8);
            controllerMap.Add((67 << 8) + 0, (int)System.Windows.Forms.Keys.F9);
            controllerMap.Add((68 << 8) + 0, (int)System.Windows.Forms.Keys.F10);

            // Control and another key.
            controllerMap.Add(1, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.A);
            controllerMap.Add(2, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.B);
            controllerMap.Add(3, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.C);
            controllerMap.Add(4, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.D);
            controllerMap.Add(5, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.E);
            controllerMap.Add(6, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.F);
            controllerMap.Add(7, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.G);
            controllerMap.Add(8, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.H);
            controllerMap.Add(10, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.J);
            controllerMap.Add(11, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.K);
            controllerMap.Add(12, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.L);
            controllerMap.Add(14, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.N);
            controllerMap.Add(15, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.O);
            controllerMap.Add(16, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.P);
            controllerMap.Add(17, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.Q);
            controllerMap.Add(18, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.R);
            controllerMap.Add(19, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.S);
            controllerMap.Add(20, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.T);
            controllerMap.Add(21, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.U);
            controllerMap.Add(22, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.V);
            controllerMap.Add(23, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.W);
            controllerMap.Add(24, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.X);
            controllerMap.Add(25, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.Y);
            controllerMap.Add(26, (int)System.Windows.Forms.Keys.Control | (int)System.Windows.Forms.Keys.Z);

            // Alt and another key.
            controllerMap.Add((16 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.Q);
            controllerMap.Add((17 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.W);
            controllerMap.Add((18 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.E);
            controllerMap.Add((19 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.R);
            controllerMap.Add((20 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.T);
            controllerMap.Add((21 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.Y);
            controllerMap.Add((22 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.U);
            controllerMap.Add((23 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.I);
            controllerMap.Add((24 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.O);
            controllerMap.Add((25 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.P);
            controllerMap.Add((30 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.A);
            controllerMap.Add((31 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.S);
            controllerMap.Add((32 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.D);
            controllerMap.Add((33 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.F);
            controllerMap.Add((34 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.G);
            controllerMap.Add((35 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.H);
            controllerMap.Add((36 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.J);
            controllerMap.Add((37 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.K);
            controllerMap.Add((38 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.L);
            controllerMap.Add((44 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.Z);
            controllerMap.Add((45 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.X);
            controllerMap.Add((46 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.C);
            controllerMap.Add((47 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.V);
            controllerMap.Add((48 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.B);
            controllerMap.Add((49 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.N);
            controllerMap.Add((50 << 8) + 0, (int)System.Windows.Forms.Keys.Alt | (int)System.Windows.Forms.Keys.M);

            // TODO: 28, 29, 30, 31 (28 CTRL+\ 29 CTRL +] 30 CTRL + 6 CTRL + '-')

            // Normal printable chars.
            controllerMap.Add(32, (0x80000 | ' '));
            controllerMap.Add(33, (0x80000 | '!'));
            controllerMap.Add(34, (0x80000 | '"'));
            controllerMap.Add(35, (0x80000 | '#'));
            controllerMap.Add(36, (0x80000 | '$'));
            controllerMap.Add(37, (0x80000 | '%'));
            controllerMap.Add(38, (0x80000 | '&'));
            controllerMap.Add(39, (0x80000 | '\''));
            controllerMap.Add(40, (0x80000 | '('));
            controllerMap.Add(41, (0x80000 | ')'));
            controllerMap.Add(42, (0x80000 | '*'));
            controllerMap.Add(43, (0x80000 | '+'));
            controllerMap.Add(44, (0x80000 | ','));
            controllerMap.Add(45, (0x80000 | '-'));
            controllerMap.Add(46, (0x80000 | '.'));
            controllerMap.Add(47, (0x80000 | '/'));
            controllerMap.Add(48, (0x80000 | '0'));
            controllerMap.Add(49, (0x80000 | '1'));
            controllerMap.Add(50, (0x80000 | '2'));
            controllerMap.Add(51, (0x80000 | '3'));
            controllerMap.Add(52, (0x80000 | '4'));
            controllerMap.Add(53, (0x80000 | '5'));
            controllerMap.Add(54, (0x80000 | '6'));
            controllerMap.Add(55, (0x80000 | '7'));
            controllerMap.Add(56, (0x80000 | '8'));
            controllerMap.Add(57, (0x80000 | '9'));
            controllerMap.Add(58, (0x80000 | ':'));
            controllerMap.Add(59, (0x80000 | ';'));
            controllerMap.Add(60, (0x80000 | '<'));
            controllerMap.Add(61, (0x80000 | '='));
            controllerMap.Add(62, (0x80000 | '>'));
            controllerMap.Add(63, (0x80000 | '?'));
            controllerMap.Add(64, (0x80000 | '@'));

            // Manhunter games use unmodified alpha chars as controllers, e.g. C and S. AGI Demo Packs do as well.
            controllerMap.Add(65, (0x80000 | 'a'));
            controllerMap.Add(66, (0x80000 | 'b'));
            controllerMap.Add(67, (0x80000 | 'c'));
            controllerMap.Add(68, (0x80000 | 'd'));
            controllerMap.Add(69, (0x80000 | 'e'));
            controllerMap.Add(70, (0x80000 | 'f'));
            controllerMap.Add(71, (0x80000 | 'g'));
            controllerMap.Add(72, (0x80000 | 'h'));
            controllerMap.Add(73, (0x80000 | 'i'));
            controllerMap.Add(74, (0x80000 | 'j'));
            controllerMap.Add(75, (0x80000 | 'k'));
            controllerMap.Add(76, (0x80000 | 'l'));
            controllerMap.Add(77, (0x80000 | 'm'));
            controllerMap.Add(78, (0x80000 | 'n'));
            controllerMap.Add(79, (0x80000 | 'o'));
            controllerMap.Add(80, (0x80000 | 'p'));
            controllerMap.Add(81, (0x80000 | 'q'));
            controllerMap.Add(82, (0x80000 | 'r'));
            controllerMap.Add(83, (0x80000 | 's'));
            controllerMap.Add(84, (0x80000 | 't'));
            controllerMap.Add(85, (0x80000 | 'u'));
            controllerMap.Add(86, (0x80000 | 'v'));
            controllerMap.Add(87, (0x80000 | 'w'));
            controllerMap.Add(88, (0x80000 | 'x'));
            controllerMap.Add(89, (0x80000 | 'y'));
            controllerMap.Add(90, (0x80000 | 'z'));
            controllerMap.Add(91, (0x80000 | '['));
            controllerMap.Add(92, (0x80000 | '\\'));
            controllerMap.Add(93, (0x80000 | ']'));
            controllerMap.Add(94, (0x80000 | '^'));
            controllerMap.Add(95, (0x80000 | '_'));
            controllerMap.Add(96, (0x80000 | '`'));
            controllerMap.Add(97,  (0x80000 | 'A'));
            controllerMap.Add(98,  (0x80000 | 'B'));
            controllerMap.Add(99,  (0x80000 | 'C'));
            controllerMap.Add(100, (0x80000 | 'D'));
            controllerMap.Add(101, (0x80000 | 'E'));
            controllerMap.Add(102, (0x80000 | 'F'));
            controllerMap.Add(103, (0x80000 | 'G'));
            controllerMap.Add(104, (0x80000 | 'H'));
            controllerMap.Add(105, (0x80000 | 'I'));
            controllerMap.Add(106, (0x80000 | 'J'));
            controllerMap.Add(107, (0x80000 | 'K'));
            controllerMap.Add(108, (0x80000 | 'L'));
            controllerMap.Add(109, (0x80000 | 'M'));
            controllerMap.Add(110, (0x80000 | 'N'));
            controllerMap.Add(111, (0x80000 | 'O'));
            controllerMap.Add(112, (0x80000 | 'P'));
            controllerMap.Add(113, (0x80000 | 'Q'));
            controllerMap.Add(114, (0x80000 | 'R'));
            controllerMap.Add(115, (0x80000 | 'S'));
            controllerMap.Add(116, (0x80000 | 'T'));
            controllerMap.Add(117, (0x80000 | 'U'));
            controllerMap.Add(118, (0x80000 | 'V'));
            controllerMap.Add(119, (0x80000 | 'W'));
            controllerMap.Add(120, (0x80000 | 'X'));
            controllerMap.Add(121, (0x80000 | 'Y'));
            controllerMap.Add(122, (0x80000 | 'Z'));
            controllerMap.Add(123, (0x80000 | '{'));
            controllerMap.Add(124, (0x80000 | '|'));
            controllerMap.Add(125, (0x80000 | '}'));
            controllerMap.Add(126, (0x80000 | '~'));

            // Joysick codes. We're going to ignore these for now. Who uses a Joystick anyway? Maybe in the 80s. :)
            controllerMap.Add((1 << 8) + 1, 0);
            controllerMap.Add((1 << 8) + 2, 0);
            controllerMap.Add((1 << 8) + 3, 0);
            controllerMap.Add((1 << 8) + 4, 0);

            return controllerMap;
        }
    }
}
