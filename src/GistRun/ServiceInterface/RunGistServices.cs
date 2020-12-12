using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.IO;
using GistRun.ServiceModel;
using ServiceStack.Script;

namespace GistRun.ServiceInterface
{
    public class RunGistServices : Service
    {
        public IServerEvents ServerEvents { get; set; }
        public AppConfig AppConfig { get; set; }
        
        public StatsLogger StatsLogger { get; set; }
        
        public object Any(Hello request) => new HelloResponse { Result = $"Hello, {request.Name}!" };

        public const string StdOut = ".gistrun/stdout.txt";
        public const string StdErr = ".gistrun/stderr.txt";
        public const string StatsJson = ".gistrun/stats.json";
        public const string VarsJson = ".gistrun/vars.json";

        public object Get(RunGist request)
        {
            if (string.IsNullOrEmpty(request.Version))
                throw new ArgumentNullException(nameof(RunGist.Version));

            var gistVersion = $"{request.Id}/{request.Version}";
            var projectPath = Path.Combine(AppConfig.ProjectsBasePath, Path.Combine(request.Id, request.Version));
            if (Directory.Exists(projectPath))
            {
                string stdout = null;
                string stderr = null;
                var fs = new FileSystemVirtualFiles(projectPath);
                var statsFile = fs.GetFile(StatsJson);
                var statsJson = statsFile.ReadAllText();
                var entry = statsJson.FromJson<StatsLogEntry>();

                var outFile = fs.GetFile(StdOut);
                if (outFile != null)
                    stdout = outFile.ReadAllText();
                var errFile = fs.GetFile(StdErr);
                if (errFile != null)
                    stderr = errFile.ReadAllText();

                var varsJson = fs.GetFile(VarsJson);
                return new RunScriptResponse {
                    GistVersion = gistVersion,
                    ExitCode = entry.ExitCode,
                    Output = stdout,
                    Error = stderr,
                    DurationMs = entry.DurationMs,
                    Vars = varsJson != null
                        ? (Dictionary<string,object>)JSON.parse(varsJson.ReadAllText())
                        : null,
                };
            }
            
            throw HttpError.NotFound("Unseen gist");
        }

        public async Task<object> Post(RunGist request)
        {
            var gistId = request.Id;
            var gistVersion = request.Version != null
                ? $"{request.Id}/{request.Version}"
                : null;

            var projectPath = gistVersion != null
                ? Path.Combine(AppConfig.ProjectsBasePath, Path.Combine(request.Id, request.Version))
                : null;

            FileSystemVirtualFiles fs;
            if (projectPath != null && Directory.Exists(projectPath))
            {
                fs = new FileSystemVirtualFiles(projectPath);
            }
            else
            {
                var gistVfs = new GistVirtualFiles(gistVersion ?? gistId, AppConfig.GitHubAccessToken);

                var gist = (GithubGist) await gistVfs.GetGistAsync();
                await gistVfs.LoadAllTruncatedFilesAsync();

                gistId = gist.Id;
                gistVersion = $"{gist.Id}/{gist.History[0].Version}";
                projectPath = Path.Combine(AppConfig.ProjectsBasePath, gistVersion);
                FileSystemVirtualFiles.AssertDirectory(projectPath);
                
                fs = new FileSystemVirtualFiles(projectPath);
                fs.WriteFiles(gistVfs.GetAllFiles());
            }

            FileSystemVirtualFiles.AssertDirectory(Path.Combine(projectPath,".gistrun"));

            StatsLogEntry entry = null;
            string stdout = null;
            string stderr = null;
            var statsFile = fs.GetFile(StatsJson);
            var useCachedResults = false;

            if (statsFile != null)
            {
                var statsJson = statsFile.ReadAllText();
                entry = statsJson.FromJson<StatsLogEntry>();
                useCachedResults = entry.StartDate + TimeSpan.FromSeconds(AppConfig.CacheResultsSecs) > DateTime.UtcNow;
                if (useCachedResults)
                {
                    entry.Count += 1;
                }
            }

            var sessionId = Request.GetSessionId();
            if (useCachedResults)
            {
                entry.SessionId = sessionId;
                entry.RemoteIp = Request.RemoteIp;
                
                var outFile = fs.GetFile(StdOut);
                if (outFile != null)
                    stdout = outFile.ReadAllText();
                var errFile = fs.GetFile(StdErr);
                if (errFile != null)
                    stderr = errFile.ReadAllText();
            }
            else
            {
                var processInfo = new ProcessStartInfo {
                    FileName = AppConfig.GetExePath(fs),
                    WorkingDirectory = projectPath,
                    Arguments = "run",
                };

                var result = sessionId != null
                    ? await ProcessUtils.RunAsync(processInfo, AppConfig.ProcessTimeoutMs,
                        onOut: data => ServerEvents.NotifySession(sessionId, "cmd.stdout", data, channel: gistId),
                        onError: data => ServerEvents.NotifySession(sessionId, "cmd.stderr", data, channel: gistId))
                    : await ProcessUtils.RunAsync(processInfo, AppConfig.ProcessTimeoutMs);

                entry = new StatsLogEntry {
                    StartDate = result.StartAt,
                    Id = gistVersion,
                    ExitCode = result.ExitCode,
                    OutLen = result.StdOut?.Length,
                    ErrLen = result.StdErr?.Length,
                    DurationMs = result.DurationMs,
                    SessionId = sessionId,
                    RemoteIp = Request.RemoteIp,
                    Error = result.StdErr?.Length > 0 ? result.StdErr : null,
                    Count = (entry?.Count ?? 0) + 1,
                };
                if (!string.IsNullOrEmpty(result.StdOut))
                {
                    stdout = result.StdOut;
                    fs.WriteFile(StdOut, result.StdOut);
                }
                if (!string.IsNullOrEmpty(result.StdErr))
                {
                    entry.Error = stderr = result.StdErr;
                    fs.WriteFile(StdErr, result.StdErr);
                }
            }

            if (entry.ExitCode == 0)
            {
                StatsLogger.Log(entry);
            }
            else
            {
                StatsLogger.LogError(entry);
            }
            fs.WriteFile(StatsJson, entry.ToJson());

            var varsJson = fs.GetFile(VarsJson);

            return new RunScriptResponse {
                GistVersion = gistVersion,
                ExitCode = entry.ExitCode,
                Output = stdout,
                Error = stderr,
                DurationMs = entry.DurationMs,
                Vars = varsJson != null
                    ? (Dictionary<string,object>)JSON.parse(varsJson.ReadAllText())
                    : null,
            };
        }
    }
}
