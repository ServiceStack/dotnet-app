using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        private static bool AppDebug = Environment.GetEnvironmentVariable("APP_DEBUG") == "1";
        public static bool FromScheme;
        public static async Task<int> Main(string[] cmdArgs)
        {
            try
            {
                Startup.AppVersion = $"Chromium: {CefRuntime.ChromeVersion}";
                Startup.GetAppHostInstructions = _ => new AppHostInstructions {
                    ImportParams = DesktopConfig.Instance.ImportParams,
                };
                DesktopConfig.Instance.Tool = "app";
                DesktopConfig.Instance.ToolVersion = Startup.GetVersion();
                DesktopConfig.Instance.ChromeVersion = CefRuntime.ChromeVersion;
                Startup.ConfigureScript = feature => feature.ScriptMethods.Add(new DesktopScriptMethods(AppLoader.AppHost));
                
                var firstArg = cmdArgs.FirstOrDefault();
                if (firstArg?.StartsWith("app:") == true || firstArg?.StartsWith("sharp:") == true || firstArg?.StartsWith("xapp:") == true)
                {
                    FromScheme = true;
                    var cmds = firstArg.ConvertUrlSchemeToCommands();
                    cmdArgs = cmds.ToArray();
                    if (AppDebug)
                        DesktopScriptMethods.MessageBox(0, cmdArgs.Join(","), "cmdArgs", 0);
                }
                else
                {
                    CreateRegistryEntryFor("app");
                    CreateRegistryEntryFor("sharp");
                    CreateRegistryEntryFor("xapp", "x.exe");
                }

                AppDebug = AppDebug || cmdArgs.Any(x => Startup.VerboseArgs.Contains(x));

                if (FromScheme && !AppDebug)
                {
                    Console.Title = "Sharp Apps Launcher - " + (cmdArgs.FirstOrDefault() ?? Guid.NewGuid().ToString().Substring(0,5));
                    var hWnd = CefPlatformWindows.FindWindow(null, Console.Title);
                    if (hWnd != IntPtr.Zero)
                        CefPlatformWindows.ShowWindow(hWnd, 0);
                }
                
                var args = cmdArgs;
                
                var cts = new CancellationTokenSource();
                Process process = null;
                CefPlatformWindows.OnExit = () => {
                    if (Startup.Verbose) $"OnExit".Print();
                    cts?.Cancel();
                    process?.Close();
                };

                var host = await Startup.CreateWebHost("app", args, new WebAppEvents {
                    CreateShortcut = Shortcut.Create,
                    HandleUnknownCommand = ctx => Startup.PrintUsage("app"),
                    OpenBrowser = url => CefPlatformWindows.Start(new CefConfig {
                        StartUrl = url, Width = 1040, DevTools = false, Icon = Startup.ToolFavIcon, HideConsoleWindow = !AppDebug,
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
                            CefPlatformWindows.Start(new CefConfig { StartUrl = url, Icon = ctx.FavIcon, HideConsoleWindow = !AppDebug }));
                    },
                });
                
                if (host == null)
                    return 0;

#pragma warning disable 4014
                host.Build().StartAsync(cts.Token);
#pragma warning restore 4014

                var config = new CefConfig(host.DebugMode) {
                    Args = args,
                    StartUrl = host.StartUrl,
                    Icon = host.FavIcon,
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
                        allowCors(proxyScheme, host.StartUrl);
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
                        allowCors(proxyScheme, host.StartUrl);
                    config.Schemes.Add(proxyScheme);
                }

                if (config.EnableCorsScheme)
                {
                    var corsScheme = new ProxyScheme {
                        Scheme = "https",
                        Domain = "cors",
                        TargetScheme = "https",
                        AllowCors = true,
                        SchemeOptions = CefSchemeOptions.Secure | CefSchemeOptions.CorsEnabled | CefSchemeOptions.CspBypassing | CefSchemeOptions.FetchEnabled,
                        ResolveUrl = url => {
                            var hostAndPath = url.RightPart("://").RightPart('/'); //https://cors/^...
                            var host = hostAndPath.LeftPart('/');
                            var port = host.IndexOf(':') >= 0 ? host.RightPart(':') : null;
                            var scheme = port == "80" ? "http" : "https";
                            var useUrl = scheme + "://" + host.LeftPart(':') + "/" + hostAndPath.RightPart('/');
                            return useUrl;
                        },
                    };
                    allowCors(corsScheme, config.StartUrl);
                    config.SchemeFactories.Add(
                        new SchemeFactory("cors", new CefProxySchemeHandlerFactory(corsScheme)));
                }
                
                config.SchemeFactories.Add(
                    new SchemeFactory("host", new CefAppHostSchemeHandlerFactory(AppLoader.AppHost)));

                if (AppDebug)
                {
                    config.HideConsoleWindow = false;
                    config.Verbose = true;
                }

                return CefPlatformWindows.Start(config);
            }
            catch (Exception ex)
            {
                if (AppDebug)
                   DesktopScriptMethods.MessageBox(0, ex.Message, "Exception", 0);
                    
                ex.HandleProgramExceptions();
                return -1;
            }
            finally
            {
                CefPlatformWindows.Provider?.ShowConsoleWindow();
            }
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
