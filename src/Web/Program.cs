using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace WebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var host = Startup.CreateWebHost("web", args)?.Build();
                host?.Run();
            } 
            catch (Exception ex)
            {
                Console.WriteLine(Startup.Verbose ? ex.ToString() : ex.Message);
            }
        }
    }
}