using System;
using System.Linq;
using ServiceStack;
using ServiceStack.Text;

namespace ServerInfo
{
    public class CustomPlugin : IPlugin
    {
        public bool ShowDrivesLinks { get; set; } = true;
        
        public bool ShowProcessLinks { get; set; }

        public void Register(IAppHost appHost)
        {
            if (ShowDrivesLinks)
            {
                var diskFormat = Env.IsWindows ? "NTFS" : "ext2";
                appHost.GetPlugin<MetadataFeature>()
                    .AddPluginLink("/drives", "All Disks")
                    .AddPluginLink($"/drives?DriveFormatIn={diskFormat}", $"{diskFormat} Disks");
            }

            if (ShowProcessLinks)
            {
                appHost.GetPlugin<MetadataFeature>()
                    .AddPluginLink("/processes", "All Processes")
                    .AddPluginLink("/process/current", "Current Process");
            }
        }
    }
}