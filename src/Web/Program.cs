using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using ServiceStack;

namespace Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var host = (await Startup.CreateWebHost("web", args))?.Build();
                host?.Run();
            } 
            catch (Exception ex)
            {
                ex.HandleProgramExceptions();
            }
        }
    }
}