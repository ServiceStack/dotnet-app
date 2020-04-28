using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using ServiceStack;
using ServiceStack.Desktop;

namespace Web
{
    public class Program
    {
        public static async Task Main(string[] cmdArgs)
        {
            Startup.GetAppHostInstructions = _ => new AppHostInstructions {
                ImportParams = DesktopConfig.Instance.ImportParams,
            };
            DesktopConfig.Instance.Tool = "x";
            DesktopConfig.Instance.ToolVersion = Startup.GetVersion();
            
            try
            {
                var firstArg = cmdArgs.FirstOrDefault();
                if (firstArg?.StartsWith("app:") == true || firstArg?.StartsWith("sharp:") == true || firstArg?.StartsWith("xapp:") == true)
                {
                    var cmds = firstArg.ConvertUrlSchemeToCommands();
                    cmdArgs = cmds.ToArray();
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
    }
}