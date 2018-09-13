using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using ServiceStack;
using ServiceStack.CefGlue;
using ServiceStack.Text;

namespace WebApp
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var host = Startup.CreateWebHost("win", args, new WebAppEvents
                    {
                        CreateShortcut = Shortcut.Create,
                        HandleUnknownCommand = ctx => Startup.PrintUsage("win"),
                        OpenBrowser = url => CefPlatformWindows.Start(new CefConfig { 
                            StartUrl = url, Width = 1040, DevTools = false, Icon = Startup.ToolFavIcon, HideConsoleWindow = false }),
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

                            using (var process = new Process
                            {
                                StartInfo =
                                {
                                    FileName = fileName,
                                    Arguments = arguments,
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                    RedirectStandardOutput = true,
                                }
                            })
                            {
                                process.OutputDataReceived += (sender, data) => {
                                    Console.WriteLine(data.Data);
                                };
                                process.StartInfo.RedirectStandardError = true;
                                process.ErrorDataReceived += (sender, data) => {
                                    Console.WriteLine(data.Data);
                                };
                                process.Start();

                                process.BeginOutputReadLine();
                                process.BeginErrorReadLine();

                                CefPlatformWindows.Start(new CefConfig { StartUrl = url, Icon = ctx.FavIcon });

                                process.Kill();
                                process.Close();
                            }
                        }
                });
                if (host == null)
                    return 0;

                host.Build().StartAsync();
                
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
                Console.WriteLine(Startup.Verbose ? ex.ToString() : ex.Message);
                return -1;
            }
        }
    }
}
