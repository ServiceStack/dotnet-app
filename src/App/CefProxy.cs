using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using ServiceStack;
using ServiceStack.CefGlue;
using ServiceStack.Support;
using ServiceStack.Text;
using Xilium.CefGlue;

namespace Web
{
    public class CefProxySchemeHandlerFactory : CefSchemeHandlerFactory
    {
        private readonly ProxyScheme config;
        public CefProxySchemeHandlerFactory(ProxyScheme config) => this.config = config;
        protected override CefResourceHandler Create(CefBrowser browser, CefFrame frame, string schemeName, CefRequest request)
        {
            return new CefProxyResourceHandler(config);
        }
    }

    public class CefProxyResourceHandler : CefResourceHandler
    {
        private readonly ProxyScheme config;
        public CefProxyResourceHandler(ProxyScheme config) => this.config = config;

        private long contentLength;
        private NameValueCollection responseHeaders;
        private ReadOnlyMemory<byte> responseMemoryBytes;
        private int responseStatus;
        private string responseStatusText;
        private string responseContentType;
        private string responseCharSet;
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
                var url = config.ResolveUrl != null
                    ? config.ResolveUrl(request.Url)
                    : (config.TargetScheme ?? "https") + "://" + request.Url.RightPart("://");
                
                var webReq = (HttpWebRequest) WebRequest.Create(url);
                webReq.Method = request.Method;

                var headers = request.GetHeaderMap();
                foreach (string key in headers)
                {
                    foreach (var value in headers.GetValues(key).Safe())
                    {
                        try
                        {
                            if (string.Equals(key, HttpHeaders.UserAgent, StringComparison.OrdinalIgnoreCase))
                                webReq.UserAgent = value;
                            else if (string.Equals(key, HttpHeaders.Accept, StringComparison.OrdinalIgnoreCase))
                                webReq.Accept = value;
                            else if (string.Equals(key, HttpHeaders.ContentType, StringComparison.OrdinalIgnoreCase))
                                webReq.ContentType = value;
                            else if (string.Equals(key, HttpHeaders.ContentLength, StringComparison.OrdinalIgnoreCase))
                                webReq.ContentLength = long.Parse(value);
                            else if (string.Equals(key, HttpHeaders.AcceptEncoding, StringComparison.OrdinalIgnoreCase))
                                webReq.Headers[key] = "gzip, deflate";
                            else
                                webReq.Headers[key] = value;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($@"ERROR ProcessRequest: {key} = {value}:");
                            Console.WriteLine(e);
                        }
                    }
                }

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

                    using var requestStream = webReq.GetRequestStream();
                    requestStream.Write(requestBytes);
                }

                using var webRes = (HttpWebResponse) webReq.GetResponse();
                InitResponse(webRes);
                callback.Continue();
                return true;
            }
            catch (WebException webEx)
            {
                InitResponse((HttpWebResponse) webEx.Response);
                callback.Continue();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                responseStatus = 500;
                responseStatusText = e.GetType().Name;
                responseContentType = MimeTypes.PlainText;
                responseMemoryBytes = MemoryProvider.Instance.ToUtf8(e.ToString());
                
                callback.Continue();
                return true;
            }
        }

        private void InitResponse(HttpWebResponse webRes)
        {
            responseStatus = (int) webRes.StatusCode;
            responseStatusText = webRes.StatusDescription;
            responseContentType = webRes.ContentType.LeftPart(';');
            responseCharSet = webRes.CharacterSet;
            contentLength = webRes.ContentLength;
            responseHeaders = new NameValueCollection(webRes.Headers);
            using var stream = webRes.GetResponseStream();

            if (string.Equals(webRes.ContentEncoding, "gzip", StringComparison.OrdinalIgnoreCase))
                responseMemoryBytes = new NetGZipProvider().GUnzipStream(stream).ReadFullyAsMemory();
            else if (string.Equals(webRes.ContentEncoding, "deflate", StringComparison.OrdinalIgnoreCase))
                responseMemoryBytes = new NetDeflateProvider().DeflateStream(stream).ReadFullyAsMemory();
            else
                responseMemoryBytes = stream.ReadFullyAsMemory();

            contentLength = responseMemoryBytes.Length;
        }

        protected override void GetResponseHeaders(CefResponse response, out long responseLength, out string redirectUrl)
        {
            if (responseHeaders == null)
            {
                response.MimeType = "text/html";
                response.Status = 500;
                response.StatusText = "No Response";
                responseLength = 0;
                redirectUrl = null;
                return;
            }

            response.Status = responseStatus;
            response.StatusText = responseStatusText;
            response.MimeType = responseContentType;
            response.Charset = responseCharSet;

            foreach (var ignoreHeader in config.IgnoreHeaders)
            {
                responseHeaders.Remove(ignoreHeader);
            }
            foreach (var entry in config.AddHeaders)
            {
                responseHeaders[entry.Key] = entry.Value;
            }
            config.OnResponseHeaders?.Invoke(responseHeaders);
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
                bytesRead = 0;
                return false;
            }

            var toRead = (int)Math.Min(contentLength - pos, bytesToRead);
            response.Write(responseMemoryBytes.Span.Slice(pos, toRead));
            pos += toRead;
            bytesRead = toRead;
            return true;
        }
        
        protected override void Cancel()
        {
        }
    }
    
}