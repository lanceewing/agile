using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGILE
{
    /// <summary>
    /// The core constants and definitions within the AGI system.
    /// </summary>
    class Defines
    {

        /* ------------------------ System variables -------------------------- */

        public const int CURROOM = 0;           /* current.room */

        public const int PREVROOM = 1;          /* previous.room */

        public const int EGOEDGE = 2;           /* edge.ego.hit */

        public const int SCORE = 3;             /* score */

        public const int OBJHIT = 4;            /* obj.hit.edge */

        public const int OBJEDGE = 5;           /* edge.obj.hit */

        public const int EGODIR = 6;            /* ego's direction */

        public const int MAXSCORE = 7;          /* maximum possible score */

        public const int MEMLEFT = 8;           /* remaining heap space in pages */

        public const int UNKNOWN_WORD = 9;      /* number of unknown word */

        public const int ANIMATION_INT = 10;    /* animation interval */

        public const int SECONDS = 11;

        public const int MINUTES = 12;          /* time since game start */

        public const int HOURS = 13;

        public const int DAYS = 14;

        public const int DBL_CLK_DELAY = 15;

        public const int CURRENT_EGO = 16;

        public const int ERROR_NUM = 17;

        public const int ERROR_PARAM = 18;

        public const int LAST_CHAR = 19;

        public const int MACHINE_TYPE = 20;

        public const int PRINT_TIMEOUT = 21;

        public const int NUM_VOICES = 22;

        public const int ATTENUATION = 23;

        public const int INPUTLEN = 24;

        public const int SELECTED_OBJ = 25;     /* selected object number */

        public const int MONITOR_TYPE = 26;


        /* ------------------------ System flags ------------------------ */

        public const int ONWATER = 0;               /* on.water */

        public const int SEE_EGO = 1;               /* can we see ego? */

        public const int INPUT = 2;                 /* have.input */

        public const int HITSPEC = 3;               /* hit.special */

        public const int HADMATCH = 4;              /* had a word match */

        public const int INITLOGS = 5;              /* signal to init logics */

        public const int RESTART = 6;               /* is a restart in progress? */

        public const int NO_SCRIPT = 7;             /* don't add to the script buffer */

        public const int DBL_CLK = 8;               /* enable double click on joystick */

        public const int SOUNDON = 9;               /* state of sound playing */

        public const int TRACE_ENABLE = 10;         /* to enable tracing */

        public const int HAS_NOISE = 11;            /* does machine have noise channel */

        public const int RESTORE = 12;              /* restore game in progress */

        public const int ENABLE_SELECT = 13;        /* allow selection of objects from inventory screen */

        public const int ENABLE_MENU = 14;

        public const int LEAVE_WIN = 15;            /* leave windows on the screen */

        public const int NO_PRMPT_RSTRT = 16;       /* don't prompt on restart */


        /* ------------------------ Miscellaneous ------------------------ */

        public const int NUMVARS = 256;             /* number of vars */

        public const int NUMFLAGS = 256;            /* number of flags */

        public const int NUMCONTROL = 50;           /* number of controllers */

        public const int NUMWORDS = 10;             /* maximum # of words recognized in input */

        public const int NUMANIMATED = 256;         /* maximum # of animated objects */

        public const int MAXVAR = 255;              /* maximum value for a var */

        public const int TEXTCOLS = 40;             /* number of columns of text */

        public const int TEXTLINES = 25;            /* number of lines of text */

        public const int MAXINPUT = 40;             /* maximum length of user input */

        public const int DIALOGUE_WIDTH = 35;       /* maximum width of dialog box */

        public const int NUMSTRINGS = 24;           /* number of user-definable strings */

        public const int STRLENGTH = 40;            /* maximum length of user strings */

        public const int GLSIZE = 40;               /* maximum length for GetLine calls, used internally for things like save dialog */

        public const int PROMPTSTR = 0;             /* string number of prompt */

        public const int ID_LEN = 7;                /* length of gameID string */

        public const int MAXDIST = 50;              /* maximum movement distance */

        public const int MINDIST = 6;               /* minimum movement distance */

        public const int BACK_MOST_PRIORITY = 4;    /* priority value of back most priority */

        /* ------------------------ Inventory item constants --------------------------- */

        public const int LIMBO = 0;                 /* room number of objects that are gone */

        public const int CARRYING = 255;            /* room number of objects in ego's posession */


        /* ------------------------ Default status and input row numbers ------------------------ */
        
        public const int STATUSROW = 21;

        public const int INPUTROW = 23;


        /* ------------------------ Screen edges ------------------------ */

        public const int TOP = 1;

        public const int RIGHT = 2;

        public const int BOTTOM = 3;

        public const int LEFT = 4;

        public const int MINX = 0;

        public const int MINY = 0;

        public const int MAXX = 159;

        public const int MAXY = 167;

        public const int HORIZON = 36;

    }
}
