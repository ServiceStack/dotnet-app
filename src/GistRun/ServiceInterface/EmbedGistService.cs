using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using GistRun.ServiceModel;
using ServiceStack;
using ServiceStack.Text;

namespace GistRun.ServiceInterface
{
    [FallbackRoute("/{PathInfo*}", Matches="AcceptsHtml")]
    public class Fallback : IReturn<string>
    {
        public string PathInfo { get; set; }
    }
    
    public class EmbedGistService : Service
    {
        public AppConfig AppConfig { get; set; }
        public GistCache GistCache { get; set; }

        public object Any(Fallback request)
        {
            var path = request.PathInfo;
            if (path?.Length > 0 && path.IndexOf('.') == -1)
            {
                var pos = path.IndexOf('/');
                var isGistId = (pos == 32 && path.Length == 73) || (pos == 20 && path.Length == 61)
                    || (path.Length == 32 || path.Length == 20);
                if (isGistId)
                    return new HttpResult(new FileInfo("wwwroot/embed.html"));
            }
            return new HttpResult(new FileInfo("wwwroot/index.html"));
        }
        
        public async Task WriteErrorJs(Exception ex, int height)
        {
            var title = ex.GetType().Name.SplitCamelCase().Replace('_', ' ');
            var body = ex.Message;
            
            if (ex is WebException webEx)
            {
                var httpStatus = webEx.GetStatus();
                if (httpStatus == HttpStatusCode.NotFound)
                {
                    title = "404 Not Found";
                    body = "This gist no longer exists.";
                }
                else if (httpStatus != null)
                {
                    title = $"{(int) httpStatus} {httpStatus}";
                }
            }

            string enc(string txt) => txt.Replace('`', '\'').HtmlEncode();

            var errorHtml = $"<div><h4 style=\"color:#c00\">{enc(title)}</h4><p>{enc(body)}</p></div>";
            var html = $"document.write(`<div style=\"height:{height}px;box-sizing:border-box;display:flex;align-items:center;justify-content:center;border:2px solid #ddd;margin:1em 0;padding:1em;\">{errorHtml}</div>`)";
            await Response.WriteAsync(html);
        }
        
        public async Task Any(EmbedGist request)
        {
            Response.ContentType = "application/javascript";
            var height = request.Height ?? 750;
            try
            {
                var hasVersion = !string.IsNullOrEmpty(request.Version);
                var gistVersion = (hasVersion
                    ? $"{request.Id}/{request.Version}"
                    : request.Id).LeftPart('.');

                var gist = await GistCache.GetGistAsync(gistVersion, nocache:false);
                var requiredQs = $"?gist={gistVersion}";
                if (string.IsNullOrEmpty(Request.QueryString["title"]))
                    requiredQs += $"&title={gist.Description.UrlEncode()}";
                if (string.IsNullOrEmpty(Request.QueryString["user"]))
                    requiredQs += $"&user={gist.Owner.Login}";
                
                var embedUrl = Request.ResolveAbsoluteUrl($"/embed{requiredQs}")
                    + (Request.RawUrl.IndexOf('?') >= 0 ? "&" + Request.RawUrl.RightPart('?') : "");
                var iframe = $"<iframe src=\"{embedUrl}\" style=\"height:{height}px;width:100%;border:1px solid #ddd\"></iframe>";
                var html = $"document.write(`{iframe}`)";

                if (AppConfig.CacheLatestGistCheckSecs > 0)
                {
                    Response.AddHeader(HttpHeaders.CacheControl, $"max-age={AppConfig.CacheLatestGistCheckSecs}");
                }
                await Response.WriteAsync(html);
            }
            catch (Exception e)
            {
                await WriteErrorJs(e, height);
            }
            Response.EndRequest();
        }
    }
}