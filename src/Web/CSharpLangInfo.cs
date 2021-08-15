using System;
using System.Collections.Generic;
using System.Linq;
using ServiceStack;
using ServiceStack.NativeTypes.CSharp;

namespace Apps.ServiceInterface.Langs
{
    public class CSharpLangInfo : LangInfo
    {
        public CSharpLangInfo()
        {
            Code = "csharp";
            Name = "C#";
            Ext = "cs";
            Files = new Dictionary<string, string> {
                ["MyApp.csproj"] = @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
    <NoWarn>1591</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""ServiceStack.Client"" Version=""5.*"" />
    <PackageReference Include=""ServiceStack.Common"" Version=""5.*"" />
  </ItemGroup>

</Project>",
                ["Program.cs"] = @"using System;
using System.Collections.Generic;
using ServiceStack;
using ServiceStack.Text;
using MyApp;

var client = new JsonServiceClient(""{BASE_URL}"");{REQUIRES_AUTH}

{API_COMMENT}var response = client.Send(new {REQUEST} {{REQUEST_BODY}
{API_COMMENT}});
{API_COMMENT}response.PrintDump();
{INSPECT_VARS}"
            };
            InspectVarsResponse = "\nInspect.vars(new { response });";
            RequiresAuthTemplate = @"
// Authentication is required
// client.Post(new Authenticate {
//     provider = ""credentials"",
//     UserName = ""..."",
//     Password = ""...""
// });";
        }

        private CSharpGenerator Gen => new(new MetadataTypesConfig());

        public override string GetPropertyAssignment(MetadataPropertyType prop, string propValue) =>
            $"    {prop.Name} = {Value(prop.Type,propValue)},";

        public override string Value(string typeName, string value) => typeName switch {
            nameof(Double) => Float(value),
            nameof(Int64) => value + "L",
            nameof(UInt32) => value + "u",
            nameof(UInt64) => value + "ul",
            nameof(Single) => Float(value) + "f",
            nameof(Decimal) => Float(value) + "m",
            _ => value
        };

        public override string GetTypeName(string typeName, string[] genericArgs) => Gen.Type(typeName, genericArgs);
        
        public override JupyterNotebook CreateNotebook(SiteInfo site, string langContent, string requestDto, string requestArgs)
        {
            var dtosSource = $@"#r ""nuget:ServiceStack.Client""
#r ""nuget:ServiceStack.Common""

{langContent}

var client = new JsonServiceClient(""{site.BaseUrl}"");
";
            var to = JupyterNotebook.CreateForCSharp();
            to.Cells = new List<JupyterCell> {
                CreateCodeCell(dtosSource),
            };

            if (requestDto != null)
            {
                var requestBody = "";
                var args = ParseJsRequest(requestArgs);
                var argKeys = args != null
                    ? new HashSet<string>(args.Keys, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>();
                if (args != null)
                {
                    var argsStringMap = args.ToStringDictionary();
                    requestBody = RequestBody(requestDto, argsStringMap, site.Metadata.Api).TrimEnd(',');
                }

                var requestOp = site.Metadata.Api.Operations.FirstOrDefault(x => x.Request.Name == requestDto);
                var clientMethod = (requestOp?.Actions?.FirstOrDefault() != null
                    ? (requestOp.Actions.First().EqualsIgnoreCase("ANY")
                        ? null
                        : requestOp.Actions.First().ToPascalCase())
                    : null) ?? "Send";
                to.Cells.Add(CreateCodeCell($"var response = client.{clientMethod}(new {requestDto} {{{requestBody}}});"));
                to.Cells.Add(CreateCodeCell("display(HTML(Inspect.htmlDump(response)));"));
                var response = requestOp?.Response;
                if (response?.Properties != null)
                {
                    var hasResults = response.Properties.FirstOrDefault(x => x.Name.EqualsIgnoreCase("Results")) != null;
                    if (hasResults)
                    {
                        var resultsCell = CreateCodeCell("Inspect.printDumpTable(response.Results);");
                        var baseClass = requestOp.Request.Inherits?.Name;
                        if (baseClass != null && AutoQueryDtoNames.Contains(baseClass))
                        {
                            var responseModel = requestOp.Request.Inherits.GenericArgs.Last();
                            var dataModel = site.Metadata.Api.Types.FirstOrDefault(x => x.Name == responseModel);
                            if (dataModel != null)
                            {
                                if (argKeys.Contains("fields")) //Already specified fields in AutoQuery Request
                                {
                                    resultsCell = CreateCodeCell("Inspect.printDumpTable(response.Results);");
                                }
                                else
                                {
                                    var propNames = dataModel.Properties.Map(x => '"' + x.Name + '"');
                                    resultsCell = CreateCodeCell(
                                        $"Inspect.printDumpTable(response.Results,\n    headers:new[]{{{string.Join(",", propNames)}}})");
                                }
                            }
                        }

                        to.Cells.Add(resultsCell);
                    }
                }
            }
            else
            {
                to.Cells.Add(CreateCodeCell("# response = client.Send(new MyRequest {});"));
                to.Cells.Add(CreateCodeCell("# display(HTML(Inspect.htmlDump(response)));"));
            }

            return to;
        }
        
    }
}