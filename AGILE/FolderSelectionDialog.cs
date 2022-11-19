using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Security;
using System.Threading;
using System.Windows.Forms;

namespace AGILE
{
    class FolderSelectionDialog : CommonDialog
    {
        private static readonly int MAX_PATH = 260;

        /// <summary>
        /// These should be robust.  We find the correct child controls in the dialog
        /// by using the GetDlgItem method, rather than the FindWindow(Ex) method,
        /// because the dialog item IDs should be constant.
        /// </summary>
        const int _dlgItemBrowseControl = 0;
        const int _dlgItemTreeView = 100;

        /// <summary>
        /// Some of the messages that the Tree View control will respond to
        /// </summary>
        private const int TV_FIRST = 0x1100;
        private const int TVM_GETNEXTITEM = (TV_FIRST + 10);
        private const int TVM_ENSUREVISIBLE = (TV_FIRST + 20);

        /// <summary>
        /// Constants used to identity specific items in the Tree View control
        /// </summary>
        private const int TVGN_CARET = 0x9;

        // Fields
        private PInvoke.BrowseFolderCallbackProc _callback;
        private string _descriptionText;
        private Environment.SpecialFolder _rootFolder;
        private string _selectedPath;
        private bool _selectedPathNeedsCheck;
        private bool _showNewFolderButton;
        private bool _newStyle = true;
        private bool _dontIncludeNetworkFoldersBelowDomainLevel;
        private int _uiFlags;
        private IntPtr _hwndEdit;
        private IntPtr _rootFolderLocation;

        // Events
        public new event EventHandler HelpRequest
        {
            add
            {
                base.HelpRequest += value;
            }
            remove
            {
                base.HelpRequest -= value;
            }
        }

        public FolderSelectionDialog()
        {
            this.Reset();
        }

        private class CSIDL
        {
            public const int PRINTERS = 4;
            public const int NETWORK = 0x12;
        }

        private class BrowseFlags
        {
            public const int BIF_DEFAULT = 0x0000;
            public const int BIF_BROWSEFORCOMPUTER = 0x1000;
            public const int BIF_BROWSEFORPRINTER = 0x2000;
            public const int BIF_BROWSEINCLUDEFILES = 0x4000;
            public const int BIF_BROWSEINCLUDEURLS = 0x0080;
            public const int BIF_DONTGOBELOWDOMAIN = 0x0002;
            public const int BIF_EDITBOX = 0x0010;
            public const int BIF_NEWDIALOGSTYLE = 0x0040;
            public const int BIF_NONEWFOLDERBUTTON = 0x0200;
            public const int BIF_RETURNFSANCESTORS = 0x0008;
            public const int BIF_RETURNONLYFSDIRS = 0x0001;
            public const int BIF_SHAREABLE = 0x8000;
            public const int BIF_STATUSTEXT = 0x0004;
            public const int BIF_UAHINT = 0x0100;
            public const int BIF_VALIDATE = 0x0020;
            public const int BIF_NOTRANSLATETARGETS = 0x0400;
        }

        private static class BrowseForFolderMessages
        {
            // messages FROM the folder browser
            public const int BFFM_INITIALIZED = 1;
            public const int BFFM_SELCHANGED = 2;
            public const int BFFM_VALIDATEFAILEDA = 3;
            public const int BFFM_VALIDATEFAILEDW = 4;
            public const int BFFM_IUNKNOWN = 5;

            // messages TO the folder browser
            public const int BFFM_SETSTATUSTEXT = 0x464;
            public const int BFFM_ENABLEOK = 0x465;
            public const int BFFM_SETSELECTIONA = 0x466;
            public const int BFFM_SETSELECTIONW = 0x467;
        }

        private int FolderBrowserCallback(IntPtr hwnd, int msg, IntPtr lParam, IntPtr lpData)
        {
            switch (msg)
            {
                case BrowseForFolderMessages.BFFM_INITIALIZED:
                    if (this._selectedPath.Length != 0)
                    {
                        PInvoke.User32.SendMessage(new HandleRef(null, hwnd), BrowseForFolderMessages.BFFM_SETSELECTIONW, 1, this._selectedPath);
                    }
                    break;

                case BrowseForFolderMessages.BFFM_SELCHANGED:
                    IntPtr pidl = lParam;
                    if (pidl != IntPtr.Zero)
                    {
                        if (((_uiFlags & BrowseFlags.BIF_BROWSEFORPRINTER) == BrowseFlags.BIF_BROWSEFORPRINTER) ||
                            ((_uiFlags & BrowseFlags.BIF_BROWSEFORCOMPUTER) == BrowseFlags.BIF_BROWSEFORCOMPUTER))
                        {
                            // we're browsing for a printer or computer, enable the OK button unconditionally.
                            PInvoke.User32.SendMessage(new HandleRef(null, hwnd), BrowseForFolderMessages.BFFM_ENABLEOK, 0, 1);
                        }
                        else
                        {
                            IntPtr pszPath = Marshal.AllocHGlobal(MAX_PATH * Marshal.SystemDefaultCharSize);
                            bool haveValidPath = PInvoke.Shell32.SHGetPathFromIDList(pidl, pszPath);
                            String displayedPath = Marshal.PtrToStringAuto(pszPath);
                            Marshal.FreeHGlobal(pszPath);
                            // whether to enable the OK button or not. (if file is valid)
                            PInvoke.User32.SendMessage(new HandleRef(null, hwnd), BrowseForFolderMessages.BFFM_ENABLEOK, 0, haveValidPath ? 1 : 0);

                            // Maybe set the Edit Box text to the Full Folder path
                            if (haveValidPath && !String.IsNullOrEmpty(displayedPath))
                            {
                                if ((_uiFlags & BrowseFlags.BIF_STATUSTEXT) == BrowseFlags.BIF_STATUSTEXT)
                                    PInvoke.User32.SendMessage(new HandleRef(null, hwnd), BrowseForFolderMessages.BFFM_SETSTATUSTEXT, 0, displayedPath);
                            }
                        }
                    }
                    IntPtr hwndFolderCtrl = PInvoke.User32.GetDlgItem(hwnd, _dlgItemBrowseControl);
                    if (hwndFolderCtrl != IntPtr.Zero)
                    {
                        IntPtr hwndTV = PInvoke.User32.GetDlgItem(hwndFolderCtrl, _dlgItemTreeView);

                        if (hwndTV != IntPtr.Zero)
                        {
                            IntPtr item = PInvoke.User32.SendMessage(hwndTV, (uint)TVM_GETNEXTITEM, new IntPtr(TVGN_CARET), IntPtr.Zero);
                            if (item != IntPtr.Zero)
                            {
                                PInvoke.User32.SendMessage(hwndTV, TVM_ENSUREVISIBLE, IntPtr.Zero, item);
                            }
                        }
                    }
                    break;
            }
            return 0;
        }

        private static PInvoke.IMalloc GetSHMalloc()
        {
            PInvoke.IMalloc[] ppMalloc = new PInvoke.IMalloc[1];
            PInvoke.Shell32.SHGetMalloc(ppMalloc);
            return ppMalloc[0];
        }

        public override void Reset()
        {
            this._rootFolder = (Environment.SpecialFolder)0;
            this._descriptionText = string.Empty;
            this._selectedPath = string.Empty;
            this._selectedPathNeedsCheck = false;
            this._showNewFolderButton = true;
            this._newStyle = true;
            this._dontIncludeNetworkFoldersBelowDomainLevel = false;
            this._hwndEdit = IntPtr.Zero;
            this._rootFolderLocation = IntPtr.Zero;
        }

        protected override bool RunDialog(IntPtr hWndOwner)
        {
            bool result = false;
            if (_rootFolderLocation == IntPtr.Zero)
            {
                PInvoke.Shell32.SHGetSpecialFolderLocation(hWndOwner, (int)this._rootFolder, ref _rootFolderLocation);
                if (_rootFolderLocation == IntPtr.Zero)
                {
                    PInvoke.Shell32.SHGetSpecialFolderLocation(hWndOwner, 0, ref _rootFolderLocation);
                    if (_rootFolderLocation == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("FolderBrowserDialogNoRootFolder");
                    }
                }
            }
            _hwndEdit = IntPtr.Zero;
            //_uiFlags = 0;
            if (_dontIncludeNetworkFoldersBelowDomainLevel)
                _uiFlags += BrowseFlags.BIF_DONTGOBELOWDOMAIN;
            if (this._newStyle)
                _uiFlags += BrowseFlags.BIF_NEWDIALOGSTYLE;
            if (!this._showNewFolderButton)
                _uiFlags += BrowseFlags.BIF_NONEWFOLDERBUTTON;

            if (Control.CheckForIllegalCrossThreadCalls && (Application.OleRequired() != ApartmentState.STA))
            {
                throw new ThreadStateException("DebuggingException: ThreadMustBeSTA");
            }
            IntPtr pidl = IntPtr.Zero;
            IntPtr hglobal = IntPtr.Zero;
            IntPtr pszPath = IntPtr.Zero;
            try
            {
                PInvoke.BROWSEINFO browseInfo = new PInvoke.BROWSEINFO();
                hglobal = Marshal.AllocHGlobal(MAX_PATH * Marshal.SystemDefaultCharSize);
                pszPath = Marshal.AllocHGlobal(MAX_PATH * Marshal.SystemDefaultCharSize);
                this._callback = new PInvoke.BrowseFolderCallbackProc(this.FolderBrowserCallback);
                browseInfo.pidlRoot = _rootFolderLocation;
                browseInfo.Owner = hWndOwner;
                browseInfo.pszDisplayName = hglobal;
                browseInfo.Title = this._descriptionText;
                browseInfo.Flags = _uiFlags;
                browseInfo.callback = this._callback;
                browseInfo.lParam = IntPtr.Zero;
                browseInfo.iImage = 0;
                pidl = PInvoke.Shell32.SHBrowseForFolder(browseInfo);
                if (((_uiFlags & BrowseFlags.BIF_BROWSEFORPRINTER) == BrowseFlags.BIF_BROWSEFORPRINTER) ||
                ((_uiFlags & BrowseFlags.BIF_BROWSEFORCOMPUTER) == BrowseFlags.BIF_BROWSEFORCOMPUTER))
                {
                    this._selectedPath = Marshal.PtrToStringAuto(browseInfo.pszDisplayName);
                    result = true;
                }
                else
                {
                    if (pidl != IntPtr.Zero)
                    {
                        PInvoke.Shell32.SHGetPathFromIDList(pidl, pszPath);
                        this._selectedPathNeedsCheck = true;
                        this._selectedPath = Marshal.PtrToStringAuto(pszPath);
                        result = true;
                    }
                }
            }
            finally
            {
                PInvoke.IMalloc sHMalloc = GetSHMalloc();
                sHMalloc.Free(_rootFolderLocation);
                _rootFolderLocation = IntPtr.Zero;
                if (pidl != IntPtr.Zero)
                {
                    sHMalloc.Free(pidl);
                }
                if (pszPath != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pszPath);
                }
                if (hglobal != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(hglobal);
                }
                this._callback = null;
            }
            return result;
        }

        // Properties

        /// <summary>
        /// This description appears near the top of the dialog box, providing direction to the user.
        /// </summary>
        public string Description
        {
            get
            {
                return this._descriptionText;
            }
            set
            {
                this._descriptionText = (value == null) ? string.Empty : value;
            }
        }

        public Environment.SpecialFolder RootFolder
        {
            get
            {
                return this._rootFolder;
            }
            set
            {
                if (!Enum.IsDefined(typeof(Environment.SpecialFolder), value))
                {
                    throw new InvalidEnumArgumentException("value", (int)value, typeof(Environment.SpecialFolder));
                }
                this._rootFolder = value;
            }
        }

        /// <summary>
        /// Set or get the selected path.  
        /// </summary>
        public string SelectedPath
        {
            get
            {
                if (((this._selectedPath != null) && (this._selectedPath.Length != 0)) && this._selectedPathNeedsCheck)
                {
                    new FileIOPermission(FileIOPermissionAccess.PathDiscovery, this._selectedPath).Demand();
                    this._selectedPathNeedsCheck = false;
                }
                return this._selectedPath;
            }
            set
            {
                this._selectedPath = (value == null) ? string.Empty : value;
                this._selectedPathNeedsCheck = true;
            }
        }

        /// <summary>
        /// Enable or disable the "New Folder" button in the browser dialog.
        /// </summary>
        public bool ShowNewFolderButton
        {
            get
            {
                return this._showNewFolderButton;
            }
            set
            {
                this._showNewFolderButton = value;
            }
        }

        /// <summary>
        /// Set whether to use the New Folder Browser dialog style.
        /// </summary>
        /// <remarks>
        /// The new style is resizable and includes a "New Folder" button.
        /// </remarks>
        public bool NewStyle
        {
            get
            {
                return this._newStyle;
            }
            set
            {
                this._newStyle = value;
            }
        }
    }

    internal static class PInvoke
    {
        static PInvoke() { }

        public delegate int BrowseFolderCallbackProc(IntPtr hwnd, int msg, IntPtr lParam, IntPtr lpData);

        internal static class User32
        {
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SendMessage(HandleRef hWnd, int msg, int wParam, string lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SendMessage(HandleRef hWnd, int msg, int wParam, int lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr FindWindowEx(HandleRef hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern Boolean SetWindowText(IntPtr hWnd, String text);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

            [DllImport("user32.dll")]
            public static extern IntPtr GetDlgItem(IntPtr hDlg, int nIDDlgItem);
        }

        [ComImport, Guid("00000002-0000-0000-c000-000000000046"), SuppressUnmanagedCodeSecurity, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMalloc
        {
            [PreserveSig]
            IntPtr Alloc(int cb);
            [PreserveSig]
            IntPtr Realloc(IntPtr pv, int cb);
            [PreserveSig]
            void Free(IntPtr pv);
            [PreserveSig]
            int GetSize(IntPtr pv);
            [PreserveSig]
            int DidAlloc(IntPtr pv);
            [PreserveSig]
            void HeapMinimize();
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class BROWSEINFO
        {
            public IntPtr Owner;
            public IntPtr pidlRoot;
            public IntPtr pszDisplayName;
            public string Title;
            public int Flags;
            public BrowseFolderCallbackProc callback;
            public IntPtr lParam;
            public int iImage;
        }

        [SuppressUnmanagedCodeSecurity]
        internal static class Shell32
        {
            // Methods
            [DllImport("shell32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SHBrowseForFolder([In] PInvoke.BROWSEINFO lpbi);
            [DllImport("shell32.dll")]
            public static extern int SHGetMalloc([Out, MarshalAs(UnmanagedType.LPArray)] PInvoke.IMalloc[] ppMalloc);
            [DllImport("shell32.dll", CharSet = CharSet.Auto)]
            public static extern bool SHGetPathFromIDList(IntPtr pidl, IntPtr pszPath);
            [DllImport("shell32.dll")]
            public static extern int SHGetSpecialFolderLocation(IntPtr hwnd, int csidl, ref IntPtr ppidl);
        }
    }
}