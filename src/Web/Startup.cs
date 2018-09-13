using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Funq;
using ServiceStack;
using ServiceStack.IO;
using ServiceStack.Auth;
using ServiceStack.Text;
using ServiceStack.Data;
using ServiceStack.Redis;
using ServiceStack.OrmLite;
using ServiceStack.Templates;
using ServiceStack.Configuration;
using ServiceStack.Azure.Storage;

namespace WebApp
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
        public IWebHost Build() => Builder.Build();

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

    public class Startup
    {
        public static WebAppEvents Events { get; set; }

        public static string GalleryUrl { get; set; } = "https://servicestack.net/apps/gallery";

        public static string GitHubSource { get; set; } = "NetCoreWebApps";
        static string[] SourceArgs = { "/s", "-s", "/source", "--source" };

        public static bool Verbose { get; set; }
        static string[] VerboseArgs = { "/verbose", "--verbose" };

        public static bool? DebugMode { get; set; }
        static string[] DebugArgs = { "/d", "-d", "/debug", "--debug" };
        static string[] ReleaseArgs = { "/r", "-r", "/release", "--release" };

        public static string InitDefaultGist { get; set; } = "5c9ee9031e53cd8f85bd0e14881ddaa8";
        public static string InitNginxGist { get; set; } = "38a125eede8228ddf40651e2529a5c70";
        public static string InitSupervisorGist { get; set; } = "2db295508517a4eed59906320e95d98a";
        public static string InitDockerGist { get; set; } = "54fbb66fa39740ad1c865a59b5ed2e31";

        public static string ToolFavIcon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "favicon.ico");

        public static WebAppContext CreateWebHost(string tool, string[] args, WebAppEvents events = null)
        {
            Events = events;
            var dotnetArgs = new List<string>();

            var createShortcut = false;
            var publish = false;
            var publishExe = false;
            string createShortcutFor = null;
            string runProcess = null;
            var appSettingPaths = new[]
            {
                "app.settings", "../app/app.settings", "app/app.settings",
                "web.settings", "../app/web.settings", "app/web.settings",
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
                    if (Events.RunNetCoreProcess == null)
                        throw new NotSupportedException($"This {tool} tool does not suppport running processes");

                    runProcess = arg;
                    continue;
                }
                if (VerboseArgs.Contains(arg))
                {
                    Verbose = true;
                    continue;
                }
                if (SourceArgs.Contains(arg))
                {
                    GitHubSource = args[++i];
                    continue;
                }
                if (arg == "shortcut")
                {
                    createShortcut = true;
                    if (i + 1 < args.Length && (args[i + 1].EndsWith(".dll") || args[i + 1].EndsWith(".exe")))
                        createShortcutFor = args[++i];
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
                if (DebugArgs.Contains(arg))
                {
                    DebugMode = true;
                    continue;
                }
                if (ReleaseArgs.Contains(arg))
                {
                    DebugMode = false;
                    continue;
                }
                dotnetArgs.Add(arg);
            }

            if (Verbose)
            {
                $"args: '{dotnetArgs.Join(" ")}'".Print();

                if (runProcess != null)
                    $"Run Process: {runProcess}".Print();
                if (createShortcut)
                    $"Create Shortcut {createShortcutFor}".Print();
                if (publish)
                    $"Command: publish".Print();
                if (publishExe)
                    $"Command: publish-exe".Print();
            }

            if (runProcess != null)
            {
                RegisterStat(tool, "run", runProcess);

                Events.RunNetCoreProcess(new WebAppContext { 
                    Arguments = dotnetArgs.ToArray(), 
                    RunProcess = runProcess,
                    FavIcon = ToolFavIcon,
                });
            
                return null;
            }

            if (HandledCommand(tool, dotnetArgs.ToArray()))
                return null;

            string webSettingsPath = null;
            foreach (var path in appSettingPaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    webSettingsPath = fullPath;
                    break;
                }
            }

            var appDir = webSettingsPath != null ? Path.GetDirectoryName(webSettingsPath) : null;
            var ctx = new WebAppContext
            {
                Tool = tool,
                Arguments = dotnetArgs.ToArray(),
                RunProcess = runProcess,
                WebSettingsPath = webSettingsPath,
                AppSettings = WebTemplateUtils.AppSettings,
                AppDir = appDir,
                ToolPath = Assembly.GetExecutingAssembly().Location,
                DebugMode =  DebugMode ?? false,
            };

            if (dotnetArgs.Count > 0)
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

            if (webSettingsPath == null && createShortcutFor == null)
                throw new Exception($"'{appSettingPaths[0]}' does not exist.\n\nView Help: {tool} --help");

            var usingWebSettings = File.Exists(webSettingsPath);
            if (Verbose || (usingWebSettings && !createShortcut && tool == "web"))
                $"Using '{webSettingsPath}'".Print();

            WebTemplateUtils.AppSettings = new MultiAppSettings(usingWebSettings
                    ? new TextFileSettings(webSettingsPath)
                    : new DictionarySettings(),
                new EnvironmentVariableSettings());

            var bind = "bind".GetAppSetting("localhost");
            var port = "port".GetAppSetting(defaultValue: "5000");
            var useUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? $"http://{bind}:{port}/";
            ctx.UseUrls = useUrls;
            ctx.StartUrl = useUrls.Replace("://*", "://localhost");
            ctx.DebugMode = GetDebugMode();
            ctx.FavIcon = GetFavIcon();

            if (createShortcut)
            {
                RegisterStat(tool, "shortcut", createShortcutFor);

                var shortcutPath = createShortcutFor == null
                    ? Path.Combine(appDir, "name".GetAppSetting(defaultValue: "WebApp"))
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
                return null;
            }
            if (publish)
            {
                RegisterStat(tool, "publish");

                var toolDir = Path.GetDirectoryName(ctx.ToolPath);

                var (publishDir, publishAppDir, publishToolDir) = GetPublishDirs(tool == "app" ? "cef" : tool, appDir);
                CreatePublishShortcut(ctx, publishDir, publishDir, publishToolDir, Path.GetFileName(ctx.ToolPath));

                appDir.CopyAllTo(publishAppDir, excludePaths: new []{ publishToolDir });
                toolDir.CopyAllTo(publishToolDir);

                if (Verbose)
                {
                    $"publish: {appDir} -> {publishAppDir}".Print();
                    $"publish: {toolDir} -> {publishToolDir}".Print();
                }

                return null;
            }
            if (publishExe)
            {
                RegisterStat(tool, "publish-exe");
                
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                var zipUrl = new GithubGateway().GetSourceZipUrl("NetCoreWebApps", "WebWin");

                var invalidFileNameChars = Path.GetInvalidFileNameChars();
                var safeFileName = new string(zipUrl.Where(c => !invalidFileNameChars.Contains(c)).ToArray());
                var cachedVersionPath = Path.Combine(homeDir, ".servicestack", "cache", safeFileName);

                var isCached = File.Exists(cachedVersionPath);
                if (Verbose)
                    Console.WriteLine((isCached ? "Using cached release: " : "Using new release: ") + cachedVersionPath);

                if (!isCached)
                {
                    if (Verbose) $"Downloading {zipUrl}".Print();
                    new GithubGateway().DownloadFile(zipUrl, cachedVersionPath);
                }

                var tmpDir = Path.Combine(Path.GetTempPath(), "servicestack", "WebWin");
                try { Directory.Delete(tmpDir, recursive: true); } catch { }
                
                if (Verbose) $"Extract to Directory: {cachedVersionPath} -> {tmpDir}".Print();

                ZipFile.ExtractToDirectory(cachedVersionPath, tmpDir);

                var (publishDir, publishAppDir, publishToolDir) = GetPublishDirs("win", appDir);
                var toolDir = new DirectoryInfo(tmpDir).GetDirectories().First().FullName;

                if (Verbose) $"Directory Move: {toolDir} -> {publishToolDir}".Print();
                DeleteDirectory(publishToolDir);
                Directory.Move(toolDir, publishToolDir);

                CreatePublishShortcut(ctx, publishDir, publishAppDir, publishToolDir, "win.exe");
                appDir.CopyAllTo(publishAppDir, excludePaths: new[] { publishToolDir });

                if (Verbose)
                {
                    $"publish-exe: {appDir} -> {publishAppDir}".Print();
                    $"publish-exe: {toolDir} -> {publishToolDir}".Print();
                }

                return null;
            }

            return CreateWebAppContext(ctx);
        }

        public static void DeleteDirectory(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;
            if (Verbose) $"RMDIR: {dirPath}".Print();
            try { Directory.Delete(dirPath, recursive: true); } catch { }
            try { Directory.Delete(dirPath); } catch { }
        }

        private static bool GetDebugMode() => DebugMode ?? "debug".GetAppSetting(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Production");

        public static void CreateShortcut(string filePath, string targetPath, string arguments, string workingDirectory, string iconPath, WebAppContext ctx)
        {
            if (Events?.CreateShortcut != null)
            {
                Events.CreateShortcut(filePath, targetPath, arguments, workingDirectory, iconPath);
            }
            else
            {
                filePath = Path.Combine(Path.GetDirectoryName(filePath), new TemplateDefaultFilters().generateSlug(Path.GetFileName(filePath)));
                var cmd = filePath + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".bat" : ".sh");

                var openBrowserCmd = string.IsNullOrEmpty(ctx?.StartUrl) || targetPath.EndsWith(".exe") ? "" : 
                    (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? $"start {ctx.StartUrl}"
                        : RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
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
            var shortcutPath = Path.Combine(publishDir, "name".GetAppSetting(defaultValue: "WebApp"));
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

            try { Directory.CreateDirectory(publishAppDir); } catch { }
            try { Directory.CreateDirectory(publishToolDir); } catch { }

            return (publishDir, publishAppDir, publishToolDir);
        }

        private static WebAppContext CreateWebAppContext(WebAppContext ctx)
        {
            var appDir = ctx.AppDir;
            var contentRoot = "contentRoot".GetAppSettingPath(appDir) ?? appDir;

            var wwwrootPath = Path.Combine(appDir, "wwwroot");
            var webRoot = Directory.Exists(wwwrootPath)
                ? wwwrootPath
                : contentRoot;

            var useWebRoot = "webRoot".GetAppSettingPath(appDir) ?? webRoot;

            var builder = WebHost.CreateDefaultBuilder(ctx.Arguments)
                .UseContentRoot(contentRoot)
                .UseWebRoot(useWebRoot)
                .UseStartup<Startup>()
                .UseUrls(ctx.UseUrls);

            ctx.Builder = builder;

            if (Verbose) ctx.GetDebugString().Print();

            return ctx;
        }

        public static string GetVersion() => Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?
            .Version.LastLeftPart('.') ?? "0.0.0";

        public static void PrintUsage(string tool)
        {
            var runProcess = "";
            if (Events?.RunNetCoreProcess != null)
            {
                runProcess =  $"  {tool} <name>.dll              Run external .NET Core App{Environment.NewLine}";
                runProcess += $"  {tool} <name>.exe              Run external self-contained .NET Core App{Environment.NewLine}";
            }

            var additional = new StringBuilder();

            string USAGE = $@"
Version:  {GetVersion()}

Usage:   
  
  {tool}                         Run App in App folder using local app.settings
  {tool} path/to/app.settings    Run App at folder containing specified app.settings
{runProcess}
  {tool} list                    List available Apps
  {tool} gallery                 Open App Gallery in a Browser
  {tool} install <name>          Install App

  {tool} publish                 Package App to /publish ready for deployment (.NET Core Required)
  {tool} publish-exe             Package self-contained .exe App to /publish  (.NET Core Embedded)

  {tool} shortcut                Create Shortcut for App
  {tool} shortcut <name>.dll     Create Shortcut for .NET Core App

  {tool} init                    Create basic .NET Core Web App
  {tool} init nginx              Create nginx example config
  {tool} init supervisor         Create supervisor example config
  {tool} init docker             Create Docker example config
  {tool} gist <gist-id>          Write all Gist text files to current directory
{additional}
  dotnet tool update -g {tool}   Update to latest version

Options:
    -h, --help, ?             Print this message
    -v, --version             Print this version
    -d, --debug               Run in Debug mode for Development
    -r, --release             Run in Release mode for Production
    -s, --source              Change GitHub Source for App Directory
        --verbose             Display verbose logging

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

        internal static void RegisterStat(string tool, string name, string type = "tool")
        {
            if (Environment.GetEnvironmentVariable("SERVICESTACK_TELEMETRY_OPTOUT") == "1") return;
            try {
                $"https://servicestack.net/stats/{type}/record?name={name}&source={tool}&version={GetVersion()}".GetBytesFromUrlAsync();
            } catch { }
        }

        public static bool HandledCommand(string tool, string[] args)
        {
            if (args.Length == 0) 
                return false;

            var cmd = System.Text.RegularExpressions.Regex.Replace(args[0], "^-+", "/");

            var checkUpdatesAndQuit = false;
            var arg = args[0];
            if (args.Length == 1)
            {
                if (arg == "list" || arg == "l")
                {
                    RegisterStat(tool, "list");

                    var repos = new GithubGateway().GetSourceRepos(GitHubSource);
                    var padName = repos.OrderByDescending(x => x.Name.Length).First().Name.Length + 1;

                    "".Print();
                    var i = 1;
                    foreach (var repo in repos)
                    {
                        $" {i++.ToString().PadLeft(3,' ')}. {repo.Name.PadRight(padName,' ')} {repo.Description}".Print();
                    }

                    "".Print();
                    $"Usage: {tool} install <name>".Print();
                    checkUpdatesAndQuit = true;
                }
                else if (arg == "gallery")
                {
                    RegisterStat(tool, "gallery");

                    var openUrl = Events?.OpenBrowser ?? OpenBrowser;
                    openUrl(GalleryUrl);
                    checkUpdatesAndQuit = true;
                }
                else if (arg == "init")
                {
                    RegisterStat(tool, "init");
                    WriteGistFile(InitDefaultGist);
                    return true;
                }
                else if (new[] { "/h", "?", "/?", "/help" }.Contains(cmd))
                {
                    PrintUsage(tool);
                    return true;
                }
                else if (new[] { "/v", "/version" }.Contains(cmd))
                {
                    $"Version: {GetVersion()}".Print();
                    checkUpdatesAndQuit = true;
                }
            }
            else if (args.Length == 2)
            {
                if (arg == "install" || arg == "i")
                {
                    var repo = args[1];
                    RegisterStat(tool, "install", repo);

                    var downloadUrl = new GithubGateway().GetSourceZipUrl(GitHubSource, repo);
                    $"Installing {repo}...".Print();

                    var tmpFile = Path.GetTempFileName();
                    if (Verbose)
                        $"Downloading: {downloadUrl}".Print();
                    new GithubGateway().DownloadFile(downloadUrl, tmpFile);
                    var tmpDir = Path.Combine(Path.GetTempPath(), "servicestack", repo);
                    if (Directory.Exists(tmpDir))
                        Directory.Delete(tmpDir,recursive:true);

                    if (Verbose)
                        $"ExtractToDirectory: {tmpFile} -> {tmpDir}".Print();
                    ZipFile.ExtractToDirectory(tmpFile, tmpDir);
                    var installDir = Path.GetFullPath(repo);

                    if (Verbose) $"Directory Move: {new DirectoryInfo(tmpDir).GetDirectories().First().FullName} -> {installDir}".Print();
                    DeleteDirectory(installDir);
                    Directory.Move(new DirectoryInfo(tmpDir).GetDirectories().First().FullName, installDir);

                    "".Print();
                    $"Installation successful, run with:".Print();
                    "".Print();
                    $"  cd {repo} && {tool}".Print();
                    return true;
                }
                else if (arg == "init")
                {
                    var gist = args[1];
                    RegisterStat(tool, "init", gist);
                    var id = gist == "nginx"
                        ? InitNginxGist
                        : (gist.StartsWith("supervisor"))
                            ? InitSupervisorGist
                            : gist == "docker"
                                ? InitDockerGist
                                : throw new Exception($"Unknown init name '{gist}'");
                    WriteGistFile(id);
                    return true;
                }
                else if (arg == "gist")
                {
                    var gist = args[1];
                    RegisterStat(tool, "gist", gist);
                    WriteGistFile(gist);
                    return true;
                }
            }

            if (checkUpdatesAndQuit)
            {
                RegisterStat(tool, cmd.TrimStart('/'));

                var json = $"https://api.nuget.org/v3/registration3/{tool}/index.json".GetJsonFromUrl();
                var response = JSON.parse(json);
                if (response is Dictionary<string, object> r &&
                    r.TryGetValue("items", out var oItems) && oItems is List<object> items &&
                    items.Count > 0 && items[0] is Dictionary<string, object> item &&
                    item.TryGetValue("upper", out var oUpper) && oUpper is string upper)
                {
                    if (GetVersion() != upper) {
                        "".Print();
                        "".Print();
                        $"new version available, update with:".Print();
                        "".Print();
                        $"  dotnet tool update -g {tool}".Print();
                    }
                }
                return true;
            }            
            return false;
        }

        public static void WriteGistFile(string gistId)
        {
            var gistFiles = new GithubGateway().GetGistFiles(gistId);
            foreach (var entry in gistFiles)
            {
                if (Verbose) $"Writing {entry.Key}...".Print();
                File.WriteAllText(entry.Key, entry.Value);
            }
        }

        IHostingEnvironment env;
        public Startup(IHostingEnvironment env) => this.env = env;

        IPlugin[] plugins;
        IPlugin[] Plugins 
        {
            get
            {
                if (plugins != null)
                    return plugins;

                var features = "features".GetAppSetting();
                if (features != null)
                {
                    var featureTypes = features.Split(',').Map(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                    featureTypes.Remove(nameof(TemplatePagesFeature)); //already added
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
                            if (type.HasInterface(typeof(IPlugin)) && !registerPlugins.Any(x => x?.GetType() == type))
                            {
                                var plugin = type.CreatePlugin();
                                remainingPlugins.Add(plugin);
                            }
                        }
                        $"Registering wildcard plugins: {remainingPlugins.Map(x => x.GetType().Name).Join(", ")}".Print(); 
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

                    return plugins = registerPlugins;
                }

                return null;
            }
        }

        AppHostBase appHost;
        AppHostBase AppHost
        {
            get
            {
                if (appHost != null)
                    return appHost;

                WebTemplateUtils.VirtualFiles = new FileSystemVirtualFiles(env.ContentRootPath);

                var assemblies = new List<Assembly>();
                var vfs = "files".GetAppSetting().GetVirtualFiles(config:"files.config".GetAppSetting());
                var pluginsDir = (vfs ?? WebTemplateUtils.VirtualFiles).GetDirectory("plugins");
                if (pluginsDir != null)
                {
                    var plugins = pluginsDir.GetFiles();
                    foreach (var plugin in plugins)
                    {
                        if (plugin.Extension != "dll" && plugin.Extension != "exe")
                            continue;

                        var dllBytes = plugin.ReadAllBytes();
                        $"Attempting to load plugin '{plugin.VirtualPath}', size: {dllBytes.Length} bytes".Print();
                        var asm = Assembly.Load(dllBytes);
                        assemblies.Add(asm);

                        if (appHost == null)
                        {
                            foreach (var type in asm.GetTypes())
                            {
                                if (typeof(AppHostBase).IsAssignableFrom(type))
                                {
                                    $"Using AppHost from Plugin '{plugin.VirtualPath}'".Print();
                                    appHost = type.CreateInstance<AppHostBase>();
                                    appHost.AppSettings = WebTemplateUtils.AppSettings;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (appHost == null)
                    appHost = new AppHost();

                WebTemplateUtils.AppHost = appHost;

                if (assemblies.Count > 0)
                    assemblies.Each(x => appHost.ServiceAssemblies.AddIfNotExists(x));

                if (vfs != null)
                    appHost.AddVirtualFileSources.Add(vfs);

                if (vfs is IVirtualFiles writableFs)
                    appHost.VirtualFiles = writableFs;
                    
                return appHost;
            }
        }

        public void ConfigureServices(IServiceCollection services) 
        {
            var appHost = AppHost;
            var plugins = Plugins;
            plugins?.Each(x => services.AddSingleton(x.GetType(), x));

            services.AddSingleton<ServiceStackHost>(appHost);

            plugins?.OfType<IStartup>().Each(x => x.ConfigureServices(services));

            (appHost as IStartup)?.ConfigureServices(services);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            plugins?.OfType<IStartup>().Each(x => x.Configure(app));

            appHost.BeforeConfigure.Add(ConfigureAppHost);

            app.UseServiceStack(appHost);
        }

        public void ConfigureAppHost(ServiceStackHost appHost)
        {
            appHost.Config.DebugMode = GetDebugMode();
            appHost.Config.ForbiddenPaths.Add("/plugins");

            var feature = appHost.GetPlugin<TemplatePagesFeature>();
            if (feature != null)
                "Using existing TemplatePagesFeature from appHost".Print();

            if (feature == null)
            {
                feature = (nameof(TemplatePagesFeature).GetAppSetting() != null
                    ? (TemplatePagesFeature)typeof(TemplatePagesFeature).CreatePlugin()
                    : new TemplatePagesFeature { ApiPath = "apiPath".GetAppSetting() ?? "/api" });
            }

            var dbFactory = "db".GetAppSetting().GetDbFactory(connectionString:"db.connection".GetAppSetting());
            if (dbFactory != null)
            {
                appHost.Container.Register<IDbConnectionFactory>(dbFactory);
                feature.TemplateFilters.Add(new TemplateDbFiltersAsync());
            }

            var redisConnString = "redis.connection".GetAppSetting();
            if (redisConnString != null)
            {
                appHost.Container.Register<IRedisClientsManager>(c => new RedisManagerPool(redisConnString));
                feature.TemplateFilters.Add(new TemplateRedisFilters { 
                    RedisManager = appHost.Container.Resolve<IRedisClientsManager>()
                });
            }

           var checkForModifiedPagesAfterSecs = "checkForModifiedPagesAfterSecs".GetAppSetting();
            if (checkForModifiedPagesAfterSecs != null)
                feature.CheckForModifiedPagesAfter = TimeSpan.FromSeconds(checkForModifiedPagesAfterSecs.ConvertTo<int>());

            var defaultFileCacheExpirySecs = "defaultFileCacheExpirySecs".GetAppSetting();
            if (defaultFileCacheExpirySecs != null)
                feature.Args[TemplateConstants.DefaultFileCacheExpiry] = TimeSpan.FromSeconds(defaultFileCacheExpirySecs.ConvertTo<int>());

            var defaultUrlCacheExpirySecs = "defaultUrlCacheExpirySecs".GetAppSetting();
            if (defaultUrlCacheExpirySecs != null)
                feature.Args[TemplateConstants.DefaultUrlCacheExpiry] = TimeSpan.FromSeconds(defaultUrlCacheExpirySecs.ConvertTo<int>());

            var markdownProvider = "markdownProvider".GetAppSetting();
            var useMarkdownDeep = markdownProvider?.EqualsIgnoreCase("MarkdownDeep") == true;
            MarkdownConfig.Transformer = useMarkdownDeep
                ? new MarkdownDeep.MarkdownDeepTransformer()
                : (IMarkdownTransformer) new MarkdigTransformer();
            if (markdownProvider != null)
                ("Using markdown provider " + (useMarkdownDeep ? "MarkdownDeep" : "Markdig")).Print();

            var contextArgKeys = WebTemplateUtils.AppSettings.GetAllKeys().Where(x => x.StartsWith("args."));
            foreach (var key in contextArgKeys)
            {
                var name = key.RightPart('.');
                var value = key.GetAppSetting();

                feature.Args[name] = value.StartsWith("{") || value.StartsWith("[")
                    ? JS.eval(value)
                    : value;
            }

            appHost.Plugins.Add(feature);

            IPlugin[] registerPlugins = Plugins;
            if (registerPlugins != null)
            {
                foreach (var plugin in registerPlugins)
                {
                    appHost.Plugins.RemoveAll(x => x.GetType() == plugin.GetType());
                    appHost.Plugins.Add(plugin);
                }
            }
        }
    }

    public class AppHost : AppHostBase
    {
        public AppHost()
            : base("name".GetAppSetting("ServiceStack Web App"), typeof(AppHost).Assembly) {}

        public override void Configure(Container container) {}
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

    public static class WebTemplateUtils
    {
        public static AppHostBase AppHost;
        public static IAppSettings AppSettings;
        public static IVirtualFiles VirtualFiles;

        public static string ResolveValue(this string value)
        {
            if (value?.StartsWith("$") == true)
            {
                var envValue = Environment.GetEnvironmentVariable(value.Substring(1));
                if (!string.IsNullOrEmpty(envValue)) return envValue;
            }
            return value;
        }

        public static string GetAppSetting(this string name) => ResolveValue(AppSettings.GetString(name));

        public static bool TryGetAppSetting(this string name, out string value) 
        {
            value = AppSettings.GetString(name);
            if (value == null)
                return false;
            value = ResolveValue(value);
            return true;
        }

        public static T GetAppSetting<T>(this string name, T defaultValue)
        {
            var value = AppSettings.GetString(name);
            if (value == null) return defaultValue;

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
            if (provider == null) return null;
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
                    var storageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse(ResolveValue(azureConfig.ConnectionString));
                    var container = storageAccount.CreateCloudBlobClient().GetContainerReference(ResolveValue(azureConfig.ContainerName));
                    container.CreateIfNotExists();
                    return new AzureBlobVirtualFiles(container);
            }
            throw new NotSupportedException($"Unknown VirtualFiles Provider '{provider}'");
        }

        public static OrmLiteConnectionFactory GetDbFactory(this string dbProvider, string connectionString)
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
                    return new OrmLiteConnectionFactory(connectionString, SqliteDialect.Provider);
                case "mssql":
                case "sqlserver":
                    return new OrmLiteConnectionFactory(connectionString, SqlServerDialect.Provider);
                case "sqlserver2012":
                    return new OrmLiteConnectionFactory(connectionString, SqlServer2012Dialect.Provider);
                case "sqlserver2014":
                    return new OrmLiteConnectionFactory(connectionString, SqlServer2014Dialect.Provider);
                case "sqlserver2016":
                    return new OrmLiteConnectionFactory(connectionString, SqlServer2016Dialect.Provider);
                case "sqlserver2017":
                    return new OrmLiteConnectionFactory(connectionString, SqlServer2017Dialect.Provider);
                case "mysql":
                    return new OrmLiteConnectionFactory(connectionString, MySqlDialect.Provider);
                case "pgsql":
                case "postgres":
                case "postgresql":
                    return new OrmLiteConnectionFactory(connectionString, PostgreSqlDialect.Provider);
            }

            throw new NotSupportedException($"Unknown DB Provider '{dbProvider}'");
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

                $"Creating AuthFeature".Print();
                if (authProviders.Count == 0)
                    throw new NotSupportedException($"List of 'AuthFeature.AuthProviders' required for feature 'AuthFeature', e.g: AuthFeature.AuthProviders TwitterAuthProvider, FacebookAuthProvider");

                plugin = new AuthFeature(() => new AuthUserSession(), authProviders.ToArray());
            }
            else
            {
                $"Creating plugin '{type.Name}'".Print();
                plugin = type.CreateInstance<IPlugin>();
            }

            if (pluginConfig != null)
            {
                var value = JS.eval(pluginConfig);
                if (value is Dictionary<string, object> objDictionary)
                {
                    $"Populating '{type.Name}' with: {pluginConfig}".Print();
                    objDictionary.PopulateInstance(plugin);
                }
                else throw new NotSupportedException($"'{pluginConfig}' is not an Object Dictionary");
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

            $"Creating Auth Provider '{type.Name}'".Print();
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
            ".git"
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
    }

    public class GithubRepo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Homepage { get; set; }
        public int Watchers_Count { get; set; }
        public int Stargazes_Count { get; set; }
        public int Size { get; set; }
        public string Full_Name { get; set; }
        public DateTime Created_at { get; set; }
        public DateTime? Updated_At { get; set; }

        public bool Has_Downloads { get; set; }
        public bool Fork { get; set; }

        public string Url { get; set; }          // https://api.github.com/repos/NetCoreWebApps/bare
        public string Html_Url { get; set; }
        public bool Private { get; set; }

        public GithubRepo Parent { get; set; }   // only on single result, e.g: /repos/NetCoreWebApps/bare
    }    

    public partial class GithubGateway
    {
        public const string GithubApiBaseUrl = "https://api.github.com/";
        public static string UserAgent = typeof(GithubGateway).Namespace.LeftPart('.');

        public string UnwrapRepoFullName(string orgName, string name)
        {
            try
            {
                var repo = GetJson<GithubRepo>($"/repos/{orgName}/{name}");
                if (repo.Fork)
                {
                    if (Startup.Verbose)
                    {
                        $"'{orgName}/{repo.Name}' is a fork.".Print();
                        if (repo.Parent == null)
                            $"Could not find parent fork for '{orgName}/{repo.Name}', using '{repo.Full_Name}'".Print();
                        else
                            return repo.Parent.Full_Name;
                    }
                }
                return repo.Full_Name;
            } 
            catch (WebException ex)
            {
                if (ex.IsNotFound())
                    throw new Exception($"App '{name}' was not found.");
                throw;
            }
        }

        public string GetSourceZipUrl(string orgName, string name)
        {
            var repoFullName = UnwrapRepoFullName(orgName, name);
            var json = GetJson($"repos/{repoFullName}/releases");
            var response = JSON.parse(json);

            if (response is List<object> releases && releases.Count > 0 &&
                releases[0] is Dictionary<string,object> release &&
                release.TryGetValue("zipball_url", out var zipUrl))
            {
                return (string)zipUrl;
            }

            if (Startup.Verbose) $"No releases found for '{repoFullName}', installing from master...".Print();
            return $"https://github.com/{repoFullName}/archive/master.zip";
        }

        public List<GithubRepo> GetSourceRepos(string orgName)
        {
            var repos = GetOrgRepos(orgName)
                .Where(x => !x.Name.StartsWith("Web") && x.Name != "LiveDemos")
                .OrderByDescending(x => x.Stargazes_Count)
                .ToList();
            return repos;
        }

        public List<GithubRepo> GetOrgRepos(string githubOrgName)
        {
            return StreamJsonCollection<List<GithubRepo>>($"orgs/{githubOrgName}/repos").SelectMany(x => x).ToList();
        }

        public string GetJson(string route)
        {
            var apiUrl = GithubApiBaseUrl.CombineWith(route);
            if (Startup.Verbose)
                $"API: {apiUrl}".Print();

            return apiUrl.GetJsonFromUrl(req => req.UserAgent = UserAgent);
        }

        public T GetJson<T>(string route) => GetJson(route).FromJson<T>();

        public IEnumerable<T> StreamJsonCollection<T>(string route)
        {
            List<T> results;
            var nextUrl = GithubApiBaseUrl.CombineWith(route);

            do
            {
                results = nextUrl.GetJsonFromUrl(req => req.UserAgent = UserAgent,
                        responseFilter: res => {
                            var links = ParseLinkUrls(res.Headers["Link"]);
                            links.TryGetValue("next", out nextUrl);
                        })
                    .FromJson<List<T>>();

                foreach (var result in results)
                {
                    yield return result;
                }

            } while (results.Count > 0 && nextUrl != null);
        }

        public static Dictionary<string, string> ParseLinkUrls(string linkHeader)
        {
            var map = new Dictionary<string, string>();
            var links = linkHeader;

            while (!string.IsNullOrEmpty(links))
            {
                var urlStartPos = links.IndexOf('<');
                var urlEndPos = links.IndexOf('>');

                if (urlStartPos == -1 || urlEndPos == -1)
                    break;

                var url = links.Substring(urlStartPos + 1, urlEndPos - urlStartPos - 1);
                var parts = links.Substring(urlEndPos).SplitOnFirst(',');

                var relParts = parts[0].Split(';');
                foreach (var relPart in relParts)
                {
                    var keyValueParts = relPart.SplitOnFirst('=');
                    if (keyValueParts.Length < 2)
                        continue;

                    var name = keyValueParts[0].Trim();
                    var value = keyValueParts[1].Trim().Trim('"');

                    if (name == "rel")
                    {
                        map[value] = url;
                    }
                }

                links = parts.Length > 1 ? parts[1] : null;
            }

            return map;
        }

        public void DownloadFile(string downloadUrl, string fileName)
        {
            var webclient = new WebClient();
            webclient.Headers.Add(HttpHeaders.UserAgent, UserAgent);
            webclient.DownloadFile(downloadUrl, fileName);
        }

        public Dictionary<string, string> GetGistFiles(string gistId)
        {
            var json = GetJson($"/gists/{gistId}");
            var response = JSON.parse(json);
            if (response is Dictionary<string, object> obj &&
                obj.TryGetValue("files", out var oFiles) && 
                oFiles is Dictionary<string, object> files)
            {
                var to = new Dictionary<string,string>();

                foreach (var entry in files)
                {
                    var meta = (Dictionary<string,object>)entry.Value;
                    to[entry.Key] = (string) meta["content"];
                }

                return to;
            }

            throw new NotSupportedException($"Invalid gist response returned for '{gistId}'");
        }
    }

}
