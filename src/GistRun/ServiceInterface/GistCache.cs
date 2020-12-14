using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.IO;
using ServiceStack.Logging;

namespace GistRun.ServiceInterface
{
    public class GistCache
    {
        public const string GistCachePath = "cache/gists";
        public static ILog log = LogManager.GetLogger(typeof(GistCache));

        private readonly AppConfig appConfig;
        private readonly GitHubGateway gateway;
        
        public GistCache(AppConfig appConfig, GitHubGateway gateway)
        {
            this.appConfig = appConfig;
            this.gateway = gateway;
        }

        async Task<string> GetCachedVersion(string gistId)
        {
            var latestInfoPath = Path.Combine(gistId.LeftPart('/'), "latest");
            var latestInfo = File.Exists(latestInfoPath)
                ? (await File.ReadAllTextAsync(latestInfoPath).ConfigureAwait(false)).Trim().Split(' ')
                : null;
            if (latestInfo?.Length > 1 && long.TryParse(latestInfo[0], out var ticks) &&
                new DateTime(ticks, DateTimeKind.Utc) + TimeSpan.FromSeconds(appConfig.CacheLatestGistCheckSecs) >
                DateTime.UtcNow)
            {
                var latestVersionPath = Path.Combine(GistCachePath, latestInfo[1] + ".json");
                return await File.ReadAllTextAsync(latestVersionPath);
            }
            return null;
        }

        public async Task<GithubGist> GetGistAsync(string gistId, bool nocache=false)
        {
            var isVersion = gistId.IndexOf('/') >= 0;
            var versionPath = isVersion
                ? Path.Combine(GistCachePath, gistId + ".json")
                : null;

            string gistJson = null;
            var versionExists = versionPath != null && File.Exists(versionPath);
            if (versionExists) //always use immutable cached version if exists
            {
                gistJson = await File.ReadAllTextAsync(versionPath);
            }
            else
            {
                if (!nocache)
                {
                    gistJson = await GetCachedVersion(gistId);                    
                }
                if (gistJson == null)
                {
                    try
                    {
                        gistJson = await gateway.GetJsonAsync($"/gists/{gistId}").ConfigureAwait(false);
                    }
                    catch (WebException e)
                    {
                        if (e.GetStatus() == HttpStatusCode.Forbidden && nocache) // rate limit exceeded, fallback to cache version if exists.
                        {
                            gistJson = await GetCachedVersion(gistId);                    
                        }
                        if (gistJson == null)
                            throw;
                    }
                }
            }

            var gist = gistJson.FromJson<GithubGist>();
            var gistVersion = gist.History[0].Version;
            await CacheGistAsync(gist.Id, gistVersion, gistJson, isLatest:!isVersion);
            return gist;
        }

        public async Task CacheGistAsync(string gistId, string gistVersion, string gistJson, bool isLatest=false)
        {
            try
            {
                var gistCachePath = Path.Combine(appConfig.ProjectsBasePath, Path.Combine(GistCachePath, gistId));
                FileSystemVirtualFiles.AssertDirectory(gistCachePath);
                await File.WriteAllTextAsync(Path.Combine(gistCachePath, $"{gistVersion}.json"), gistJson);
                if (isLatest)
                {
                    var now = DateTime.UtcNow.Ticks;
                    var contents = now + " " + gistVersion;
                    await File.WriteAllTextAsync(Path.Combine(gistCachePath, "latest"),contents);
                }
            }
            catch (Exception e)
            {
                log.Error($"Could not cache gist '{gistId}'", e);
            }
        }
    }
}