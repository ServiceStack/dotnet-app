using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Funq;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUglify;
using ServiceStack;
using ServiceStack.IO;
using ServiceStack.Auth;
using ServiceStack.Text;
using ServiceStack.Data;
using ServiceStack.Redis;
using ServiceStack.OrmLite;
using ServiceStack.Configuration;
using ServiceStack.Azure.Storage;
using ServiceStack.Desktop;
using ServiceStack.Html;
using ServiceStack.Logging;
using ServiceStack.Script;

namespace Web
{
    public class WebAppContext
    {
        public string Tool { get; set; }
        public string[] Arguments { get; set; }
        public string WebSettingsPath { get; set; }
        public string StartUrl { get; set; }
        public string UseUrls { get; set; }
        public string IconPath { get; set; }
        public string AppDir { get; set; }
        public string ToolPath { get; set; }
        public string FavIcon { get; set; }
        public string RunProcess { get; set; }
        public bool DebugMode { get; set; }

        public IWebHostBuilder Builder { get; set; }
        public IAppSettings AppSettings { get; set; }
        public IWebHost Build()
        {
            AppLoader.Init(AppDir);
            return Builder.Build();
        }

        public string GetDebugString() => new Dictionary<string, object>
        {
            [nameof(Tool)] = Tool,
            [nameof(Arguments)] = Arguments,
            [nameof(WebSettingsPath)] = WebSettingsPath,
            [nameof(StartUrl)] = StartUrl,
            [nameof(UseUrls)] = UseUrls,
            [nameof(IconPath)] = IconPath,
            [nameof(AppDir)] = AppDir,
            [nameof(ToolPath)] = ToolPath,
            [nameof(FavIcon)] = FavIcon,
            [nameof(RunProcess)] = RunProcess,
            [nameof(DebugMode)] = DebugMode,
        }.Dump();
    }

    public delegate void CreateShortcutDelegate(string fileName, string targetPath, string arguments, string workingDirectory, string iconPath);

    public class WebAppEvents
    {
        public CreateShortcutDelegate CreateShortcut { get; set; }
        public Action<string> OpenBrowser { get; set; }
        public Action<WebAppContext> HandleUnknownCommand { get; set; }
        public Action<WebAppContext> RunNetCoreProcess { get; set; }
    }
    
    public partial class Startup : ModularStartup
    {
        public static WebAppEvents Events { get; set; }

        public static Func<IAppHost, AppHostInstructions> GetAppHostInstructions { get; set; }
        public static string GitHubSource { get; set; } = "sharp-apps Sharp Apps";
        public static string GitHubSourceTemplates { get; set; } = "NetCoreTemplates .NET Core C# Templates;NetFrameworkTemplates .NET Framework C# Templates;NetFrameworkCoreTemplates ASP.NET Core Framework Templates";

        public static string GistAppsId { get; set; } = "802daba52b6fe6e2ed1430348dc596cb";

        public static string GrpcSource { get; set; } = "https://grpc.servicestack.net";

        public static List<GistLink> GetGistAppsLinks() => GetGistLinks(GistAppsId, "apps.md");
        public static string GetAppsPath(string gistAlias)
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".sharp-apps", gistAlias);
        }

        public static bool? DebugMode { get; set; }
        public static string[] DebugArgs = CreateArgs("debug", withFlag:'d');
        public static string[] ReleaseArgs = CreateArgs("release", withFlag:'c');
        public static string[] DescriptionArgs = CreateArgs("desc");
        
        public static string Description { get; set; }
        
        static string[] TokenArgs = CreateArgs("token");
        
        static string[] PathArgs = CreateArgs("path");
        public static string PathArg { get; set; }

        public static string RunScript { get; set; }
        public static bool WatchScript { get; set; }

        public static bool GistNew { get; set; }
        public static bool GistUpdate { get; set; }

        public static Dictionary<string, object> RunScriptArgs = new Dictionary<string, object>();
        public static List<string> RunScriptArgV = new List<string>();

        public static bool Open { get; set; }
        
        public static string ToolFavIcon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "favicon.ico");

        public static GistVirtualFiles GistVfs;
        public static Task<Gist> GistVfsTask;
        public static Task GistVfsLoadTask;

        public static async Task<WebAppContext> CreateWebHost(string tool, string[] args, WebAppEvents events = null)
        {
            Events = events;

            if (args.Length > 0 && args[0] == "mix")
            {
                await Mix($"{tool} mix", args.Skip(1).ToArray());
                return null;
            }
            
            var dotnetArgs = new List<string>();

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_SOURCE")))
                GitHubSource = Environment.GetEnvironmentVariable("APP_SOURCE");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_SOURCE_TEMPLATES")))
                GitHubSourceTemplates = Environment.GetEnvironmentVariable("APP_SOURCE_TEMPLATES");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_SOURCE_GISTS")))
                GistLinksId = Environment.GetEnvironmentVariable("APP_SOURCE_GISTS");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_SOURCE_APPS")))
                GistAppsId = Environment.GetEnvironmentVariable("APP_SOURCE_APPS");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_GIST_TOKEN")))
                GitHubToken = Environment.GetEnvironmentVariable("GITHUB_GIST_TOKEN");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_TOKEN")))
                GitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_SOURCE_GRPC")))
                GrpcSource = Environment.GetEnvironmentVariable("APP_SOURCE_GRPC");
            
            InitMix();

            var createShortcut = false;
            var publish = false;
            var publishExe = false;
            string createShortcutFor = null;
            string runProcess = null;
            var runSharpApp = false;
            var runLispRepl = false;
            
            var appSettingPaths = new[]
            {
                "app.settings", "../app/app.settings", "app/app.settings",
            };
            
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.EndsWith(".settings"))
                {
                    appSettingPaths = new[] { arg };
                    continue;
                }
                if (arg.EndsWith(".dll") || arg.EndsWith(".exe"))
                {
                    if (Events?.RunNetCoreProcess == null)
                        throw new NotSupportedException($"This {tool} tool does not support running processes");

                    runProcess = arg;
                    continue;
                }
                if (ProcessFlags(args, arg, ref i)) 
                    continue;
                if (arg == "shortcut")
                {
                    createShortcut = true;
                    if (i + 1 < args.Length && (args[i + 1].EndsWith(".dll") || args[i + 1].EndsWith(".exe")))
                        createShortcutFor = args[++i];
                    continue;
                }
                if (arg == "lisp")
                {
                    runLispRepl = runSharpApp = true;
                    continue;
                }
                if (arg == "gist-new")
                {
                    GistNew = true;
                    continue;
                }
                if (arg == "gist-update")
                {
                    GistUpdate = true;
                    continue;
                }
                if (arg == "publish")
                {
                    publish = true;
                    continue;
                }
                if (arg == "publish-exe")
                {
                    publishExe = true;
                    continue;
                }
                if (arg == "mix" || arg == "-mix")
                {
                    if (!await Mix($"{tool} mix", new[] {args[++i]}))
                        return null;
                    continue;
                }
                if (arg == "run" || arg == "watch")
                {
                    if (i + 1 >= args.Length)
                    {
                        runSharpApp = true;
                        continue;
                    }

                    var script = args[i + 1];
                    if (script.EndsWith(".settings"))
                    {
                        runSharpApp = true;
                        appSettingPaths = new[] { script };
                        i++;
                        continue;
                    }
                    
                    if (!(script.EndsWith(".html") || script.EndsWith(".ss") || script.EndsWith(".sc") || script.EndsWith(".l")))
                    {
                        // Run SharpApp
                        string appsDir = GetAppsPath(script);
                        if (Directory.Exists(appsDir))
                        {
                            RetryExec(() => Directory.SetCurrentDirectory(appsDir));
                            runSharpApp = true;

                            // Run Gist SharpApp
                            var gistFile = appsDir + ".gist";
                            if (File.Exists(gistFile))
                            {
                                if (Verbose) $"Loading GistVirtualFiles from: {gistFile}".Print();
                                var gistJson = await File.ReadAllTextAsync(gistFile);
                                var gist = gistJson.FromJson<Gist>();
                                GistVfs = new GistVirtualFiles(gist);
                                GistVfsTask = GistVfs.GetGistAsync(); // fire to load asynchronously
                            }
                            
                            i++;
                            continue;
                        }
                        
                        throw new ArgumentException(script.IndexOf('.', StringComparison.Ordinal) >= 0 
                            ? "Only .ss. .sc. .l or .html scripts can be run"
                            : $"No '{script}' App installed");
                    }
                        
                    RunScript = script;
                    WatchScript = arg == "watch";

                    i += 2; //'run' 'script.ss'
                    for (; i < args.Length; i++)
                    {
                        var key = args[i];
                        if (key == "mix" || key == "-mix")
                        {
                            if (++i >= args.Length)
                                throw new Exception($"Usage: {tool} run <name> mix <gist>");
                            
                            ForceApproval = Silent = true;
                            if (!await Mix($"{tool} mix", new[] { args[i] }))
                                return null;
                            continue;
                        }
                        
                        if (ProcessFlags(args, key, ref i))
                            continue;

                        RunScriptArgV.Add(key);

                        if (!(key.FirstCharEquals('-') || key.FirstCharEquals('/')))
                            continue;

                        var hasArgValue = i + 1 < args.Length;
                        RunScriptArgs[key.Substring(1)] = hasArgValue ? args[i + 1] : null;
                        if (hasArgValue) RunScriptArgV.Add(args[i++ + 1]);
                    }
                    continue;
                }
                if (arg == "open")
                {
                    if (i + 1 >= args.Length)
                    {
                        PrintGistLinks(tool, GetGistAppsLinks(), usage:$"Usage: {tool} open <name>");
                        return null;
                    }
                    
                    var target = args[i+1];

                    for (var j=i+2; j<args.Length; j++)
                        ProcessFlags(args, args[j], ref j);
                    
                    RegisterStat(tool, target, "open");

                    var isGitHubUrl = target.StartsWith("https://gist.github.com/") ||
                                      target.StartsWith("https://github.com/");
                    if (!isGitHubUrl && !target.IsUrl() && target.IndexOf('/') >= 0)
                    {
                        target = "https://github.com/" + target;
                        isGitHubUrl = true;
                    }

                    var gistLinks = !isGitHubUrl ? GetGistAppsLinks() : null;
                    var gistLink = GetGistAliasLink(target) ?? gistLinks?.FirstOrDefault(x => x.Name == target);

                    if (!InstallGistApp(tool, target, gistLink, gistLinks, out var appsDir)) 
                        return null;

                    runSharpApp = true;
                    Open = true;

                    i += 2; //'open' 'target'
                    for (; i < args.Length; i++)
                    {
                        var key = args[i];
                        if (key == "mix" || key == "-mix")
                        {
                            if (++i >= args.Length)
                                throw new Exception($"Usage: {tool} open <name> mix <gist>");
                            
                            RetryExec(() => Directory.SetCurrentDirectory(appsDir));
                            ForceApproval = Silent = true;
                            if (!await Mix($"{tool} mix", new[] { args[i] }))
                                return null;
                            continue;
                        }
                        
                        if (ProcessFlags(args, key, ref i))
                            continue;
                        
                        RunScriptArgV.Add(key);

                        if (!(key.FirstCharEquals('-') || key.FirstCharEquals('/')))
                            continue;

                        var hasArgValue = i + 1 < args.Length;
                        RunScriptArgs[key.Substring(1)] = hasArgValue ? args[i + 1] : null;
                        if (hasArgValue) RunScriptArgV.Add(args[i++ + 1]);
                    }
                    continue;
                }
                if (arg == "install" || arg == "i")
                {
                    var gistLinks = GetGistAppsLinks();

                    if (i + 1 >= args.Length)
                    {
                        PrintGistLinks(tool, gistLinks, usage:$"Usage: {tool} open <name>");
                        return null;
                    }
                    
                    var target = args[i+1];

                    for (var j=i+2; j<args.Length; j++)
                        ProcessFlags(args, args[j], ref j);
                    
                    RegisterStat(tool, target, "install");
                    
                    var isGitHubUrl = target.StartsWith("https://gist.github.com/") ||
                                      target.StartsWith("https://github.com/");
                    if (!isGitHubUrl && !target.IsUrl() && target.IndexOf('/') >= 0)
                    {
                        target = "https://github.com/" + target;
                        isGitHubUrl = true;
                    }
                    if (isGitHubUrl)
                    {
                        InstallGistApp(tool, target, null, null, out var appsDir);
                        return null;
                    }
                    
                    var gistLink = GetGistAliasLink(target) ?? gistLinks.FirstOrDefault(x => x.Name == target);
                    if (gistLink == null)
                    {
                        $"No match found for '{target}', available Apps:".Print();
                        PrintGistLinks(tool, gistLinks, usage:$"Usage: {tool} open <name>");
                        return null;
                    }
                    if (gistLink.GistId != null)
                    {
                        if (!InstallGistApp(tool, target, gistLink, gistLinks, out var appsDir)) 
                            return null;
                        
                        var gist = await GistVfsTask;
                        GistVfsLoadTask = GistVfs.LoadAllTruncatedFilesAsync();
                        await GistVfsLoadTask;
                        SerializeGistAppFiles();
                        
                        $"Gist App Installed, run with:".Print();
                        $"  {tool} run {target}".Print();
                        return null;
                    }
                    if (gistLink.Repo != null)
                    {
                        InstallRepo(gistLink.Url.EndsWith(".zip") 
                                ? gistLink.Url 
                                : await GitHubUtils.Gateway.GetSourceZipUrlAsync(gistLink.User, gistLink.Repo), 
                            target);
                    }

                    "".Print();
                    $"Installation successful, run with:".Print();
                    "".Print();
                    $"  {tool} run {target}".Print();

                    return null;
                }
                if (arg == "uninstall")
                {
                    if (i + 1 >= args.Length)
                    {
                        PrintAppUsage(tool, arg);
                        return null;
                    }

                    var target = args[i + 1];

                    for (var j=i+2; j<args.Length; j++)
                        ProcessFlags(args, args[j], ref j);

                    var installDir = GetAppsPath(target);
                    var gistFile = installDir + ".gist";

                    if (!Directory.Exists(installDir) && !File.Exists(gistFile))
                    {
                        "".Print();
                        $"App '{target}' is not installed.".Print();
                        PrintAppUsage(tool, arg);
                        return null;
                    }
                    
                    if (Directory.Exists(installDir))
                        DeleteDirectory(installDir);
                    if (File.Exists(gistFile))
                        DeleteFile(gistFile);
                        
                    "".Print();
                    $"App '{target}' was uninstalled.".Print();
                    return null;
                }
                if (arg == "proto-langs")
                {
                    var client = new JsonServiceClient(GrpcSource);
                    var response = client.Get(new GetLanguages());

                    "".Print();
                    $"gRPC Supported Languages:".Print();
                    "".Print();
                    var maxKeyLen = response.Results.Max(x => x.Key.Length);
                    foreach (var kvp in response.Results)
                    {
                        $"  {kvp.Key.PadRight(maxKeyLen)}   {kvp.Value}".Print();
                    }

                    "".Print();
                    "Usage:".Print();
                    $"{tool} proto-<lang> <url>             Add gRPC .proto and generate language".Print();
                    "".Print();
                    $"{tool} proto-<lang> <file|dir>        Update gRPC .proto and re-gen language".Print();
                    $"{tool} proto-<lang>                   Update all gRPC .proto's and re-gen lang".Print();
                    "".Print();
                    "Options:".Print();
                    "   --out <dir>                     Save generated gRPC language sources to <dir>".Print();
                    return null;
                }
                if (arg == "alias")
                {
                    var settings = GetGistAliases();
                    if (i + 1 >= args.Length)
                    {
                        var keys = settings.GetAllKeys();
                        if (keys.Count == 0)
                        {
                            "No gist aliases have been defined.".Print();
                            $"Usage: {tool} alias <alias> <gist-id>".Print();
                        }
                        else
                        {
                            foreach (var key in keys)
                            {
                                var gistId = settings.GetRequiredString(key);
                                $"{key} {gistId}".Print();
                            }
                        }
                        return null;
                    }

                    var target = args[i + 1];
                    if (i + 2 >= args.Length)
                    {
                        settings.GetRequiredString(target).Print();
                    }
                    else
                    {
                        var gistId = args[i + 2];
                        if (gistId.StartsWith("https://gist.github.com/"))
                            gistId = gistId.ToGistId();
                        
                        if (gistId.Length != 20 && gistId.Length != 32)
                        {
                            $"'{args[i + 2]}' is not a valid gist id or URL".Print();
                            return null;
                        }

                        var aliasPath = GetGistAliasesFilePath();
                        if (settings.Exists(target))
                        {
                            var newAliases = new StringBuilder();
                            foreach (var line in await File.ReadAllLinesAsync(aliasPath))
                            {
                                newAliases.AppendLine(line.StartsWith(target + " ") ? $"{target} {gistId}" : line);
                            }
                            await File.WriteAllTextAsync(aliasPath, newAliases.ToString());
                        }
                        else
                        {
                            using var fs = File.AppendText(aliasPath);
                            await fs.WriteLineAsync($"{target} {gistId}");
                        }
                    }

                    return null;
                }
                if (arg == "unalias")
                {
                    if (i + 1 >= args.Length)
                    {
                        var settings = GetGistAliases();
                        var keys = settings.GetAllKeys();
                        if (keys.Count == 0)
                        {
                            "No gist aliases have been defined.".Print();
                        }
                        else
                        {
                            foreach (var key in keys)
                            {
                                var gistId = settings.GetRequiredString(key);
                                $"{key} {gistId}".Print();
                            }
                        }
                        return null;
                    }
                    
                    var removeAliases = new List<string>();
                    for (var j = i + i; j < args.Length; j++)
                    {
                        removeAliases.Add(args[j]);
                    }

                    var aliasPath = GetGistAliasesFilePath();
                    var newAliases = new StringBuilder();
                    foreach (var line in await File.ReadAllLinesAsync(aliasPath))
                    {
                        if (removeAliases.Any(x => line.StartsWith(x + " ")))
                            continue;
                        newAliases.AppendLine(line);
                    }
                    await File.WriteAllTextAsync(aliasPath, newAliases.ToString());
                    
                    return null;
                }
                dotnetArgs.Add(arg);
            }
            
            if (publish)
            {
                await PublishToGist(tool);
                return null;
            }

            var allow = new[] { "-h", "-help", "--help", "-v", "-version", "--version", "-clear", "--clear", "-clean", "--clean" };
            var unknownFlag = dotnetArgs.FirstOrDefault(x => x.StartsWith("-") && !allow.Contains(x)); 
            if (unknownFlag != null)
                throw new Exception($"Unknown flag: '{unknownFlag}'");

            if (Verbose)
            {
                $"args: '{dotnetArgs.Join(" ")}'".Print();
                $"APP_SOURCE={GitHubSource}".Print();

                if (runProcess != null)
                    $"Run Process: {runProcess}".Print();
                if (createShortcut)
                    $"Create Shortcut {createShortcutFor}".Print();
                if (GistNew)
                    $"Command: gist-new".Print();
                if (GistUpdate)
                    $"Command: gist-update".Print();
                if (publishExe)
                    $"Command: publish-exe".Print();
                if (RunScript != null)
                    $"Command: run {RunScript} {RunScriptArgs.ToJsv()}".Print();
                if (runLispRepl)
                    $"Command: LISP REPL".Print();
            }

            if (runProcess != null)
            {
                RegisterStat(tool, runProcess, "run");

                var publishDir = Path.GetDirectoryName(Path.GetFullPath(runProcess)).AssertDirectory();
                Events.RunNetCoreProcess(new WebAppContext { 
                    Arguments = dotnetArgs.ToArray(), 
                    RunProcess = runProcess,
                    AppDir = publishDir,
                    FavIcon = File.Exists(Path.Combine(publishDir, "favicon.ico")) 
                        ? Path.Combine(publishDir, "favicon.ico")
                        : ToolFavIcon,
                });
            
                return null;
            }

            var instruction = await HandledCommandAsync(tool, dotnetArgs.ToArray());
            if (instruction?.Handled == true)
                return null;

            string appSettingsPath = instruction?.AppSettingsPath;
            foreach (var path in appSettingPaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    appSettingsPath = fullPath;
                    break;
                }
            }
            
            if (!runSharpApp && RunScript == null && dotnetArgs.Count == 0 && appSettingsPath == null && createShortcutFor == null)
            {
                PrintUsage(tool);
                return null;
            }

            var appDir = appSettingsPath != null 
                ? Path.GetDirectoryName(appSettingsPath) 
                : createShortcutFor != null
                    ? Path.GetDirectoryName(Path.GetFullPath(createShortcutFor))
                    : Environment.CurrentDirectory;

            var ctx = new WebAppContext
            {
                Tool = tool,
                Arguments = dotnetArgs.ToArray(),
                RunProcess = runProcess,
                WebSettingsPath = appSettingsPath,
                AppSettings = WebTemplateUtils.AppSettings,
                AppDir = appDir.AssertDirectory(),
                ToolPath = Assembly.GetExecutingAssembly().Location,
                DebugMode =  DebugMode ?? false,
            };

            if (instruction == null && dotnetArgs.Count > 0)
            {
                if (Events?.HandleUnknownCommand != null)
                {
                    Events.HandleUnknownCommand(ctx);
                }
                else
                {
                    $"Unknown command '{dotnetArgs.Join(" ")}'".Print();
                    PrintUsage(tool);
                }
                return null;
            }

            var appSettingsContent = File.Exists(appSettingsPath)
                ? await File.ReadAllTextAsync(appSettingsPath)
                : null;

            if (GistVfsTask != null)
            {
                var gist = await GistVfsTask;
                if (string.IsNullOrEmpty(appSettingsContent))
                {
                    appSettingsContent = gist.Files.TryGetValue("app.settings", out var file)
                        ? (string.IsNullOrEmpty(file.Content) && file.Truncated
                            ? DownloadCachedStringFromUrl(file.Raw_Url)
                            : file.Content)
                        : null;
                }
                
                // start downloading any truncated gist content whilst AppHost initializes
                GistVfsLoadTask = GistVfs.LoadAllTruncatedFilesAsync(); 

                if (string.IsNullOrEmpty(appSettingsContent))
                {
                    appSettingsPath = Path.Combine(appDir, "app.settings");
                    appSettingsContent = File.Exists(appSettingsPath)
                        ? await File.ReadAllTextAsync(appSettingsPath)
                        : $"debug false{Environment.NewLine}name {gist.Description ?? "Gist App"}{Environment.NewLine}";
                }
            }

            if (appSettingsContent == null && (appSettingsPath == null && createShortcutFor == null && RunScript == null && !runLispRepl))
            {
                if (Directory.Exists(GetAppsPath("")) && Directory.GetDirectories(GetAppsPath("")).Length > 0)
                {
                    PrintAppUsage(tool, "run");
                    return null;
                }

                throw new Exception($"'{appSettingPaths[0]}' does not exist.\n\nView Help: {tool} ?");
            }

            var usingWebSettings = File.Exists(appSettingsPath);
            if (Verbose || (usingWebSettings && !createShortcut && (tool == "x") && instruction == null && appSettingsPath != null))
                $"Using '{appSettingsPath}'".Print();

            if (appSettingsContent == null && RunScript == null)
            {
                appSettingsContent = usingWebSettings 
                    ? await File.ReadAllTextAsync(appSettingsPath)
                    : "debug false";
            }
            
            var appSettings = appSettingsContent != null
                ? new DictionarySettings(appSettingsContent.ParseKeyValueText(delimiter:" "))
                : new DictionarySettings();
            if (RunScript != null)
            {
                var context = new ScriptContext().Init();
                var page = OneTimePage(context, await File.ReadAllTextAsync(RunScript));
                if (page.Args.Count > 0)
                    appSettings = new DictionarySettings(page.Args.ToStringDictionary());
            }

            WebTemplateUtils.AppSettings = new MultiAppSettings(
                appSettings,
                new EnvironmentVariableSettings());

            var bind = "bind".GetAppSetting("localhost");
            var ssl = "ssl".GetAppSetting(defaultValue: false);
            var port = "port".GetAppSetting(defaultValue: ssl ? "5001-" : "5000-");
            if (port.IndexOf('-') >= 0)
            {
                var startPort = int.TryParse(port.LeftPart('-'), out var val) ? val : 5000;
                var endPort = int.TryParse(port.RightPart('-'), out val) ? val : 65535;
                port = HostContext.FindFreeTcpPort(startPort, endPort).ToString();
            }
            var scheme = ssl ? "https" : "http";
            var useUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? $"{scheme}://{bind}:{port}/";
            ctx.UseUrls = useUrls;
            ctx.StartUrl = useUrls.Replace("://*", "://localhost");
            ctx.DebugMode = GetDebugMode();
            ctx.FavIcon = GetIconPath(appDir);

            if (createShortcut || instruction?.Command == "shortcut")
            {
                if (instruction?.Command != "shortcut")
                    RegisterStat(tool, createShortcutFor, "shortcut");

                var shortcutPath = createShortcutFor == null
                    ? Path.Combine(appDir, "name".GetAppSetting(defaultValue: "Sharp App"))
                    : Path.GetFullPath(createShortcutFor.LastLeftPart('.'));

                var toolPath = ctx.ToolPath;
                var arguments = createShortcutFor == null
                    ? $"\"{ctx.WebSettingsPath}\""
                    : $"\"{createShortcutFor}\"";

                var targetPath = toolPath;
                if (toolPath.EndsWith(".dll"))
                {
                    targetPath = "dotnet";
                    arguments = $"{toolPath} {arguments}";
                }

                var icon = GetIconPath(appDir, createShortcutFor);

                if (Verbose) $"CreateShortcut: {shortcutPath}, {targetPath}, {arguments}, {appDir}, {icon}".Print();

                CreateShortcut(shortcutPath, targetPath, arguments, appDir, icon, ctx);

                if (instruction != null && tool == "app")
                    $"{Environment.NewLine}Shortcut: {new DirectoryInfo(Path.GetDirectoryName(shortcutPath)).Name}{Path.DirectorySeparatorChar}{Path.GetFileName(shortcutPath)}".Print();

                return null;
            }
            
            if (RunScript != null || runLispRepl)
            {
                void ExecScript(SharpPagesFeature feature)
                {
                    var ErrorPrefix = $"FAILED run {RunScript} [{string.Join(' ', RunScriptArgV)}]:";

                    try
                    {
                        var script = File.ReadAllText(RunScript);
                        var page = OneTimePage(feature, script);
                        var pageResult = new PageResult(page) {
                            Args = {
                                ["ARGV"] = RunScriptArgV.ToArray(),
                            }
                        };
                        RunScriptArgs.Each(entry => pageResult.Args[entry.Key] = entry.Value);
                        var output = pageResult.RenderToStringAsync().Result;
                        output.Print();

                        if (!Silent && pageResult.LastFilterError != null)
                        {
                            ErrorPrefix.Print();
                            pageResult.LastFilterStackTrace.Map(x => "   at " + x)
                                .Join(Environment.NewLine).Print();

                            "".Print();
                            pageResult.LastFilterError.Message.Print();
                            pageResult.LastFilterError.ToString().Print();
                        }
                    }
                    catch (Exception ex)
                    {
                        ex = ex.UnwrapIfSingleException();
                        if (ex is StopFilterExecutionException)
                        {
                            $"{ErrorPrefix} {ex.InnerException?.Message}".Print();
                            return;
                        }

                        Verbose = true;
                        ErrorPrefix.Print();
                        throw;
                    }
                }
                
                bool breakLoop = false;

                try
                {
                    Console.TreatControlCAsInput = false;
                    Console.CancelKeyPress += delegate {
//                    if (Verbose) $"Console.CancelKeyPress".Print();
                        breakLoop = true;
                    };
                }
                catch {} // fails when called from unit test
                
                RegisterStat(tool, RunScript, WatchScript ? "watch" : "run");
                var (contentRoot, useWebRoot) = GetDirectoryRoots(ctx);
                AppLoader.Init(contentRoot);
                var builder = new WebHostBuilder()
                    .UseFakeServer()
                    .UseSetting(WebHostDefaults.SuppressStatusMessagesKey, "True")
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(useWebRoot)
                    .ConfigureLogging(config =>
                    {
                        if (!GetDebugMode() && !Verbose &&
                            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
                        {
                            config.ClearProviders();
                            config.SetMinimumLevel(LogLevel.None);
                        }
                    })
                    .UseModularStartup<Startup>();

                using (var webHost = builder.Build())
                {
                    var cts = new CancellationTokenSource();
                    var task = webHost.RunAsync(cts.Token);
                    
                    var appHost = WebTemplateUtils.AppHost;
                    if (StartupException != null) throw StartupException;
                    
                    var feature = appHost.AssertPlugin<SharpPagesFeature>();

                    if (runLispRepl)
                    {
                        Console.WriteLine($"\nWelcome to #Script Lisp! The time now is: {DateTime.Now.ToShortTimeString()}");
                        Lisp.RunRepl(feature);
                        return null;
                    }

                    var script = new FileInfo(RunScript);
                    var lastWriteAt = DateTime.MinValue;
                    
                    if (WatchScript)
                    {
                        $"Watching '{RunScript}' (Ctrl+C to stop):".Print();

                        while (!breakLoop)
                        {
                            do
                            {
                                if (breakLoop)
                                    break;
                                await Task.Delay(100, cts.Token);
                                script.Refresh();
                            } while(script.LastWriteTimeUtc == lastWriteAt);

                            if (breakLoop)
                                break;
                            try
                            {
                                Console.Clear();
                                ExecScript(feature);
                            }
                            catch (Exception ex)
                            {
                                ex = ex.UnwrapIfSingleException();
                                Console.WriteLine(ex.ToString());
                            }
                            lastWriteAt = script.LastWriteTimeUtc;
                        }
                        
                        if (Verbose) $"breakLoop = {breakLoop}".Print();
                        cts.Cancel();
                    }
                    else
                    {
                        ExecScript(feature);
                    }
                }
                return null;
            }

            return CreateWebAppContext(ctx);
        }

        private static async Task PublishToGist(string tool)
        {
            RegisterStat(tool, "publish");

            AssertGitHubToken();

            var files = new Dictionary<string, object>();
            Environment.CurrentDirectory.CopyAllToDictionary(files,
                excludePaths: WebTemplateUtils.ExcludeFoldersNamed.ToArray(),
                excludeExtensions: WebTemplateUtils.ExcludeFileExtensions.ToArray());

            string publishUrl = null;
            string appName = null;

            var isSharpApp = File.Exists("app.settings");
            if (isSharpApp)
            {
                foreach (var line in await File.ReadAllLinesAsync("app.settings"))
                {
                    if (line.StartsWith("description") || (line.StartsWith("name ") && string.IsNullOrEmpty(Description)))
                    {
                        Description = line.RightPart(' ');
                    }

                    if (line.StartsWith("appName "))
                    {
                        appName = line.RightPart(' ');
                    }

                    if (line.StartsWith("publish "))
                    {
                        publishUrl = line.RightPart(' ').Trim();
                    }
                }
            }

            if (File.Exists(".publish"))
            {
                foreach (var line in await File.ReadAllLinesAsync(".publish"))
                {
                    if (line.StartsWith("gist "))
                    {
                        publishUrl = line.RightPart(' ').Trim();
                    }
                }
            }

            var gateway = new GitHubGateway(GitHubToken);

            "".Print();

            Task<RegisterSharpAppResponse> registerTask = null;
            var client = new JsonServiceClient("https://servicestack.net");

            var createGist = string.IsNullOrEmpty(publishUrl);
            if (createGist)
            {
                var defaultName = new DirectoryInfo(Environment.CurrentDirectory).Name.Replace("_", " ");
                var gist = gateway.CreateGithubGist(
                    Description ?? defaultName + (isSharpApp ? " Sharp App" : ""),
                    isPublic: true,
                    files: files);

                // Html_Url doesn't include username in URL, which it redirects to. 
                // Use URL with username instead so app listing can extract username
                var htmlUrl = !string.IsNullOrEmpty(gist.Owner?.Login)
                    ? $"https://gist.github.com/{gist.Owner.Login}/{gist.Id}"
                    : gist.Html_Url;

                $"published to: {htmlUrl}".Print();

                await File.WriteAllTextAsync(".publish", $"gist {htmlUrl}");

                if (isSharpApp && appName != null && gist.Url != null)
                {
                    registerTask = client.PostAsync(new RegisterSharpApp {
                        AppName = appName,
                        Publish = htmlUrl,
                    });
                }
            }
            else
            {
                var gistId = publishUrl.LastRightPart('/');
                gateway.WriteGistFiles(gistId, files);
                $"updated: {publishUrl}".Print();
            }

            if (isSharpApp)
            {
                "".Print();
                if (appName == null)
                {
                    "Publish App to the public registry by re-publishing with app.settings:".Print();
                    "".Print();
                    "appName     <app alias>    # required: alpha-numeric snake-case characters only, 30 chars max".Print();
                    "description <app summary>  # optional: 20-150 chars".Print();
                    "tags        <app tags>     # optional: space delimited, alpha-numeric snake-case, 3 tags max".Print();
                }
                else if (registerTask != null)
                {
                    try
                    {
                        registerTask.Wait();
                    }
                    catch (WebServiceException ex)
                    {
                        $"REGISTRY ERROR: {ex.Message}".Print();
                    }

                    "Run published App:".Print();
                    "".Print();
                    $"    {tool} open {appName}".Print();
                }
            }
        }

        public static GistLink GetGistAliasLink(string alias)
        {
            var localAliases = GetGistAliases();
            if (localAliases.Exists(alias))
            {
                var gistId = localAliases.GetRequiredString(alias);
                return new GistLink {
                    GistId = gistId,
                    Url = $"https://gist.github.com/{gistId}",
                    To = ".",
                };
            }
            return null;
        }

        private static void AssertGitHubToken()
        {
            if (string.IsNullOrEmpty(GitHubToken))
            {
                var CR = Environment.NewLine;
                throw new Exception($"GitHub Access Token required to publish App to Gist.{CR}" +
                                    $"Specify Token with --token <token> or GITHUB_TOKEN Environment Variable.{CR}" +
                                    $"Generate Access Token at: https://github.com/settings/tokens");
            }
        }

        private static bool ProcessFlags(string[] args, string arg, ref int i)
        {
            string NextArg(ref int iRef) => iRef + 1 < args.Length ? args[++iRef] : null;
            
            if (VerboseArgs.Contains(arg))
            {
                Verbose = true;
                return true;
            }

            if (QuietArgs.Contains(arg))
            {
                Silent = true;
                return true;
            }

            if (SourceArgs.Contains(arg))
            {
                GitHubSource = GitHubSourceTemplates = NextArg(ref i);
                return true;
            }

            if (ForceArgs.Contains(arg))
            {
                ForceApproval = true;
                return true;
            }

            if (TokenArgs.Contains(arg))
            {
                GitHubToken = NextArg(ref i);
                return true;
            }

            if (DebugArgs.Contains(arg))
            {
                DebugMode = true;
                return true;
            }

            if (ReleaseArgs.Contains(arg))
            {
                DebugMode = false;
                return true;
            }

            if (OutArgs.Contains(arg))
            {
                OutDir = NextArg(ref i);
                return true;
            }

            if (PathArgs.Contains(arg))
            {
                PathArg = NextArg(ref i);
                return true;
            }

            if (DescriptionArgs.Contains(arg))
            {
                Description = NextArg(ref i);
                return true;
            }

            if (IgnoreSslErrorsArgs.Contains(arg))
            {
                IgnoreSslErrors = true;
                return true;
            }

            return false;
        }

        private static SharpPage OneTimePage(ScriptContext context, string script)
        {
            if (RunScript == null)
                return context.Pages.OneTimePage(script, ".html");
            return RunScript.EndsWith(".sc") 
                ? context.CodeSharpPage(script)
                : RunScript.EndsWith(".l") 
                    ? context.LispSharpPage(script)
                    : context.Pages.OneTimePage(script, ".html");
        }

        private static bool InstallGistApp(string tool, string target, GistLink gistLink, List<GistLink> gistLinks, out string appsDir)
        {
            appsDir = GetAppsPath(target);

            var gistId = gistLink?.GistId;
            if (gistId == null)
            {
                if (target.StartsWith("https://gist.github.com/"))
                {
                    gistId = target.ToGistId();
                    appsDir = GetAppsPath(gistId);
                }
                else if (target.StartsWith("https://github.com/"))
                {
                    var pathInfo = target.Substring("https://github.com/".Length);
                    var user = pathInfo.LeftPart('/');
                    var repo = pathInfo.RightPart('/').LeftPart('/');

                    appsDir = InstallRepo(target.EndsWith(".zip")
                            ? target
                            : GitHubUtils.Gateway.GetSourceZipUrl(user, repo),
                        repo);

                    if (!Directory.Exists(appsDir))
                    {
                        $"Could not install {target}".Print();
                        return false;
                    }
                }
                else if (target.Length == GistAppsId.Length)
                {
                    gistId = target;
                }
                else if (gistLink?.Repo != null)
                {
                    appsDir = InstallRepo(gistLink.Url.EndsWith(".zip")
                            ? gistLink.Url
                            : GitHubUtils.Gateway.GetSourceZipUrl(gistLink.User, gistLink.Repo),
                        target);

                    if (!Directory.Exists(appsDir))
                    {
                        $"Could not install {target}".Print();
                        return false;
                    }
                }
                else
                {
                    $"No match found for '{target}', available Apps:".Print();
                    PrintGistLinks(tool, gistLinks ?? GetGistAppsLinks(), usage: $"Usage: {tool} open <name>");
                    return false;
                }
            }

            if (gistId != null)
            {
                GistVfs = new GistVirtualFiles(gistId);
                GistVfsTask = GistVfs.GetGistAsync(); // fire to load asynchronously
            }

            var useDir = appsDir;
            if (!Directory.Exists(useDir))
            {
                RetryExec(() => Directory.CreateDirectory(useDir));
            }
            
            RetryExec(() => Directory.SetCurrentDirectory(useDir));

            return true;
        }

        private static void PrintAppUsage(string tool, string cmd)
        {
            "".Print();
            $"Usage: {tool} {cmd} <app>".Print();
            var appsDir = GetAppsPath("");
            var appNames = Directory.GetDirectories(appsDir);
            if (appNames.Length > 0)
            {
                "".Print();
                "Installed Apps:".Print();
                appNames.Each(x => $"  {new DirectoryInfo(x).Name}".Print());
            }
        }

        private static string InstallRepo(string downloadUrl, string appName)
        {
            var installDir = GetAppsPath(appName);

            var gistFile = installDir + ".gist";
            DeleteFile(gistFile);
            
            var cachedVersionPath = DownloadCachedZipUrl(downloadUrl);
            var tmpDir = Path.Combine(Path.GetTempPath(), "servicestack", appName);
            DeleteDirectory(tmpDir);

            if (Verbose) $"ExtractToDirectory: {cachedVersionPath} -> {tmpDir}".Print();
            ZipFile.ExtractToDirectory(cachedVersionPath, tmpDir);

            DeleteDirectory(installDir);
            MoveDirectory(new DirectoryInfo(tmpDir).GetDirectories().First().FullName, installDir);
            
            $"Installed App '{appName}'".Print();
            
            return installDir;
        }

        private static string DownloadCachedZipUrl(string zipUrl)
        {
            var noCache = zipUrl.IndexOf("master.zip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               zipUrl.IndexOf("main.zip", StringComparison.OrdinalIgnoreCase) >= 0;
            if (noCache)
            {
                var tempFile = Path.GetTempFileName();
                if (Verbose) $"Downloading {zipUrl} => {tempFile} (nocache)".Print();
                Path.GetDirectoryName(tempFile).AssertDirectory();
                GitHubUtils.DownloadFile(zipUrl, tempFile);
                return tempFile;
            }
            
            var cachedVersionPath = GetCachedFilePath(zipUrl);

            var isCached = File.Exists(cachedVersionPath);
            if (Verbose) ((isCached ? "Using cached release: " : "Using new release: ") + cachedVersionPath).Print();

            if (!isCached)
            {
                if (Verbose) $"Downloading {zipUrl} => {cachedVersionPath}".Print();
                Path.GetDirectoryName(cachedVersionPath).AssertDirectory();
                GitHubUtils.DownloadFile(zipUrl, cachedVersionPath);
            }

            return cachedVersionPath;
        }


        public static void MoveDirectory(string fromPath, string toPath)
        {
            if (Verbose) $"Directory Move: {fromPath} -> {toPath}".Print();
            try
            {
                Directory.GetParent(toPath).AssertDirectory();
                Directory.Move(fromPath, toPath);
            }
            catch (IOException ex) //Source and destination path must have identical roots. Move will not work across volumes.
            {
                if (Verbose) $"Directory Move failed: '{ex.Message}', trying COPY Directory...".Print();
                if (Verbose) $"Directory Copy: {fromPath} -> {toPath}".Print();
                fromPath.CopyAllTo(toPath);
            }
        }

        public static bool GetDebugMode() => DebugMode ?? "debug".GetAppSetting(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Production");

        public static void CreateShortcut(string filePath, string targetPath, string arguments, string workingDirectory, string iconPath, WebAppContext ctx)
        {
            if (Events?.CreateShortcut != null)
            {
                Events.CreateShortcut(filePath, targetPath, arguments, workingDirectory, iconPath);
            }
            else
            {
                filePath = Path.Combine(Path.GetDirectoryName(filePath)!, new DefaultScripts().generateSlug(Path.GetFileName(filePath)));
                var cmd = filePath + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".bat" : ".sh");

                if (ctx == null) 
                    return;
                
                var openBrowserCmd = string.IsNullOrEmpty(ctx?.StartUrl) || targetPath.EndsWith(".exe") ? "" : 
                    (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? $"start {ctx.StartUrl}"
                        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                            ? $"open {ctx.StartUrl}"
                            : $"xdg-open {ctx.StartUrl}") + Environment.NewLine;

                File.WriteAllText(cmd, $"{openBrowserCmd}{targetPath} {arguments}");
            }
        }

        private static void CreatePublishShortcut(WebAppContext ctx, string publishDir, string publishAppDir, string publishToolDir, string toolName)
        {
            var appDir = publishAppDir;
            var toolFilePath = Path.Combine(publishToolDir, toolName);
            var targetPath = toolFilePath.EndsWith(".dll") ? "dotnet" : toolFilePath;
            var arguments = toolFilePath.EndsWith(".dll") ? $"\"{toolFilePath}\"" : "";
            var icon = GetIconPath(appDir);
            var shortcutPath = Path.Combine(publishDir, "name".GetAppSetting(defaultValue: "SharpApp"));
            if (Verbose) $"CreateShortcut: {shortcutPath}, {targetPath}, {arguments}, {appDir}, {icon}".Print();
            CreateShortcut(shortcutPath, targetPath, arguments, appDir, icon, ctx);
        }

        private static string GetIconPath(string appDir, string createShortcutFor=null) => createShortcutFor == null
            ? "icon".GetAppSettingPath(appDir) ?? GetFavIcon()
            : GetFavIcon();

        private static string GetFavIcon() => File.Exists("favicon.ico") ? Path.GetFullPath("favicon.ico") : ToolFavIcon;

        private static (string publishDir, string publishAppDir, string publishToolDir) GetPublishDirs(string toolName, string appDir)
        {
            var publishDir = Path.Combine(appDir, "publish");
            var publishAppDir = Path.Combine(publishDir, "app");
            var publishToolDir = Path.Combine(publishDir, toolName);

            publishDir.AssertDirectory();
            publishAppDir.AssertDirectory();
            publishToolDir.AssertDirectory();

            return (publishDir, publishAppDir, publishToolDir);
        }

        private static (string contentRoot, string useWebRoot) GetDirectoryRoots(WebAppContext ctx)
        {
            var appDir = ctx.AppDir;
            var contentRoot = "contentRoot".GetAppSettingPath(appDir) ?? appDir;

            var wwwrootPath = Path.Combine(appDir, "wwwroot");
            var webRoot = Directory.Exists(wwwrootPath)
                ? wwwrootPath
                : contentRoot;

            var useWebRoot = "webRoot".GetAppSettingPath(appDir) ?? webRoot;
            return (contentRoot, useWebRoot);
        }

        private static WebAppContext CreateWebAppContext(WebAppContext ctx)
        {
            var (contentRoot, useWebRoot) = GetDirectoryRoots(ctx);
            var builder = WebHost.CreateDefaultBuilder(ctx.Arguments)
                .UseContentRoot(contentRoot)
                .UseWebRoot(useWebRoot)
                .UseModularStartup<Startup>()
                .ConfigureLogging(config =>
                {
                    if (!GetDebugMode() && !Verbose &&
                        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
                    {
                        config.ClearProviders();
                        config.SetMinimumLevel(LogLevel.None);
                    }
                })
                .UseUrls(ctx.UseUrls);

            ctx.Builder = builder;

            if (Verbose) ctx.GetDebugString().Print();

            return ctx;
        }

        public static void PrintUsage(string tool)
        {
            var runProcess = "";
            if (Events?.RunNetCoreProcess != null)
            {
                runProcess =  $"  {tool} <name>.dll              Run external .NET Core App{Environment.NewLine}";
                runProcess += $"  {tool} <name>.exe              Run external self-contained .NET Core App{Environment.NewLine}";
            }

            var additional = new StringBuilder();

            var indt = "".PadLeft(tool.Length, ' ');
            
            string USAGE = $@"
Version:  {GetVersion()}

Usage:   
  
  {tool} new                     List available Project Templates
  {tool} new <template> <name>   Create New Project From Template
  {tool} download <user>/<repo>  Download latest GitHub Repo Release
  {tool} get <url>               Download remote file                     (-out <file|dir>)

  {tool} <lang>                  Update all ServiceStack References in directory (recursive)
  {tool} <file>                  Update existing ServiceStack Reference (e.g. dtos.cs)
  {tool} <lang>     <url> <file> Add ServiceStack Reference and save to file name
  {tool} csharp     <url>        Add C# ServiceStack Reference            (Alias 'cs')
  {tool} typescript <url>        Add TypeScript ServiceStack Reference    (Alias 'ts')
  {tool} swift      <url>        Add Swift ServiceStack Reference         (Alias 'sw')
  {tool} java       <url>        Add Java ServiceStack Reference          (Alias 'ja')
  {tool} kotlin     <url>        Add Kotlin ServiceStack Reference        (Alias 'kt')
  {tool} dart       <url>        Add Dart ServiceStack Reference          (Alias 'da')
  {tool} fsharp     <url>        Add F# ServiceStack Reference            (Alias 'fs')
  {tool} vbnet      <url>        Add VB.NET ServiceStack Reference        (Alias 'vb')
  {tool} tsd        <url>        Add TypeScript Definition ServiceStack Reference

  {tool} proto <url>             Add gRPC .proto ServiceStack Reference
  {tool} proto <url> <name>      Add gRPC .proto and save to <name>.services.proto
  {tool} proto                   Update all gRPC *.services.proto ServiceStack References
  {tool} proto-langs             Display list of gRPC supported languages
  {tool} proto-<lang> <url>      Add gRPC .proto and generate language    (-out <dir>)
  {tool} proto-<lang> <file|dir> Update gRPC .proto and re-gen language   (-out <dir>)
  {tool} proto-<lang>            Update all gRPC .proto's and re-gen lang (-out <dir>)

  {tool} mix                     Show available gists to mixin            (Alias '+')
  {tool} mix <name>              Write gist files locally, e.g:           (Alias +init)
  {tool} mix init                Create empty .NET Core ServiceStack App
  {tool} mix [tag]               Search available gists
  {tool} mix <gist-url>          Write all Gist text files to current directory
  {tool} gist <gist-id>          Write all Gist text files to current directory

  {tool} alias                   Show all local gist aliases (for usage in mix or app's)
  {tool} alias <alias>           Print local alias value
  {tool} alias <alias> <gist-id> Set local alias with Gist Id or Gist URL
  {tool} unalias <alias>         Remove local alias

  {tool} run <name>.ss           Run #Script within context of AppHost   (or <name>.html)
  {tool} watch <name>.ss         Watch #Script within context of AppHost (or <name>.html)
  {indt}                         Language File Extensions:
                                   .ss - #Script source file
                                   .sc - #Script `code` source file
                                   .l  - #Script `lisp` source file
  {tool} lisp                    Start Lisp REPL

  {tool} open                    List of available Sharp Apps
  {tool} open <app>              Install and run Sharp App

  {tool} run                     Run Sharp App in current directory
  {tool} run <name>              Run Installed Sharp App
  {tool} run path/app.settings   Run Sharp App at directory containing specified app.settings
{runProcess}
  {tool} install                 List available Sharp Apps to install     (Alias 'l')
  {tool} install <app>           Install Sharp App                        (Alias 'i')

  {tool} uninstall               List Installed Sharp Apps
  {tool} uninstall <app>         Uninstall Sharp App
  
  {tool} publish                 Publish Current Directory to Gist (requires token)
  
  {tool} gist-new <dir>          Create new Gist with Directory Files (requires token)
  {tool} gist-update <id> <dir>  Update Gist ID with Directory Files  (requires token)
  
  {tool} shortcut                Create Shortcut for Sharp App
  {tool} shortcut <name>.dll     Create Shortcut for .NET Core App
{additional}

  dotnet tool update -g {tool}   Update to latest version

Options:
    -h, --help, ?             Print this message
    -v, --version             Print this version
    -d, --debug               Run in Debug mode for Development
    -r, --release             Run in Release mode for Production
    -s, --source              Change GitHub Source for App Directory
    -f, --force               Quiet mode, always approve, never prompt
        --token               Use GitHub Auth Token 
        --clean               Delete downloaded caches
        --verbose             Display verbose logging
        --ignore-ssl-errors   Ignore SSL Errors

This tool collects anonymous usage to determine the most used commands to improve your experience.
To disable set SERVICESTACK_TELEMETRY_OPTOUT=1 environment variable to 1 using your favorite shell.";
            Console.WriteLine(USAGE);
        }

        public static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        public class Instruction
        {
            public string Command;
            public string AppDir;
            public string AppSettingsPath;
            public bool Handled;
        }

        public static async Task<Instruction> HandledCommandAsync(string tool, string[] args)
        {
            if (args.Length == 0) 
                return null;

            var cmd = Regex.Replace(args[0], "^-+", "/");

            Task<string> checkUpdatesAndQuit = null;
                        
            var arg = args[0];
            if (RefAlias.Keys.Contains(arg) || RefAlias.Values.Contains(arg))
            {
                var lang = RefAlias.TryGetValue(arg, out var value)
                    ? value
                    : arg;
                var dtosExt = RefExt[lang];
                
                if (args.Length == 1)
                {
                    UpdateAllReferences(tool, lang, Environment.CurrentDirectory, dtosExt);
                }
                else if (args.Length >= 2)
                {
                    var target = args[1];
                    if (target.IsUrl())
                    {
                        var fileName = args.Length == 3 ? args[2] : null;
                        var filePath = GetDtoTypesFilePath(target, dtosExt, fileName);

                        var typesUrl = target.IndexOf($"/types/{lang}", StringComparison.Ordinal) == -1
                            ? string.IsNullOrEmpty(PathArg) 
                                ? target.CombineWith($"/types/{lang}")
                                : target.CombineWith(PathArg)
                            : target;

                        SaveReference(tool, lang, typesUrl, filePath);
                    } 
                    else 
                    {
                        UpdateReference(tool, lang, Path.GetFullPath(target));
                    }
                }
                return new Instruction { Handled = true };
            }
            
            if (arg.StartsWith("proto-"))
            {
                var lang = arg.RightPart('-');
                var target = args.Length > 1 ? args[1] : null;
                if (lang == null)
                    return null;

                if (target == null) //find first *.proto + re-gen dir
                {
                    var fs = new FileSystemVirtualFiles(Environment.CurrentDirectory);
                    var protoFiles = fs.GetAllMatchingFiles("*services.proto");

                    var firstProto = protoFiles.FirstOrDefault();
                    if (firstProto == null)
                        throw new Exception("Could not find any *.services.proto gRPC Service Descriptions");
                    
                    fs = new FileSystemVirtualFiles(firstProto.Directory.RealPath);
                    protoFiles = fs.GetAllMatchingFiles("*.proto");
                    var protoFilePaths = protoFiles.Map(x => x.RealPath);
                    
                    WriteGrpcFiles(lang, protoFilePaths, GetOutDir(firstProto.RealPath));
                }
                else if (target.IsUrl())
                {
                    var fileName = args.Length > 2 ? args[2] : null;
                    if (fileName != null && !fileName.IsValidFileName())
                        throw new Exception($"Not a valid file name '{fileName}'");
                        
                    var filePath = GetDtoTypesFilePath(target, "services.proto", fileName);
                    
                    var typesUrl = target.IndexOf($"/types/", StringComparison.Ordinal ) == -1
                        ? target.CombineWith($"/types/proto")
                        : target;

                    var writtenFiles = SaveReference(tool, lang, typesUrl, filePath);

                    WriteGrpcFiles(lang, writtenFiles, GetOutDir(filePath));
                }
                else if (File.Exists(target))
                {
                    WriteGrpcFiles(lang, new[]{ target }, GetOutDir(target));
                }
                else if (Directory.Exists(target))
                {
                    var fs = new FileSystemVirtualFiles(target);
                    var protoFiles = fs.GetAllMatchingFiles("*.proto");
                    var protoFilePaths = protoFiles.Map(x => x.RealPath);
                    
                    WriteGrpcFiles(lang, protoFilePaths, GetOutDir(target));
                }
                else throw new Exception($"Could not find valid *.services.proto from '{target}'");

                return new Instruction { Handled = true };
            }

            if (GistNew)
            {
                AssertGitHubToken();

                var dirArg = args.Length == 1 ? args[0] : null;
                if (dirArg == null || !Directory.Exists(dirArg))
                    throw new Exception($"Usage: {tool} gist-new <dir>");
                
                var dirInfo = new DirectoryInfo(dirArg);
                var files = new Dictionary<string, object>();
                dirInfo.FullName.CopyAllToDictionary(files, 
                    excludePaths:WebTemplateUtils.ExcludeFoldersNamed.ToArray(),
                    excludeExtensions:WebTemplateUtils.ExcludeFileExtensions.ToArray());

                if (files.Count == 0)
                    throw new Exception("Directory contained no files to upload");

                var gateway = new GitHubGateway(GitHubToken);

                var gist = gateway.CreateGithubGist(
                    dirInfo.Name + " files", 
                    isPublic: true, 
                    files: files);

                var plural = files.Count == 1 ? "s" : "";
                $"{files.Count} file{plural} uploaded to: {gist.Url}".Print();

                return new Instruction { Handled = true };
            }
            else if (GistUpdate)
            {
                AssertGitHubToken();

                var gistId = args.Length == 2 ? args[0] : null;
                var dirArg = gistId != null ? args[1] : null;
                if (dirArg == null || !Directory.Exists(dirArg))
                    throw new Exception($"Usage: {tool} gist-update <gist-id> <dir>");

                var gateway = new GitHubGateway(GitHubToken);

                var dirInfo = new DirectoryInfo(dirArg);
                var files = new Dictionary<string, object>();
                dirInfo.FullName.CopyAllToDictionary(files, 
                    excludePaths:WebTemplateUtils.ExcludeFoldersNamed.ToArray(),
                    excludeExtensions:WebTemplateUtils.ExcludeFileExtensions.ToArray());
                
                if (files.Count == 0)
                    throw new Exception("Directory contained no files to upload");

                gateway.WriteGistFiles(gistId, files);

                var plural = files.Count == 1 ? "s" : "";
                $"Updated {files.Count}{plural} at: https://gist.github.com/{gistId}".Print();
            }
            
            if (args.Length == 1)
            {
                if (RefExt.Values.Any(ext => arg.EndsWith(ext)))
                {
                    foreach (var entry in RefExt)
                    {
                        if (arg.EndsWith(entry.Value))
                        {
                            UpdateReference(tool, entry.Key, Path.GetFullPath(arg));
                            return new Instruction { Handled = true };
                        }
                    }
                }
                if (arg == "list" || arg == "l")
                {
                    RegisterStat(tool, "list");
                    checkUpdatesAndQuit = BeginCheckUpdates(tool);

                    PrintGistLinks(tool, GetGistAppsLinks(), usage:$"Usage: {tool} open <name>");
                }
                else if (arg == "new")
                {
                    RegisterStat(tool, "new");
                    checkUpdatesAndQuit = BeginCheckUpdates(tool);
                    
                    await PrintSources(GitHubSourceTemplates.Split(';'));
                    
                    $"Usage: {tool} new <template> <name>".Print();
                }
                else if (arg[0] == '+')
                {
                    if (arg == "+")
                    {
                        RegisterStat(tool, arg);
                        checkUpdatesAndQuit = BeginCheckUpdates(tool);
                        PrintGistLinks(tool, GetGistApplyLinks());
                    }
                    else
                    {
                        RegisterStat(tool, arg, "+");

                        var gistAliases = arg.Substring(1).Split('+');
                        if (ApplyGists(tool, gistAliases))
                            return new Instruction { Command = "+", Handled = true };

                        checkUpdatesAndQuit = BeginCheckUpdates(tool);
                    }
                }
                else if (arg == "init") //backwards compat, create Sharp App
                {
                    RegisterStat(tool, arg);
                    Silent = true;
                    WriteGistFile("5c9ee9031e53cd8f85bd0e14881ddaa8", null, ".", null, null);
                    return new Instruction { Command = "init", Handled = true };
                }
                else if (new[] { "/h", "?", "/?", "/help" }.Contains(cmd))
                {
                    PrintUsage(tool);
                    return new Instruction { Command = "help", Handled = true };
                }
                else if (new[] { "/v", "/version" }.Contains(cmd))
                {
                    RegisterStat(tool, "version");
                    checkUpdatesAndQuit = BeginCheckUpdates(tool);
                    $"Version: {GetVersion()}".Print();
                    $"ServiceStack: {Env.VersionString}".Print();
                    $"Framework: {RuntimeInformation.FrameworkDescription}".Print();
                    $"OS: {Environment.OSVersion}".Print();
                    AppVersion?.Print();
                }
                else if (new[] { "/clean", "/clear" }.Contains(cmd))
                {
                    RegisterStat(tool, "clean");
                    checkUpdatesAndQuit = BeginCheckUpdates(tool);
                    var cachesDir = GetCacheDir();
                    DeleteDirectory(cachesDir);
                    $"All caches deleted in '{cachesDir}'".Print();
                }
            }
            else if (args.Length == 2)
            {
                if (arg[0] == '+')
                {
                    if (args[1][0] == '#')
                    {
                        RegisterStat(tool, arg + args[1], "+");
                        PrintGistLinks(tool, GetGistApplyLinks(), args[1].Substring(1));
                        return new Instruction { Command = "+", Handled = true };
                    }
                    
                    RegisterStat(tool, arg + "-project", "+");

                    var gistAliases = arg.Substring(1).Split('+');
                    if (ApplyGists(tool, gistAliases))
                        return new Instruction { Command = "+", Handled = true };

                    checkUpdatesAndQuit = BeginCheckUpdates(tool);
                }
                if (arg == "gist")
                {
                    var gist = args[1];
                    RegisterStat(tool, gist, "gist");
                    WriteGistFile(gist, gistAlias:null, to:".", projectName:null, getUserApproval:UserInputYesNo);
                    return new Instruction { Command = "gist", Handled = true };
                }
                if (arg == "get")
                {
                    var url = args[1];
                    if (!url.IsUrl())
                        throw new Exception($"'{url}' is not a URL");

                    string fileName = url.LastRightPart('/');

                    RegisterStat(tool, fileName, "get");
                    // checkUpdatesAndQuit = BeginCheckUpdates(tool);

                    var bytes = await url.GetBytesFromUrlAsync(requestFilter: req => {
                        req.UserAgent = GitHubUtils.UserAgent;
                    }, responseFilter: res => {
                        var disposition = res.Headers[HttpHeaders.ContentDisposition];
                        if (!string.IsNullOrEmpty(disposition))
                        {
                            var parts = disposition.Split(';');
                            foreach (var part in parts)
                            {
                                if (part.TrimStart().StartsWithIgnoreCase("filename"))
                                {
                                    var name = part.RightPart('=');
                                    name = name.StripQuotes();
                                    if (!name.IsValidFileName())
                                        fileName = name;
                                    return;
                                }
                            }
                        }
                    });

                    if (!string.IsNullOrEmpty(OutDir))
                    {
                        fileName = Directory.Exists(OutDir)
                            ? Path.Combine(OutDir, fileName)
                            : OutDir;
                    }
                    await File.WriteAllBytesAsync(Path.GetFullPath(fileName), bytes);
                    return new Instruction { Command = "get", Handled = true };
                }
            }

            var isDownload = arg == "download";
            if ((arg == "new" && (args.Length == 2 || args.Length == 3)) || (isDownload && args.Length == 2))
            {
                var repo = args[1];
                var parts = repo.Split('+');
                string[] gistAliases = null; 
                if (parts.Length > 1)
                {
                    repo = parts[0];
                    gistAliases = parts.Skip(1).ToArray();
                    
                    var links = GetGistApplyLinks();
                    foreach (var gistAlias in gistAliases)
                    {
                        var gistLink = GistLink.Get(links, gistAlias);
                        if (gistLink == null)
                        {
                            $"No match found for '{gistAlias}', available gists:".Print();
                            PrintGistLinks(tool, links);
                            return new Instruction { Command = "new+", Handled = true };
                        }
                    }
                }
                
                var projectName = isDownload
                    ? args[1].LastRightPart('/')
                    : args.Length > 2 ? args[2] : args[1].SafeVarRef();
                if (!isDownload)
                    AssertValidProjectName(projectName, tool);

                RegisterStat(tool, repo, arg);

                var orgs = GitHubSourceTemplates.Split(';').Select(x => x.LeftPart(' ')).ToArray();
                string downloadUrl = repo.IndexOf("://", StringComparison.OrdinalIgnoreCase) >= 0
                    ? repo
                    : null;

                if (downloadUrl == null)
                {
                    var isFullRepo = repo.IndexOf('/') >= 0;
                    var fullRepo = isFullRepo 
                        ? Tuple.Create(repo.LeftPart('/'), repo.RightPart('/'))
                        : GitHubUtils.Gateway.FindRepo(orgs, repo);

                    if (isFullRepo)
                    {
                        repo = repo.RightPart('/');
                    }
                
                    downloadUrl = await GitHubUtils.Gateway.GetSourceZipUrlAsync(fullRepo.Item1, fullRepo.Item2);
                    $"Installing {repo}...".Print();
                }
                else
                {
                    repo = "master";
                    $"Installing {downloadUrl}...".Print();
                }

                var cachedVersionPath = DownloadCachedZipUrl(downloadUrl);
                var tmpDir = Path.Combine(Path.GetTempPath(), "servicestack", repo);
                DeleteDirectory(tmpDir);

                if (Verbose) $"ExtractToDirectory: {cachedVersionPath} -> {tmpDir}".Print();
                ZipFile.ExtractToDirectory(cachedVersionPath, tmpDir);
                var installDir = Path.GetFullPath(repo);

                var projectDir = new DirectoryInfo(Path.Combine(new DirectoryInfo(installDir).Parent!.FullName, projectName));
                DeleteDirectory(projectDir.FullName);
                MoveDirectory(new DirectoryInfo(tmpDir).GetDirectories().First().FullName, projectDir.FullName);

                RenameProject(str => ReplaceMyApp(str, projectName), projectDir);

                // Restore Solution
                var slns = Directory.GetFiles(projectDir.FullName, "*.sln", SearchOption.AllDirectories);
                if (slns.Length == 1)
                {
                    var sln = slns[0];
                    if (Verbose) $"Found {sln}".Print();
                    
                    if (GetExePath("nuget", out var nugetPath))
                    {
                        $"running nuget restore...".Print();
                        PipeProcess(nugetPath, $"restore \"{Path.GetFileName(sln)}\"", workDir: Path.GetDirectoryName(sln));                        
                    }
                    else if (GetExePath("dotnet", out var dotnetPath))
                    {
                        $"running dotnet restore...".Print();
                        PipeProcess(dotnetPath, $"restore \"{Path.GetFileName(sln)}\"", workDir: Path.GetDirectoryName(sln));                        
                    }
                    else
                    {
                        $"'nuget' or 'dotnet' not found in PATH, skipping restore.".Print();
                    }
                }
                else if (Verbose) $"Found {slns.Length} *.sln".Print();

                // Install npm dependencies (if any)
                var packageJsons = Directory.GetFiles(projectDir.FullName, "package.json", SearchOption.AllDirectories);
                if (packageJsons.Length == 1)
                {
                    var packageJson = packageJsons[0];
                    if (Verbose) $"Found {packageJson}".Print();

                    var npmScript = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "npm.cmd" : "npm";
                    if (GetExePath(npmScript, out var npmPath))
                    {
                        $"running npm install...".Print();
                        PipeProcess(npmPath, "install", workDir: Path.GetDirectoryName(packageJson));
                    }
                    else
                    {
                        $"'npm' not found in PATH, skipping npm install.".Print();
                    }
                }
                else if (Verbose) $"Found {packageJsons.Length} package.json".Print();

                // Install libman dependencies (if any)
                var packageLibmans = Directory.GetFiles(projectDir.FullName, "libman.json", SearchOption.AllDirectories);
                if (packageLibmans.Length == 1)
                {
                    var packageLibman = packageLibmans[0];
                    if (Verbose) $"Found {packageLibman}".Print();

                    if (GetExePath("libman", out var libmanPath))
                    {
                        $"running libman restore...".Print();
                        PipeProcess(libmanPath, "restore", workDir: Path.GetDirectoryName(packageLibman));
                    }
                    else
                    {
                        $"'libman' not found in PATH, skipping 'libman restore'.".Print();
                        $"Install 'libman cli' with: dotnet tool install -g Microsoft.Web.LibraryManager.CLI".Print();
                    }
                }
                else if (Verbose) $"Found {packageLibmans.Length} libman.json".Print();
                
                "".Print();

                if (gistAliases != null)
                {
                    Directory.SetCurrentDirectory(projectDir.FullName);
                    var links = GetGistApplyLinks();
                    foreach (var gistAlias in gistAliases)
                    {
                        var gistLink = GistLink.Get(links, gistAlias);
                        WriteGistFile(gistLink.Url, gistAlias, to:gistLink.To, projectName:projectName, getUserApproval:null);
                    }
                }
                if (arg != "download")
                    $"{projectName} {repo} project created.".Print();
                else
                    $"{repo} project downloaded.".Print();
                
                "".Print();
                return new Instruction { Handled = true };
            }
            
            if (await CheckForUpdates(tool, checkUpdatesAndQuit))
                return new Instruction { Handled = true };
            
            return null;
        }

        private static void WriteGrpcFiles(string lang, IEnumerable<string> protoFiles, string outDir)
        {
            var client = new JsonServiceClient(GrpcSource);

            var request = new Protoc {
                Lang = lang,
                Files = new Dictionary<string, string>()
            };

            foreach (var writtenFilePath in protoFiles)
            {
                var fi = new FileInfo(writtenFilePath);
                request.Files[fi.Name] = fi.ReadAllText();
            }

            if (Verbose) $"API: {GrpcSource}{request.ToPostUrl()}".Print();
            var response = client.Post(request);

            outDir.AssertDirectory();

            var fileNames = string.Join(", ", response.GeneratedFiles.Keys);
            if (Verbose) $"writing {fileNames} to {outDir} ...".Print();

            var fs = new FileSystemVirtualFiles(outDir);
            fs.WriteFiles(response.GeneratedFiles);
        }

        private static string GetOutDir(string filePath)
        {
            var outDir = OutDir != null
                ? Path.GetFullPath(OutDir)
                : Directory.Exists(filePath)
                    ? Path.GetFullPath(filePath)
                    : File.Exists(filePath) 
                        ? Path.GetDirectoryName(Path.GetFullPath(filePath))
                        : Environment.CurrentDirectory;
            return outDir;
        }

        private static string GetDtoTypesFilePath(string targetUrl, string dtosExt, string fileName=null)
        {
            if (fileName == null)
            {
                // if it's the first, use shortest convention or allow overriding default .proto
                if (!File.Exists(dtosExt) || dtosExt == "services.proto") 
                {
                    fileName = dtosExt;
                }
                else
                {
                    var parts = new Uri(targetUrl).Host.Split('.');
                    fileName = parts.Length >= 2
                        ? parts[parts.Length - 2]
                        : parts[0];
                }
            }

            if (!fileName.EndsWith(dtosExt))
            {
                fileName = $"{fileName}.{dtosExt}";
            }

            if (OutDir != null)
                fileName = Path.Combine(OutDir, fileName);

            return Path.GetFullPath(fileName);
        }

        private static string GetCacheDir()
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var cachesDir = Path.Combine(homeDir, ".servicestack", "cache");
            return cachesDir.AssertDirectory();
        }

        private static readonly Dictionary<string,string> RefAlias = new Dictionary<string, string>
        {
            {"cs", "csharp"},
            {"ts", "typescript"},
            {"sw", "swift"},
            {"ja", "java"},
            {"kt", "kotlin"},
            {"da", "dart"},
            {"fs", "fsharp"},
            {"vb", "vbnet"},
            {"tsd", "typescript.d"},
            {"po", "proto"},
        };
        
        private static readonly Dictionary<string,string> RefExt = new Dictionary<string, string>
        {
            {"csharp", "dtos.cs"},
            {"typescript", "dtos.ts"},
            {"swift", "dtos.swift"},
            {"java", "dtos.java"},
            {"kotlin", "dtos.kt"},
            {"dart", "dtos.dart"},
            {"fsharp", "dtos.fs"},
            {"vbnet", "dtos.vb"},
            {"typescript.d", "dtos.d.ts"},
            {"proto", "services.proto"},
        };

        public class Http2CustomHandler : WinHttpHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                request.Version = new Version("2.0");
                return base.SendAsync(request, cancellationToken);
            }
        }

        public static List<string> SaveReference(string tool, string lang, string typesUrl, string filePath)
        {
            var exists = File.Exists(filePath);
            RegisterStat(tool, lang, exists ? "updateref" : "addref");

            if (Verbose) $"API: {typesUrl}".Print();
            string dtosSrc;
            try 
            { 
                dtosSrc = typesUrl.GetStringFromUrl(requestFilter:req => req.ApplyRequestFilters());
            }
            catch (WebException) // .NET HttpWebRequest doesn't support HTTP/2 yet, try with HttpClient/WinHttpHandler
            {
#if !NETCORE3
                var handler = new Http2CustomHandler();
                using var client = new HttpClient(handler);
                var res = TaskExt.RunSync(async () => await client.GetAsync(typesUrl));
                dtosSrc = res.Content.ReadAsStringAsync().Result;
#else
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                var req = new HttpRequestMessage(HttpMethod.Get, typesUrl) {
                     Version = new Version(2, 0) 
                };
                var client = new HttpClient();
                dtosSrc = client.SendAsync(req).Result.Content.ReadAsStringAsync().Result;
#endif
            }

            if (dtosSrc.IndexOf("Options:", StringComparison.Ordinal) == -1) 
                throw new Exception($"Invalid Response from {typesUrl}");

            var filesWritten = new List<string>();
            File.WriteAllText(filePath, dtosSrc, Utf8WithoutBom);
            filesWritten.Add(filePath);

            if (lang == "proto")
            {
                const string BaseProtoBufNetRawUrl = "https://raw.githubusercontent.com/protobuf-net/protobuf-net/master/src/protobuf-net.Reflection/protobuf-net/";
                const string ImportProtoBufNetPrefix = "import \"protobuf-net/";
                foreach (var line in dtosSrc.ReadLines())
                {
                    if (line.StartsWith(ImportProtoBufNetPrefix))
                    {
                        var protoFileName = line.Substring(ImportProtoBufNetPrefix.Length).LeftPart('"');
                        var rawUrl = BaseProtoBufNetRawUrl.CombineWith(protoFileName);
                        var protoContents = rawUrl.GetStringFromUrl();

                        var dirPath = Path.Combine(Path.GetDirectoryName(filePath)!, "protobuf-net");
                        if (!Directory.Exists(dirPath))
                            Directory.CreateDirectory(dirPath);

                        var protoFilePath = Path.Combine(dirPath, protoFileName);
                        if (Verbose) $"Writing protobuf-net/{protoFileName}".Print();
                        File.WriteAllText(protoFilePath, protoContents, Utf8WithoutBom);
                        filesWritten.Add(protoFilePath);
                    }
                }
            }

            var fileName = Path.GetFileName(filePath);
            (exists ? $"Updated: {fileName}" : $"Saved to: {fileName}").Print();

            return filesWritten;
        }

        public static void UpdateReference(string tool, string lang, string existingRefPath)
        {
            if (!File.Exists(existingRefPath))
                throw new Exception($"File does not exist: {existingRefPath.Replace('\\', '/')}");

            var target = Path.GetFileName(existingRefPath);
            var targetExt = target.LastRightPart('.');
            var langExt = RefExt[lang].LastRightPart('.');
            if (targetExt != langExt) 
                throw new Exception($"Invalid file type: '{target}', expected '.{langExt}' source file");

            var existingRefSrc = File.ReadAllText(existingRefPath);

            var startPos = existingRefSrc.IndexOf("Options:", StringComparison.Ordinal);
            if (startPos == -1) 
                throw new Exception($"{target} is not an existing ServiceStack Reference");

            var options = new Dictionary<string,string>();
            var baseUrl = "";
            var usePath = "";

            existingRefSrc = existingRefSrc.Substring(startPos);
            var lines = existingRefSrc.Replace("\r","").Split('\n');
            foreach (var l in lines) 
            {
                var line = l;
                if (line.StartsWith("*/") || line.StartsWith("*)"))
                    break;
                if (lang == "vbnet")
                {
                    if (line.Trim().Length == 0)
                        break;
                    if (line[0] == '\'')
                        line = line.Substring(1);
                }            
        
                if (line.StartsWith("BaseUrl: ")) 
                {
                    baseUrl = line.Substring("BaseUrl: ".Length);
                } 
                else if (line.StartsWith("UsePath: ")) 
                {
                    usePath = line.Substring("UsePath: ".Length);
                } 
                else if (!string.IsNullOrEmpty(baseUrl)) 
                {
                    if (!line.StartsWith("//") && !line.StartsWith("'")) 
                    {
                        var parts = line.SplitOnFirst(":");
                        if (parts.Length == 2) {
                            var key = parts[0].Trim();
                            var val = parts[1].Trim();
                            options[key] = val;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(baseUrl))
                throw new Exception($"Could not find baseUrl in {target}");

            var qs = "";
            foreach (var key in options.Keys) {
                qs += qs.Length > 0 ? "&" : "?";
                qs += $"{key}={options[key].UrlEncode()}";
            }

            var updateUrl = string.IsNullOrEmpty(usePath)
                ? baseUrl.CombineWith($"/types/{lang}")
                : baseUrl.CombineWith(usePath);

            var typesUrl = updateUrl + qs;
            SaveReference(tool, lang, typesUrl, existingRefPath);
        }

        private static void UpdateAllReferences(string tool, string lang, string dirPath, string dtosExt)
        {
            var dtoRefs = Directory.GetFiles(dirPath, $"*{dtosExt}", SearchOption.AllDirectories);
            if (dtoRefs.Length == 0)
                throw new Exception($"No '{dtosExt}' ServiceStack References found in '{dirPath}'");

            if (Verbose) $"Updating {dtoRefs.Length} Reference(s)".Print();

            foreach (var dtoRef in dtoRefs)
            {
                try
                {
                    UpdateReference(tool, lang, dtoRef);
                }
                catch (Exception ex)
                {
                    $"Could not update ServiceStack Reference '{dtoRef}': ".Print();
                    (Verbose ? ex.ToString() : ex.Message).Print();
                }
            }
        }

        private static readonly Regex ValidNameChars = new Regex("^[a-zA-Z_$][0-9a-zA-Z_$.]*$", RegexOptions.Compiled);
        private static readonly string[] IllegalNames = "CON|AUX|PRN|COM1|LP2|.|..".Split('|');
        private static readonly string[] IgnoreExtensions = "jpg|jpeg|png|gif|ico|eot|otf|webp|svg|ttf|woff|woff2|mp4|webm|wav|mp3|m4a|aac|oga|ogg|dll|exe|pdb|so|zip|key|snk|p12|swf|xap|class|doc|xls|ppt|sqlite|db".Split('|');


        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes:true);

        private static void RenameProject(Func<string,string> replaceFn, DirectoryInfo projectDir)
        {
            foreach (var file in projectDir.GetFiles())
            {
                var fileName = file.Name;
                var parts = fileName.SplitOnLast('.');
                var ext = parts.Length == 2 ? parts[1].ToLower() : null;

                if (!IgnoreExtensions.Contains(ext))
                {
                    var textContents = File.ReadAllText(file.FullName);
                    var newContents = replaceFn(textContents);
                    if (textContents != newContents)
                    {
                        if (Verbose) $"Replacing {file.FullName}".Print();
                        try
                        {
                            File.WriteAllText(file.FullName, newContents, Utf8WithoutBom);
                        }
                        catch (Exception ex)
                        {
                            $"Could not replace text in file '{file.Name}': {ex.Message}".Print();
                        }
                    }
                }

                var oldPath = file.FullName;
                var newName = replaceFn(fileName);
                if (newName != fileName)
                {
                    var newPath = Path.Combine(projectDir.FullName, newName);
                    if (Verbose) $"Moving File {oldPath} -> {newPath}".Print();
                    File.Move(oldPath, newPath);
                }
            }
            
            foreach (var dir in projectDir.GetDirectories())
            {
                RenameProject(replaceFn, dir);
            }

            var newDirName = replaceFn(projectDir.Name);
            if (newDirName != projectDir.Name && projectDir.Parent != null)
            {
                var newDirPath = Path.Combine(projectDir.Parent.FullName, newDirName);
                MoveDirectory(projectDir.FullName, newDirPath);
            }
        }
        
        private static void AssertValidProjectName(string name, string tool) 
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException($"Project Name required.\n\nUsage: {tool} new <template> <name>");

            if (!ValidNameChars.IsMatch(name))
                throw new ArgumentException($"Illegal char in project name: {name}");

            if (IllegalNames.Contains(name))
                throw new ArgumentException($"Illegal project name: {name}");

            if (Directory.Exists(name))
                throw new ArgumentException($"Project folder already exists: {name}");
        }

        private static async Task PrintSources(string[] sources)
        {
            var sourceRepos = new Dictionary<string,string>();
            sources.Each(x => sourceRepos[x.LeftPart(' ')] = x.RightPart(' '));
            
            var sourceTasks = sourceRepos.Map(source => (source.Value, GitHubUtils.Gateway.GetSourceReposAsync(source.Key)));

            foreach (var sourceTask in sourceTasks)
                
            {
                var (source, task) = sourceTask;
                var repos = await task;
                var padName = repos.OrderByDescending(x => x.Name.Length).First().Name.Length + 1;

                "".Print();
                if (sources.Length > 1) $"{source}:{Environment.NewLine}".Print();
                var i = 1;
                foreach (var repo in repos)
                {
                    $" {i++.ToString().PadLeft(3, ' ')}. {repo.Name.PadRight(padName, ' ')} {repo.Description}".Print();
                }
            }
            
            "".Print();
        }

        IConfiguration configuration;
        public Startup(IConfiguration configuration)
            : base(configuration, AppLoader.Assemblies)
        {
            this.configuration = configuration;
        }

        public new void ConfigureServices(IServiceCollection services) 
        {
            var appHost = AppLoader.AppHost;
            var plugins = AppLoader.Plugins;
            plugins?.Each(x => services.AddSingleton(x.GetType(), x));

            services.AddSingleton<ServiceStackHost>(appHost);

            // plugins?.OfType<IStartup>().Each(x => x.ConfigureServices(services));
            // plugins?.OfType<IConfigureServices>().Each(x => x.Configure(services));

            appHost.Configure(services);
        }

#if NETCOREAPP2_1
        public void Configure(IApplicationBuilder app, Microsoft.AspNetCore.Hosting.IHostingEnvironment env)
#else
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
#endif
        {
            // plugins?.OfType<IStartup>().Each(x => x.Configure(app));
            // plugins?.OfType<IConfigureApp>().Each(x => x.Configure(app));

            var appHost = AppLoader.AppHost;
            appHost.BeforeConfigure.Add(PreConfigureAppHost);
            appHost.AfterConfigure.Add(PostConfigureAppHost);

            if (GistVfs != null)
            {
                appHost.InsertVirtualFileSources.Add(GistVfs);
            }

            appHost.AfterInitCallbacks.Add(item: _ => {

                if (DebugMode != null)
                    appHost.Config.DebugMode = appHost.ScriptContext.DebugMode = DebugMode.Value;

                if (GistVfs != null)
                {
                    GistVfsLoadTask?.GetResult();
                    if (Open)
                    {
                        ThreadPool.QueueUserWorkItem(state => {
                            SerializeGistAppFiles();
                        });
                    }
                    var svgDir = GistVfs.GetDirectory("/svg");
                    if (svgDir != null) 
                        Svg.Load(svgDir);
                }
                else
                {
                    var svgDir = appHost.RootDirectory.GetDirectory("/svg"); 
                    if (svgDir != null) 
                        Svg.Load(svgDir);
                }

                var appHostInstructions = GetAppHostInstructions?.Invoke(appHost);
                var feature = appHost.AssertPlugin<SharpPagesFeature>();
                var importParams =  appHostInstructions?.ImportParams ?? new List<string>(); //force explicit declaration of import params
                if ("importParams".TryGetAppSetting(out var configParams))
                    importParams.AddRange(configParams.Trim('[', ']').Split(','));

                if (importParams.Count > 0)
                {
                    foreach (var entry in RunScriptArgs.Safe())
                    {
                        if (importParams.Contains(entry.Key))
                        {
                            feature.Args[entry.Key] = entry.Value;
                        }
                    }
                }
            });
            
            app.UseServiceStack(appHost);
        }

        private static void SerializeGistAppFiles()
        {
            try
            {
                var dirName = new DirectoryInfo(Environment.CurrentDirectory).Name;
                var gist = GistVfs.GetGist();

                try 
                {
                    if (gist.Files.TryGetValue("app.settings", out var appSettingsFile))
                    {
                        foreach (var line in appSettingsFile.Content.ReadLines())
                        {
                            if (line.StartsWith("icon "))
                            {
                                var iconPath = line.Substring("icon ".Length);
                                var file = GistVfs.GetFile(iconPath);
                                if (file != null)
                                {
                                    var fs = new FileSystemVirtualFiles(Environment.CurrentDirectory);
                                    fs.WriteFile(file.VirtualPath, file.GetContents());
                                }
                                else if (Verbose) $"Could not find icon '{iconPath}' in Gist".Print();
                            
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (Verbose) $"ERROR: could not save icon: {e.Message}".Print();
                }
                
                var json = gist.ToJson();
                var cachedGistPath = GetAppsPath($"{dirName}.gist");
                if (Verbose) $"Saving gist '{GistVfs.GistId}' size: {json.Length}' to: {cachedGistPath}".Print();
                File.WriteAllText(cachedGistPath, json);
            }
            catch (Exception ex)
            {
                if (Verbose) $"ERROR: cannot save gist '{GistVfs.GistId}': {ex.Message}".Print();
            }
        }

        private static Exception StartupException = null;

        public void PreConfigureAppHost(ServiceStackHost appHost)
        {
            try 
            { 
                appHost.Config.DebugMode = GetDebugMode();
                appHost.Config.ForbiddenPaths.Add("/plugins");
                appHost.Config.DefaultRedirectPath = "defaultRedirect".GetAppSetting();

                var appName = "appName".GetAppSetting();
                if (appName != null)
                    DesktopConfig.Instance.AppName ??= appName;

                var feature = appHost.GetPlugin<SharpPagesFeature>();
                if (feature != null && Verbose)
                    "Using existing SharpPagesFeature from appHost".Print();
    
                if (feature == null)
                {
                    var isWebApp = appHost.RootDirectory.GetFile("index.html") != null ||
                                   appHost.RootDirectory.GetFile("_layout.html") != null ||
                                   GistVfs?.GetFile("index.html") != null ||
                                   GistVfs?.GetFile("_layout.html") != null;
                    var customFeature = nameof(SharpPagesFeature).GetAppSetting() != null;
                    if (Verbose) "Registering new {0}SharpPagesFeature{1}".Print(customFeature ? "customized " : "", isWebApp ? " Web App" : "");
                    feature = (customFeature
                        ? (SharpPagesFeature)typeof(SharpPagesFeature).CreatePlugin()
                        : new SharpPagesFeature {
                            ApiPath = "apiPath".GetAppSetting() ?? "/api",
                            EnableSpaFallback = isWebApp,
                        });
                }

                var desktopFeature = appHost.GetPlugin<DesktopFeature>();
                if (desktopFeature != null && Verbose)
                    "Using existing DesktopFeature from appHost".Print();

                if (desktopFeature == null)
                {
                    appHost.Config.EmbeddedResourceBaseTypes.Add(typeof(DesktopAssets));

                    appHost.RegisterService<DesktopDownloadUrlService>(DesktopFeature.DesktopDownloadUrlRoutes);

                    if (DesktopConfig.Instance.AppName != null)
                        appHost.RegisterService<DesktopFileService>(DesktopFeature.DesktopFileRoutes);

                    if (Verbose) "Configuring DesktopAssets, DownloadUrlRoutes{0}".Print(DesktopConfig.Instance.AppName != null ? ", FileRoutes" : "");
                }

                feature.Args["ARGV"] = RunScriptArgV;
                
                // Use ~/.servicestack/cache for all downloaded scripts
                feature.CacheFiles = new FileSystemVirtualFiles(GetCacheDir());
                feature.ScriptLanguages.Add(ScriptLisp.Language);

                if (!feature.Plugins.Any(x => x is GitHubPlugin))
                    feature.Plugins.Add(new GitHubPlugin());

                var namedConnectionsKeys = WebTemplateUtils.AppSettings.GetAllKeys()
                    .Where(x => x.StartsWith("db.connections[")).ToList();
    
                var dbFactory = "db".GetAppSetting().GetDbFactory(connectionString:"db.connection".GetAppSetting());
                if (dbFactory != null || namedConnectionsKeys.Count > 0)
                {
                    if (dbFactory == null)
                        dbFactory = new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider);
                    
                    appHost.Container.Register<IDbConnectionFactory>(dbFactory);
                    feature.ScriptMethods.Add(new DbScriptsAsync());

                    dbFactory.RegisterDialectProvider("sqlite", SqliteDialect.Provider);
                    dbFactory.RegisterDialectProvider("sqlserver", SqlServerDialect.Provider);
                    dbFactory.RegisterDialectProvider("sqlserver2012", SqlServer2012Dialect.Provider);
                    dbFactory.RegisterDialectProvider("sqlserver2014", SqlServer2014Dialect.Provider);
                    dbFactory.RegisterDialectProvider("sqlserver2016", SqlServer2016Dialect.Provider);
                    dbFactory.RegisterDialectProvider("sqlserver2017", SqlServer2017Dialect.Provider);
                    dbFactory.RegisterDialectProvider("mysql", MySqlDialect.Provider);
                    dbFactory.RegisterDialectProvider("postgresql", PostgreSqlDialect.Provider);
                    dbFactory.RegisterDialectProvider("postgres", PostgreSqlDialect.Provider);
                    dbFactory.RegisterDialectProvider("pgsql", PostgreSqlDialect.Provider);

                    foreach (var key in namedConnectionsKeys)
                    {
                        var connObj = key.GetAppSetting();
                        if (connObj == null)
                            break;

                        var name = key.RightPart('[').LeftPart(']');
                        if (string.IsNullOrEmpty(name))
                            throw new NotSupportedException($"{key} requires a name for its connection");
                        
                        connObj.ParseJsExpression(out var token);
                        if (!(token is JsObjectExpression expr))
                            throw new NotSupportedException("Expected object literal {db:string, connection:string} but was: " + token.GetType().Name);

                        var dbProp = expr.Properties.FirstOrDefault(x => x.Key is JsIdentifier id && id.Name == "db")
                            ?? throw new NotSupportedException(name + " is missing 'db'");
                        var connectionProp = expr.Properties.FirstOrDefault(x => x.Key is JsIdentifier id && id.Name == "connection")
                            ?? throw new NotSupportedException(name + " is missing 'connection'");

                        string resolveValue(JsProperty prop) => (prop.Value is JsLiteral l
                                ? l.Value as string
                                : prop.Value is JsIdentifier id
                                    ? id.Name
                                    : null)
                            .ResolveValue() ?? throw new NotSupportedException($"'{prop.Key}' is required");

                        var db = resolveValue(dbProp);
                        var connection = resolveValue(connectionProp);

                        var namedDbFactory = db.GetDbFactory(connectionString: connection, setGlobalDialectProvider:false);
                        dbFactory.RegisterConnection(name, namedDbFactory);
                    }
                }
    
                var redisConnString = "redis.connection".GetAppSetting();
                if (redisConnString != null)
                {
                    appHost.Container.Register<IRedisClientsManager>(c => new RedisManagerPool(redisConnString));
                    feature.ScriptMethods.Add(new RedisScripts { 
                        RedisManager = appHost.Container.Resolve<IRedisClientsManager>()
                    });
                }
    
                var checkForModifiedPagesAfterSecs = "checkForModifiedPagesAfterSecs".GetAppSetting();
                if (checkForModifiedPagesAfterSecs != null)
                    feature.CheckForModifiedPagesAfter = TimeSpan.FromSeconds(checkForModifiedPagesAfterSecs.ConvertTo<int>());
    
                var defaultFileCacheExpirySecs = "defaultFileCacheExpirySecs".GetAppSetting();
                if (defaultFileCacheExpirySecs != null)
                    feature.Args[ScriptConstants.DefaultFileCacheExpiry] = TimeSpan.FromSeconds(defaultFileCacheExpirySecs.ConvertTo<int>());
    
                var defaultUrlCacheExpirySecs = "defaultUrlCacheExpirySecs".GetAppSetting();
                if (defaultUrlCacheExpirySecs != null)
                    feature.Args[ScriptConstants.DefaultUrlCacheExpiry] = TimeSpan.FromSeconds(defaultUrlCacheExpirySecs.ConvertTo<int>());
    
                var markdownProvider = "markdownProvider".GetAppSetting();
                var useMarkdownDeep = markdownProvider?.EqualsIgnoreCase("MarkdownDeep") == true;
                MarkdownConfig.Transformer = useMarkdownDeep
                    ? new MarkdownDeep.MarkdownDeepTransformer()
                    : (IMarkdownTransformer) new MarkdigTransformer();
                if (markdownProvider != null)
                    ("Using markdown provider " + (useMarkdownDeep ? "MarkdownDeep" : "Markdig")).Print();
    
                var useJsMin = "jsMinifier".GetAppSetting()?.EqualsIgnoreCase("servicestack") == true;
                if (!useJsMin)
                    Minifiers.JavaScript = new NUglifyJsMinifier();
                var useCssMin = "cssMinifier".GetAppSetting()?.EqualsIgnoreCase("servicestack") == true;
                if (!useCssMin)
                    Minifiers.Css = new NUglifyCssMinifier();
                var useHtmlMin = "htmlMinifier".GetAppSetting()?.EqualsIgnoreCase("servicestack") == true;
                if (!useHtmlMin)
                    Minifiers.Html = new NUglifyHtmlMinifier();
    
                var contextArgKeys = WebTemplateUtils.AppSettings.GetAllKeys().Where(x => x.StartsWith("args."));
                foreach (var key in contextArgKeys)
                {
                    var name = key.RightPart('.');
                    var value = key.GetAppSetting();
    
                    feature.Args[name] = value.StartsWith("{") || value.StartsWith("[")
                        ? JS.eval(value)
                        : value;
                }
                
                ConfigureScript?.Invoke(feature);
    
                appHost.Plugins.AddIfNotExists(feature);
    
                IPlugin[] registerPlugins = AppLoader.Plugins;
                if (registerPlugins != null)
                {
                    foreach (var plugin in registerPlugins)
                    {
                        appHost.Plugins.RemoveAll(x => x.GetType() == plugin.GetType());
                        appHost.Plugins.Add(plugin);
                    }
                }
            }
            catch (Exception ex)
            {
                StartupException = ex;
                throw;
            }
        }

        public void PostConfigureAppHost(ServiceStackHost appHost) {}
        
        public static Action<SharpPagesFeature> ConfigureScript { get; set; }

        public static void RetryExec(Action fn, int retryTimes=5)
        {
            while (retryTimes-- > 0)
            {
                try
                {
                    fn();
                    return;
                }
                catch
                {
                    Thread.Sleep(200);
                }
            }
        }
    }
    
    public static class AppLoader
    {
        private static string ContentRootPath;
        public static Assembly[] Assemblies => AppHost.ServiceAssemblies.ToArray();
        
        static IPlugin[] plugins;
        public static IPlugin[] Plugins => plugins ?? throw new Exception("Plugins not Loaded yet");

        static AppHostBase appHost;
        public static AppHostBase AppHost => appHost ?? throw new Exception("AppHost not loaded yet");

        public static AppHostBase TryGetAppHost() => appHost;

        public static void Init(string contentRootPath)
        {
            if (Startup.GetDebugMode() && Startup.Verbose)
                LogManager.LogFactory = new ConsoleLogFactory(debugEnabled: true);
            
            ContentRootPath = contentRootPath;
            InitAppHost();

            if (Startup.Verbose)
                ($"StartupAssemblies: " + AppHost.ServiceAssemblies.Map(x => x.GetType().FullName).Join(", ")).Print();
            
            InitPlugins();
        }

        static void InitPlugins()
        {
            if (plugins != null)
                throw new Exception("Plugins already initialized");
            
            var features = "features".GetAppSetting();
            if (features != null)
            {
                var featureTypes = features.Split(',').Map(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                featureTypes.Remove(nameof(SharpPagesFeature)); //already added
                var featureIndex = featureTypes.ToArray();
                var registerPlugins = new IPlugin[featureTypes.Count];

                foreach (var type in appHost.ScanAllTypes())
                {
                    if (featureTypes.Count == 0)
                        break;

                    if (featureTypes.Contains(type.Name))
                    {
                        registerPlugins[Array.IndexOf(featureIndex, type.Name)] = type.CreatePlugin();
                        featureTypes.Remove(type.Name);
                    }
                }
                
                //Register any wildcard plugins at end
                const string AllRemainingPlugins = "plugins/*";
                if (featureTypes.Count == 1 && featureTypes[0] == AllRemainingPlugins)
                {
                    var remainingPlugins = new List<IPlugin>();
                    foreach (var type in appHost.ServiceAssemblies.SelectMany(x => x.GetTypes()))
                    {
                        if (type.HasInterface(typeof(IPlugin)) && registerPlugins.All(x => x?.GetType() != type))
                        {
                            var plugin = type.CreatePlugin();
                            remainingPlugins.Add(plugin);
                        }
                    }
                    if (Startup.Verbose) $"Registering wildcard plugins: {remainingPlugins.Map(x => x.GetType().Name).Join(", ")}".Print(); 
                    featureTypes.Remove(AllRemainingPlugins);
                    if (remainingPlugins.Count > 0)
                    {
                        var mergedPlugins = new List<IPlugin>(registerPlugins.Where(x => x != null));
                        mergedPlugins.AddRange(remainingPlugins);
                        registerPlugins = mergedPlugins.ToArray();
                    }
                }

                if (featureTypes.Count > 0)
                {
                    var plural = featureTypes.Count > 1 ? "s" : "";
                    throw new NotSupportedException($"Unable to locate plugin{plural}: " + string.Join(", ", featureTypes));
                }

                plugins = registerPlugins;
            }
            else
            {
                plugins = new IPlugin[0];
            }
        }

        static void InitAppHost()
        {
            if (appHost != null)
                throw new Exception("AppHost already initialized");
            
            RegisterKey();
            WebTemplateUtils.VirtualFiles = new FileSystemVirtualFiles(ContentRootPath);

            var assemblies = new List<Assembly>();
            var filesConfig = "files.config".GetAppSetting();               
            var vfs = "files".GetAppSetting().GetVirtualFiles(config:filesConfig);
            var pluginsDir = (vfs ?? WebTemplateUtils.VirtualFiles).GetDirectory("plugins")
                ?? Startup.GistVfs?.GetDirectory("plugins");
            if (pluginsDir != null)
            {
                var pluginDllFiles = pluginsDir.GetFiles();

                AssemblyLoadContext.Default.Resolving += (context, asmName) => {
                    var dll = asmName.Name + ".dll";
                    var exe = asmName.Name + ".exe";
                    if (Startup.Verbose) $"Resolving Assembly in /plugins: {asmName.Name} / {asmName.FullName}".Print();
                    var plugin = pluginDllFiles.FirstOrDefault(x =>
                        x.Name.EqualsIgnoreCase(dll) || x.Name.EqualsIgnoreCase(exe));
                    if (Startup.Verbose)
                    {
                        if (plugin != null)
                            $"Found matching '{plugin.Name}' in /plugins".Print();
                        else
                            $"Could not find '{asmName.Name}' in /plugins".Print();
                    }
                    if (plugin != null)
                    {
                        using var stream = plugin.OpenRead();
                        var asm = AssemblyLoadContext.Default.LoadFromStream(stream);
                        return asm;
                    }
                    return null;
                };

                foreach (var plugin in pluginDllFiles)
                {
                    if (plugin.Extension != "dll" && plugin.Extension != "exe")
                        continue;

                    if (Startup.Verbose) $"Attempting to load plugin '{plugin.VirtualPath}', size: {plugin.Length} bytes".Print();

                    using (var stream = plugin.OpenRead())
                    {
                        var asm = AssemblyLoadContext.Default.LoadFromStream(stream);
                        assemblies.Add(asm);
                        
                        if (appHost == null)
                        {
                            foreach (var type in asm.GetTypes())
                            {
                                if (typeof(AppHostBase).IsAssignableFrom(type))
                                {
                                    if (Startup.Verbose) $"Using AppHost from Plugin '{plugin.VirtualPath}'".Print();
                                    appHost = type.CreateInstance<AppHostBase>();
                                    appHost.AppSettings = WebTemplateUtils.AppSettings;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (appHost == null)
                appHost = new AppHost();

            WebTemplateUtils.AppHost = appHost;

            if (assemblies.Count > 0)
            {
                assemblies.Each(x => appHost.ServiceAssemblies.AddIfNotExists(x));
            }
  
            if (vfs != null)
                appHost.AddVirtualFileSources.Add(vfs);

            if (vfs is IVirtualFiles writableFs)
                appHost.VirtualFiles = writableFs;
        }

        public static void RegisterKey() => Licensing.RegisterLicense(
            "1001-e1JlZjoxMDAxLE5hbWU6VGVzdCBCdXNpbmVzcyxUeXBlOkJ1c2luZXNzLEhhc2g6UHVNTVRPclhvT2ZIbjQ5MG5LZE1mUTd5RUMzQnBucTFEbTE3TDczVEF4QUNMT1FhNXJMOWkzVjFGL2ZkVTE3Q2pDNENqTkQyUktRWmhvUVBhYTBiekJGUUZ3ZE5aZHFDYm9hL3lydGlwUHI5K1JsaTBYbzNsUC85cjVJNHE5QVhldDN6QkE4aTlvdldrdTgyTk1relY2eis2dFFqTThYN2lmc0JveHgycFdjPSxFeHBpcnk6MjAxMy0wMS0wMX0=");
    }

    public class FakeServer : IServer {
        public IFeatureCollection Features => new FeatureCollection();
        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void Dispose() {}
    }
    public static class FakeServerWebHostBuilderExtensions {
        public static IWebHostBuilder UseFakeServer(this IWebHostBuilder builder) => builder.ConfigureServices((builderContext, services) => services.AddSingleton<IServer, FakeServer>());
    }

    public class AppHost : AppHostBase
    {
        public AppHost()
            : base("name".GetAppSetting("Sharp App"), typeof(AppHost).Assembly) {}

        public override void Configure(Container container) {}
        
        public override void OnStartupException(Exception ex)
        {
            throw ex;
        }
    }

    public class AwsConfig
    {
        public string AccessKey { get; set; }
        public string SecretKey { get; set; }
        public string Region { get; set; }
    }

    public class S3Config : AwsConfig
    {
        public string Bucket { get; set; }
    }

    public class AzureConfig
    {
        public string ConnectionString { get; set; }
        public string ContainerName { get; set; }
    }

    public class MarkdigTransformer : IMarkdownTransformer
    {
        private Markdig.MarkdownPipeline Pipeline { get; } = 
            Markdig.MarkdownExtensions.UseAdvancedExtensions(new Markdig.MarkdownPipelineBuilder()).Build();
        public string Transform(string markdown) => Markdig.Markdown.ToHtml(markdown, Pipeline);
    }

    public class NUglifyJsMinifier : ICompressor
    {
        public string Compress(string js) => Uglify.Js(js).Code;
    }
    public class NUglifyCssMinifier : ICompressor
    {
        public string Compress(string css) => Uglify.Css(css).Code;
    }
    public class NUglifyHtmlMinifier : ICompressor
    {
        public string Compress(string html) => Uglify.Html(html).Code;
    }

    public static class WebTemplateUtils
    {
        public static AppHostBase AppHost;
        public static IAppSettings AppSettings;
        public static IVirtualFiles VirtualFiles;

        public static string AssertDirectory(this DirectoryInfo dir) => AssertDirectory(dir?.FullName);
        public static string AssertDirectory(this string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;
            
            ExecUtils.RetryOnException(() => {
                if (!Directory.Exists(filePath))
                    Directory.CreateDirectory(filePath);
            }, TimeSpan.FromSeconds(10));
            return filePath;
        }

        public static string ResolveValue(this string value)
        {
            if (value?.StartsWith("$") == true)
            {
                var envValue = Environment.GetEnvironmentVariable(value.Substring(1));
                if (!string.IsNullOrEmpty(envValue)) 
                    return envValue;
                if (value.StartsWith("$HOME"))
                    return value.Replace("$HOME", Environment.ExpandEnvironmentVariables("%USERPROFILE%"));

                var pos = value.IndexOfAny(new[]{'/','\\',':',';'});
                var envName = pos >= 0 
                    ? value.Substring(1, pos - 1) 
                    : value.Substring(1);
                if (Enum.TryParse<Environment.SpecialFolder>(envName, out var specialFolder))
                    return value.Replace("$" + specialFolder, Environment.GetFolderPath(specialFolder));
            }
            return value;
        }

        public static string GetSetting(string name)
        {
            if (Startup.RunScriptArgs.TryGetValue(name, out var val))
                return val.ToString();
            
            return AppSettings?.GetString(name);
        }

        public static string GetAppSetting(this string name) => ResolveValue(GetSetting(name));
        public static Dictionary<string,object> GetObjectDictionaryAppSetting(this string name)
        {
            var configStr = ResolveValue(GetSetting(name));
            if (configStr == null) 
                return null;
            
            var value = JS.eval(configStr);
            if (value is Dictionary<string, object> objDictionary)
                return objDictionary;
            throw new NotSupportedException($"'{name}' is not an Object Dictionary");
        }

        public static bool TryGetAppSetting(this string name, out string value) 
        {
            value = GetSetting(name);
            if (value == null)
                return false;
            value = ResolveValue(value);
            return true;
        }

        public static T GetAppSetting<T>(this string name, T defaultValue)
        {
            var value = GetSetting(name);
            if (value == null) 
                return defaultValue;

            var resolvedValue = ResolveValue(value);
            return resolvedValue.FromJsv<T>();
        }

        public static string GetAppSettingPath(this string name, string appDir)
        {
            var path = name.GetAppSetting();
            if (path == null)
                return path;
            return path.StartsWith("~/")
                ? path.MapAbsolutePath()
                : Path.GetFullPath(Path.Combine(appDir, path));
        }

        public static IVirtualPathProvider GetVirtualFiles(this string provider, string config)
        {
            if (provider == null) 
                return null;
            
            if (config == null)
                throw new Exception("Missing app setting 'files.config'");
            
            switch (provider.ToLower())
            {
                case "fs":
                case "filesystem":
                    if (config.StartsWith("~/"))
                    {
                        var dir = VirtualFiles.GetDirectory(config.Substring(2));
                        if (dir != null)
                            config = dir.RealPath;
                    }
                    else
                    {
                        config = Path.Combine(VirtualFiles.RootDirectory.RealPath, config);
                    }
                    return new FileSystemVirtualFiles(config);
                case "s3":
                case "s3virtualfiles":
                    var s3Config = config.FromJsv<S3Config>();
                    var region = Amazon.RegionEndpoint.GetBySystemName(s3Config.Region.ResolveValue());
                    s3Config.AccessKey = s3Config.AccessKey.ResolveValue();
                    s3Config.SecretKey = s3Config.SecretKey.ResolveValue();
                    var awsClient = new Amazon.S3.AmazonS3Client(s3Config.AccessKey, s3Config.SecretKey, region);
                    return new S3VirtualFiles(awsClient, s3Config.Bucket.ResolveValue());
                case "azure":
                case "azureblob":
                case "azureblobvirtualfiles":
                    var azureConfig = config.FromJsv<AzureConfig>();
                    var storageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse(azureConfig.ConnectionString.ResolveValue());
                    var container = storageAccount.CreateCloudBlobClient().GetContainerReference(azureConfig.ContainerName.ResolveValue());
                    container.CreateIfNotExists();
                    return new AzureBlobVirtualFiles(container);
            }
            throw new NotSupportedException($"Unknown VirtualFiles Provider '{provider}'");
        }

        public static OrmLiteConnectionFactory GetDbFactory(this string dbProvider, string connectionString, bool setGlobalDialectProvider=true)
        {
            if (dbProvider == null || connectionString == null)
                return null;
            
            switch (dbProvider.ToLower())
            {
                case "sqlite":
                    var customConnection = connectionString.StartsWith(":") || connectionString.Contains("Data Source=");
                    if (!customConnection)
                    {
                        if (connectionString.StartsWith("~/"))
                        {
                            var file = VirtualFiles.GetFile(connectionString.Substring(2));
                            if (file != null)
                            {
                                connectionString = file.RealPath;
                            }
                            else
                            {
                                connectionString = AppHost.MapProjectPath(connectionString);
                            }
                        }
                        else
                        {
                            connectionString = Path.Combine(VirtualFiles.RootDirectory.RealPath, connectionString);
                        }
                        if (!File.Exists(connectionString))
                        {
                            var fs = File.Create(connectionString);
                            fs.Close();
                        }
                    }
                    if (Startup.Verbose) $"SQLite connectionString: {connectionString}".Print();
                    return new OrmLiteConnectionFactory(connectionString, SqliteDialect.Provider, setGlobalDialectProvider).Configure();
                case "mssql":
                case "sqlserver":
                    return new OrmLiteConnectionFactory(connectionString, SqlServerDialect.Provider, setGlobalDialectProvider).Configure();
                case "sqlserver2012":
                    return new OrmLiteConnectionFactory(connectionString, SqlServer2012Dialect.Provider, setGlobalDialectProvider).Configure();
                case "sqlserver2014":
                    return new OrmLiteConnectionFactory(connectionString, SqlServer2014Dialect.Provider, setGlobalDialectProvider).Configure();
                case "sqlserver2016":
                    return new OrmLiteConnectionFactory(connectionString, SqlServer2016Dialect.Provider, setGlobalDialectProvider).Configure();
                case "sqlserver2017":
                    return new OrmLiteConnectionFactory(connectionString, SqlServer2017Dialect.Provider, setGlobalDialectProvider).Configure();
                case "mysql":
                    return new OrmLiteConnectionFactory(connectionString, MySqlDialect.Provider, setGlobalDialectProvider).Configure();
                case "pgsql":
                case "postgres":
                case "postgresql":
                    return new OrmLiteConnectionFactory(connectionString, PostgreSqlDialect.Provider, setGlobalDialectProvider).Configure();
            }

            throw new NotSupportedException($"Unknown DB Provider '{dbProvider}'");
        }

        public static OrmLiteConnectionFactory Configure(this OrmLiteConnectionFactory dbFactory)
        {
            dbFactory.RegisterDialectProvider("sqlite", SqliteDialect.Provider);
            dbFactory.RegisterDialectProvider("sqlserver", SqlServerDialect.Provider);
            dbFactory.RegisterDialectProvider("sqlserver2012", SqlServer2012Dialect.Provider);
            dbFactory.RegisterDialectProvider("sqlserver2014", SqlServer2012Dialect.Provider);
            dbFactory.RegisterDialectProvider("sqlserver2016", SqlServer2016Dialect.Provider);
            dbFactory.RegisterDialectProvider("sqlserver2017", SqlServer2016Dialect.Provider);
            dbFactory.RegisterDialectProvider("sqlserver2016", SqlServer2017Dialect.Provider);
            dbFactory.RegisterDialectProvider("mysql", MySqlDialect.Provider);
            dbFactory.RegisterDialectProvider("pgsql", PostgreSqlDialect.Provider);
            dbFactory.RegisterDialectProvider("postgres", PostgreSqlDialect.Provider);
            dbFactory.RegisterDialectProvider("postgresql", PostgreSqlDialect.Provider);
            return dbFactory;
        }

        public static IEnumerable<Type> ScanAllTypes(this ServiceStackHost appHost)
        {
            var externalPlugins = new[] {
                typeof(ServiceStack.Api.OpenApi.OpenApiFeature),
                typeof(ServiceStack.AutoQueryFeature), 
            };

            foreach (var type in externalPlugins)
                yield return type;
            foreach (var type in typeof(ServiceStackHost).Assembly.GetTypes())
                yield return type;
            foreach (var type in appHost.ServiceAssemblies.SelectMany(x => x.GetTypes()))
                yield return type;
        }

        public static IPlugin CreatePlugin(this Type type)
        {
            if (!type.HasInterface(typeof(IPlugin)))
                throw new NotSupportedException($"'{type.Name}' is not a ServiceStack IPlugin");
            
            IPlugin plugin = null;
            var pluginConfig = type.Name.GetAppSetting();

            if (type.Name == nameof(AuthFeature))
            {
                var authProviders = new List<IAuthProvider>();
                var authProviderNames = "AuthFeature.AuthProviders".GetAppSetting();
                var authProviderTypes = authProviderNames != null
                    ? authProviderNames.Split(',').Map(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
                    : new List<string>();

                foreach (var t in AppHost.ScanAllTypes())
                {
                    if (authProviderTypes.Count == 0)
                        break;

                    if (authProviderTypes.Contains(t.Name))
                    {
                        authProviders.Add(t.CreateAuthProvider());
                        authProviderTypes.Remove(t.Name);
                    }
                }

                if (authProviderTypes.Count > 0)
                {
                    var plural = authProviderTypes.Count > 1 ? "s" : "";
                    throw new NotSupportedException($"Unable to locate AuthProvider{plural}: " + string.Join(", ", authProviderTypes));
                }

                if (Startup.Verbose) $"Creating AuthFeature".Print();
                if (authProviders.Count == 0)
                    throw new NotSupportedException($"List of 'AuthFeature.AuthProviders' required for feature 'AuthFeature', e.g: AuthFeature.AuthProviders TwitterAuthProvider, FacebookAuthProvider");

                plugin = new AuthFeature(() => new AuthUserSession(), authProviders.ToArray());
            }
            else
            {
                if (Startup.Verbose) $"Creating plugin '{type.Name}'".Print();
                plugin = type.CreateInstance<IPlugin>();
            }

            if (pluginConfig != null)
            {
                var value = JS.eval(pluginConfig);
                if (value is Dictionary<string, object> objDictionary)
                {
                    if (Startup.Verbose) $"Populating '{type.Name}' with: {pluginConfig}".Print();
                    objDictionary.PopulateInstance(plugin);
                }
                else throw new NotSupportedException($"'{pluginConfig}' is not an Object Dictionary");
            }

            if (plugin is AutoQueryFeature autoQuery)
            {
                var objDictionary = "AutoQueryFeature.GenerateCrudServices".GetObjectDictionaryAppSetting();
                if (objDictionary != null)
                {
                    autoQuery.GenerateCrudServices = new GenerateCrudServices();
                    objDictionary.PopulateInstance(autoQuery.GenerateCrudServices);
                }
            }

            return plugin;
        }

        public static IAuthProvider CreateAuthProvider(this Type type)
        {
            var ctorWithAppSettings = type.GetConstructors()
                .FirstOrDefault(x => x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType == typeof(IAppSettings));

            var ctorDefault = type.GetConstructors().FirstOrDefault(x => x.GetParameters().Length == 0);

            if (ctorWithAppSettings == null && ctorDefault == null)
                throw new NotSupportedException($"No IAppSettings or default Constructor found for Type '{type.Name}'");

            if (Startup.Verbose) $"Creating Auth Provider '{type.Name}'".Print();
            var authProvider = ctorWithAppSettings != null
                ? (IAuthProvider)ctorWithAppSettings.Invoke(new object[]{ WebTemplateUtils.AppSettings })
                : (IAuthProvider)ctorDefault.Invoke(new object[]{ WebTemplateUtils.AppSettings });

            var authProviderConfig = type.Name.GetAppSetting();
            if (authProviderConfig != null)
            {
                var value = JS.eval(authProviderConfig);
                if (value is Dictionary<string, object> objDictionary)
                {
                    objDictionary.PopulateInstance(authProvider);
                }
            }

            return authProvider;
        }

        public static List<string> ExcludeFoldersNamed = new List<string>
        {
            ".git",        //git
            "bin",         //.NET
            "obj",         //.NET
            "publish",     //Gist publish folder
            "GPUCache",    //CEF GPU Caches
            ".idea",       //Intellij IDEAs
            ".vs",         //VS
            ".dart_tool",  //dart
            "xcuserdata",  //Xcode
            ".gistrun",    //gistrun
        };
        
        public static List<string> ExcludeFileExtensions = new List<string> {
            ".log",
            ".publish",
        };

        public static void CopyAllTo(this string src, string dst, string[] excludePaths=null)
        {
            var d = Path.DirectorySeparatorChar;

            foreach (string dirPath in Directory.GetDirectories(src, "*.*", SearchOption.AllDirectories))
            {
                if (!excludePaths.IsEmpty() && excludePaths.Any(x => dirPath.StartsWith(x)))
                    continue;
                if (ExcludeFoldersNamed.Any(x => dirPath.Contains($"{d}{x}{d}", StringComparison.OrdinalIgnoreCase)
                    || dirPath.EndsWithIgnoreCase($"{d}{x}")))
                    continue;

                if (Startup.Verbose) $"MAKEDIR {dirPath.Replace(src, dst)}".Print();
                try { Directory.CreateDirectory(dirPath.Replace(src, dst)); } catch { }
            }

            foreach (string newPath in Directory.GetFiles(src, "*.*", SearchOption.AllDirectories))
            {
                if (!excludePaths.IsEmpty() && excludePaths.Any(x => newPath.StartsWith(x)))
                    continue;

                if (ExcludeFoldersNamed.Any(x => newPath.Contains($"{d}{x}{d}", StringComparison.OrdinalIgnoreCase)))
                    continue;

                try
                {
                    if (Startup.Verbose) $"COPY {newPath.Replace(src, dst)}".Print();

                    if (newPath.EndsWith(".settings"))
                    {
                        var text = File.ReadAllText(newPath);
                        if (text.Contains("debug true"))
                        {
                            text = text.Replace("debug true", "debug false");
                            File.WriteAllText(newPath.Replace(src, dst), text);
                            continue;
                        }
                    }

                    File.Copy(newPath, newPath.Replace(src, dst), overwrite: true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(Startup.Verbose ? ex.ToString() : ex.Message);
                }
            }
        }

        public static void CopyAllToDictionary(this string src, Dictionary<string, object> to, 
            string[] excludePaths = null,
            string[] excludeExtensions = null)
        {
            var d = Path.DirectorySeparatorChar;
            string VirtualPath(string filePath) => filePath.Substring(src.Length + 1);

            foreach (string newPath in Directory.GetFiles(src, "*.*", SearchOption.AllDirectories))
            {
                if (!excludePaths.IsEmpty() && excludePaths.Any(x => newPath.StartsWith(x)))
                    continue;

                if (ExcludeFoldersNamed.Any(x => newPath.Contains($"{d}{x}{d}", StringComparison.OrdinalIgnoreCase)))
                    continue;
                
                if (excludeExtensions?.Length > 0 && excludeExtensions.Any(x => newPath.EndsWith(x)))
                    continue;

                try
                {
                    // if (newPath.EndsWith(".settings"))
                    // {
                    //     var text = File.ReadAllText(newPath);
                    //     if (text.Contains("debug true"))
                    //     {
                    //         text = text.Replace("debug true", "debug false");
                    //         to[VirtualPath(newPath)] = text;
                    //         continue;
                    //     }
                    // }

                    to[VirtualPath(newPath)] = MimeTypes.IsBinary(MimeTypes.GetMimeType(newPath))
                        ? (object)File.ReadAllBytes(newPath)
                        : File.ReadAllText(newPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(Startup.Verbose ? ex.ToString() : ex.Message);
                }
            }
        }

        public static List<string> ConvertUrlSchemeToCommands(this string firstArg)
        {
            var hasQs = firstArg.IndexOf('?') >= 0;
            var qs = hasQs ? firstArg.RightPart('?') : null;
            var cmd = firstArg.IndexOf("://", StringComparison.Ordinal) >= 0 ? "open" : "run";
            var cmds = new List<string> { cmd, firstArg.RightPart(':').LeftPart('?').Trim('/') };
            if (!string.IsNullOrEmpty(qs))
            {
                var kvps = QueryHelpers.ParseQuery(qs);
                foreach (var entry in kvps)
                {
                    var values = entry.Value.ToArray().Join(",");
                    cmds.Add("-" + entry.Key);
                    if (!string.IsNullOrEmpty(values))
                        cmds.Add(values);
                }
            }

            return cmds;
        }

        public static PostDataElement ParsePostDataElement(this ReadOnlySpan<char> dataElement)
        {
            dataElement = dataElement.TrimStart();
            var boundary = dataElement.LeftPart("\r\n");
            var rest = dataElement.RightPart("\r\n");
            var to = new PostDataElement();

            var startIndex = 0;
            while (rest.TryReadLine(out var line, ref startIndex))
            {
                if (line.StartsWithIgnoreCase("Content-Disposition:"))
                {
                    to.Disposition = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var disposition = line.AdvancePastChar(':');
                    while (!disposition.IsEmpty)
                    {
                        var segment = disposition.LeftPart(';');
                        if (to.Type == null)
                        {
                            to.Type = segment.Trim().ToString();
                        }
                        else
                        {
                            var key = segment.LeftPart('=');
                            var value = segment.AdvancePastChar('=');
                            value.ParseJsToken(out var token);
                            value = token is JsLiteral literal ? literal.Value as string : null;
                            to.Disposition[key.Trim().ToString()] = value.ToString();
                        }

                        disposition = disposition.AdvancePastChar(';');
                    }
                }
            }

            var bodyStartPos = dataElement.IndexOf("\r\n\r\n");
            if (bodyStartPos == -1)
                throw new ArgumentException("Invalid Body Start", nameof(dataElement));

            var body = dataElement.Advance(bodyStartPos + 4);
            var bodyEndPos = body.IndexOf(boundary);
            if (bodyEndPos == -1)
                throw new ArgumentException("Invalid Body End", nameof(dataElement));

            to.Body = body.Slice(0, bodyEndPos - 2).ToString(); // /r/n

            return to;
        }
    }

    public class PostDataElement
    {
        public string Type { get; set; }
        public string Body { get; set; }
        public Dictionary<string,string> Disposition { get; set; }
        public string Name => Disposition != null && Disposition.TryGetValue(nameof(Name), out var s) ? s : null;
    }

    [Route("/sharp-apps/registry", "POST")]
    public class RegisterSharpApp : IReturn<RegisterSharpAppResponse>
    {
        public string AppName { get; set; }
        public string Publish { get; set; }
    }

    public class RegisterSharpAppResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
    }
    
    [Route("/langs", "GET")]
    public partial class GetLanguages : IReturn<GetLanguagesResponse> {}
    public partial class GetLanguagesResponse
    {
        public virtual List<KeyValuePair<string,string>> Results { get; set; }
    }

    [Route("/protoc/{Lang}")]
    public partial class Protoc : IReturn<ProtocResponse>
    {
        public virtual string Lang { get; set; }
        public virtual Dictionary<string, string> Files { get; set; }
    }

    public partial class ProtocResponse
    {
        public virtual string Lang { get; set; }
        public virtual Dictionary<string, string> GeneratedFiles { get; set; }
        public virtual string ArchiveUrl { get; set; }
        public virtual ResponseStatus ResponseStatus { get; set; }
    }
}

