using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using ServiceStack;
using WebApp;

namespace Run
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
                ex = ex.UnwrapIfSingleException();
                Console.WriteLine(Startup.Verbose ? ex.ToString() : ex.Message);
            }
        }
    }
}