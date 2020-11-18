using System;
using System.Runtime.InteropServices;
using ServiceStack.Text;
using WinApi.Windows;
using Xilium.CefGlue;
using Xilium.CefGlue.Wrapper;

namespace ServiceStack.CefGlue.Win64
{
    public class CefGlueHost : EventedWindowCore
    {
        private readonly CefConfig context;
        public CefGlueHost(CefConfig context) => this.context = context;

        public CefGlueBrowser browser;

        public CefGlueBrowser Browser => browser;

        protected bool MultiThreadedMessageLoop => context.CefSettings.MultiThreadedMessageLoop;

        protected override void OnCreate(ref CreateWindowPacket packet)
        {
            CefRuntime.Load();
            CefRuntime.EnableHighDpiSupport();

            var argv = context.Args;
            if (CefRuntime.Platform != CefRuntimePlatform.Windows)
            {
                argv = new string[context.Args.Length + 1];
                Array.Copy(context.Args, 0, argv, 1, context.Args.Length);
                argv[0] = "-";
            }

            var mainArgs = new CefMainArgs(argv);

            var app = new WebCefApp(context);

            var exitCode = CefRuntime.ExecuteProcess(mainArgs, app, IntPtr.Zero);
            if (exitCode != -1)
                return;

            CefRuntime.Initialize(mainArgs, context.CefSettings, app, IntPtr.Zero);

            this.browser = new CefGlueBrowser(this.Handle, app, context);

            base.OnCreate(ref packet);
        }

        protected override void OnSize(ref SizePacket packet)
        {
            base.OnSize(ref packet);
            var size = packet.Size;
            this.browser.ResizeWindow(size.Width, size.Height);
        }
        protected override void OnDestroy(ref Packet packet)
        {
            this.browser.Dispose();
            CefRuntime.Shutdown();
            base.OnDestroy(ref packet);
        }

        protected override void OnClose(ref Packet packet)
        {
            CefPlatformWindows.OnExit?.Invoke();
            base.OnClose(ref packet);
        }

        private void PostTask(CefThreadId threadId, Action action)
        {
            CefRuntime.PostTask(threadId, new ActionTask(action));
        }
        private class ActionTask : CefTask
        {
            private Action mAction;
            public ActionTask(Action action) => this.mAction = action;
            protected override void Execute()
            {
                this.mAction();
                this.mAction = null;
            }
        }

        internal sealed class WebCefApp : CefApp
        {
            private readonly CefConfig context;
            public string[] Args => context.Args;
            public WebCefApp(CefConfig context)
            {
                this.context = context;
            }

            protected override void OnRegisterCustomSchemes(CefSchemeRegistrar registrar)
            {
                foreach (var proxyScheme in context.Schemes)
                {
                    if (proxyScheme.Scheme == "http" || proxyScheme.Scheme == "https" || proxyScheme.SchemeOptions == null) 
                        continue;
                    registrar.AddCustomScheme(proxyScheme.Scheme, proxyScheme.SchemeOptions.Value);
                }
                foreach (var schemeFactory in context.SchemeFactories)
                {
                    if (schemeFactory.SchemeOptions == null) 
                        continue;
                    registrar.AddCustomScheme(schemeFactory.Scheme, schemeFactory.SchemeOptions.Value);
                }
            }

            protected override void OnBeforeCommandLineProcessing(string processType, CefCommandLine commandLine)
            {
                context.OnBeforeCommandLineProcessing?.Invoke(processType, commandLine);
            }

            protected override CefRenderProcessHandler GetRenderProcessHandler()
            {
                return new WebCefRenderProcessHandler(context);
            }

            protected override CefBrowserProcessHandler GetBrowserProcessHandler()
            {
                return new WebCefBrowserProcessHandler();
            }
        }
        
        public class WebCefRenderProcessHandler : CefRenderProcessHandler
        {
            private CefConfig context;
            public WebCefRenderProcessHandler(CefConfig context) => this.context = context;

            // internal CefMessageRouterRendererSide MessageRouter { get; private set; }
            //
            // public WebCefRenderProcessHandler()
            // {
            //     MessageRouter = new CefMessageRouterRendererSide(new CefMessageRouterConfig());
            // }

            protected override CefLoadHandler GetLoadHandler()
            {
                return base.GetLoadHandler();
            }

            protected override void OnBrowserCreated(CefBrowser browser, CefDictionaryValue extraInfo)
            {
                base.OnBrowserCreated(browser, extraInfo);
            }

            protected override void OnBrowserDestroyed(CefBrowser browser)
            {
                base.OnBrowserDestroyed(browser);
            }

            protected override void OnContextCreated(CefBrowser browser, CefFrame frame, CefV8Context context)
            {
                // MessageRouter.OnContextCreated(browser, frame, context);
                base.OnContextCreated(browser, frame, context);
            }

            protected override void OnContextReleased(CefBrowser browser, CefFrame frame, CefV8Context context)
            {
                // MessageRouter.OnContextReleased(browser, frame, context);
                base.OnContextReleased(browser, frame, context);
            }

            protected override void OnUncaughtException(CefBrowser browser, CefFrame frame, CefV8Context context, CefV8Exception exception,
                CefV8StackTrace stackTrace)
            {
                base.OnUncaughtException(browser, frame, context, exception, stackTrace);
            }

            protected override void OnFocusedNodeChanged(CefBrowser browser, CefFrame frame, CefDomNode node)
            {
                base.OnFocusedNodeChanged(browser, frame, node);
            }

            protected override bool OnProcessMessageReceived(CefBrowser browser, CefFrame frame, CefProcessId sourceProcess,
                CefProcessMessage message)
            {
                // MessageRouter.OnProcessMessageReceived(browser, sourceProcess, message);
                return base.OnProcessMessageReceived(browser, frame, sourceProcess, message);
            }

            protected override void OnWebKitInitialized()
            {
                base.OnWebKitInitialized();
            }
        }
        
        public class WebCefBrowserProcessHandler : CefBrowserProcessHandler
        {
            protected override void OnContextInitialized()
            {
                base.OnContextInitialized(); //#2
            }

            protected override void OnBeforeChildProcessLaunch(CefCommandLine commandLine)
            {
                base.OnBeforeChildProcessLaunch(commandLine); //#1
            }
            
            protected override void OnScheduleMessagePumpWork(long delayMs)
            {
                base.OnScheduleMessagePumpWork(delayMs);
            }

            protected override CefPrintHandler GetPrintHandler()
            {
                return base.GetPrintHandler();
            }
        }
        
        public class V8Handler : Xilium.CefGlue.CefV8Handler
        {
            protected override bool Execute(string name, CefV8Value obj, CefV8Value[] arguments, out CefV8Value returnValue, out string exception)
            {
                returnValue = CefV8Value.CreateNull();
                exception = null;
                return true;
            }
        }
                
    }
}