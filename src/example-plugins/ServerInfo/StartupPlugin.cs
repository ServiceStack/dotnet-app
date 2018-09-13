using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Text;

namespace ServerInfo
{
    public class StartupDep
    {
        public string Name { get; } = nameof(StartupDep);
    }

    public class StartupPlugin : IPlugin, IStartup
    {
        public void Configure(IApplicationBuilder app) {}

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new StartupDep());
            return null;
        }

        public void Register(IAppHost appHost)
        {
            appHost.GetPlugin<MetadataFeature>()
                .AddPluginLink("/startup-dep", "Startup Service");
        }       
    }

    [Route("/startup-dep")]
    public class GetStartupDep : IReturn<string> {}

    public class StartupServices : Service
    {
        public StartupDep StartupDep { get; set; }

        [AddHeader(ContentType = MimeTypes.PlainText)]
        public object Any(GetStartupDep request) => StartupDep.Name;
    }
}