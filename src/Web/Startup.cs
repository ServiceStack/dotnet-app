using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
using Microsoft.EntityFrameworkCore.Internal;
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
using ServiceStack.FluentValidation.Internal;
using ServiceStack.Html;
using ServiceStack.OrmLite.PostgreSQL;
using ServiceStack.Script;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

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
        
        public Func<DialogOptions,DialogResult> SelectFolder { get; set; }
    }
    
    public class DialogOptions
    {
        public int? Flags { get; set; }
        public string Title { get; set; }
        public string Filter { get; set; }
        public string InitialDir { get; set; }
        public string DefaultExt { get; set; }
        public bool IsFolderPicker { get; set; }
    }

    public class DialogResult
    {
        public string FolderPath { get; set; }
        public string FileTitle { get; set; }
        
        public bool Ok { get; set; }
    }

    public class Startup
    {
        public static WebAppEvents Events { get; set; }

        public static string GalleryUrl { get; set; } = "https://servicestack.net/apps/gallery";

        public static string GitHubSource { get; set; } = "sharp-apps Sharp Apps";
        public static string GitHubSourceTemplates { get; set; } = "NetCoreTemplates .NET Core C# Templates;NetFrameworkTemplates .NET Framework C# Templates;NetFrameworkCoreTemplates ASP.NET Core Framework Templates";
        static string[] SourceArgs = { "/s", "-s", "/source", "--source" };

        public static bool Verbose { get; set; }
        public static bool Silent { get; set; }
        static string[] VerboseArgs = { "/verbose", "--verbose" };

        public static bool? DebugMode { get; set; }
        static string[] DebugArgs = { "/d", "-d", "/debug", "--debug" };
        static string[] ReleaseArgs = { "/r", "-r", "/release", "--release" };

        public static string RunScript { get; set; }
        public static bool WatchScript { get; set; }
        
        public static bool ForceApproval { get; set; }
        static string[] ForceArgs = { "/f", "-f", "/force", "--force" };

        public static string GistLinksId { get; set; } = "f3fa8c016bbd253badc61d80afe399d9";

        public static string ToolFavIcon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "favicon.ico");

        public static async Task<WebAppContext> CreateWebHost(string tool, string[] args, WebAppEvents events = null)
        {
            Events = events;
            var dotnetArgs = new List<string>();

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_SOURCE")))
                GitHubSource = Environment.GetEnvironmentVariable("APP_SOURCE");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_SOURCE_TEMPLATES")))
                GitHubSourceTemplates = Environment.GetEnvironmentVariable("APP_SOURCE_TEMPLATES");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_GALLERY")))
                GalleryUrl = Environment.GetEnvironmentVariable("APP_GALLERY");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_SOURCE_GISTS")))
                GistLinksId = Environment.GetEnvironmentVariable("APP_SOURCE_GISTS");

            var createShortcut = false;
            var publish = false;
            var publishExe = false;
            string createShortcutFor = null;
            string runProcess = null;
            var runScriptArgs = new Dictionary<string, object>();
            var runSharpApp = false;
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
                        throw new NotSupportedException($"This {tool} tool does not support running processes");

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
                    GitHubSource = GitHubSourceTemplates = args[++i];
                    continue;
                }
                if (ForceArgs.Contains(arg))
                {
                    ForceApproval = true;
                    continue;
                }
                if (arg == "shortcut")
                {
                    createShortcut = true;
                    if (i + 1 < args.Length && (args[i + 1].EndsWith(".dll") || args[i + 1].EndsWith(".exe")))
                        createShortcutFor = args[++i];
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

                    if (!(script.EndsWith(".html") || script.EndsWith(".ss")))
                        throw new ArgumentException("Only .ss or .html scripts can be run");
                        
                    RunScript = script;
                    WatchScript = arg == "watch";
                    i += 2; //'run' 'script.ss'
                    for (; i < args.Length; i += 2)
                    {
                        var key = args[i];
                        if (!key.FirstCharEquals('-') && key.FirstCharEquals('/'))
                        {
                            $"Unknown run script argument '{key}', argument example: -name value".Print();
                            return null;
                        }

                        runScriptArgs[key.Substring(1)] = (i + 1) < args.Length ? args[i + 1] : null;
                    }

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
//                if (arg == "openfolder")
//                {
//                    if (Events.SelectFolder != null)
//                    {
//                        var result = Events.SelectFolder(new DialogOptions {
//                            Title = "Select a Folder",
//                            InitialDir = "c:\\src",
//                            IsFolderPicker = true,
//                            Filter = "Folder only\0$$$.$$$\0\0",
//                            //DefaultExt = "txt",
//                            //Filter = "Log files\0*.log\0Batch files\0*.bat\0"
//                        });
//                        if (result.Ok)
//                        {
//                            result.FolderPath.Print();
//                            result.FileTitle.Print();
//                        }
//                        else
//                        {
//                            $"No folder selected.".Print();
//                        }
//                    }
//                    else
//                    {
//                        $"Events.OpenFolder == null".Print();
//                    }
//                    return null;
//                }
                dotnetArgs.Add(arg);
            }

            if (Verbose)
            {
                $"args: '{dotnetArgs.Join(" ")}'".Print();
                $"APP_SOURCE={GitHubSource}".Print();

                if (GalleryUrl != "https://servicestack.net/apps/gallery")
                    $"APP_GALLERY={GalleryUrl}".Print();
                if (runProcess != null)
                    $"Run Process: {runProcess}".Print();
                if (createShortcut)
                    $"Create Shortcut {createShortcutFor}".Print();
                if (publish)
                    $"Command: publish".Print();
                if (publishExe)
                    $"Command: publish-exe".Print();
                if (RunScript != null)
                    $"Command: run {RunScript} {runScriptArgs.ToJsv()}".Print();
            }

            if (runProcess != null)
            {
                RegisterStat(tool, runProcess, "run");

                var publishDir = Path.GetDirectoryName(Path.GetFullPath(runProcess));
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

            if (!runSharpApp && RunScript == null && dotnetArgs.Count == 0 && appSettingsPath == null)
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
                AppDir = appDir,
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

            if (appSettingsPath == null && createShortcutFor == null && RunScript == null)
                throw new Exception($"'{appSettingPaths[0]}' does not exist.\n\nView Help: {tool} --help");

            var usingWebSettings = File.Exists(appSettingsPath);
            if (Verbose || (usingWebSettings && !createShortcut && tool == "web" && instruction == null))
                $"Using '{appSettingsPath}'".Print();

            var dictionarySettings = new DictionarySettings();
            if (RunScript != null)
            {
                var context = new ScriptContext().Init();
                var page = context.OneTimePage(File.ReadAllText(RunScript), "html");
                if (page.Args.Count > 0)
                    dictionarySettings = new DictionarySettings(page.Args.ToStringDictionary());
            }
            
            WebTemplateUtils.AppSettings = new MultiAppSettings(usingWebSettings
                    ? new TextFileSettings(appSettingsPath)
                    : dictionarySettings,
                new EnvironmentVariableSettings());

            var bind = "bind".GetAppSetting("localhost");
            var port = "port".GetAppSetting(defaultValue: "5000");
            var useUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? $"http://{bind}:{port}/";
            ctx.UseUrls = useUrls;
            ctx.StartUrl = useUrls.Replace("://*", "://localhost");
            ctx.DebugMode = GetDebugMode();
            ctx.FavIcon = GetIconPath(appDir);

            if (createShortcut || instruction?.Command == "shortcut")
            {
                if (instruction?.Command != "shortcut")
                    RegisterStat(tool, createShortcutFor, "shortcut");

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

                if (instruction != null && tool == "app")
                    $"{Environment.NewLine}Shortcut: {new DirectoryInfo(Path.GetDirectoryName(shortcutPath)).Name}{Path.DirectorySeparatorChar}{Path.GetFileName(shortcutPath)}".Print();

                return null;
            }
            if (publish)
            {
                RegisterStat(tool, "publish");

                var toolDir = Path.GetDirectoryName(ctx.ToolPath);

                var (publishDir, publishAppDir, publishToolDir) = GetPublishDirs(tool == "app" ? "cef" : tool, appDir);
                CreatePublishShortcut(ctx, publishDir, publishAppDir, publishToolDir, Path.GetFileName(ctx.ToolPath));

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
                
                var zipUrl = new GithubGateway().GetSourceZipUrl("ServiceStack", "WebWin");

                var cachedVersionPath = DownloadCachedZipUrl(zipUrl);

                var tmpDir = Path.Combine(Path.GetTempPath(), "servicestack", "WebWin");
                DeleteDirectory(tmpDir);
                
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
            if (RunScript != null)
            {
                void ExecScript(SharpPagesFeature feature)
                {
                    try 
                    { 
                        var html = File.ReadAllText(RunScript);
                        var page = feature.Pages.OneTimePage(html, ".html");
                        var pageResult = new PageResult(page);
                        runScriptArgs.Each(entry => pageResult.Args[entry.Key] = entry.Value);
                        var output = pageResult.RenderToStringAsync().Result;
                        output.Print();
    
                        if (pageResult.LastFilterError != null)
                        {
                            $"FAILED run {RunScript} {runScriptArgs.ToJsv()}:".Print();
                            pageResult.LastFilterStackTrace.Map(x => "   at " + x)
                                .Join(Environment.NewLine).Print();
    
                            "".Print();
                            pageResult.LastFilterError.Message.Print();
                            pageResult.LastFilterError.ToString().Print();
                        }
                    }
                    catch (Exception)
                    {
                        Verbose = true;
                        $"FAILED run {RunScript} {runScriptArgs.ToJsv()}:".Print();
                        throw;
                    }
                }
                
                RegisterStat(tool, RunScript, WatchScript ? "watch" : "run");
                var (contentRoot, useWebRoot) = GetDirectoryRoots(ctx);
                var builder = new WebHostBuilder()
                    .UseFakeServer()
                    .UseSetting(WebHostDefaults.SuppressStatusMessagesKey, "True")
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(useWebRoot)
                    .UseStartup<Startup>();

                using (var webHost = builder.Build())
                {
                    var task = webHost.RunAsync();
                    
                    var appHost = WebTemplateUtils.AppHost;
                    if (StartupException != null) throw StartupException;
                    
                    var feature = appHost.AssertPlugin<SharpPagesFeature>();

                    var script = new FileInfo(RunScript);
                    var lastWriteAt = DateTime.MinValue;
                    
                    if (WatchScript)
                    {
                        bool breakLoop = false;
                        
                        Console.CancelKeyPress += delegate { breakLoop = true; };
                        
                        $"Watching '{RunScript}' (Ctrl+C to stop):".Print();

                        while (true)
                        {
                            do
                            {
                                if (breakLoop)
                                    break;
                                await Task.Delay(100);
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
        
        private static string DownloadCachedZipUrl(string zipUrl)
        {
            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            var safeFileName = new string(zipUrl.Where(c => !invalidFileNameChars.Contains(c)).ToArray());
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var cachedVersionPath = Path.Combine(homeDir, ".servicestack", "cache", safeFileName);

            var isCached = File.Exists(cachedVersionPath);
            if (Verbose) Console.WriteLine((isCached ? "Using cached release: " : "Using new release: ") + cachedVersionPath);

            if (!isCached)
            {
                if (Verbose) $"Downloading {zipUrl}".Print();
                new GithubGateway().DownloadFile(zipUrl, cachedVersionPath.AssertDirectory());
            }

            return cachedVersionPath;
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
                filePath = Path.Combine(Path.GetDirectoryName(filePath), new DefaultScripts().generateSlug(Path.GetFileName(filePath)));
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
  
  {tool} new                     List available Project Templates
  {tool} new <template> <name>   Create New Project From Template

  {tool} <lang>                  Update all ServiceStack References in directory (recursive)
  {tool} <file>                  Update existing ServiceStack Reference (e.g. dtos.cs)
  {tool} <lang>     <url> <file> Add ServiceStack Reference and save to file name
  {tool} csharp     <url>        Add C# ServiceStack Reference         (Alias 'cs')
  {tool} typescript <url>        Add TypeScript ServiceStack Reference (Alias 'ts')
  {tool} swift      <url>        Add Swift ServiceStack Reference      (Alias 'sw')
  {tool} java       <url>        Add Java ServiceStack Reference       (Alias 'ja')
  {tool} kotlin     <url>        Add Kotlin ServiceStack Reference     (Alias 'kt')
  {tool} dart       <url>        Add Dart ServiceStack Reference       (Alias 'da')
  {tool} fsharp     <url>        Add F# ServiceStack Reference         (Alias 'fs')
  {tool} vbnet      <url>        Add VB.NET ServiceStack Reference     (Alias 'vb')
  {tool} tsd        <url>        Add TypeScript Definition ServiceStack Reference

  {tool} +                       Show available gists
  {tool} +<name>                 Write gist files locally, e.g:
  {tool} +init                   Create empty .NET Core 2.2 ServiceStack App
  {tool} + #<tag>                Search available gists
  {tool} gist <gist-id>          Write all Gist text files to current directory

  {tool} run <name>.ss           Run #Script within context of AppHost   (or <name>.html)
  {tool} watch <name>.ss         Watch #Script within context of AppHost (or <name>.html)

  {tool} run                     Run Sharp App in App folder using local app.settings
  {tool} run path/app.settings   Run Sharp App at folder containing specified app.settings
{runProcess}
  {tool} list                    List available Sharp Apps            (Alias 'l')
  {tool} gallery                 Open Sharp App Gallery in a Browser  (Alias 'g')
  {tool} install <name>          Install Sharp App                    (Alias 'i')

  {tool} publish                 Package Sharp App to /publish ready for deployment (.NET Core Required)
  {tool} publish-exe             Package self-contained .exe Sharp App to /publish  (.NET Core Embedded)

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
        --clean               Delete downloaded caches
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
            if (Environment.GetEnvironmentVariable("SERVICESTACK_TELEMETRY_OPTOUT") == "1" || 
                Environment.GetEnvironmentVariable("SERVICESTACK_TELEMETRY_OPTOUT") == "true")
                return;
            try {
                $"https://servicestack.net/stats/{type}/record?name={name}&source={tool}&version={GetVersion()}".GetBytesFromUrlAsync();
            } catch { }
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
            Task<string> beginCheckUpdates() => $"https://api.nuget.org/v3/registration3/{tool}/index.json".GetJsonFromUrlAsync();
                        
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
                    var isUrl = target.IndexOf("://", StringComparison.Ordinal) >= 0;
                    if (isUrl)
                    {
                        string fileName;
                        if (args.Length == 3)
                        {
                            fileName = args[2];
                        }
                        else if (!File.Exists(dtosExt)) // if it's the first, use shortest convention
                        {
                            fileName = dtosExt;
                        }
                        else
                        {
                            var parts = new Uri(target).Host.Split('.');
                            fileName = parts.Length >= 2
                                ? parts[parts.Length - 2]
                                : parts[0];
                        }

                        if (!fileName.EndsWith(dtosExt))
                        {
                            fileName = $"{fileName}.{dtosExt}";
                        }

                        var typesUrl = target.IndexOf($"/types/{lang}", StringComparison.Ordinal) == -1
                            ? target.CombineWith($"/types/{lang}")
                            : target;

                        SaveReference(tool, lang, typesUrl, Path.GetFullPath(fileName));
                    } 
                    else 
                    {
                        UpdateReference(tool, lang, Path.GetFullPath(target));
                    }
                }
                return new Instruction { Handled = true };
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
                    checkUpdatesAndQuit = beginCheckUpdates();

                    await PrintSources(GitHubSource.Split(';'));

                    $"Usage: {tool} install <name>".Print();
                }
                else if (arg == "new")
                {
                    RegisterStat(tool, "new");
                    checkUpdatesAndQuit = beginCheckUpdates();
                    
                    await PrintSources(GitHubSourceTemplates.Split(';'));
                    
                    $"Usage: {tool} new <template> <name>".Print();
                }
                else if (arg == "gallery" || arg == "g")
                {
                    RegisterStat(tool, "gallery");
                    checkUpdatesAndQuit = beginCheckUpdates();

                    var openUrl = Events?.OpenBrowser ?? OpenBrowser;
                    openUrl(GalleryUrl);
                }
                else if (arg[0] == '+')
                {
                    if (arg == "+")
                    {
                        RegisterStat(tool, arg);
                        PrintGistLinks(tool, GetGistApplyLinks());
                        checkUpdatesAndQuit = beginCheckUpdates();
                    }
                    else
                    {
                        RegisterStat(tool, arg, "+");

                        var gistAliases = arg.Substring(1).Split('+');
                        foreach (var gistAlias in gistAliases)
                        {
                            var links = GetGistApplyLinks();
                            var gistLink = GistLink.Get(links, gistAlias);
                            if (gistLink == null)
                            {
                                $"No match found for '{gistAlias}', available gists:".Print();
                                PrintGistLinks(tool, links);
                                checkUpdatesAndQuit = beginCheckUpdates();
                                break;
                            }
                            
                            var currentDirName = new DirectoryInfo(Environment.CurrentDirectory).Name;
                            WriteGistFile(gistLink.Url, gistAlias, to: gistLink.To, projectName: currentDirName, getUserApproval: UserInputYesNo);
                        }
                        
                        if (checkUpdatesAndQuit == null)
                            return new Instruction { Command = "+", Handled = true };
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
                    checkUpdatesAndQuit = beginCheckUpdates();
                    $"Version: {GetVersion()}".Print();
                }
                else if (new[] { "/clean", "/clear" }.Contains(cmd))
                {
                    RegisterStat(tool, "clean");
                    checkUpdatesAndQuit = beginCheckUpdates();
                    var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var cachesDir = Path.Combine(homeDir, ".servicestack", "cache");
                    DeleteDirectory(cachesDir);
                    $"All caches deleted in '{cachesDir}'".Print();
                }
            }
            else if (args.Length == 2)
            {
                if (arg == "install" || arg == "i")
                {
                    var repo = args[1];
                    RegisterStat(tool, repo, "install");

                    var downloadUrl = new GithubGateway().GetSourceZipUrl(GitHubSource, repo);
                    $"Installing {repo}...".Print();

                    var cachedVersionPath = DownloadCachedZipUrl(downloadUrl);
                    var tmpDir = Path.Combine(Path.GetTempPath(), "servicestack", repo);
                    DeleteDirectory(tmpDir);

                    if (Verbose) $"ExtractToDirectory: {cachedVersionPath} -> {tmpDir}".Print();
                    ZipFile.ExtractToDirectory(cachedVersionPath, tmpDir);
                    var installDir = Path.GetFullPath(repo);

                    if (Verbose) $"Directory Move: {new DirectoryInfo(tmpDir).GetDirectories().First().FullName} -> {installDir}".Print();
                    DeleteDirectory(installDir);
                    Directory.Move(new DirectoryInfo(tmpDir).GetDirectories().First().FullName, installDir);

                    "".Print();
                    $"Installation successful, run with:".Print();
                    "".Print();
                    $"  cd {repo} && {tool}".Print();

                    var appSettingsPath = Path.Combine(installDir, "app.settings");
                    if (!File.Exists(appSettingsPath))
                        return new Instruction { Command = "shortcut", Handled = true };

                    return new Instruction
                    {
                        Command = "shortcut",
                        AppDir = installDir,
                        AppSettingsPath = appSettingsPath,
                    };
                }
                if (arg[0] == '+')
                {
                    if (args[1][0] == '#')
                    {
                        RegisterStat(tool, arg + args[1], "+");
                        PrintGistLinks(tool, GetGistApplyLinks(), args[1].Substring(1));
                        return new Instruction { Command = "+", Handled = true };
                    }
                    
                    RegisterStat(tool, arg + "-project", "+");

                    var links = GetGistApplyLinks();
                    var gistAliases = arg.Substring(1).Split('+');
                    foreach (var gistAlias in gistAliases)
                    {
                        var gistLink = GistLink.Get(links, gistAlias);
                        if (gistLink == null)
                        {
                            $"No match found for '{gistAlias}', available gists:".Print();
                            PrintGistLinks(tool, links);
                            checkUpdatesAndQuit = beginCheckUpdates();
                            break;
                        }
                    }
                    if (checkUpdatesAndQuit == null)
                    {
                        foreach (var gistAlias in gistAliases)
                        {
                            var gistLink = GistLink.Get(links, gistAlias);
                            WriteGistFile(gistLink.Url, gistAlias, to:gistLink.To, projectName:args[1], getUserApproval:UserInputYesNo);
                            ForceApproval = true; //If written once user didn't cancel, assume approval for remaining gists
                        }
                        
                        return new Instruction { Command = "+", Handled = true };
                    }
                }
                if (arg == "gist")
                {
                    var gist = args[1];
                    RegisterStat(tool, gist, "gist");
                    WriteGistFile(gist, gistAlias:null, to:".", projectName:null, getUserApproval:UserInputYesNo);
                    return new Instruction { Command = "gist", Handled = true };
                }
                if (arg == "new")
                {
                    await PrintSources(GitHubSourceTemplates.Split(';'));
                    AssertValidProjectName(null, tool);
                }
            }
            else if (args.Length == 3)
            {
                if (arg == "new")
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
                    
                    var projectName = args[2];
                    AssertValidProjectName(projectName, tool);
    
                    RegisterStat(tool, repo, "new");
    
                    var downloadUrl = new GithubGateway().GetSourceZipUrl(GitHubSourceTemplates, repo);
                    $"Installing {repo}...".Print();
    
                    var cachedVersionPath = DownloadCachedZipUrl(downloadUrl);
                    var tmpDir = Path.Combine(Path.GetTempPath(), "servicestack", repo);
                    DeleteDirectory(tmpDir);
    
                    if (Verbose) $"ExtractToDirectory: {cachedVersionPath} -> {tmpDir}".Print();
                    ZipFile.ExtractToDirectory(cachedVersionPath, tmpDir);
                    var installDir = Path.GetFullPath(repo);
    
                    var projectDir = new DirectoryInfo(Path.Combine(new DirectoryInfo(installDir).Parent?.FullName, projectName));
                    if (Verbose) $"Directory Move: {new DirectoryInfo(tmpDir).GetDirectories().First().FullName} -> {projectDir.FullName}".Print();
                    DeleteDirectory(projectDir.FullName);
                    Directory.Move(new DirectoryInfo(tmpDir).GetDirectories().First().FullName, projectDir.FullName);
    
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
                        var links = GetGistApplyLinks();
                        foreach (var gistAlias in gistAliases)
                        {
                            var gistLink = GistLink.Get(links, gistAlias);
                            WriteGistFile(gistLink.Url, gistAlias, to:gistLink.To, projectName:projectName, getUserApproval:null);
                        }
                    }
                    
                    $"{projectName} {repo} project created.".Print();
                    "".Print();
                    return new Instruction { Handled = true };
                    
                }
            }

            if (checkUpdatesAndQuit != null)
            {
                RegisterStat(tool, cmd.TrimStart('/'));

                var json = await checkUpdatesAndQuit;
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
                return new Instruction { Handled = true };
            }            
            return null;
        }

        private static ConcurrentDictionary<string,List<GistLink>> GistLinksCache = 
            new ConcurrentDictionary<string, List<GistLink>>();

        private static List<GistLink> GetGistApplyLinks() => GetGistLinks(GistLinksId,"apply.md");

        private static List<GistLink> GetGistLinks(string gistId, string name)
        {
            var gistsIndex = new GithubGateway().GetGistFiles(gistId)
                .FirstOrDefault(x => x.Key == name);

            if (gistsIndex.Key == null)
                throw new NotSupportedException($"Could not find '{name}' file in gist '{GistLinksId}'");

            return GistLinksCache.GetOrAdd(gistId+":"+name, key => {
                var links = GistLink.Parse(gistsIndex.Value);
                return links;                
            });
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
        };

        class GistLink
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public string User { get; set; }
            public string To { get; set; }
            public string Description { get; set; }
            
            public string[] Tags { get; set; }
            
            public string ToTagsString() => Tags == null ? "" : $"[" + string.Join(",",Tags) + "]";

            public static List<GistLink> Parse(string md)
            {
                var to = new List<GistLink>();

                if (!string.IsNullOrEmpty(md))
                {
                    foreach (var strLine in md.ReadLines())
                    {
                        var line = strLine.AsSpan();
                        if (!line.TrimStart().StartsWith("- ["))
                            continue;
                        
                        line.SplitOnFirst('[', out _, out var startName);
                        startName.SplitOnFirst(']', out var name, out var endName);
                        endName.SplitOnFirst('(', out _, out var startUrl);
                        startUrl.SplitOnFirst(')', out var url, out var endUrl);

                        var afterModifiers = endUrl.ParseJsToken(out var token);

                        var toPath = (token is JsObjectExpression obj ?
                            obj.Properties.FirstOrDefault(x => x.Key is JsIdentifier key && key.Name == "to")?.Value as JsLiteral : null)?.Value?.ToString();

                        string tags = null;
                        afterModifiers = afterModifiers.TrimStart();
                        if (afterModifiers.StartsWith("`"))
                        {
                            afterModifiers = afterModifiers.Advance(1);
                            var pos = afterModifiers.IndexOf('`');
                            tags = afterModifiers.Substring(0, pos);
                            afterModifiers = afterModifiers.Advance(pos + 1);
                        }
                       
                        if (name == null || toPath == null || url == null)
                            continue;
                        
                        var link = new GistLink {
                            Name = name.ToString(),
                            Url = url.ToString(),
                            To = toPath,
                            Description = afterModifiers.Trim().ToString(),
                            User = url.LastLeftPart('/').LastRightPart('/').ToString(),
                            Tags = tags?.Split(',').Map(x => x.Trim()).ToArray(),
                        };

                        if (link.User == "gistlyn" || link.User == "mythz")
                            link.User = "ServiceStack";
                        
                        to.Add(link);
                    }
                }

                return to;
            }

            public static GistLink Get(List<GistLink> links, string gistAlias)
            {
                var sanitizedAlias = gistAlias.Replace("-", "");
                var gistLink = links.FirstOrDefault(x => x.Name.Replace("-","").EqualsIgnoreCase(sanitizedAlias));
                return gistLink;
            }

            public bool MatchesTag(string tagName)
            {
                if (Tags == null)
                    return false;
                
                var searchTags = tagName.Split(',').Map(x => x.Trim());
                return searchTags.Count == 1  
                    ? Tags.Any(x => x.EqualsIgnoreCase(tagName))
                    : Tags.Any(x => searchTags.Any(x.EqualsIgnoreCase));
            }
        }

        public static void SaveReference(string tool, string lang, string typesUrl, string filePath)
        {
            var exists = File.Exists(filePath);
            RegisterStat(tool, lang, exists ? "updateref" : "addref");

            if (Verbose) $"API: {typesUrl}".Print();
            var dtos = typesUrl.GetStringFromUrl();

            if (dtos.IndexOf("Options:", StringComparison.Ordinal) == -1) 
                throw new Exception($"Invalid Response from {typesUrl}");
            
            File.WriteAllText(filePath, dtos, Utf8WithoutBom);

            var fileName = Path.GetFileName(filePath);
            (exists ? $"Updated: {fileName}" : $"Saved to: {fileName}").Print();
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

            existingRefSrc = existingRefSrc.Substring(startPos);
            var lines = existingRefSrc.Replace("\r","").Split('\n');
            foreach (var l in lines) 
            {
                var line = l;
                if (line.StartsWith("*/"))
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

            var typesUrl = baseUrl.CombineWith($"/types/{lang}") + qs;
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

        private static string CamelToKebab(string str) => Regex.Replace((str ?? ""),"([a-z])([A-Z])","$1-$2").ToLower();

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes:true);

        public static void PipeProcess(string fileName, string arguments, string workDir = null, Action fn = null)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                }
            };
            if (workDir != null)
                process.StartInfo.WorkingDirectory = workDir;

            using (process)
            {
                process.OutputDataReceived += (sender, data) => {
                    Console.WriteLine(data.Data);
                };
                process.StartInfo.RedirectStandardError = true;
                process.ErrorDataReceived += (sender, data) => {                    
                    Console.Error.WriteLine(data.Data);
                };
                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (fn != null)
                {
                    fn();
                    process.Kill();
                }
                else
                {
                    process.WaitForExit();
                }
                
                process.Close();
            }            
        }

        public static bool GetExePath(string exeName, out string fullPath)
        {
            try
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                            ? "where"  //Win 7/Server 2003+
                            : "which", //macOS / Linux
                        Arguments = exeName,
                        RedirectStandardOutput = true
                    }
                };
                p.Start();
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    // just return first match
                    fullPath = output.Substring(0, output.IndexOf(Environment.NewLine, StringComparison.Ordinal));
                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        if (Verbose) $"Found path for '{exeName}' at '{fullPath}'".Print();
                        return true;
                    }
                }
            }
            catch {}               
            fullPath = null;
            return false;
        }
        
        static string ReplaceMyApp(string input, string projectName)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(projectName))
                return input;
            
            var projectNameKebab = CamelToKebab(projectName);
            var splitPascalCase = projectName.SplitPascalCase();
            var ret = input
                .Replace("My App", splitPascalCase)
                .Replace("MyApp", projectName)
                .Replace("my-app", projectNameKebab);

            if (!Env.IsWindows)
                ret = ret.Replace("\r", "");
            
            return ret;
        }

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
                if (Verbose) $"Moving Directory {projectDir.FullName} -> {newDirPath}".Print();
                Directory.Move(projectDir.FullName, newDirPath);
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
            
            var sourceTasks = sourceRepos.Map(source => (source.Value, new GithubGateway().GetSourceReposAsync(source.Key)));

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

        private static void PrintGistLinks(string tool, List<GistLink> links, string tag=null)
        {
            "".Print();

            var tags = links.Where(x => x.Tags != null).SelectMany(x => x.Tags).Distinct().OrderBy(x => x).ToList();

            if (!string.IsNullOrEmpty(tag))
            {
                links = links.Where(x => x.MatchesTag(tag)).ToList();
                var plural = tag.Contains(',') ? "s" : "";
                $"Results matching tag{plural} [{tag}]:".Print();
                "".Print();
            }

            var i = 1;
            var padName = links.OrderByDescending(x => x.Name.Length).First().Name.Length + 1;
            var padTo = links.OrderByDescending(x => x.To.Length).First().To.Length + 1;
            var padBy = links.OrderByDescending(x => x.User.Length).First().User.Length + 1;
            var padDesc = links.OrderByDescending(x => x.Description.Length).First().Description.Length + 1;

            foreach (var link in links)
            {
                $" {i++.ToString().PadLeft(3, ' ')}. {link.Name.PadRight(padName, ' ')} {link.Description.PadRight(padDesc, ' ')} to: {link.To.PadRight(padTo, ' ')} by @{link.User.PadRight(padBy, ' ')} {link.ToTagsString()}".Print();
            }

            "".Print();

            $" Usage:  {tool} +<name>".Print();
            $"         {tool} +<name> <UseName>".Print();
            
            "".Print();

            var tagSearch = "#<tag>";
            $"Search:  {tool} + {tagSearch.PadRight(Math.Max(padName-9,0), ' ')} Available tags: {string.Join(", ", tags)}".Print();

            "".Print();
        }
        
        public static List<string> HostFiles = new List<string> {
               "appsettings.json",
               "Web.config",
               "App.config",
               "Startup.cs",
               "Program.cs",
               "*.csproj",
        };

        public static string ResolveBasePath(string to, string exSuffix="")
        {
            if (to == "." || string.IsNullOrEmpty(to))
                return Environment.CurrentDirectory;
            
            if (to.IndexOf("..", StringComparison.Ordinal) >= 0)
                throw new NotSupportedException($"Invalid location '{to}'{exSuffix}");

            if (to.StartsWith("/"))
                if (Env.IsWindows)
                    throw new NotSupportedException($"Cannot write to '{to}' on Windows{exSuffix}");
                else
                    return to;
            
            if (to.IndexOf(":\\", StringComparison.Ordinal) >= 0)
                if (!Env.IsWindows)
                    throw new NotSupportedException($"Cannot write to '{to}'{exSuffix}");
                else
                    return to;

            if (to[0] == '$')
            {
                if (to.StartsWith("$HOST"))
                {
                    foreach (var hostFile in HostFiles)
                    {
                        var matchingFiles = Directory.GetFiles(Environment.CurrentDirectory, hostFile, SearchOption.AllDirectories);
                        if (matchingFiles.Length > 0)
                        {
                            var dirName = Path.GetDirectoryName(matchingFiles[0]);
                            return dirName;
                        }
                    }

                    var hostFiles = string.Join(", ", HostFiles); 
                    throw new NotSupportedException($"Couldn't find host project location containing any of {hostFiles}{exSuffix}");
                }
                
                if (to.StartsWith("$HOME"))
                    return to.Replace("$HOME", Env.IsWindows 
                        ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) 
                        : Environment.GetEnvironmentVariable("HOME"));

                var folderValues = EnumUtils.GetValues<Environment.SpecialFolder>();
                foreach (var specialFolder in folderValues)
                {
                    if (to.StartsWith(specialFolder.ToString()))
                        return to.Replace("$" + specialFolder,
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                }
            }
            else
            {
                if (to.EndsWith("/"))
                {
                    var dirName = to.Substring(0, to.Length - 2);
                    var matchingDirs = Directory.GetDirectories(Environment.CurrentDirectory, dirName, SearchOption.AllDirectories);
                    if (matchingDirs.Length == 0)
                        throw new NotSupportedException($"Unable to find Directory named '{dirName}'{exSuffix}");
                    return matchingDirs[0];
                }
                else
                {
                    var matchingFiles = Directory.GetFiles(Environment.CurrentDirectory, to, SearchOption.AllDirectories);
                    if (matchingFiles.Length == 0)
                        throw new NotSupportedException($"Unable to find File named '{to}'{exSuffix}");

                    var dirName = Path.GetDirectoryName(matchingFiles[0]);
                    return dirName;
                }
            }
            
            throw new NotSupportedException($"Unknown location '{to}'{exSuffix}");
        }

        public static string SanitizeProjectName(string projectName)
        {
            if (string.IsNullOrEmpty(projectName))
                return null;

            var sepChars = new[] { ' ', '-', '+', '_' };
            if (projectName.IndexOfAny(sepChars) == -1)
                return projectName;
            
            var sb = new StringBuilder();
            var words = projectName.Split(sepChars);
            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(word))
                    continue;

                sb.Append(char.ToUpper(word[0])).Append(word.Substring(1));
            }

            return sb.ToString();
        }

        public static void WriteGistFile(string gistId, string gistAlias, string to, string projectName, Func<bool> getUserApproval = null)
        {
            projectName = SanitizeProjectName(projectName);
            var gistLinkUrl = $"https://gist.github.com/{gistId}";
            if (gistId.IsUrl())
            {
                gistLinkUrl = gistId;
                gistId = gistId.LastRightPart('/');
            }
            
            var gistFiles = new GithubGateway().GetGistFiles(gistId);
            var resolvedFiles = new List<KeyValuePair<string,string>>();
            KeyValuePair<string, string>? initFile = null;

            foreach (var gistFile in gistFiles)
            {
                if (gistFile.Key.IndexOf("..", StringComparison.Ordinal) >= 0)
                    throw new Exception($"Invalid file name '{gistFile.Key}' from '{gistLinkUrl}'");

                var alias = !string.IsNullOrEmpty(gistAlias)
                    ? $"'{gistAlias}' "
                    : "";
                var exSuffix = $" required by {alias}{gistLinkUrl}";
                var basePath = ResolveBasePath(to, exSuffix);
                try
                {
                    if (gistFile.Key == "_init")
                    {
                        initFile = KeyValuePair.Create(gistFile.Key, gistFile.Value);
                        continue;
                    }

                    var useFileName = ReplaceMyApp(gistFile.Key, projectName);
                    bool noOverride = false;
                    if (useFileName.EndsWith("?"))
                    {
                        noOverride = true;
                        useFileName = useFileName.Substring(0, useFileName.Length - 1);
                    }

                    var resolvedFile = Path.GetFullPath(useFileName, basePath.Replace("\\","/"));
                    if (noOverride && File.Exists(resolvedFile))
                    {
                        if (Verbose) $"Skipping existing optional file: {resolvedFile}".Print();
                        continue;
                    }
                    
                    resolvedFiles.Add(KeyValuePair.Create(resolvedFile, ReplaceMyApp(gistFile.Value, projectName)));
                }
                catch (Exception ex)
                {
                    throw new Exception($"Cannot write file '{gistFile.Key}' from '{gistLinkUrl}': {ex.Message}", ex);
                }
            }

            var label = !string.IsNullOrEmpty(gistAlias)
                ? $"'{gistAlias}' "
                : "";
            
            var sb = new StringBuilder();
            foreach (var resolvedFile in resolvedFiles)
            {
                sb.AppendLine("  " + resolvedFile.Key);
            }

            var silentMode = getUserApproval == null;
            if (!silentMode)
            {
                if (!ForceApproval)
                {
                    sb.Insert(0, $"Write files from {label}{gistLinkUrl} to:{Environment.NewLine}");
                    sb.AppendLine()
                        .AppendLine("Proceed? (n/Y):");
    
                    sb.ToString().Print();
    
                    if (!getUserApproval())
                        throw new Exception("Operation cancelled by user.");
                }
                else
                {
                    sb.Insert(0, $"Writing files from {label}{gistLinkUrl} to:{Environment.NewLine}");
                    sb.ToString().Print();                
                }
            }

            if (initFile != null)
            {
                var hostDir = ResolveBasePath(to, $" required by {gistLinkUrl}");

                var lines = initFile.Value.Value.ReadLines();
                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("#"))
                        continue;
                    
                    var cmd = line.Trim();
                    if (!cmd.StartsWith("nuget") && !cmd.StartsWith("dotnet"))
                    {
                        if (Verbose) $"Command '{cmd}' not supported".Print();
                        continue;
                    }

                    if (cmd.StartsWith("nuget") && !(cmd.StartsWith("nuget add") || 
                                                     cmd.StartsWith("nuget restore") ||
                                                     cmd.StartsWith("nuget update")))
                    {
                        if (Verbose) $"Command '{cmd}' not allowed".Print();
                        continue;
                    }
                    if (cmd.StartsWith("dotnet") && !(cmd.StartsWith("dotnet add ") || 
                                                      cmd.StartsWith("dotnet restore ") || 
                                                      cmd.Equals("dotnet restore")))
                    {
                        if (Verbose) $"Command '{cmd}' not allowed".Print();
                        continue;
                    }

                    if (cmd.IndexOfAny(new[]{ '"', '\'', '&', ';', '$', '@', '|', '>' }) >= 0)
                    {
                        $"Command contains illegal characters, ignoring: '{cmd}'".Print();
                        continue;
                    }

                    if (cmd.StartsWith("nuget"))
                    {
                        if (GetExePath("nuget", out var nugetPath))
                        {
                            cmd.Print();
                            var cmdArgs = cmd.RightPart(' ');
                            PipeProcess(nugetPath, cmdArgs, workDir: hostDir);                        
                        }
                        else
                        {
                            $"'nuget' not found in PATH, skipping: '{cmd}'".Print();                            
                        }
                    }
                    else if (cmd.StartsWith("dotnet"))
                    {
                        if (GetExePath("dotnet", out var dotnetPath))
                        {
                            cmd.Print();
                            var cmdArgs = cmd.RightPart(' ');
                            PipeProcess(dotnetPath, cmdArgs, workDir: hostDir);                        
                        }
                        else
                        {
                            $"'dotnet' not found in PATH, skipping: '{cmd}'".Print();                            
                        }
                    }                    
                }
            }
            
            foreach (var resolvedFile in resolvedFiles)
            {
                if (resolvedFile.Key == "_init") 
                    continue;
                if (Verbose) $"Writing {resolvedFile.Key}...".Print();
                var dir = Path.GetDirectoryName(resolvedFile.Key);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(resolvedFile.Key, resolvedFile.Value);
            }
        }

        public static Func<bool> UserInputYesNo { get; set; } = UseConsoleRead;

        public static bool UseConsoleRead()
        {
            var keyInfo = Console.ReadKey(intercept:true);
            return keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.Y;
        }

        public static bool ApproveUserInputRequests() => true;
        public static bool DenyUserInputRequests() => false;

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
                var filesConfig = "files.config".GetAppSetting();               
                var vfs = "files".GetAppSetting().GetVirtualFiles(config:filesConfig);
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

        private static Exception StartupException = null;

        public void ConfigureAppHost(ServiceStackHost appHost)
        {
            try 
            { 
                appHost.Config.DebugMode = GetDebugMode();
                appHost.Config.ForbiddenPaths.Add("/plugins");
    
                var feature = appHost.GetPlugin<SharpPagesFeature>();
                if (feature != null)
                    "Using existing SharpPagesFeature from appHost".Print();
    
                if (feature == null)
                {
                    feature = (nameof(SharpPagesFeature).GetAppSetting() != null
                        ? (SharpPagesFeature)typeof(SharpPagesFeature).CreatePlugin()
                        : new SharpPagesFeature { ApiPath = "apiPath".GetAppSetting() ?? "/api" });
                }
    
                var dbFactory = "db".GetAppSetting().GetDbFactory(connectionString:"db.connection".GetAppSetting());
                if (dbFactory != null)
                {
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
            catch (Exception ex)
            {
                StartupException = ex;
                throw;
            }
        }
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

        public static string AssertDirectory(this string filePath)
        {
            try { 
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)); 
            } catch { }
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
            ".git",
            "publish"
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

        public static bool IsUrl(this string gistId) => gistId.IndexOf("://", StringComparison.Ordinal) >= 0;
    }

    public class GithubRepo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Homepage { get; set; }
        public int Watchers_Count { get; set; }
        public int Stargazers_Count { get; set; }
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
                    return null;
                throw;
            }
        }

        public string GetSourceZipUrl(string orgNames, string name)
        {
            var orgs = orgNames.Split(';')
                .Map(x => x.LeftPart(' '));
            foreach (var orgName in orgs)
            {
                var repoFullName = UnwrapRepoFullName(orgName, name);
                if (repoFullName == null)
                    continue;

                var json = GetJson($"repos/{repoFullName}/releases");
                var response = JSON.parse(json);

                if (response is List<object> releases && releases.Count > 0 &&
                    releases[0] is Dictionary<string, object> release &&
                    release.TryGetValue("zipball_url", out var zipUrl))
                {
                    return (string)zipUrl;
                }

                if (Startup.Verbose) $"No releases found for '{repoFullName}', installing from master...".Print();
                return $"https://github.com/{repoFullName}/archive/master.zip";
            }

            throw new Exception($"'{name}' was not found in sources: {orgs.Join(", ")}");
        }

        public async Task<List<GithubRepo>> GetSourceReposAsync(string orgName)
        {
            var repos = (await GetUserAndOrgReposAsync(orgName))
                .OrderBy(x => x.Name)
                .ToList();
            return repos;
        }

        public async Task<List<GithubRepo>> GetUserAndOrgReposAsync(string githubOrgOrUser)
        {
            var map = new Dictionary<string,GithubRepo>();

            var userRepos = GetJsonCollectionAsync<List<GithubRepo>>($"users/{githubOrgOrUser}/repos");
            var orgRepos = GetJsonCollectionAsync<List<GithubRepo>>($"orgs/{githubOrgOrUser}/repos");

            try
            {
                foreach (var repos in await userRepos)
                foreach (var repo in repos)
                    map[repo.Name] = repo;
            }
            catch (Exception e) { if (!e.IsNotFound()) throw; }

            try
            {
                foreach (var repos in await userRepos)
                foreach (var repo in repos)
                    map[repo.Name] = repo;
            }
            catch (Exception e) { if (!e.IsNotFound()) throw; }

            return map.Values.ToList();
        }

        public List<GithubRepo> GetUserRepos(string githubUser) => 
            StreamJsonCollection<List<GithubRepo>>($"users/{githubUser}/repos").SelectMany(x => x).ToList();

        public List<GithubRepo> GetOrgRepos(string githubOrg) => 
            StreamJsonCollection<List<GithubRepo>>($"orgs/{githubOrg}/repos").SelectMany(x => x).ToList();

        public string GetJson(string route)
        {
            var apiUrl = !route.IsUrl() 
                ? GithubApiBaseUrl.CombineWith(route)
                : route;
            if (Startup.Verbose) $"API: {apiUrl}".Print();

            return apiUrl.GetJsonFromUrl(req => req.UserAgent = UserAgent);
        }

        public T GetJson<T>(string route) => GetJson(route).FromJson<T>();

        public IEnumerable<T> StreamJsonCollection<T>(string route)
        {
            List<T> results;
            var nextUrl = GithubApiBaseUrl.CombineWith(route);

            do
            {
                if (Startup.Verbose) $"API: {nextUrl}".Print();

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

        public async Task<List<T>> GetJsonCollectionAsync<T>(string route)
        {
            var to = new List<T>();
            List<T> results;
            var nextUrl = GithubApiBaseUrl.CombineWith(route);

            do
            {
                if (Startup.Verbose) $"API: {nextUrl}".Print();

                results = (await nextUrl.GetJsonFromUrlAsync(req => req.UserAgent = UserAgent,
                        responseFilter: res => {
                            var links = ParseLinkUrls(res.Headers["Link"]);
                            links.TryGetValue("next", out nextUrl);
                        }))
                    .FromJson<List<T>>();
                
                to.AddRange(results);

            } while (results.Count > 0 && nextUrl != null);

            return to;
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
            var webClient = new WebClient();
            webClient.Headers.Add(HttpHeaders.UserAgent, UserAgent);
            webClient.DownloadFile(downloadUrl, fileName);
        }
        
        ConcurrentDictionary<string,Dictionary<string, string>> GistFilesCache = 
            new ConcurrentDictionary<string, Dictionary<string, string>>();

        public Dictionary<string, string> GetGistFiles(string gistId)
        {
            return GistFilesCache.GetOrAdd(gistId, gistKey => {
                var json = GetJson($"/gists/{gistKey}");
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

                throw new NotSupportedException($"Invalid gist response returned for '{gistKey}'");
            });
        }
    }
    
}
