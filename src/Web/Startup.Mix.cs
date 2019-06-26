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
        public static string GistLinksId { get; set; } = "f3fa8c016bbd253badc61d80afe399d9";

        public static bool Verbose { get; set; }
        public static bool Silent { get; set; }
        static string[] VerboseArgs = {"/verbose", "--verbose"};

        static string[] SourceArgs = { "/s", "-s", "/source", "--source" };

        public static bool ForceApproval { get; set; }
        static string[] ForceArgs = { "/f", "-f", "/force", "--force" };

        public static bool IgnoreSslErrors { get; set; }
        private static string[] IgnoreSslErrorsArgs = {"/ignore-ssl-errors", "--ignore-ssl-errors"};

        static string[] NameArgs = { "/name", "--name", "-name" };

        static string[] DeleteArgs = { "/delete", "--delete", "-delete" };
        static string[] ReplaceArgs = { "/replace", "--replace", "-replace" };

        static string[] HelpArgs = { "/help", "--help", "-help", "?" };

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

        private static void PrintGistLinks(string tool, List<GistLink> links, string tag = null)
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
                $" {i++.ToString().PadLeft(3, ' ')}. {link.Name.PadRight(padName, ' ')} {link.Description.PadRight(padDesc, ' ')} to: {link.To.PadRight(padTo, ' ')} by @{link.User.PadRight(padBy, ' ')} {link.ToTagsString()}"
                    .Print();
            }

            "".Print();

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

            "".Print();
        }

        class GistLink
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public string User { get; set; }
            public string To { get; set; }
            public string Description { get; set; }

            public string[] Tags { get; set; }

            public string ToTagsString() => Tags == null ? "" : $"[" + string.Join(",", Tags) + "]";

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

                        var toPath =
                            (token is JsObjectExpression obj
                                ? obj.Properties.FirstOrDefault(x => x.Key is JsIdentifier key && key.Name == "to")
                                    ?.Value as JsLiteral
                                : null)?.Value?.ToString();

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
                var gistLink = links.FirstOrDefault(x => x.Name.Replace("-", "").EqualsIgnoreCase(sanitizedAlias));
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

        private static ConcurrentDictionary<string, List<GistLink>> GistLinksCache =
            new ConcurrentDictionary<string, List<GistLink>>();

        private static List<GistLink> GetGistApplyLinks() => GetGistLinks(GistLinksId, "apply.md");

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
                var gistLink = GistLink.Get(links, gistAlias);
                if (gistLink == null)
                {
                    $"No match found for '{gistAlias}', available gists:".Print();
                    PrintGistLinks(tool, links);
                    return false;
                }
                            
                WriteGistFile(gistLink.Url, gistAlias, to: gistLink.To, projectName: projectName, getUserApproval: UserInputYesNo);
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
            foreach (var gistAlias in gistAliases)
            {
                var gistLink = GistLink.Get(links, gistAlias);
                if (gistLink == null)
                {
                    $"No match found for '{gistAlias}', available gists:".Print();
                    PrintGistLinks(tool, links);
                }

                var gistId = gistLink.Url;
                var gistLinkUrl = $"https://gist.github.com/{gistId}";
                if (gistId.IsUrl())
                {
                    gistLinkUrl = gistId;
                    gistId = gistId.LastRightPart('/');
                }

                var alias = !string.IsNullOrEmpty(gistAlias)
                    ? $"'{gistAlias}' "
                    : "";
                var exSuffix = $" required by {alias}{gistLinkUrl}";

                var gistFiles = GitHubUtils.Gateway.GetGistFiles(gistId);
                var basePath = ResolveBasePath(gistLink.To, exSuffix);

                var resolvedFiles = new List<string>();
                foreach (var gistFile in gistFiles)
                {
                    var resolvedFile = ResolveFilePath(gistFile.Key, basePath, projectName, gistLink.To);
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
                    sb.AppendLine($"Delete {resolvedFiles.Count} files from {label}{gistLinkUrl}:");
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
            var silentMode = getUserApproval == null;
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
                try
                {
                    File.Delete(resolvedFile);
                    folders.Add(Path.GetDirectoryName(resolvedFile));
                }
                catch (Exception ex)
                {
                    if (Verbose) $"ERROR: {ex.Message}".Print();
                }
            }

            // Delete empty folders that had gist files
            var subFoldersFirst = folders.OrderByDescending(x => x);
            folders = new HashSet<string>();
            foreach (var folder in subFoldersFirst)
            {
                if (Directory.GetFiles(folder).Length == 0 && Directory.GetDirectories(folder).Length == 0)
                {
                    if (Verbose) $"Deleting folder {folder} ...".Print();
                    try
                    {
                        DeleteDirectoryRecursive(folder);
                    }
                    catch (Exception ex)
                    {
                        if (Verbose) $"ERROR: {ex.Message}".Print();
                    }
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
            var useFileName = ReplaceMyApp(gistFilePath, projectName);
            if (useFileName.EndsWith("?"))
                useFileName = useFileName.Substring(0, useFileName.Length - 1);

            var resolvedFile = Path.GetFullPath(useFileName, basePath.Replace("\\", "/"));

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

                File.WriteAllText(resolvedFile.Key, resolvedFile.Value);
            }
        }


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
                    
                    "Simple Usage:  ".Print();
                    
                    $"   mix <name> <name> ...".Print();
                    
                    "".Print();

                    "Mix using numbered list index instead:".Print();
                    
                    $"   mix 1 3 5 ...".Print();
                    
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
                var replaceStatSuffix = ReplaceTokens.Count > 0 ? $"_replace{ReplaceTokens.Count}" : "";
                if (args[0].FirstCharEquals('#'))
                {
                    RegisterStat(tool, args[0], "search");
                    PrintGistLinks(tool, GetGistApplyLinks(), args[0].Substring(1));
                }
                else
                {
                    if (dotnetArgs.Count == 1 && dotnetArgs[0].IndexOf('+') >= 0)
                        dotnetArgs = dotnetArgs[0].Split('+').ToList();

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
    }

    public static class GitHubUtils
    {
        public const string UserAgent = "web dotnet tool";
        
        public static GitHubGateway Gateway { get; } = new GitHubGateway {
            UserAgent = UserAgent,
            GetJsonFilter = GetJson
        };
            
        public static string GetJson(string apiUrl)
        {
            if (Startup.Verbose) 
                $"API: {apiUrl}".Print();
                
            return apiUrl.GetJsonFromUrl(req => req.ApplyRequestFilters());
        }

        public static ConcurrentDictionary<string, Dictionary<string, string>> GistFilesCache =
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
                        to[entry.Key] = (string) meta["content"];
                    }

                    return to;
                }

                throw new NotSupportedException($"Invalid gist response returned for '{gistKey}'");
            });
        }
    }

    
}