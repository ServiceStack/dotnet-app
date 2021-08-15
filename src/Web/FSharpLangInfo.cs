using System;
using System.Collections.Generic;
using System.Linq;
using ServiceStack;
using ServiceStack.NativeTypes.FSharp;
using ServiceStack.Text;

namespace Apps.ServiceInterface.Langs
{
    public class FSharpLangInfo : LangInfo
    {
        public FSharpLangInfo()
        {
            Code = "fsharp";
            Name = "F#";
            Ext = "fs";
            ItemsSep = ';';
            Files = new Dictionary<string, string> {
                ["MyApp.fsproj"] = @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
    <NoWarn>1591,FS0058</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""ServiceStack.Client"" Version=""5.*"" />
    <PackageReference Include=""ServiceStack.Common"" Version=""5.*"" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include=""dtos.fs"" />
    <Compile Include=""Program.fs"" />
  </ItemGroup>

</Project>",
                ["Program.fs"] = @"open System
open System.Collections.Generic
open System.Linq
open ServiceStack
open ServiceStack.Text
open MyApp

module Program =

    [<EntryPoint>]
    let main args =

        let client = new JsonServiceClient(""{BASE_URL}""){REQUIRES_AUTH}

        {API_COMMENT}let response = client.Send(new {REQUEST}({REQUEST_BODY}))

        {API_COMMENT}response.PrintDump()
        {INSPECT_VARS}

        0 //exitCode
"
            };
            InspectVarsResponse = "Inspect.vars({| response = response |})";
            RequiresAuthTemplate = @"
        // Authentication is required
        // client.Post(new Authenticate(
        //     provider = ""credentials"",
        //     UserName = ""..."",
        //     Password = ""...""))";
        }
        private FSharpGenerator Gen => new(new MetadataTypesConfig());
        public override string GetTypeName(string typeName, string[] genericArgs) => Gen.Type(typeName, genericArgs);

        public override string GetPropertyAssignment(MetadataPropertyType prop, string propValue) =>
            $"            {Gen.GetPropertyName(prop.Name)} = {Value(prop.Type,propValue)},";

        public override string Value(string typeName, string value) => typeName switch {
            nameof(Int32) => value,
            nameof(Double) => value + (value.IndexOf('.') >= 0 ? "" : ".0"),
            nameof(Byte) => value + "uy",
            nameof(SByte) => value + "y",
            nameof(Int16) => value + "s",
            nameof(Int64) => value + "L",
            nameof(UInt16) => value + "us",
            nameof(UInt32) => value + "ul",
            nameof(UInt64) => value + "UL",
            nameof(Single) => value + "f",
            nameof(Decimal) => value + "m",
            _ => value
        };

        public override string GetCollectionLiteral(string collectionBody, string collectionType, string elementType) =>
            IsArray(collectionType)
                ? $"[| " + collectionBody + " |]"
                : collectionType.StartsWith("ResizeArray")
                    ? $"ResizeArray([{collectionBody}])"
                    : $"new {collectionType}([{collectionBody}])";

        public override string RequestBodyFilter(string assignments)
        {
            var to = assignments.TrimEnd();
            return to.EndsWith(",") 
                ? to.Substring(0, to.Length - 1) 
                : to;
        }

        public override string New(string ctor) => ctor; //no new
        
        
        public override JupyterNotebook CreateNotebook(SiteInfo site, string langContent, string requestDto, string requestArgs)
        {
            var nsPos = langContent.IndexOf("namespace ", StringComparison.Ordinal);
            if (nsPos >= 0)
            {
                var nlAfterNs = langContent.IndexOf('\n', nsPos);
                langContent = langContent.Substring(nlAfterNs);
            }
            
            var dtosSource = $@"#r ""nuget:ServiceStack.Client""
#r ""nuget:ServiceStack.Common""

{langContent}

let client = new JsonServiceClient(""{site.BaseUrl}"")
";
            var to = JupyterNotebook.CreateForFSharp();
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
                to.Cells.Add(CreateCodeCell($"let response = client.{clientMethod}(new {requestDto} ({requestBody}))"));
                to.Cells.Add(CreateCodeCell("display(HTML(Inspect.htmlDump(response)))"));
                var response = requestOp?.Response;
                if (response?.Properties != null)
                {
                    var hasResults = response.Properties.FirstOrDefault(x => x.Name.EqualsIgnoreCase("Results")) != null;
                    if (hasResults)
                    {
                        var resultsCell = CreateCodeCell("Inspect.printDumpTable(response.Results)");
                        var baseClass = requestOp.Request.Inherits?.Name;
                        if (baseClass != null && AutoQueryDtoNames.Contains(baseClass))
                        {
                            var responseModel = requestOp.Request.Inherits.GenericArgs.Last();
                            var dataModel = site.Metadata.Api.Types.FirstOrDefault(x => x.Name == responseModel);
                            if (dataModel != null)
                            {
                                if (argKeys.Contains("fields")) //Already specified fields in AutoQuery Request
                                {
                                    resultsCell = CreateCodeCell("Inspect.printDumpTable(response.Results)");
                                }
                                else
                                {
                                    var propNames = dataModel.Properties.Map(x => '"' + x.Name + '"');
                                    resultsCell = CreateCodeCell(
                                        $"Inspect.printDumpTable(response.Results,\n    headers=[|{string.Join("; ", propNames)}|])");
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