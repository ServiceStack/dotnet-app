using System.Collections.Generic;
using ServiceStack;

namespace GistRun.ServiceModel
{
    [Route("/gists/{Id}/run")]
    [Route("/gists/{Id}/{Version}/run")]
    public class RunGist : IReturn<RunScriptResponse>
    {
        public string Id { get; set; }
        public string Version { get; set; }
    }

    public class RunScriptResponse
    {
        public string GistVersion { get; set; }
        public int? ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public long DurationMs { get; set; }
        public Dictionary<string, object> Vars { get; set; }
        
        public ResponseStatus ResponseStatus { get; set; }
    }
}