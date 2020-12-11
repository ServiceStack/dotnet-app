using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Funq;
using ServiceStack;
using ServiceStack.Configuration;
using GistRun.ServiceInterface;
using ServiceStack.Script;
using ServiceStack.Web;
using System;
using System.IO;
using ServiceStack.IO;
using ServiceStack.Text;
using ServiceStack.Logging;

namespace GistRun
{
    public class Startup : ModularStartup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public new void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseServiceStack(new AppHost
            {
                AppSettings = new NetCoreAppSettings(Configuration)
            });
        }
    }

    public class AppHost : AppHostBase
    {
        public AppHost() : base("Gist Run", typeof(RunGistServices).Assembly) { }

        // Configure your AppHost with the necessary configuration and dependencies your App needs
        public override void Configure(Container container)
        {
            SetConfig(new HostConfig
            {
                UseSameSiteCookies = true,
                DebugMode = HostingEnvironment.IsDevelopment()
            });
            
            var dataDir = Path.Combine(HostingEnvironment.ContentRootPath, "App_Data");
            AppDomain.CurrentDomain.SetData("DataDirectory", dataDir);

            container.Register(new AppConfig {
                ProjectsBasePath = dataDir,
                GitHubAccessToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
                DotNetPath = ScriptContext.ProtectedMethods.exePath("dotnet"),
                ProcessTimeoutMs = AppSettings.Get(nameof(AppConfig.ProcessTimeoutMs), 60 * 1000),
                CacheResultsSecs = AppSettings.Get(nameof(AppConfig.CacheResultsSecs), 24 * 60 * 60),
            });

            Plugins.Add(new SharpPagesFeature());

            container.Register(c =>
                new StatsLogger(new FileSystemVirtualFiles(Path.Combine(dataDir, "stats").AssertDir())));

            Plugins.Add(new ServerEventsFeature());
        }
    }
}
