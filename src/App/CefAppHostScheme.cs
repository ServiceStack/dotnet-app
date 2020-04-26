using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.Script;
using ServiceStack.Text;
using Web;
using Xilium.CefGlue;

namespace WebApp
{
    public class CefAppHostSchemeHandlerFactory : CefSchemeHandlerFactory
    {
        private readonly IAppHost appHost;
        public CefAppHostSchemeHandlerFactory(IAppHost appHost) => this.appHost = appHost;
        protected override CefResourceHandler Create(CefBrowser browser, CefFrame frame, string schemeName, CefRequest request)
        {
            return new CefAppHostSchemeResourceHandler(appHost);
        }
    }
    
    public class CefAppHostSchemeResourceHandler : CefResourceHandler
    {
        private IAppHost appHost;
        public CefAppHostSchemeResourceHandler(IAppHost appHost) => this.appHost = appHost;
        
        private long contentLength;
        private NameValueCollection responseHeaders = new NameValueCollection();
        private ReadOnlyMemory<byte> responseMemoryBytes;
        private int responseStatus;
        private string responseStatusText;
        private MemoryStream ms;
        private string responseContentType;
        private string responseCharSet = "UTF-8";
        private const int Unknown = -1;
        private int pos;

        protected override bool Open(CefRequest request, out bool handleRequest, CefCallback callback)
        {
            // Backwards compatibility. ProcessRequest will be called.
            callback.Dispose();
            handleRequest = false;
            return false;
        }

        [Obsolete("This method is deprecated. Use Open instead.")]
        protected override bool ProcessRequest(CefRequest request, CefCallback callback)
        {
            try
            {
                var uri = new Uri(request.Url);
                var pathInfo = uri.AbsolutePath.Trim('/');
                var qs = !string.IsNullOrEmpty(uri.Query)
                    ? PclExportClient.Instance.ParseQueryString(uri.Query)
                    : null;

                var target = pathInfo.LeftPart('/');
                if (target == "script")
                {
                    string method = null;
                    string script = null;

                    if (qs?.Count > 0)
                    {
                        method = qs.AllKeys[0];
                        script = qs[method];
                    }
                    else if (request.PostData?.Count > 0)
                    {
                        var postEls = request.PostData?.GetElements();
                        if (postEls?.Length > 0)
                        {
                            var firstEl = postEls[0];
                            var requestBytes = firstEl.ElementType switch {
                                CefPostDataElementType.Empty => TypeConstants.EmptyByteArray,
                                CefPostDataElementType.File => File.ReadAllBytes(firstEl.GetFile()),
                                CefPostDataElementType.Bytes => firstEl.GetBytes(),
                                _ => throw new NotSupportedException($"{firstEl.ElementType}")
                            };

                            method = pathInfo.RightPart('/').LeftPart(pathInfo);
                            script = MemoryProvider.Instance.FromUtf8(requestBytes).ToString();
                        }
                    }

                    void setResult(object value, string resultType=" result")
                    {
                        responseContentType = MimeTypes.Json;
                        ms = MemoryStreamFactory.GetStream();
                        JsonSerializer.SerializeToStream(value, ms);
                        responseMemoryBytes = ms.GetBufferAsMemory();
                        contentLength = responseMemoryBytes.Length;
                        
                        responseStatus = 200;
                        responseStatusText = method + resultType;
                        callback.Continue();
                    }
                    
                    void setOutput(PageResult result)
                    {
                        responseContentType = MimeTypes.PlainText;
                        ms = MemoryStreamFactory.GetStream();
                        result.RenderToStream(ms);
                        responseMemoryBytes = ms.GetBufferAsMemory();
                        contentLength = responseMemoryBytes.Length;
                        
                        responseStatus = 200;
                        responseStatusText = method + " result";
                        callback.Continue();
                    }
                        
                    if (method.EqualsIgnoreCase(nameof(ScriptTemplateUtils.EvaluateScript)))
                        setResult(appHost.ScriptContext.Evaluate(script));
                    else if (method.EqualsIgnoreCase(nameof(ScriptTemplateUtils.RenderScript)))
                        setOutput(new PageResult(appHost.ScriptContext.SharpScriptPage(script)));
                        
                    else if (method.EqualsIgnoreCase(nameof(ScriptCodeUtils.EvaluateCode)))
                        setResult(appHost.ScriptContext.EvaluateCode(ScriptCodeUtils.EnsureReturn(script)));
                    else if (method.EqualsIgnoreCase(nameof(ScriptCodeUtils.RenderCode)))
                        setOutput(new PageResult(appHost.ScriptContext.CodeSharpPage(script)));
                        
                    else if (method.EqualsIgnoreCase(nameof(ScriptLispUtils.EvaluateLisp)))
                        setResult(appHost.ScriptContext.EvaluateLisp(ScriptLispUtils.EnsureReturn(script)));
                    else if (method.EqualsIgnoreCase(nameof(ScriptLispUtils.RenderLisp)))
                        setOutput(new PageResult(appHost.ScriptContext.LispSharpPage(script)));

                    if (responseContentType != null)
                        return true;

                    async Task setResultAsync(Task<object> valueTask, string resultType=" result")
                    {
                        try
                        {
                            responseContentType = MimeTypes.Json;
                            ms = MemoryStreamFactory.GetStream();
                            JsonSerializer.SerializeToStream(await valueTask, ms);
                            responseMemoryBytes = ms.GetBufferAsMemory();
                            contentLength = responseMemoryBytes.Length;
                        
                            responseStatus = 200;
                            responseStatusText = method + resultType;
                            callback.Continue();
                        }
                        catch (Exception e)
                        {
                            HandleException(callback, e);
                        }
                    }
                    
                    async Task setOutputAsync(PageResult result)
                    {
                        try
                        {
                            responseContentType = MimeTypes.PlainText;
                            ms = MemoryStreamFactory.GetStream();
                            await result.RenderToStreamAsync(ms);
                            responseMemoryBytes = ms.GetBufferAsMemory();
                            contentLength = responseMemoryBytes.Length;

                            responseStatus = 200;
                            responseStatusText = method + " async result";
                            callback.Continue();
                        }
                        catch (Exception e)
                        {
                            HandleException(callback, e);
                        }
                    }

                    if (method.EqualsIgnoreCase(nameof(ScriptTemplateUtils.EvaluateScriptAsync)))
                        Task.Run(async () => await setResultAsync(appHost.ScriptContext.EvaluateAsync(script), " async result"));
                    else if (method.EqualsIgnoreCase(nameof(ScriptTemplateUtils.RenderScriptAsync)))
                        Task.Run(async () => await setOutputAsync(new PageResult(appHost.ScriptContext.SharpScriptPage(script))));

                    else if (method.EqualsIgnoreCase(nameof(ScriptCodeUtils.EvaluateCodeAsync)))
                        Task.Run(async () => await setResultAsync(appHost.ScriptContext.EvaluateCodeAsync(ScriptCodeUtils.EnsureReturn(script)), " async result"));
                    else if (method.EqualsIgnoreCase(nameof(ScriptCodeUtils.RenderCodeAsync)))
                        Task.Run(async () => await setOutputAsync(new PageResult(appHost.ScriptContext.CodeSharpPage(script))));

                    else if (method.EqualsIgnoreCase(nameof(ScriptLispUtils.EvaluateLispAsync)))
                        Task.Run(async () => await setResultAsync(appHost.ScriptContext.EvaluateLispAsync(ScriptLispUtils.EnsureReturn(script)), " async result"));
                    else if (method.EqualsIgnoreCase(nameof(ScriptLispUtils.RenderLispAsync)))
                        setOutput(new PageResult(appHost.ScriptContext.LispSharpPage(script)));
                    else throw new NotSupportedException($"Unsupported script API '{method}', supported: " +
                        "EvaluateScript/Async, EvaluateCode/Async, EvaluateLisp/Async");

                    return true;
                }
                
                throw new NotSupportedException($"Unknown host api: {uri.Host}, supported: script");
            }
            catch (Exception e)
            {
                HandleException(callback, e);
                return true;
            }
        }

        private void HandleException(CefCallback callback, Exception e)
        {
            if (Startup.Verbose) 
                Console.WriteLine(e);
            responseStatus = 500;
            responseStatusText = e.GetType().Name;
            responseContentType = MimeTypes.PlainText;
            responseMemoryBytes = MemoryProvider.Instance.ToUtf8(e.ToString());
            contentLength = responseMemoryBytes.Length;
            callback.Continue();
        }

        protected override void GetResponseHeaders(CefResponse response, out long responseLength, out string redirectUrl)
        {
            response.Status = responseStatus;
            response.StatusText = responseStatusText;
            response.MimeType = responseContentType;
            response.Charset = responseCharSet;

            response.SetHeaderMap(responseHeaders);
            responseLength = contentLength;
            redirectUrl = responseHeaders[HttpHeaders.Location];
        }

        protected override bool Skip(long bytesToSkip, out long bytesSkipped, CefResourceSkipCallback callback)
        {
            bytesSkipped = (long)CefErrorCode.Failed;
            return false;
        }

        protected override bool Read(IntPtr dataOut, int bytesToRead, out int bytesRead, CefResourceReadCallback callback)
        {
            // Backwards compatibility. ReadResponse will be called.
            callback.Dispose();
            bytesRead = -1;
            return false;
        }

        protected override bool ReadResponse(Stream response, int bytesToRead, out int bytesRead, CefCallback callback)
        {
            if (bytesToRead == 0 || pos >= contentLength)
            {
                Dispose();
                bytesRead = 0;
                return false;
            }

            var toRead = (int)Math.Min(contentLength - pos, bytesToRead);
            response.Write(responseMemoryBytes.Span.Slice(pos, toRead));
            pos += toRead;
            bytesRead = toRead;
            return true;
        }
        
        protected override void Cancel() => Dispose();

        private void Dispose()
        {
            ms?.Dispose();
            ms = null;
        }
    }
}