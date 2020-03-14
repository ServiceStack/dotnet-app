using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.Script;
using ServiceStack.Text;

namespace Web
{
    public partial class Startup
    {
        public static string GistLinksId { get; set; } = "9b32b03f207a191099137429051ebde8";

        public static bool Verbose { get; set; }
        static string[] VerboseArgs = {"/v", "-v", "/verbose", "--verbose"};

        public static bool Silent { get; set; }
        static string[] QuietArgs = { "/q", "-q", "/quiet", "-quiet", "--quiet" };

        static string[] SourceArgs = { "/s", "-s", "/source", "-source", "--source" };

        public static bool ForceApproval { get; set; }
        static string[] ForceArgs = { "/f", "-f", "/force", "-force", "--force" };

        public static bool IgnoreSslErrors { get; set; }
        private static string[] IgnoreSslErrorsArgs = {"/ignore-ssl-errors", "--ignore-ssl-errors"};

        static string[] NameArgs = { "/name", "-name", "--name" };

        static string[] DeleteArgs = { "/delete", "-delete", "--delete" };
        static string[] ReplaceArgs = { "/replace", "-replace", "--replace" };

        static string[] HelpArgs = { "/help", "--help", "-help", "?" };

        static string[] OutArgs = { "/out", "-out", "--out" };

        public static string OutDir { get; set; }
        
        public static string GitHubToken { get; set; } 

        public static List<KeyValuePair<string,string>> ReplaceTokens { get; set; } = new List<KeyValuePair<string, string>>();
        
        public static Func<bool> UserInputYesNo { get; set; } = UseConsoleRead;

        private static string CamelToKebab(string str) => Regex.Replace((str ?? ""),"([a-z])([A-Z])","$1-$2").ToLower();

        public static bool UseConsoleRead()
        {
            var keyInfo = Console.ReadKey(intercept:true);
            return keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.Y;
        }

        public static bool ApproveUserInputRequests() => true;
        public static bool DenyUserInputRequests() => false;

        public static void InitMix()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MIX_SOURCE")))
                GistLinksId = Environment.GetEnvironmentVariable("MIX_SOURCE");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_TOKEN")))
                GitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        }

        public static string GetVersion() => Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?
            .Version.LastLeftPart('.') ?? "0.0.0";

        public static void RegisterStat(string tool, string name, string type = "tool")
        {
            if (Environment.GetEnvironmentVariable("SERVICESTACK_TELEMETRY_OPTOUT") == "1" ||
                Environment.GetEnvironmentVariable("SERVICESTACK_TELEMETRY_OPTOUT") == "true")
                return;
            try
            {
                $"https://servicestack.net/stats/{type}/record?name={name}&source={tool}&version={GetVersion()}"
                    .GetBytesFromUrlAsync(requestFilter:req => req.ApplyRequestFilters());
            }
            catch { }
        }

        private static void PrintGistLinks(string tool, List<GistLink> links, string tag = null, string usage = null)
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
            var padTo = (links.OrderByDescending(x => x.To?.Length ?? 0).First().To?.Length ?? 0) + 1;
            var padBy = links.OrderByDescending(x => x.User.Length).First().User.Length + 1;
            var padDesc = links.OrderByDescending(x => x.Description.Length).First().Description.Length + 1;

            foreach (var link in links)
            { 
                var toLabel = link.To != null
                    ? $" to: {link.To.PadRight(padTo, ' ')}" 
                    : "";
                $" {i++.ToString().PadLeft(3, ' ')}. {link.Name.PadRight(padName, ' ')} {link.Description.PadRight(padDesc, ' ')}{toLabel} by @{link.User.PadRight(padBy, ' ')} {link.ToTagsString()}"
                    .Print();
            }

            "".Print();

            if (usage != null)
            {
                usage.Print();
                return;
            }

            if (tool.EndsWith("mix"))
            {
                $"   Usage:  mix <name> <name> ...".Print();

                "".Print();

                $"  Search:  mix #<tag> Available tags: {string.Join(", ", tags)}".Print();

                "".Print();

                $"Advanced:  mix ?".Print();
            }
            else
            {
                $" Usage:  {tool} +<name>".Print();
                $"         {tool} +<name> <UseName>".Print();

                "".Print();

                var tagSearch = "#<tag>";
                $"Search:  {tool} + {tagSearch.PadRight(Math.Max(padName - 9, 0), ' ')} Available tags: {string.Join(", ", tags)}"
                    .Print();
            }
        }

        private static ConcurrentDictionary<string, List<GistLink>> GistLinksCache =
            new ConcurrentDictionary<string, List<GistLink>>();

        private static List<GistLink> GetGistApplyLinks() => GetGistLinks(GistLinksId, "mix.md");

        private static List<GistLink> GetGistLinks(string gistId, string name)
        {
            var gistsIndex = GitHubUtils.Gateway.GetGistFiles(gistId)
                .FirstOrDefault(x => x.Key == name);

            if (gistsIndex.Key == null)
                throw new NotSupportedException($"Could not find '{name}' file in gist '{GistLinksId}'");

            return GistLinksCache.GetOrAdd(gistId + ":" + name, key => {
                var links = GistLink.Parse(gistsIndex.Value);
                return links;
            });
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

            public string Url { get; set; } // https://api.github.com/repos/NetCoreWebApps/bare
            public string Html_Url { get; set; }
            public bool Private { get; set; }

            public GithubRepo Parent { get; set; } // only on single result, e.g: /repos/NetCoreWebApps/bare
        }

        public static async Task<bool> CheckForUpdates(string tool, Task<string> checkUpdatesAndQuit)
        {
            if (checkUpdatesAndQuit != null)
            {
                try
                {
                    var json = await checkUpdatesAndQuit;
                    var response = JSON.parse(json);
                    if (response is Dictionary<string, object> r &&
                        r.TryGetValue("items", out var oItems) && oItems is List<object> items &&
                        items.Count > 0 && items[0] is Dictionary<string, object> item &&
                        item.TryGetValue("upper", out var oUpper) && oUpper is string upper)
                    {
                        if (GetVersion() != upper) {
                            "".Print();
                            $"new version available, update with:".Print();
                            "".Print();
                            $"  dotnet tool update -g {tool}".Print();
                        }
                    }
                }
                catch (Exception)
                {
                    /*ignore*/
                }
                return true;
            }
            return false;
        }

        private static string[] ResolveGistAliases(string[] gistAliases, List<GistLink> links)
        {
            var hasNums = gistAliases.Any(x => int.TryParse(x, out _));

            if (hasNums)
            {
                var resolvedAliases = new List<string>();
                foreach (var gistAlias in gistAliases)
                {
                    if (!int.TryParse(gistAlias, out var index))
                    {
                        resolvedAliases.Add(gistAlias);
                        continue;
                    }

                    if (index <= 0 || index > links.Count)
                        throw new ArgumentOutOfRangeException($"Invalid Index '{index}'. Valid Range: 1...{links.Count - 1}");

                    resolvedAliases.Add(links[index - 1].Name);
                }

                gistAliases = resolvedAliases.ToArray();
            }

            return gistAliases;
        }

        public static bool ApplyGists(string tool, string[] gistAliases, string projectName = null)
        {
            projectName = projectName ?? new DirectoryInfo(Environment.CurrentDirectory).Name;
            var links = GetGistApplyLinks();
            
            gistAliases = ResolveGistAliases(gistAliases, links);

            foreach (var gistAlias in gistAliases)
            {
                if (gistAlias.StartsWith("https://gist.github.com/"))
                {
                    WriteGistFile(gistAlias, gistAlias, to: OutDir ?? ".", projectName: projectName, getUserApproval: UserInputYesNo);
                    ForceApproval = true; //If written once user didn't cancel, assume approval for remaining gists
                    continue;
                }
                
                var gistLink = GistLink.Get(links, gistAlias);
                if (gistLink == null)
                {
                    $"No match found for '{gistAlias}', available gists:".Print();
                    PrintGistLinks(tool, links);
                    return false;
                }
                            
                WriteGistFile(gistLink.Url, gistAlias, to: OutDir ?? gistLink.To, projectName: projectName, getUserApproval: UserInputYesNo);
                ForceApproval = true; //If written once user didn't cancel, assume approval for remaining gists
            }
            return true;
        }

        public static void DeleteGists(string tool, string[] gistAliases, string projectName)
        {
            projectName = projectName ?? new DirectoryInfo(Environment.CurrentDirectory).Name;
            var links = GetGistApplyLinks();
            
            gistAliases = ResolveGistAliases(gistAliases, links);

            var sb = new StringBuilder();
            var allResolvedFiles = new List<string>();
            string gistId;
            string gistLinkUrl;
            string to = ".";
            
            foreach (var gistAlias in gistAliases)
            {
                if (!gistAlias.StartsWith("https://gist.github.com"))
                {
                    var gistLink = GistLink.Get(links, gistAlias);
                    if (gistLink == null)
                    {
                        $"No match found for '{gistAlias}', available gists:".Print();
                        PrintGistLinks(tool, links);
                        return;
                    }

                    gistId = gistLink.Url;
                    gistLinkUrl = $"https://gist.github.com/{gistId}";
                    if (gistId.IsUrl())
                    {
                        gistLinkUrl = gistId;
                        gistId = gistLinkUrl.LastRightPart('/');
                    }
                    to = gistLink.To;
                }
                else
                {
                    gistLinkUrl = gistAlias;
                    gistId = gistLinkUrl.LastRightPart('/');
                }

                var alias = !string.IsNullOrEmpty(gistAlias)
                    ? $"'{gistAlias}' "
                    : "";
                var exSuffix = $" required by {alias}{gistLinkUrl}";

                var gistFiles = GitHubUtils.Gateway.GetGistFiles(gistId);
                var basePath = ResolveBasePath(to, exSuffix);

                var resolvedFiles = new List<string>();
                foreach (var gistFile in gistFiles)
                {
                    var resolvedFile = ResolveFilePath(gistFile.Key, basePath, projectName, to);
                    if (!File.Exists(resolvedFile))
                    {
                        if (Verbose) $"Skipping deleting non-existent file: {resolvedFile}".Print();
                        continue;
                    }
                    
                    resolvedFiles.Add(resolvedFile);
                    allResolvedFiles.Add(resolvedFile);
                }

                if (resolvedFiles.Count > 0)
                {
                    var label = !string.IsNullOrEmpty(gistAlias)
                        ? $"'{gistAlias}' "
                        : "";
                    sb.AppendLine();
                    var plural = resolvedFiles.Count != 1 ? "s" : "";
                    sb.AppendLine($"Delete {resolvedFiles.Count} file{plural} from {label}{gistLinkUrl}:");
                    sb.AppendLine();

                    foreach (var resolvedFile in resolvedFiles)
                    {
                        sb.AppendLine(resolvedFile);
                    }
                }
            }

            if (allResolvedFiles.Count == 0)
            {
                var gistsList = string.Join(",", gistAliases);
                $"Did not find any existing files from '{gistsList}' to delete".Print();
                return;
            }

            var getUserApproval = UserInputYesNo;
            var silentMode = Silent || getUserApproval == null;
            if (!silentMode)
            {
                if (!ForceApproval)
                {
                    sb.AppendLine()
                        .AppendLine("Proceed? (n/Y):");
    
                    sb.ToString().Print();

                    if (!getUserApproval())
                        throw new Exception("Operation cancelled by user.");
                }
                else
                {
                    sb.ToString().Print();
                }

                "".Print();
                $"Deleting {allResolvedFiles.Count} files...".Print();
            }

            var folders = new HashSet<string>();
            foreach (var resolvedFile in allResolvedFiles)
            {
                DeleteFile(resolvedFile);
                folders.Add(Path.GetDirectoryName(resolvedFile));
            }

            // Delete empty folders that had gist files
            var subFoldersFirst = folders.OrderByDescending(x => x);
            folders = new HashSet<string>();
            foreach (var folder in subFoldersFirst)
            {
                if (Directory.GetFiles(folder).Length == 0 && Directory.GetDirectories(folder).Length == 0)
                {
                    DeleteDirectory(folder);
                }
                else
                {
                    folders.Add(folder);
                }
            }

            if (!silentMode)
            {
                $"Done.".Print();
            }
        }

        private static string ResolveFilePath(string gistFilePath, string basePath, string projectName, string applyTo)
        {
            var useFileName = ReplaceMyApp(osPaths(gistFilePath), projectName);
            if (useFileName.EndsWith("?"))
                useFileName = useFileName.Substring(0, useFileName.Length - 1);

            var resolvedFile = Path.GetFullPath(useFileName, osPaths(basePath));

            var writesToFolder = gistFilePath.IndexOf('\\') >= 0;
            if (applyTo == "$HOST" && writesToFolder && !Directory.Exists(Path.GetDirectoryName(resolvedFile)))
            {
                // If resolved file doesn't exist for $HOST gists, check current folder for $"{projectName}.Folder\file.ext"
                // e.g. $HOST\ServiceModel => .\ProjectName.ServiceModel 
                var currentBasePath = Environment.CurrentDirectory;
                var tryPath = projectName + "." + gistFilePath;
                var resolvedPath = ResolveFilePath(tryPath, currentBasePath, projectName, applyTo:".");
                if (Directory.Exists(Path.GetDirectoryName(resolvedPath)))
                {
                    if (Verbose) $"Using matching qualified path: {resolvedPath}".Print();
                    return resolvedPath;
                }
            }
            
            return resolvedFile;
        }

        // More resilient impl for .NET Core
        public static void DeleteDirectoryRecursive(string path)
        {
            //modified from https://stackoverflow.com/a/1703799/85785
            foreach (var directory in Directory.GetDirectories(path))
            {
                var files = Directory.GetFiles(directory);
                foreach (var file in files)
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                DeleteDirectoryRecursive(directory);
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException) 
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path, true);
            }
        }        

        public static void DeleteDirectory(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;
            if (Verbose) $"RMDIR: {dirPath}".Print();
            try { DeleteDirectoryRecursive(dirPath); } catch (Exception ex) { Print(ex); }
            try { Directory.Delete(dirPath); } catch { }
        }

        public static void DeleteFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            if (Verbose) $"RM: {filePath}".Print();
            try { File.Delete(filePath); }  catch (Exception ex) { Print(ex); }
        }

        public static void Print(Exception ex)
        {
            if (Verbose) $"ERROR: {ex.Message}".Print();
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
                .Replace("my-app", projectNameKebab)
                .Replace("myapp", projectName.ToLower());

            if (!Env.IsWindows)
                ret = ret.Replace("\r", "");

            foreach (var replacePair in ReplaceTokens)
            {
                ret = ret.Replace(replacePair.Key, replacePair.Value);
            }
            
            return ret;
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
                    var dirName = to.Substring(0, to.Length - 1);
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

        public static string osPaths(string path) => Env.IsWindows
            ? path.Replace('/', '\\')
            : path.Replace('\\', '/');

        public static void WriteGistFile(string gistId, string gistAlias, string to, string projectName, Func<bool> getUserApproval = null)
        {
            projectName = SanitizeProjectName(projectName);
            var gistLinkUrl = $"https://gist.github.com/{gistId}";
            if (gistId.IsUrl())
            {
                gistLinkUrl = gistId;
                gistId = gistId.LastRightPart('/');
            }
            
            var gistFiles = GitHubUtils.Gateway.GetGistFiles(gistId);
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

                    var resolvedFile = ResolveFilePath(gistFile.Key, basePath, projectName, to);
                    var noOverride = gistFile.Key.EndsWith("?");
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
                    var nl = Environment.NewLine;
                    sb.Insert(0, $"{nl}Write files from {label}{gistLinkUrl} to:{nl}{nl}");
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

                var filePath = resolvedFile.Key;
                var fileContents = resolvedFile.Value;

                if (filePath.EndsWith("|base64"))
                {
                    try
                    {
                        filePath = filePath.LastLeftPart('|');
                        var fileBytes = Convert.FromBase64String(fileContents);
                        File.WriteAllBytes(filePath, fileBytes);
                    }
                    catch (Exception ex)
                    {
                        $"Could not Convert Base64 binary file '{filePath}': {ex.Message}".Print();
                        throw;
                    }
                }
                else
                {
                    File.WriteAllText(filePath, fileContents);
                }
            }
        }


        public static Process PipeProcess(string fileName, string arguments, string workDir = null, Action fn = null)
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
            return process;
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

        public static async Task Mix(string tool, string[] args)
        {
            InitMix();

            bool deleteMode = false;
            string projectName = null;
            var dotnetArgs = new List<string>();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (HelpArgs.Contains(arg))
                {
                    $"Version: {GetVersion()}".Print();
                    
                    "".Print();
                    
                    "View all published gists:  ".Print();
                    
                    $"   mix".Print();
                    
                    "".Print();
                    
                    "Simple Usage:  ".Print();
                    
                    $"   mix <name> <name> ...".Print();
                    
                    "".Print();

                    "Mix using numbered list index instead:".Print();
                    
                    $"   mix 1 3 5 ...".Print();
                    
                    "".Print();

                    "Mix file contents from gist URL:".Print();
                    
                    $"   mix <gist-url>".Print();
                    
                    "".Print();

                    "Delete previously mixed gists:".Print();
                    
                    $"   mix -delete <name> <name> ...".Print();
                    
                    "".Print();

                    $"Use custom project name instead of current folder name (replaces MyApp):".Print();

                    $"   mix -name ProjectName <name> <name> ...".Print();
                    
                    "".Print();

                    $"Replace additional tokens before mixing:".Print();

                    $"   mix -replace term=with <name> <name> ...".Print();

                    "".Print();

                    $"Multi replace with escaped string example:".Print();

                    $"   mix -replace term=with -replace \"This Phrase\"=\"With This\" <name> <name> ...".Print();
                    
                    "".Print();

                    $"Only display available gists with a specific tag:".Print();

                    $"  mix #<tag>".Print();
                    $"  mix #<tag>,<tag>,<tag>".Print();
                    
                    return;
                }
                if (VerboseArgs.Contains(arg))
                {
                    Verbose = true;
                    continue;
                }
                if (SourceArgs.Contains(arg))
                {
                    GistLinksId = args[++i];
                    continue;
                }
                if (ForceArgs.Contains(arg))
                {
                    ForceApproval = true;
                    continue;
                }
                if (IgnoreSslErrorsArgs.Contains(arg))
                {
                    IgnoreSslErrors = true;
                    continue;
                }
                if (DeleteArgs.Contains(arg))
                {
                    deleteMode = true;
                    continue;
                }
                if (OutArgs.Contains(arg))
                {
                    OutDir = args[i + 1];
                    if (!OutDir.EndsWith("/"))
                        OutDir += "/"; //required for mix to specify directory
                    i++;
                    continue;
                }
                if (NameArgs.Contains(arg))
                {
                    projectName = i < arg.Length - 1
                        ? args[i+1]
                        : throw new Exception("Missing -name value");
                    i++;
                    continue;
                }
                if (ReplaceArgs.Contains(arg))
                {
                    var replacePair = i < arg.Length - 1
                        ? args[i+1]
                        : throw new Exception("Missing -replace value, e.g -replace term=with");

                    const string InvalidUsage = "Invalid -replace usage, e.g: -replace term=with OR -replace \"the phrase\"=\"with this text\"";

                    var literal = replacePair.AsSpan().ParseJsToken(out var token).ToString();
                    var term = token is JsIdentifier termId 
                        ? termId.Name
                            : token is JsLiteral termLiteral 
                                ? (string)termLiteral.Value
                                : throw new Exception(InvalidUsage);
                    
                    if (literal[0] != '=')
                        throw new Exception(InvalidUsage);
                    
                    literal = literal.Substring(1).AsSpan().ParseJsToken(out token).ToString();
                    var with = token is JsIdentifier withId 
                        ? withId.Name
                        : token is JsLiteral withLiteral 
                            ? (string)withLiteral .Value
                            : throw new Exception(InvalidUsage);

                    ReplaceTokens.Add(KeyValuePair.Create(term, with));

                    i++;
                    continue;
                }
                if (arg.StartsWith("-"))
                    throw new Exception("Unknown switch: " + arg);
                    
                dotnetArgs.Add(arg);
            }

            Task<string> checkUpdatesAndQuit = null;
            Task<string> beginCheckUpdates() =>
                $"https://api.nuget.org/v3/registration3/{tool}/index.json".GetJsonFromUrlAsync(req => req.ApplyRequestFilters());
            
            if (dotnetArgs.Count == 0)
            {
                RegisterStat(tool, "list");
                checkUpdatesAndQuit = beginCheckUpdates();
                PrintGistLinks(tool, GetGistApplyLinks());
            }
            else
            {
                if (args[0].FirstCharEquals('#'))
                {
                    RegisterStat(tool, args[0], "search");
                    PrintGistLinks(tool, GetGistApplyLinks(), args[0].Substring(1));
                }
                else
                {
                    if (dotnetArgs.Count == 1 && dotnetArgs[0].IndexOf('+') >= 0)
                        dotnetArgs = dotnetArgs[0].Split('+').ToList();

                    var replaceStatSuffix = ReplaceTokens.Count > 0 ? $"_replace{ReplaceTokens.Count}" : "";
                    if (!deleteMode)
                    {
                        RegisterStat(tool, string.Join("_", dotnetArgs.OrderBy(x => x)), "+" + replaceStatSuffix);
                        ApplyGists(tool, dotnetArgs.ToArray(), projectName:projectName);
                    }
                    else
                    {
                        RegisterStat(tool, string.Join("_", dotnetArgs.OrderBy(x => x)), "+" + replaceStatSuffix);
                        DeleteGists(tool, dotnetArgs.ToArray(), projectName);
                    }
                }
            }

            await CheckForUpdates(tool, checkUpdatesAndQuit);
        }
        
        private static string GetCachedFilePath(string zipUrl)
        {
            var invalidFileNameChars = new HashSet<char>(Path.GetInvalidFileNameChars()) { ':' };
            var safeFileName = new string(zipUrl.Where(c => !invalidFileNameChars.Contains(c)).ToArray());
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var cachedPath = Path.Combine(homeDir, ".servicestack", "cache", safeFileName);
            return cachedPath;
        }

        internal static string DownloadCachedStringFromUrl(string url)
        {
            var cachedPath = GetCachedFilePath(url);

            var isCached = File.Exists(cachedPath);
            if (Verbose && !isCached) $"Downloading uncached '{url}' ...".Print();

            if (File.Exists(cachedPath))
                return File.ReadAllText(cachedPath);

            var text = url.GetStringFromUrl(requestFilter: req => req.UserAgent = GitHubUtils.UserAgent);
            File.WriteAllText(cachedPath, text);

            return text;
        }
    }

    public static class MixUtils
    {
        public static bool IsUrl(this string gistId) => gistId.IndexOf("://", StringComparison.Ordinal) >= 0;
        
        public static string SplitPascalCase(this string input) 
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var retVal = new StringBuilder(input.Length + 5);

            for (int i = 0; i < input.Length; ++i) {
                var currentChar = input[i];
                if (char.IsUpper(currentChar)) {
                    if ((i > 1 && !char.IsUpper(input[i - 1]))
                        || (i + 1 < input.Length && !char.IsUpper(input[i + 1])))
                        retVal.Append(' ');
                }

                retVal.Append(currentChar);
            }

            return retVal.ToString().Trim();
        }

        public static void ApplyRequestFilters(this HttpWebRequest req)
        {
            req.UserAgent = GitHubUtils.UserAgent;

            if (!string.IsNullOrEmpty(Startup.GitHubToken))
            {
                req.Headers["Authorization"] = "token " + Startup.GitHubToken;
            }
            

            if (Startup.IgnoreSslErrors)
            {
                req.ServerCertificateValidationCallback = (webReq, cert, chain, errors) => true;
            }
        }

        public static void HandleProgramExceptions(this Exception ex)
        {
            ex = ex.UnwrapIfSingleException();
            Console.WriteLine(Startup.Verbose ? ex.ToString() : ex.Message);

            if (ex.Message.IndexOf("SSL connection", StringComparison.Ordinal) >= 0)
            {
                Console.WriteLine("");
                Console.WriteLine("SSL Connection Errors can be ignored with care using switch: --ignore-ssl-errors");
            }
        }

        public static bool IsValidFileName(this string filename) =>
            !string.IsNullOrEmpty(filename) && filename.IndexOfAny(Path.GetInvalidFileNameChars()) == -1 && filename[0] != '-';
    }

    public static class GitHubUtils
    {
        public const string UserAgent = "web dotnet tool";

        private static GitHubGateway gateway;
        public static GitHubGateway Gateway => gateway ?? (gateway = new GitHubGateway {
            AccessToken = Startup.GitHubToken,
            UserAgent = UserAgent,
            GetJsonFilter = GetJson
        });
            
        public static string GetJson(string apiUrl)
        {
            if (Startup.Verbose) 
                $"API: {apiUrl}".Print();
                
            return apiUrl.GetJsonFromUrl(req => req.ApplyRequestFilters());
        }

        public static readonly ConcurrentDictionary<string, Dictionary<string, string>> GistFilesCache =
            new ConcurrentDictionary<string, Dictionary<string, string>>();

        public static Dictionary<string, string> GetGistFiles(this GitHubGateway gateway, string gistId)
        {
            return GistFilesCache.GetOrAdd(gistId, gistKey => {
                var json = gateway.GetJson($"/gists/{gistKey}");
                var response = JSON.parse(json);
                if (response is Dictionary<string, object> obj &&
                    obj.TryGetValue("files", out var oFiles) &&
                    oFiles is Dictionary<string, object> files)
                {
                    var to = new Dictionary<string, string>();
                    foreach (var entry in files)
                    {
                        var meta = (Dictionary<string, object>) entry.Value;
                        var contents = (string) meta["content"];
                        if (string.IsNullOrEmpty(contents) && meta["truncated"] is bool b && b)
                        {
                            contents = Startup.DownloadCachedStringFromUrl((string)meta["raw_url"]);
                        }
                        
                        to[entry.Key] = contents;
                    }

                    return to;
                }

                throw new NotSupportedException($"Invalid gist response returned for '{gistKey}'");
            });
        }
        
        public static string ToGistId(this string url) => url?.LastRightPart('/');
    }
    
}