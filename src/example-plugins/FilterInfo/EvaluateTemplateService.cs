using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ServiceStack;
using ServiceStack.IO;
using ServiceStack.Web;
using ServiceStack.Data;
using ServiceStack.OrmLite;
using ServiceStack.Templates;

namespace FilterInfo
{
    [Route("/template/eval")]
    public class EvaluateTemplate
    {
        public string Template { get; set; }
    }

    [ReturnExceptionsInJson]
    public class TemplateServices : Service
    {
        public ITemplatePages Pages { get; set; }

        public async Task<string> Any(EvaluateTemplate request)
        {
            var feature = HostContext.GetPlugin<TemplatePagesFeature>();
            var context = new TemplateContext {
                TemplateFilters = { 
                    new FilterInfoFilters(), 
                    feature.TemplateFilters.FirstOrDefault(x => x.GetType().Name == "ServerInfoFilters")
                },
                VirtualFiles = feature.VirtualFiles,
            }.Init();

            var pageResult = new PageResult(context.OneTimePage(request.Template))
            {
                Args = base.Request.GetTemplateRequestParams(importRequestParams:true)
            };
            return await pageResult.RenderToStringAsync();
        }
    }
}