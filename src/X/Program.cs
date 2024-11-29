using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Win32;
using ServiceStack;
using ServiceStack.Desktop;
using ServiceStack.Text;

namespace Web
{
    public class Program
    {
        public static async Task Main(string[] cmdArgs)
        {
            try
            {
                Startup.GetAppHostInstructions = _ => new AppHostInstructions {
                    ImportParams = DesktopConfig.Instance.ImportParams,
                };
                DesktopState.Tool = "x";
                DesktopState.ToolVersion = Startup.GetVersion();
                DesktopState.AppDebug = cmdArgs.Any(x => Startup.DebugArgs.Contains(x));
                if (DesktopState.AppDebug)
                    Startup.DebugMode = true;
            
                var firstArg = cmdArgs.FirstOrDefault();
                if (firstArg?.StartsWith("app:") == true || firstArg?.StartsWith("sharp:") == true || firstArg?.StartsWith("xapp:") == true)
                {
                    var cmds = firstArg.ConvertUrlSchemeToCommands();
                    cmdArgs = cmds.ToArray();
                }
                else if (firstArg?.StartsWith("gist:") == true)
                {
                    cmdArgs = firstArg.ConvertUrlSchemeToCommands("gist-open").ToArray();
                }
                else
                {
                    CreateRegistryEntryFor("gist");
                    CreateRegistryEntryFor("xapp");
                }
                
                var args = cmdArgs;
                
                var host = (await Startup.CreateWebHost("x", args))?.Build();
                host?.Run();
            } 
            catch (Exception ex)
            {
                ex.HandleProgramExceptions();
            }
        }
        
        private static void CreateRegistryEntryFor(string rootKey, string exeName = "x.exe")
        {
            if (!Env.IsWindows) return;
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
                    key.SetValue("URL Protocol", "x");   
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