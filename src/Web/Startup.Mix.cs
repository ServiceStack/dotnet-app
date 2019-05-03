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

            if (tool == "mix")
            {
                $" Usage:  mix <name> <name> ...".Print();

                "".Print();

                $"Search:  mix #<tag> Available tags: {string.Join(", ", tags)}".Print();
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
            var gistsIndex = new GithubGateway().GetGistFiles(gistId)
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

        public static string UserAgent = typeof(GithubGateway).Namespace.LeftPart('.');

        public partial class GithubGateway
        {
            public const string GithubApiBaseUrl = "https://api.github.com/";

            public string UnwrapRepoFullName(string orgName, string name)
            {
                try
                {
                    var repo = GetJson<GithubRepo>($"/repos/{orgName}/{name}");
                    if (repo.Fork)
                    {
                        
                        if (Startup.Verbose) $"'{orgName}/{repo.Name}' is a fork.".Print();
                        if (repo.Parent == null)
                        {
                            if (Startup.Verbose) $"Could not find parent fork for '{orgName}/{repo.Name}', using '{repo.Full_Name}'".Print(); 
                        }
                        else
                        {
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
                        return (string) zipUrl;
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
                var map = new Dictionary<string, GithubRepo>();

                var userRepos = GetJsonCollectionAsync<List<GithubRepo>>($"users/{githubOrgOrUser}/repos");
                var orgRepos = GetJsonCollectionAsync<List<GithubRepo>>($"orgs/{githubOrgOrUser}/repos");

                try
                {
                    foreach (var repos in await userRepos)
                    foreach (var repo in repos)
                        map[repo.Name] = repo;
                }
                catch (Exception e)
                {
                    if (!e.IsNotFound()) throw;
                }

                try
                {
                    foreach (var repos in await userRepos)
                    foreach (var repo in repos)
                        map[repo.Name] = repo;
                }
                catch (Exception e)
                {
                    if (!e.IsNotFound()) throw;
                }

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

                return apiUrl.GetJsonFromUrl(req => req.ApplyRequestFilters());
            }

            public T GetJson<T>(string route) => GetJson(route).FromJson<T>();

            public IEnumerable<T> StreamJsonCollection<T>(string route)
            {
                List<T> results;
                var nextUrl = GithubApiBaseUrl.CombineWith(route);

                do
                {
                    if (Startup.Verbose) $"API: {nextUrl}".Print();

                    results = nextUrl.GetJsonFromUrl(req => req.ApplyRequestFilters(),
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

                    results = (await nextUrl.GetJsonFromUrlAsync(req => req.ApplyRequestFilters(),
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

            ConcurrentDictionary<string, Dictionary<string, string>> GistFilesCache =
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

        public static bool ApplyGists(string tool, string[] gistAliases)
        {
            var links = GetGistApplyLinks();
            
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
                
                    resolvedAliases.Add(links[index-1].Name);
                }
                gistAliases = resolvedAliases.ToArray();
            }
            
            foreach (var gistAlias in gistAliases)
            {
                var gistLink = GistLink.Get(links, gistAlias);
                if (gistLink == null)
                {
                    $"No match found for '{gistAlias}', available gists:".Print();
                    PrintGistLinks(tool, links);
                    return false;
                }
                            
                var currentDirName = new DirectoryInfo(Environment.CurrentDirectory).Name;
                WriteGistFile(gistLink.Url, gistAlias, to: gistLink.To, projectName: currentDirName, getUserApproval: UserInputYesNo);
                ForceApproval = true; //If written once user didn't cancel, assume approval for remaining gists
            }
            return true;
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

        public static async Task Mix(string[] args)
        {
            InitMix();
            
            var dotnetArgs = new List<string>();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
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
                
                    
                dotnetArgs.Add(arg);
            }

            var tool = "mix";
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

                    RegisterStat(tool, string.Join("_", dotnetArgs.OrderBy(x => x)), "+");
                    ApplyGists(tool, dotnetArgs.ToArray());
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
            req.UserAgent = Startup.UserAgent;

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
}