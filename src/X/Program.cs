using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using ServiceStack;

namespace Web
{
    public class Program
    {
        public static async Task Main(string[] cmdArgs)
        {
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