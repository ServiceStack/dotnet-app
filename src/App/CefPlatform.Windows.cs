using System;
using System.Linq;
using PInvoke;
using Xilium.CefGlue;
using WinApi.Windows;
using ServiceStack.CefGlue.Win64;
using ServiceStack.Desktop;
using Web;
using Xilium.CefGlue.Wrapper;

namespace ServiceStack.CefGlue
{
    public sealed class CefPlatformWindows : CefPlatform
    {
        private static CefPlatformWindows provider;
        public static CefPlatformWindows Provider => provider ??= new CefPlatformWindows();
        private CefPlatformWindows() { }

        public static Action OnExit { get; set; }

        public static int Start(CefConfig config)
        {
            Instance = Provider;
            return Provider.Run(config);
        }

        private CefGlueHost window;
        public CefGlueHost Window => window;

        private CefConfig config;

        public int Run(CefConfig config)
        {
            this.config = config;
            var res = Instance.GetScreenResolution();
            if (config.FullScreen || config.Kiosk)
            {
                config.Width = (int) (ScaleFactor * res.Width) -1;
                config.Height = (int) (ScaleFactor * res.Height);
            }
            else
            {
                config.Width = (int) (config.Width > 0 ? config.Width * ScaleFactor : res.Width * .75);
                config.Height = (int) (config.Height > 0 ? config.Height * ScaleFactor : res.Height * .75);
            }

            if (config.HideConsoleWindow && !config.Verbose)
                Instance.HideConsoleWindow();

            var factory = WinapiHostFactory.Init(config.Icon);
            using (window = factory.CreateWindow(
                () => new CefGlueHost(config),
                config.WindowTitle,
                constructionParams: new FrameWindowConstructionParams()))
            {
                try
                {
                    DesktopState.BrowserHandle = window.Handle;
                    DesktopState.ConsoleHandle = ConsoleHandle;

                    foreach (var scheme in config.Schemes)
                    {
                        CefRuntime.RegisterSchemeHandlerFactory(scheme.Scheme, scheme.Domain,
                            new CefProxySchemeHandlerFactory(scheme));
                        if (scheme.AllowCors && scheme.Domain != null)
                        {
                            CefRuntime.AddCrossOriginWhitelistEntry(config.StartUrl, scheme.TargetScheme ?? scheme.Scheme,
                                scheme.Domain, true);
                        }
                    }

                    foreach (var schemeFactory in config.SchemeFactories)
                    {
                        CefRuntime.RegisterSchemeHandlerFactory(schemeFactory.Scheme, schemeFactory.Domain,
                            schemeFactory.Factory);
                        if (schemeFactory.AddCrossOriginWhitelistEntry)
                            CefRuntime.AddCrossOriginWhitelistEntry(config.StartUrl, schemeFactory.Scheme,
                                schemeFactory.Domain, true);
                    }

                    // if (config.Verbose)
                    // {
                    //     Console.WriteLine(
                    //         @$"GetScreenResolution: {res.Width}x{res.Height}, scale:{ScaleFactor}, {(int) (ScaleFactor * res.Width)}x{(int) (ScaleFactor * res.Width)}");
                    //     var rect = Instance.GetClientRectangle(window.Handle);
                    //     Console.WriteLine(
                    //         @$"GetClientRectangle:  [{rect.Top},{rect.Left}] [{rect.Bottom},{rect.Right}], scale: [{(int) (rect.Bottom * ScaleFactor)},{(int) (rect.Right * ScaleFactor)}]");
                    // }

                    if (config.CenterToScreen)
                    {
                        window.CenterToScreen();
                    }
                    else if (config.X != null || config.Y != null)
                    {
                        window.SetPosition(config.X.GetValueOrDefault(), config.Y.GetValueOrDefault());
                    }
                    if (config.Kiosk || config.FullScreen)
                    {
                        Instance.SetWindowFullScreen(window.Handle);
                    }
                    else
                    {
                        window.SetSize(config.Width, config.Height - 1); //force redraw in BrowserCreated
                    }

                    window.Browser.BrowserCreated += (sender, args) => {
                        var cef = (CefGlueBrowser) sender;
                        if (!cef.Config.Kiosk)
                        {
                            window.SetSize(config.Width, config.Height); //trigger refresh to sync browser frame with window
                            if (config.CenterToScreen)
                                window.CenterToScreen();

                            if (config.FullScreen)
                            {
                                User32.ShowWindow(window.Handle, User32.WindowShowStyle.SW_MAXIMIZE);
                            }
                        }
                        else
                        {
                            EnterKioskMode();
                            cef.BrowserWindowHandle.ShowScrollBar(NativeWin.SB_BOTH, false);
                        }
                    };

                    window.Show();

                    return new EventLoop().Run(window);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        private CefSize? screenResolution;
        public CefSize ScreenResolution => screenResolution ??= GetScreenResolution();

        public float? scaleFactor;
        public float ScaleFactor => scaleFactor ??= User32.GetDC(IntPtr.Zero).GetScalingFactor();

        private IntPtr? consoleHandle;
        public IntPtr ConsoleHandle => consoleHandle ??= GetConsoleHandle();
        
        public static IntPtr GetConsoleHandle()
        {
            if (DesktopState.ConsoleHandle != IntPtr.Zero)
                return DesktopState.ConsoleHandle;

            Console.Title = "App Launcher - " + (DesktopState.CommandArgs?.FirstOrDefault()
                                                 ?? Guid.NewGuid().ToString().Substring(0, 5));

            return DesktopState.ConsoleHandle = User32.FindWindow(null, Console.Title);
        }

        public void ShowConsoleWindow()
        {
            if (this.config?.HideConsoleWindow != true)
                return;

            var hWnd = ConsoleHandle;
            if (hWnd != IntPtr.Zero)
                User32.ShowWindow(hWnd, User32.WindowShowStyle.SW_SHOW);
        }

        public override void HideConsoleWindow()
        {
            var hWnd = ConsoleHandle;
            if (hWnd != IntPtr.Zero)
                User32.ShowWindow(hWnd, User32.WindowShowStyle.SW_HIDE);
        }

        CefSize ToCefSize(System.Drawing.Size size) => new CefSize(size.Width, size.Height);

        public override CefSize GetScreenResolution() => ToCefSize(NativeWin.GetScreenResolution());

        public override System.Drawing.Rectangle GetClientRectangle(IntPtr handle)
        {
            handle.GetClientRect(out var result);
            return System.Drawing.Rectangle.FromLTRB(result.left, result.top, result.right, result.bottom);
        }

        public override void ResizeWindow(IntPtr handle, int width, int height) => handle.ResizeWindow(width, height);

        private void EnterKioskMode()
        {
            var mi = window.Handle.SetKioskMode();
        }

        public void SetWindowFullScreen() => SetWindowFullScreen(window.Handle);

        public override void SetWindowFullScreen(IntPtr handle) => handle.SetWindowFullScreen();

        public void ShowScrollBar(bool show) => window.Handle.ShowScrollBar(show);

        public override void ShowScrollBar(IntPtr handle, bool show) => handle.ShowScrollBar(show);
    }

    public class WebCefMessageRouterHandler : CefMessageRouterBrowserSide.Handler
    {
        private CefPlatformWindows app;

        public WebCefMessageRouterHandler(CefPlatformWindows app)
        {
            this.app = app;
        }

        public override bool OnQuery(CefBrowser browser, CefFrame frame, long queryId, string request, bool persistent,
            CefMessageRouterBrowserSide.Callback callback)
        {
            return base.OnQuery(browser, frame, queryId, request, persistent, callback);
        }

        public override void OnQueryCanceled(CefBrowser browser, CefFrame frame, long queryId)
        {
            base.OnQueryCanceled(browser, frame, queryId);
        }
    }

}