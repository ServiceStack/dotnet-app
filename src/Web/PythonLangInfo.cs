using System;
using System.Collections.Generic;
using ServiceStack;
using ServiceStack.NativeTypes.Python;
using ServiceStack.Text;

namespace Apps.ServiceInterface.Langs
{
    public class PythonLangInfo : LangInfo
    {
        public PythonLangInfo()
        {
            Code = "Python";
            Name = "Python";
            Ext = "py";
            LineComment = "#";
            DtosPathPrefix = "my_app\\";
            Files = new Dictionary<string, string> {
                ["requirements.txt"] = @"servicestack>=0.0.7",
                ["setup.py"] = @"from setuptools import setup, find_packages

setup(
    name='my_app',
    packages=find_packages(),
)",
                ["main.py"] = @"from servicestack import JsonServiceClient
{API_COMMENT}from my_app.dtos import *

client = JsonServiceClient('{BASE_URL}')

{REQUIRES_AUTH}
{API_COMMENT}response = client.send({REQUEST}({REQUEST_BODY}))

{API_COMMENT}printdump(response)
{INSPECT_VARS}
",
                ["my_app\\__init__.py"] = @"",
            };
            InspectVarsResponse = "inspect_vars({\"response\": response})";
            RequiresAuthTemplate = @"
# Authentication is required
# client.post(Authenticate( 
#     provider='credentials',
#     userName='...',
#     password='...'}))";
        }
        private PythonGenerator Gen => new(new MetadataTypesConfig());
        public override string GetTypeName(string typeName, string[] genericArgs) => Gen.Type(typeName, genericArgs);

        public override string GetPropertyAssignment(MetadataPropertyType prop, string propValue) =>
            $"    {Gen.GetPropertyName(prop.Name)}={Value(prop.Type,propValue)},";

        public override string RequestBodyFilter(string assignments)
        {
            var to = assignments.TrimEnd();
            return to.EndsWith(",") 
                ? to.Substring(0, to.Length - 1) 
                : to;
        }

        public override string New(string ctor) => ctor; //no new

        public override string Value(string typeName, string value) => typeName switch {
            nameof(Double) => Float(value),
            nameof(Single) => Float(value),
            nameof(Decimal) => Float(value),
            _ => value
        };

        public override string GetCollectionLiteral(string collectionBody, string collectionType, string elementType) =>
            "[" + collectionBody + "]";
        public override string GetByteArrayLiteral(byte[] bytes) =>
            $"from_bytearray(\"{Convert.ToBase64String(bytes)}\")";
        
        public override string GetDateTimeLiteral(string value)
        {
            var dateValue = value.ConvertTo<DateTime>().ToUniversalTime();
            if (dateValue.Hour + dateValue.Minute + dateValue.Second + dateValue.Millisecond == 0)
                return New($"datetime({dateValue.Year},{dateValue.Month},{dateValue.Day})");
            if (dateValue.Millisecond == 0)
                return New($"datetime({dateValue.Year},{dateValue.Month},{dateValue.Day},{dateValue.Hour},{dateValue.Minute},{dateValue.Second})");
            return New($"datetime({dateValue.Year},{dateValue.Month},{dateValue.Day},{dateValue.Hour},{dateValue.Minute},{dateValue.Second},{dateValue.Millisecond})");
        }

        public override string GetTimeSpanLiteral(string value)
        {
            var from = value.ConvertTo<TimeSpan>();
            var sb = StringBuilderCache.Allocate();
            if (from.Days > 0)
                sb.Append($"days={from.Days}");
            if (from.Hours > 0)
                sb.Append(sb.Length > 0 ? "," : "").Append($"hours={from.Hours}");
            if (from.Minutes > 0)
                sb.Append(sb.Length > 0 ? "," : "").Append($"minutes={from.Minutes}");
            if (from.Seconds > 0)
                sb.Append(sb.Length > 0 ? "," : "").Append($"seconds={from.Seconds}");
            if (from.Milliseconds > 0)
                sb.Append(sb.Length > 0 ? "," : "").Append($"milliseconds={from.Milliseconds}");
            if (sb.Length == 0)
                sb.Append($"seconds={from.Seconds}");
            var to = $"timedelta({StringBuilderCache.ReturnAndFree(sb)})";
            return to;
        }

        public override string GetGuidLiteral(string value) => $"\"{value.ConvertTo<Guid>():D}\"";    }
}