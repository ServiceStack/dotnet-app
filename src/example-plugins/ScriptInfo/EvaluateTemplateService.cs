using System.Linq;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.Script;

namespace ScriptInfo
{
    [Route("/script/eval")]
    public class EvaluateScript
    {
        public string Script { get; set; }
    }

    [ReturnExceptionsInJson]
    public class ScriptServices : Service
    {
        public ISharpPages Pages { get; set; }

        public async Task<string> Any(EvaluateScript request)
        {
            var feature = HostContext.GetPlugin<SharpPagesFeature>();
            var context = new ScriptContext {
                ScriptMethods = {
                    new ScriptInfoMethods(), 
                    feature.ScriptMethods.FirstOrDefault(x => x.GetType().Name == nameof(ScriptInfoMethods))
                },
                VirtualFiles = feature.VirtualFiles,
            }.Init();

            var pageResult = new PageResult(context.OneTimePage(request.Script))
            {
                Args = base.Request.GetRequestParams().ToObjectDictionary()
            };
            return await pageResult.RenderToStringAsync();
        }
    }
}