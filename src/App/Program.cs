using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using ServiceStack;
using ServiceStack.CefGlue;
using ServiceStack.Desktop;
using ServiceStack.Text;
using WebApp;
using Xilium.CefGlue;

namespace Web
{
    public class Program
    {
        public static async Task<int> Main(string[] cmdArgs)
        {
            try
            {
                Startup.AppVersion = $"Chromium: {CefRuntime.ChromeVersion}";
                Startup.GetAppHostInstructions = _ => new AppHostInstructions {
                    ImportParams = DesktopConfig.Instance.ImportParams,
                };
                DesktopState.AppDebug = Environment.GetEnvironmentVariable("APP_DEBUG") == "1";
                DesktopState.OriginalCommandArgs = cmdArgs;
                DesktopState.Tool = "app";
                DesktopState.ToolVersion = Startup.GetVersion();
                DesktopState.ChromeVersion = CefRuntime.ChromeVersion;
                Startup.ConfigureScript = feature => feature.ScriptMethods
                    .Add(new DesktopScripts(scope => DesktopState.BrowserHandle));
                
                var firstArg = cmdArgs.FirstOrDefault();
                if (firstArg?.StartsWith("app:") == true || firstArg?.StartsWith("sharp:") == true || firstArg?.StartsWith("xapp:") == true)
                {
                    DesktopState.FromScheme = true;
                    var cmds = firstArg.ConvertUrlSchemeToCommands();
                    
                    if (cmds.Any(x => Startup.DebugArgs.Contains(x) && !cmdArgs.Any(x => Startup.VerboseArgs.Contains(x))))
                        cmds.Add("-verbose");
                    
                    cmdArgs = cmds.ToArray();
                    if (DesktopState.AppDebug)
                        NativeWin.MessageBox(0, cmdArgs.Join(","), "cmdArgs", 0);
                }
                else
                {
                    CreateRegistryEntryFor("app");
                    CreateRegistryEntryFor("sharp");
                    CreateRegistryEntryFor("xapp", "x.exe");
                }

                var cefDebug = DesktopState.AppDebug = DesktopState.AppDebug || cmdArgs.Any(x => Startup.DebugArgs.Contains(x));
                if (DesktopState.AppDebug)
                    Startup.DebugMode = true;

                var args = DesktopState.CommandArgs = cmdArgs;
                var kiosk = args.Contains("-kiosk");
                if (kiosk)
                    args = args.Where(x => x != "-kiosk").ToArray();

                string startUrl = null;
                string favIcon = Startup.ToolFavIcon;
                
                var startPos = Array.IndexOf(args, "start");
                if (startPos >= 0)
                {
                    if (startPos == args.Length - 1)
                    {
                        Console.WriteLine(@"Usage: app start {url}");
                        return -1;
                    }
                    startUrl = args[startPos + 1];
                    if (startUrl.IndexOf("://", StringComparison.Ordinal) == -1)
                    {
                        Console.WriteLine(@$"Not a valid URL: '{startUrl}'");
                        Console.WriteLine(@"Usage: app start {url}");
                        return -2;
                    }
                }

                if (DesktopState.FromScheme && !DesktopState.AppDebug)
                {
                    var hWnd = CefPlatformWindows.GetConsoleHandle();
                    if (hWnd != IntPtr.Zero)
                        hWnd.ShowWindow(0);
                }
                
                var cts = new CancellationTokenSource();
                Process process = null;
                CefPlatformWindows.OnExit = () => {
                    if (Startup.Verbose) $"OnExit".Print();
                    DesktopConfig.Instance.OnExit?.Invoke();
                    cts?.Cancel();
                    process?.Close();
                };

                if (startUrl == null)
                {
                    var host = await Startup.CreateWebHost("app", args, new WebAppEvents {
                        CreateShortcut = Shortcut.Create,
                        HandleUnknownCommand = ctx => Startup.PrintUsage("app"),
                        OpenBrowser = url => CefPlatformWindows.Start(new CefConfig {
                            StartUrl = url, Width = 1040, DevTools = false, Icon = Startup.ToolFavIcon, 
                            HideConsoleWindow = !DesktopState.AppDebug,
                        }),
                        RunNetCoreProcess = ctx => {
                            var url = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.LeftPart(';') ??
                                      "http://localhost:5000";
                            var target = ctx.RunProcess;

                            var fileName = ctx.RunProcess;
                            var arguments = "";
                            if (target.EndsWith(".dll"))
                            {
                                fileName = "dotnet";
                                arguments = ctx.RunProcess;
                            }

                            process = Startup.PipeProcess(fileName, arguments, fn: () =>
                                CefPlatformWindows.Start(new CefConfig { StartUrl = url, Icon = ctx.FavIcon, 
                                    HideConsoleWindow = !DesktopState.AppDebug }));
                        },
                    });
                
                    if (host == null)
                        return 0;

                    startUrl = host.StartUrl;
                    favIcon = host.FavIcon;
                    cefDebug = host.DebugMode || DesktopState.AppDebug;
#pragma warning disable 4014
                    host.Build().StartAsync(cts.Token);
#pragma warning restore 4014
                }

                var config = new CefConfig(cefDebug) {
                    Args = args,
                    StartUrl = startUrl,
                    Icon = favIcon,
                    CefSettings = {
                        PersistSessionCookies = true,
                    },
                    CefBrowserSettings = new CefBrowserSettings
                    {
                        DefaultEncoding = "UTF-8",
                        FileAccessFromFileUrls = CefState.Enabled,
                        UniversalAccessFromFileUrls = CefState.Enabled,
                        JavaScriptCloseWindows = CefState.Enabled,
                        JavaScriptAccessClipboard = CefState.Enabled,
                        JavaScriptDomPaste = CefState.Enabled,
                        JavaScript = CefState.Enabled,
                    },
                };

                if ("name".TryGetAppSetting(out var name))
                    config.WindowTitle = name;

                if ("CefConfig".TryGetAppSetting(out var cefConfigString))
                {
                    var cefConfig = JS.eval(cefConfigString);
                    if (cefConfig is Dictionary<string, object> objDictionary)
                        objDictionary.PopulateInstance(config);
                }

                if (kiosk)
                    config.Kiosk = true;

                if ("CefConfig.CefSettings".TryGetAppSetting(out var cefSettingsString))
                {
                    var cefSettings = JS.eval(cefSettingsString);
                    if (cefSettings is Dictionary<string, object> objDictionary)
                        objDictionary.PopulateInstance(config.CefSettings);
                }

                void allowCors(ProxyScheme proxyScheme, string origin)
                {
                    proxyScheme.IgnoreHeaders.AddIfNotExists("Content-Security-Policy");
                    proxyScheme.AddHeaders[HttpHeaders.AllowMethods] = "GET, POST, PUT, DELETE, PATCH, OPTIONS, HEAD";
                    proxyScheme.AddHeaders[HttpHeaders.AllowHeaders] = "Content-Type";
                    proxyScheme.AddHeaders[HttpHeaders.AllowCredentials] = "true";
                    proxyScheme.OnResponseHeaders = headers => headers[HttpHeaders.AllowOrigin] = origin;
                }

                var i = 0;
                while ($"CefConfig.Schemes[{i++}]".TryGetAppSetting(out var proxyConfigString))
                {
                    var proxyScheme = new ProxyScheme();
                    var objDictionary = (Dictionary<string, object>)JS.eval(proxyConfigString);
                    objDictionary.PopulateInstance(proxyScheme);
                    if (proxyScheme.AllowCors)
                    {
                        allowCors(proxyScheme, startUrl);
                    }
                    if (objDictionary.ContainsKey("allowIFrames"))
                    {
                        proxyScheme.IgnoreHeaders.AddIfNotExists("Content-Security-Policy");
                        proxyScheme.IgnoreHeaders.Add("X-Frame-Options");
                    }
                    config.Schemes.Add(proxyScheme);
                }
                foreach (var proxyConfig in DesktopConfig.Instance.ProxyConfigs)
                {
                    var proxyScheme = proxyConfig.ConvertTo<ProxyScheme>();
                    if (proxyScheme.AllowCors)
                        allowCors(proxyScheme, startUrl);
                    config.Schemes.Add(proxyScheme);
                }

                if (config.EnableCorsScheme)
                {
                    var corsScheme = CreateCorsProxy();
                    allowCors(corsScheme, config.StartUrl);
                    config.SchemeFactories.Add(
                        new SchemeFactory("cors", new CefProxySchemeHandlerFactory(corsScheme)));
                }

                var appHost = AppLoader.TryGetAppHost();
                config.SchemeFactories.Add(appHost != null
                    ? new SchemeFactory("host", new CefAppHostSchemeHandlerFactory(appHost))
                    : new SchemeFactory("host", new CefProxySchemeHandlerFactory(CreateCorsProxy("host", startUrl)) {
                        RequestFilter = (webReq, request) => {
                            webReq.Headers["X-Window-Handle"] = DesktopState.BrowserHandle.ToString();
                            webReq.Headers["X-Desktop-Info"] = NativeWin.GetDesktopInfo().ToJsv();
                        }
                    }));

                if (DesktopState.AppDebug)
                {
                    config.HideConsoleWindow = false;
                    config.Verbose = true;
                }

                return CefPlatformWindows.Start(config);
            }
            catch (Exception ex)
            {
                DesktopConfig.Instance.OnError?.Invoke(ex);
                
                if (DesktopState.AppDebug)
                   NativeWin.MessageBox(0, ex.Message, "Exception", 0);
                    
                ex.HandleProgramExceptions();
                return -1;
            }
            finally
            {
                CefPlatformWindows.Provider?.ShowConsoleWindow();
            }
        }

        private static ProxyScheme CreateCorsProxy(string domain="cors", string startUrl=null)
        {
            var corsScheme = new ProxyScheme {
                Scheme = "https",
                Domain = domain,
                TargetScheme = "https",
                AllowCors = true,
                SchemeOptions = CefSchemeOptions.Secure | CefSchemeOptions.CorsEnabled | CefSchemeOptions.CspBypassing |
                                CefSchemeOptions.FetchEnabled,
                ResolveUrl = url => {
                    var hostAndPath = url.RightPart("://").RightPart('/'); //https://cors/^...
                    if (startUrl == null)
                    {
                        var targetHost = hostAndPath.LeftPart('/');
                        var targetPort = targetHost.IndexOf(':') >= 0 ? targetHost.RightPart(':') : null;
                        var targetScheme = targetPort == "80" ? "http" : "https";
                        var targetUrl = targetScheme + "://" + targetHost.LeftPart(':') + "/" + hostAndPath.RightPart('/');
                        return targetUrl;
                    }
                    return startUrl.CombineWith(hostAndPath); //host
                },
            };
            return corsScheme;
        }

        private static void CreateRegistryEntryFor(string rootKey, string exeName = "app.exe")
        {
            if (Environment.GetEnvironmentVariable("APP_NOSCHEME") == "1") return;
            
            var openKeys = new List<RegistryKey>();
            RegistryKey recordKey(RegistryKey key)
            {
                if (key != null) openKeys.Add(key);
                return key;
            }

            try
            {
                var appKey = recordKey(Registry.CurrentUser.OpenSubKey("Software")?.OpenSubKey("Classes")?.OpenSubKey(rootKey));
                if (appKey == null)
                {
                    var userRoot = recordKey(Registry.CurrentUser.OpenSubKey("Software", true))?
                        .OpenSubKey("Classes", true);   
                    var key = userRoot.CreateSubKey(rootKey);   
                    key.SetValue("URL Protocol", "Sharp App");   
                    var profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var commandStr = Path.Combine(profilePath, ".dotnet", "tools", exeName) + " \"%1\"";
                    key.CreateSubKey(@"shell\open\command")?.SetValue("", commandStr);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                foreach (var key in openKeys)
                {
                    try
                    {
                        key.Close();
                    }
                    catch { }
                }
            }
        }


    }
}
