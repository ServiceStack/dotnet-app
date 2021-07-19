using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Apps.ServiceInterface.Langs;
using ServiceStack;
using ServiceStack.Text;

namespace Apps.ServiceInterface
{
    public class SiteInfo
    {
        public SiteInfo() => Languages = new Languages(this);
        public string BaseUrl { get; set; }
        public string Slug { get; set; }
        public string Name { get; set; }
        public AppMetadata Metadata { get; set; }
        public List<string> Plugins { get; set; }
        public List<string> Auth { get; set; }
        public DateTime AddedDate { get; set; }
        public DateTime AccessDate { get; set; }
        public Languages Languages { get; }
    }

    public class Languages
    {
        public SiteInfo Site { get; }
        public Languages(SiteInfo site) => Site = site;

        public ConcurrentDictionary<string, LanguageInfo> Map { get; set; } = new();

        public async Task<LanguageInfo> GetLangContentAsync(string lang, string includeTypes = null)
        {
            try
            {
                var langTypesUrl = Site.BaseUrl.CombineWith("types", lang);
                var useGlobalNs = lang == "csharp" || lang == "fsharp" || lang == "vbnet";
                if (useGlobalNs)
                    langTypesUrl += "?GlobalNamespace=MyApp";
                if (lang == "java" || lang == "kotlin")
                    langTypesUrl += "?Package=myapp";

                if (includeTypes != null)
                    langTypesUrl += (langTypesUrl.IndexOf('?') >= 0 ? "&" : "?") + $"IncludeTypes={includeTypes}";

                var content = await langTypesUrl
                    .GetStringFromUrlAsync(requestFilter: req => req.UserAgent = "apps.servicestack.net");
                return new LanguageInfo(this, lang, langTypesUrl, content);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<LanguageInfo> GetLanguageInfoAsync(string lang)
        {
            if (Map.TryGetValue(lang, out var info))
                return info;

            info = await GetLangContentAsync(lang);
            Map[lang] = info;
            return info;
        }

        public async Task<IDictionary<string, LanguageInfo>> GetLanguageInfosAsync()
        {
            var langTasks = Sites.Languages
                .Where(lang => !Map.ContainsKey(lang))
                .Map(async lang => KeyValuePair.Create(lang, await GetLangContentAsync(lang)));

            if (langTasks.Count > 0)
            {
                var results = await Task.WhenAll(langTasks);
                foreach (var result in results)
                {
                    Map[result.Key] = result.Value;
                }
            }

            return Map;
        }
    }

    public class LanguageInfo
    {
        public LanguageInfo(Languages languages, string code, string url, string content)
        {
            Languages = languages;
            Code = code;
            Url = url;
            Content = content;
        }

        public Languages Languages { get; }
        public string Code { get; }
        public string Url { get; }
        public string Content { get; }

        public ConcurrentDictionary<string, LanguageInfo> RequestMap { get; set; } = new();

        public async Task<LanguageInfo> ForRequestAsync(string includeTypes)
        {
            if (RequestMap.TryGetValue(includeTypes, out var requestLanguage))
                return requestLanguage;

            requestLanguage = await Languages.GetLangContentAsync(Code, includeTypes);
            RequestMap[includeTypes] = requestLanguage;
            return requestLanguage;
        }
    }

    public class Sites
    {
        public static Sites Instance = new();

        public static string[] Languages = {
            "typescript",
            "csharp",
            "python",
            "dart",
            "java",
            "kotlin",
            "swift",
            "vbnet",
            "fsharp",
        };

        internal ConcurrentDictionary<string, SiteInfo> Map = new(StringComparer.OrdinalIgnoreCase);

        public SiteInfo FindSite(string slug) => Map.Values.FirstOrDefault(x => x.Slug == slug);

        public void RemoveSite(string slug)
        {
            var useBaseUrl = SiteUtils.UrlFromSlug(slug);
            if (!Map.TryRemove(useBaseUrl, out _))
            {
                var site = FindSite(slug);
                Map.TryRemove(site.BaseUrl, out _);
            }
        }

        public async Task<SiteInfo> GetSiteAsync(string slug)
        {
            var site = FindSite(slug);
            if (site != null)
                return site;

            var useBaseUrl = SiteUtils.UrlFromSlug(slug);
            var appMetadata = await useBaseUrl.GetAppMetadataAsync();

            var siteInfo = new SiteInfo {
                BaseUrl = useBaseUrl,
                Slug = slug,
                Metadata = appMetadata,
                AddedDate = DateTime.Now,
                AccessDate = DateTime.Now,
            };

            Map[useBaseUrl] = siteInfo;
            return siteInfo;
        }

        public async Task<SiteInfo> AssertSiteAsync(string slug) => string.IsNullOrEmpty(slug)
            ? throw new ArgumentNullException(nameof(SiteInfo.Slug))
            : await GetSiteAsync(slug) ?? throw HttpError.NotFound("Site does not exist");
        
        private static char[] WildcardChars = {'*', ',', '{'};
        private static HashSet<string> AutoQueryDtoNames = new() {"QueryDb`1", "QueryDb`2", "QueryData`1", "QueryData`2"};

        public async Task<JupyterNotebook> CreateNotebookAsync(string slug, string requestDto = null, string requestArgs = null)
        {
            var site = await AssertSiteAsync(slug);

            var includeTypes = requestDto == null
                ? null
                : requestDto.IndexOfAny(WildcardChars) >= 0
                    ? requestDto
                    : requestDto + ".*";

            var baseUrl = SiteUtils.UrlFromSlug(slug);
            var lang = await site.Languages.GetLangContentAsync("python", includeTypes);
            var srcLines = lang.Content.ReadLines().Map(x => x);
            srcLines.Add("");
            srcLines.Add("");
            srcLines.Add("from IPython.core.display import display, HTML");
            srcLines.Add("");
            srcLines.Add($"client = JsonServiceClient(\"{baseUrl}\")");

            var to = new JupyterNotebook {
                Cells = new List<JupyterCell> {
                    new() {
                        Source = srcLines
                    },
                }
            };

            if (requestDto != null)
            {
                var requestBody = "";
                var args = ParseJsRequest(requestArgs);
                if (args != null)
                {
                    var python = new PythonLangInfo();
                    var argsStringMap = args.ToStringDictionary();
                    requestBody = python.RequestBody(requestDto, argsStringMap, site.Metadata.Api);
                }
                var requestOp = site.Metadata.Api.Operations.FirstOrDefault(x => x.Request.Name == requestDto);
                var clientMethod = (requestOp?.Actions?.FirstOrDefault() != null
                        ? (requestOp.Actions.First().EqualsIgnoreCase("ANY")
                            ? null
                            : requestOp.Actions.First().ToLower())
                    : null) ?? "send";
                to.Cells.Add(new() {
                        Source = {
                            $"response = client.{clientMethod}({requestDto}({requestBody}))"
                        }
                    }
                );
                to.Cells.Add(new() {
                        Source = {
                            "display(HTML(htmldump(response)))"
                        }
                    }
                );
                var response = requestOp?.Response;
                if (response != null && response.Properties != null)
                {
                    var hasResults = response.Properties.FirstOrDefault(x => x.Name.EqualsIgnoreCase("Results")) != null;
                    if (hasResults)
                    {
                        var resultsCell = new JupyterCell {
                            Source = {
                                "printtable(response.results)"
                            }
                        };
                        var baseClass = requestOp.Request.Inherits?.Name;
                        if (baseClass != null && AutoQueryDtoNames.Contains(baseClass))
                        {
                            var responseModel = requestOp.Request.Inherits.GenericArgs.Last();
                            var dataModel = site.Metadata.Api.Types.FirstOrDefault(x => x.Name == responseModel);
                            if (dataModel != null)
                            {
                                var propNames = dataModel.Properties.Map(x => 
                                    '"' + x.Name.SplitCamelCase().ToLower().Replace(" ","_") + '"');
                                resultsCell = new JupyterCell {
                                    Source = {
                                        "printtable(response.results,",
                                        $"           headers=[{string.Join(",", propNames)}])"
                                    }
                                };
                            }
                        }
                        to.Cells.Add(resultsCell);
                    }
                }
            }
            else
            {
                to.Cells.Add(new() {
                        Source = {
                            $"# response = client.send(MyRequest())"
                        }
                    }
                );
                to.Cells.Add(new() {
                        Source = {
                            "# display(HTML(htmldump(response)))"
                        }
                    }
                );
            }
            
            
            return to;
        }

        public static Dictionary<string, object> ParseJsRequest(string requestArgs)
        {
            if (!string.IsNullOrEmpty(requestArgs))
            {
                try
                {
                    var ret = JS.eval(requestArgs);
                    return (Dictionary<string, object>)ret;
                }
                catch (Exception e)
                {
                    throw new Exception("Request args should be a valid JavaScript Object literal");
                }
            }
            return null;
        }

        public static readonly List<string> VerbMarkers = new[]{ nameof(IGet), nameof(IPost), nameof(IPut), nameof(IDelete), nameof(IPatch) }.ToList();
        public static string InferRequestMethod(MetadataOperationType op)
        {
            var method = op.Request.Implements?.FirstOrDefault(x => VerbMarkers.Contains(x.Name))?.Name.Substring(1).ToUpper();
            if (method == null)
            {
                if (IsAutoQuery(op))
                    return HttpMethods.Get;
            }
            return method;
        }
        
        public static readonly List<string> AutoQueryBaseTypes = new[] { "QueryDb`1", "QueryDb`2", "QueryData`1", "QueryData`2" }.ToList();

        public static bool IsAutoQuery(MetadataOperationType op)
        {
            return op.Request.Inherits != null && AutoQueryBaseTypes.Contains(op.Request.Inherits.Name);            
        }

    }


    [DataContract]
    public class JupyterNotebook
    {
        [DataMember(Name = "cells")]
        public List<JupyterCell> Cells { get; set; }

        [DataMember(Name = "metadata")]
        public JupyterMetadata Metadata { get; set; }

        [DataMember(Name = "nbformat")]
        public int Nbformat { get; set; }

        [DataMember(Name = "nbformat_minor")]
        public int NbformatMinor { get; set; }
    }

    [DataContract]
    public class JupyterOutput
    {
        [DataMember(Name = "name")]
        public string Name { get; set; } = "stdout";

        [DataMember(Name = "output_type")]
        public string OutputType { get; set; } = "stream"; // display_data

        [DataMember(Name = "text")]
        public List<string> Text { get; set; }

        [DataMember(Name = "data")]
        public Dictionary<string, List<string>>
            Data { get; set; } //= text/html => [src_lines], text/plain => [src_lines]

        [DataMember(Name = "metadata")]
        public Dictionary<string, string> Metadata { get; set; }
    }

    [DataContract]
    public class JupyterMetadata
    {
        [DataMember(Name = "interpreter")]
        public JupyterInterpreter Interpreter { get; set; }

        [DataMember(Name = "kernelspec")]
        public JupyterKernel Kernelspec { get; set; }

        [DataMember(Name = "language_info")]
        public JupyterLanguageInfo LanguageInfo { get; set; }

        [DataMember(Name = "orig_nbformat")]
        public int OrigNbformat { get; set; } = 4;
    }

    [DataContract]
    public class JupyterInterpreter
    {
        [DataMember(Name = "hash")]
        public string Hash { get; set; } = "63fd5069d213b44bf678585dea6b12cceca9941eaf7f819626cde1f2670de90d";
    }

    [DataContract]
    public class JupyterKernel
    {
        [DataMember(Name = "display_name")]
        public string DisplayName { get; set; } = "Python 3.9.6 64-bit";

        [DataMember(Name = "name")]
        public string Name { get; set; } = "python3";
    }

    [DataContract]
    public class JupyterLanguageInfo
    {
        [DataMember(Name = "codemirror_mode")]
        public JupyterCodemirrorMode CodemirrorMode { get; set; } = new();

        [DataMember(Name = "file_extension")]
        public string FileExtension { get; set; } = "py";

        [DataMember(Name = "mimetype")]
        public string Mimetype { get; set; } = "text/x-python";

        [DataMember(Name = "name")]
        public string Name { get; set; } = "python";

        [DataMember(Name = "nbconvert_exporter")]
        public string NbconvertExporter { get; set; } = "python";

        [DataMember(Name = "pygments_lexer")]
        public string PygmentsLexer { get; set; } = "ipython3";

        [DataMember(Name = "version")]
        public string Version { get; set; } = "3.9.6";
    }

    [DataContract]
    public class JupyterCodemirrorMode
    {
        [DataMember(Name = "name")]
        public string Name { get; set; } = "ipython";

        [DataMember(Name = "version")]
        public int Version { get; set; } = 3;
    }

    [DataContract]
    public class JupyterCell
    {
        [DataMember(Name = "cell_type")]
        public string CellType { get; set; } = "code";

        [DataMember(Name = "execution_count")]
        public int ExecutionCount { get; set; } = 1;

        [DataMember(Name = "metadata")]
        public Dictionary<string, string> Metadata { get; set; }

        [DataMember(Name = "outputs")]
        public List<JupyterOutput> Outputs { get; set; }

        [DataMember(Name = "source")]
        public List<string> Source { get; set; } = new();
    }
}