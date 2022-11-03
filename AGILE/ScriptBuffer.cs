using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGILE
{
    class ScriptBuffer
    {
        public enum ScriptBufferEventType
        {
            LoadLogic,
            LoadView,
            LoadPic,
            LoadSound,
            DrawPic,
            AddToPic,
            DiscardPic,
            DiscardView,
            OverlayPic
        }

        public class ScriptBufferEvent
        {
            public ScriptBufferEventType type;
            public int resourceNumber;
            public byte[] data;

            public ScriptBufferEvent(ScriptBufferEventType type, int resourceNumber, byte[] data)
            {
                this.type = type;
                this.resourceNumber = resourceNumber;
                this.data = data;
            }
        }

        /// <summary>
        /// The GameState class holds all of the data and state for the Game currently 
        /// being run by the interpreter.
        /// </summary>
        private GameState state;

        /// <summary>
        /// A transcript of events leading to the current state in the current room.
        /// </summary>
        public List<ScriptBufferEvent> Events;

        /// <summary>
        /// Whether or not the storage of script events in the buffer is enabled or not.
        /// </summary>
        private bool doScript;

        public int MaxScript;
        public int ScriptSize;
        public int ScriptEntries { get { return Events.Count; } }
        public int SavedScript;

        /// <summary>
        /// Constructor for ScriptBuffer.
        /// </summary>
        /// <param name="state"></param>
        public ScriptBuffer(GameState state)
        {
            // Default script size is 50 according to original AGI specs.
            this.ScriptSize = 50;
            this.Events = new List<ScriptBufferEvent>();
            this.state = state;
            InitScript();
        }

        /// <summary>
        /// 
        /// </summary>
        public void ScriptOff()
        {
            doScript = false;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ScriptOn()
        {
            doScript = true;
        }

        /// <summary>
        /// Initialize the script buffer.
        /// </summary>
        public void InitScript()
        {
            Events.Clear();
        }

        /// <summary>
        /// Add an event to the script buffer
        /// </summary>
        /// <param name="action"></param>
        /// <param name="who"></param>
        public void AddScript(ScriptBufferEventType action, int who, byte[] data = null)
        {
	        if (state.Flags[Defines.NO_SCRIPT]) return;

	        if (doScript)
            {
		        if (Events.Count >= this.ScriptSize)
                {
                    // TODO: Error. Error(11, maxScript);
                    return;
                }
                else
                {
                    Events.Add(new ScriptBufferEvent(action, who, data));
                }
		    }

            if (Events.Count > MaxScript)
            {
                MaxScript = Events.Count;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scriptSize"></param>
        public void SetScriptSize(int scriptSize)
        {
            this.ScriptSize = scriptSize;
            this.Events.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        public void PushScript()
        {
            this.SavedScript = Events.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        public void PopScript()
        {
            if (Events.Count > this.SavedScript)
            {
                Events.RemoveRange(this.SavedScript, (Events.Count - this.SavedScript));
            }
        }

        /// <summary>
        /// Returns the script event buffer as a raw byte array.
        /// </summary>
        /// <returns></returns>
        public byte[] Encode()
        {
            // Each script entry is two bytes long.
            MemoryStream stream = new MemoryStream(this.ScriptSize * 2);

            foreach (ScriptBufferEvent e in Events)
            {
                stream.WriteByte((byte)(e.type));
                stream.WriteByte((byte)e.resourceNumber);
                if (e.data != null)
                {
                    stream.Write(e.data, 0, e.data.Length);
                }
            }

            // We deliberately use GetBuffer rather than ToArray so that we get the 
            // unused part as well.
            return stream.GetBuffer();
        }

        /// <summary>
        /// Add an event to the script buffer without checking NO_SCRIPT flag. Used primarily by restore save game function.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="who"></param>
        public void RestoreScript(ScriptBufferEventType action, int who, byte[] data = null)
        {
            Events.Add(new ScriptBufferEvent(action, who, data));

            if (Events.Count > MaxScript)
            {
                MaxScript = Events.Count;
            }
        }
    }
}
