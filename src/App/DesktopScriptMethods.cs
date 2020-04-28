using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.CefGlue;
using ServiceStack.Desktop;
using ServiceStack.Script;
using ServiceStack.Text.Pools;
using Web;

namespace WebApp
{
    public class DesktopScriptMethods : ScriptMethods
    {
        public static DesktopScriptMethods Instance { get; private set; } 
        public static int ClipboardPollMs { get; set; } = 200;
        
        private IAppHost appHost;
        public DesktopScriptMethods(IAppHost appHost)
        {
            this.appHost = appHost;
            Instance ??= this;
        }
        
        private static IntPtr WindowHandle => CefPlatformWindows.Provider.Window.Handle;

        public Dictionary<string,string> desktopInfo() => new Dictionary<string, string> {
            ["tool"] = DesktopConfig.Instance.Tool,
            ["toolVersion"] = DesktopConfig.Instance.ToolVersion,
            ["chromeVersion"] = DesktopConfig.Instance.ChromeVersion,
        };

        public IgnoreResult openUrl(string url)
        {
            OpenBrowser(url);
            return IgnoreResult.Value;
        }

        public bool sendToForeground(string windowName)
        {
            var handle = windowName == "browser"
                ? WindowHandle
                : IntPtr.Zero;
            if (handle != IntPtr.Zero)
                return SetForegroundWindow(handle);
            return false;
        }

        public static Process OpenBrowser(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Process.Start(new ProcessStartInfo("cmd", $"/c start {url}"));
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return Process.Start("xdg-open", url);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return Process.Start("open", url);
            throw new NotSupportedException("Unknown platform");
        }

        public string expandEnvVars(string path) => string.IsNullOrEmpty(path) || path.IndexOf('%') == -1
            ? path
            : Environment.ExpandEnvironmentVariables(path);

        public DialogResult openFile(Dictionary<string, object> options)
        {
            var isFolderPicker = options.TryGetValue("isFolderPicker", out var oIsFolderPicker) && oIsFolderPicker is bool b && b;

            string normalizeFilter(string filter) => !string.IsNullOrEmpty(filter)
                ? filter.IndexOf('\0') >= 0
                    ? filter
                    : filter.Replace("|","\0") + "\0\0"
                : isFolderPicker
                    ? "Folder only\0$$$.$$$\0\0"
                    : "All Files\0*.*\0\0"; 
            
            var dlgArgs = new OpenFileName();
            dlgArgs.lStructSize = Marshal.SizeOf(dlgArgs);
            dlgArgs.lpstrFile = new string(new char[256]);
            dlgArgs.nMaxFile = dlgArgs.lpstrFile.Length;
            dlgArgs.lpstrFileTitle = new string(new char[46]);
            dlgArgs.nMaxFileTitle = dlgArgs.lpstrFileTitle.Length;

            dlgArgs.hwndOwner = WindowHandle;

            dlgArgs.Flags = options.TryGetValue("flags", out var oFlags) ? Convert.ToInt32(oFlags) : 0x00080000;
            dlgArgs.lpstrTitle = options.TryGetValue("title", out var oTitle) ? oTitle as string 
                : isFolderPicker
                    ? "Select a Folder"
                    : "Open File Dialog...";
            dlgArgs.lpstrFilter = normalizeFilter(options.TryGetValue("filter", out var oFilter) ? oFilter as string : null);
            if (options.TryGetValue("filterIndex", out var oFilterIndex))
                dlgArgs.nFilterIndex = Convert.ToInt32(oFilterIndex);
            dlgArgs.lpstrInitialDir = options.TryGetValue("initialDir", out var oInitialDir)
                ? expandEnvVars(oInitialDir as string)
                : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (options.TryGetValue("templateName", out var oTemplateName))
                dlgArgs.lpTemplateName = oTemplateName as string;
            if (options.TryGetValue("defaultExt", out var oDefaultExt))
                dlgArgs.lpstrDefExt = oDefaultExt as string;

            if (isFolderPicker)
            {
                //HACK http://unafaltadecomprension.blogspot.com/2013/04/browsing-for-files-and-folders-c.html
                dlgArgs.Flags |= (int) (FileOpenOptions.NoValidate | FileOpenOptions.PathMustExist);
                dlgArgs.lpstrFile = "Folder Selection.";
            }

            if (GetOpenFileName(dlgArgs))
            {
                var file = isFolderPicker
                    ? dlgArgs.lpstrFile.Replace("Folder Selection", "")
                    : dlgArgs.lpstrFile;
                var ret = new DialogResult {
                    File = file,
                    FileTitle = dlgArgs.lpstrFileTitle,
                    Ok = true,
                };
                return ret;
            }

            return new DialogResult();
        }

        public string clipboard() => GetClipboardAsString();

        public IgnoreResult setClipboard(string data)
        {
            SetStringInClipboard(data);
            return IgnoreResult.Value;
        }

        public int messageBox(string text, string caption, uint type)
        {
            return MessageBox(0, text, caption, type);
        }
        
        //Message Box
        [DllImport("user32.dll", SetLastError = true, CharSet= CharSet.Auto)]
        public static extern int MessageBox(int hWnd, String text, String caption, uint type);        
        
        //Window Operations
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        
        //File Dialog
        [Flags]
        internal enum FileOpenOptions : int
        {
            OverwritePrompt = 0x00000002,
            StrictFileTypes = 0x00000004,
            NoChangeDirectory = 0x00000008,
            PickFolders = 0x00000020,
            // Ensure that items returned are filesystem items.
            ForceFilesystem = 0x00000040,
            // Allow choosing items that have no storage.
            AllNonStorageItems = 0x00000080,
            NoValidate = 0x00000100,
            AllowMultiSelect = 0x00000200,
            PathMustExist = 0x00000800,
            FileMustExist = 0x00001000,
            CreatePrompt = 0x00002000,
            ShareAware = 0x00004000,
            NoReadOnlyReturn = 0x00008000,
            NoTestFileCreate = 0x00010000,
            HideMruPlaces = 0x00020000,
            HidePinnedPlaces = 0x00040000,
            NoDereferenceLinks = 0x00100000,
            DontAddToRecent = 0x02000000,
            ForceShowHidden = 0x10000000,
            DefaultNoMiniMode = 0x20000000,
            OFN_EXPLORER = 0x00080000, // Old explorer dialog
        }
        
        private const int MAX_PATH = 260;
        
        [DllImport("comdlg32.dll", SetLastError=true, CharSet = CharSet.Auto)]
        static extern bool GetOpenFileName([In, Out] OpenFileName ofn);
        
        public delegate IntPtr WndProc(IntPtr hWnd, Int32 msg, IntPtr wParam, IntPtr lParam);
        
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        public class OpenFileName
        {
            public int      lStructSize = SizeOf();
            public IntPtr   hwndOwner;
            public IntPtr   hInstance;
            public string   lpstrFilter; // separate filters with \0
            public IntPtr   lpstrCustomFilter;
            public int      nMaxCustFilter;
            public int      nFilterIndex;
            //public IntPtr   lpstrFile;
            public string   lpstrFile;
            public int      nMaxFile = MAX_PATH;
            //public IntPtr   lpstrFileTitle;
            public string   lpstrFileTitle;
            public int      nMaxFileTitle = MAX_PATH;
            public string   lpstrInitialDir;
            public string   lpstrTitle;
            public int      Flags;
            public short    nFileOffset;
            public short    nFileExtension;
            public string   lpstrDefExt;
            public IntPtr   lCustData;
            public WndProc  lpfnHook;
            public string   lpTemplateName;
            public IntPtr   pvReserved;
            public int      dwReserved;
            public int      FlagsEx;
            
            [System.Security.SecuritySafeCritical]
            private static int SizeOf() => Marshal.SizeOf(typeof(OpenFileName));
        }
        

        //Clipboard from: https://github.com/SimonCropp/TextCopy/blob/master/src/TextCopy/WindowsClipboard.cs
        [DllImport("User32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("User32.dll", SetLastError = true)]
        static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

        [DllImport("user32.dll")]
        static extern bool EmptyClipboard();

        [DllImport("Kernel32.dll", SetLastError = true)]
        static extern int GlobalSize(IntPtr hMem);
        
        const uint cfUnicodeText = 13;
        
        static void TryOpenClipboard()
        {
            var num = 10;
            while (true)
            {
                if (OpenClipboard(default))
                {
                    break;
                }

                if (--num == 0)
                {
                    ThrowWin32();
                }

                Thread.Sleep(100);
            }
        }
        
        static string GetClipboardAsString()
        {
            TryOpenClipboard();
            
            IntPtr handle = default;
            IntPtr pointer = default;
            byte[] buff = null;
            try
            {
                handle = GetClipboardData(cfUnicodeText);
                if (handle == default)
                    return null;

                pointer = GlobalLock(handle);
                if (pointer == default)
                    return null;

                var size = GlobalSize(handle);
                buff = BufferPool.GetBuffer(size);
                Marshal.Copy(pointer, buff, 0, size);
                return Encoding.Unicode.GetString(buff, 0, size).TrimEnd('\0');
            }
            finally
            {
                if (buff != null)
                    BufferPool.ReleaseBufferToPool(ref buff);
                if (pointer != default)
                    GlobalUnlock(handle);
                CloseClipboard();
            }
        }
        
        static void SetStringInClipboard(string text)
        {
            TryOpenClipboard();
            
            IntPtr hGlobal = default;
            try
            {
                EmptyClipboard();

                var bytes = (text.Length + 1) * 2;
                hGlobal = Marshal.AllocHGlobal(bytes);

                if (hGlobal == default)
                    ThrowWin32();

                var target = GlobalLock(hGlobal);

                if (target == default)
                    ThrowWin32();

                try
                {
                    Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                }
                finally
                {
                    GlobalUnlock(target);
                }

                if (SetClipboardData(cfUnicodeText, hGlobal) == default)
                    ThrowWin32();

                hGlobal = default;
            }
            finally
            {
                if (hGlobal != default)
                    Marshal.FreeHGlobal(hGlobal);

                CloseClipboard();
            }
        }

        static void ThrowWin32() => throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    
}