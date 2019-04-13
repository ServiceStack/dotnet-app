using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
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
                await Startup.Mix(args);
            } 
            catch (Exception ex)
            {
                ex.HandleProgramExceptions();
            }
        }
    }
}