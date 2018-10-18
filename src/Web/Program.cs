using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace WebApp
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
                Console.WriteLine(Startup.Verbose ? ex.ToString() : ex.Message);
            }
        }
    }
}