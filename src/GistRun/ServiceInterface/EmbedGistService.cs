using System;
using System.Net;
using System.Threading.Tasks;
using GistRun.ServiceModel;
using ServiceStack;
using ServiceStack.Text;

namespace GistRun.ServiceInterface
{
    public class EmbedGistService : Service
    {
        public AppConfig AppConfig { get; set; }
        public GistCache GistCache { get; set; }

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
            var height = request.Height ?? 600;
            try
            {
                var hasVersion = !string.IsNullOrEmpty(request.Version);
                var gistVersion = (hasVersion
                    ? $"{request.Id}/{request.Version}"
                    : request.Id).LeftPart('.');

                
                var gist = await GistCache.GetGistAsync(gistVersion, nocache:false);
                var embedUrl = Request.ResolveAbsoluteUrl($"/embed?gist={gistVersion}&title={gist.Description.UrlEncode()}&user={gist.Owner.Login}")
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