using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Apps.ServiceInterface.Langs;
using ServiceStack;

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

        public async Task<LanguageInfo> GetLangContentAsync(string lang, string includeTypes = null, bool excludeNamespace = false)
        {
            try
            {
                var langTypesUrl = Site.BaseUrl.CombineWith("types", lang);
                var useGlobalNs = lang is "csharp" or "fsharp" or "vbnet";
                if (useGlobalNs)
                    langTypesUrl += "?GlobalNamespace=MyApp";
                if (lang == "java" || lang == "kotlin")
                    langTypesUrl += "?Package=myapp";

                if (includeTypes != null && includeTypes != "*")
                    langTypesUrl += (langTypesUrl.IndexOf('?') >= 0 ? "&" : "?") + $"IncludeTypes={includeTypes}";
                if (excludeNamespace)
                    langTypesUrl += (langTypesUrl.IndexOf('?') >= 0 ? "&" : "?") + "ExcludeNamespace=true";

                var content = await langTypesUrl
                    .GetStringFromUrlAsync(requestFilter: req => req.UserAgent = "apps.servicestack.net");
                return new LanguageInfo(this, lang, langTypesUrl, content);
            }
            catch (Exception ex)
            {
                throw;
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

        public static string RemoveHeaderCommentsFromDtos(string lang, string langContent)
        {
            var dtosOnly = lang switch {
                "python" => langContent.Substring(20).RightPart("\"\"\""),
                "fsharp" => langContent.RightPart("*)"),
                "vbnet" => langContent.RightPart("\n\n"),
                _ => langContent.RightPart("*/")
            };
            return dtosOnly;
        }

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
        
        private static readonly char[] WildcardChars = {'*', ',', '{'};

        public async Task<JupyterNotebook> CreateNotebookAsync(LangInfo lang, string slug, string includeTypes = null, string requestDto = null, string requestArgs = null)
        {
            var site = await AssertSiteAsync(slug);

            if (string.IsNullOrEmpty(includeTypes))
            {
                includeTypes = requestDto == null
                    ? null
                    : requestDto.IndexOfAny(WildcardChars) >= 0
                        ? requestDto
                        : requestDto + ".*";
            }

            var languageInfo = await site.Languages.GetLangContentAsync(lang.Code, includeTypes, excludeNamespace:true);
            var langContent = LanguageInfo.RemoveHeaderCommentsFromDtos(lang.Code, languageInfo.Content);

            return lang.CreateNotebook(site, langContent, requestDto, requestArgs);
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

        private static readonly List<string> AutoQueryBaseTypes = new[] { "QueryDb`1", "QueryDb`2", "QueryData`1", "QueryData`2" }.ToList();

        public static bool IsAutoQuery(MetadataOperationType op)
        {
            return op.Request.Inherits != null && AutoQueryBaseTypes.Contains(op.Request.Inherits.Name);            
        }
    }
}