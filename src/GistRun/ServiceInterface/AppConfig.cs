using System;
using ServiceStack;
using ServiceStack.IO;

namespace GistRun.ServiceInterface
{
    public class AppConfig
    {
        public int? ProcessTimeoutMs { get; set; }
        public string ProjectsBasePath { get; set; }
        public string GitHubAccessToken { get; set; }
        public string DotNetPath { get; set; }
        public int CacheResultsSecs { get; set; }
        public int CacheLatestGistCheckSecs { get; set; }

        public string GetExePath(IVirtualFiles vfs)
        {
            if (DotNetPath == null)
                throw new ArgumentException("Could not resolve path to 'dotnet'");

            return DotNetPath;
        }
    }
}