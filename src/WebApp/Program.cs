using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.CefGlue;
using ServiceStack.Text;

namespace Web
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                var cts = new CancellationTokenSource();
                Process process = null;
                CefPlatformWindows.OnExit = () => {
                    if (Startup.Verbose) $"OnExit".Print();
                    cts?.Cancel();
                    process?.Close();
                    CefPlatformWindows.Provider.ShowConsoleWindow();
                };
                
                var host = await Startup.CreateWebHost("app", args, new WebAppEvents
                    {
                        CreateShortcut = Shortcut.Create,
                        HandleUnknownCommand = ctx => Startup.PrintUsage("app"),
                        OpenBrowser = url => CefPlatformWindows.Start(new CefConfig { 
                            StartUrl = url, Width = 1040, DevTools = false, Icon = Startup.ToolFavIcon }),
                        RunNetCoreProcess = ctx =>
                        {
                            var url = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.LeftPart(';') ?? "http://localhost:5000";
                            var target = ctx.RunProcess;

                            var fileName = ctx.RunProcess;
                            var arguments = "";
                            if (target.EndsWith(".dll"))
                            {
                                fileName = "dotnet";
                                arguments = ctx.RunProcess;
                            }
                            
                            process = Startup.PipeProcess(fileName, arguments, fn: () => 
                                CefPlatformWindows.Start(new CefConfig { StartUrl = url, Icon = ctx.FavIcon }));
                        },
                        SelectFolder = options => {

                            var dlgArgs = new OpenFileName {
                                hwndOwner = Process.GetCurrentProcess().MainWindowHandle,
                                
                                Flags = options.Flags.GetValueOrDefault(),
                                lpstrTitle = options.Title,
                                lpstrFilter = options.Filter,
                                lpstrInitialDir = options.InitialDir,
                                lpstrDefExt = options.DefaultExt,
                            };

                            if (options.IsFolderPicker)
                            {
                                //HACK http://unafaltadecomprension.blogspot.com/2013/04/browsing-for-files-and-folders-c.html
                                dlgArgs.Flags |= (int)(FileOpenOptions.NoValidate | FileOpenOptions.PathMustExist);
                                dlgArgs.lpstrFile = "Folder Selection.";
                            }

                            if (GetOpenFileName(dlgArgs))
                            {
                                //var fileName = Marshal.PtrToStringAuto(dlgArgs.lpstrFile);
                                var fileName = dlgArgs.lpstrFile.Replace("Folder Selection", "");
                                var ret = new DialogResult {
                                    FolderPath = fileName,
                                    Ok = true,
                                };
                                return ret;
                            }
                            
                            return new DialogResult();
                        }
                });
                if (host == null)
                    return 0;

#pragma warning disable 4014
                host.Build().StartAsync(cts.Token);
#pragma warning restore 4014
                
                var config = new CefConfig(host.DebugMode)
                {
                    Args = args,
                    StartUrl = host.StartUrl,
                    Icon = host.FavIcon,
                };

                if ("name".TryGetAppSetting(out var name))
                    config.WindowTitle = name;

                if ("CefConfig".TryGetAppSetting(out var cefConfigString))
                {
                    var cefConfig = JS.eval(cefConfigString);
                    if (cefConfig is Dictionary<string, object> objDictionary)
                        objDictionary.PopulateInstance(config);
                }
                if ("CefConfig.CefSettings".TryGetAppSetting(out var cefSettingsString))
                {
                    var cefSettings = JS.eval(cefSettingsString);
                    if (cefSettings is Dictionary<string, object> objDictionary)
                        objDictionary.PopulateInstance(config.CefSettings);
                }

                return CefPlatformWindows.Start(config);
            } 
            catch (Exception ex)
            {
                ex.HandleProgramExceptions();
                return -1;
            }
        }

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
    }
}
