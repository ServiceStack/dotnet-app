using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ServiceStack;
using ServiceStack.Host;
using ServiceStack.NativeTypes;
using ServiceStack.Text;
using ServiceStack.Text.Support;

namespace Apps.ServiceInterface.Langs
{
    public abstract class LangInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Ext { get; set; }
        public string DtosPathPrefix { get; set; } = "";
        public string LineComment { get; set; } = "//";
        public string InspectVarsResponse { get; set; }
        public string RequiresAuthTemplate { get; set; }
        public char ItemsSep { get; set; } = ',';
        public Dictionary<string, string> Files { get; set; } = new();

        public virtual string RequestBody(string requestDto, Dictionary<string, string> args, MetadataTypes types)
        {
            var requestType = types.Operations.FirstOrDefault(x => x.Request.Name == requestDto)?.Request;
            return RequestBody(requestType, args, types);
        }

        public virtual string RequestBody(MetadataType requestType, Dictionary<string, string> args, MetadataTypes types)
        {
            if (requestType != null)
            {
                var sb = StringBuilderCache.Allocate();
                sb.AppendLine();
                var requestProps = requestType.GetFlattenedProperties(types);
                foreach (var entry in args)
                {
                    var prop = requestProps.FirstOrDefault(x => x.Name == entry.Key)
                               ?? requestProps.FirstOrDefault(x =>
                                   string.Equals(x.Name, entry.Key, StringComparison.OrdinalIgnoreCase));
                    if (prop != null)
                    {
                        var propValue = GetLiteralValue(entry.Value, prop, types);
                        if (propValue == null)
                            continue;
                        var propAssign = GetPropertyAssignment(prop, propValue);
                        sb.AppendLine(propAssign);
                    }
                }

                var props = StringBuilderCache.ReturnAndFree(sb);
                return RequestBodyFilter(props);
            }

            return "";
        }

        public virtual string RequestBodyFilter(string assignments) => assignments.TrimEnd();

        public abstract string GetPropertyAssignment(MetadataPropertyType prop, string propValue);

        public virtual string GetLiteralValue(string value, MetadataPropertyType prop, MetadataTypes types)
        {
            var useType = prop.Type == "Nullable`1"
                ? prop.GenericArgs[0]
                : prop.Type;
            var isArray = useType.EndsWith("[]");
            var elementType = isArray
                ? useType.LeftPart("[")
                : null;
            var enumType = prop.IsEnum == true
                ? types.FindType(prop.Type, prop.TypeNamespace)
                : null;
            var collectionType = prop.TypeNamespace == "System.Collections.Generic"
                ? GetTypeName(prop.Type, prop.GenericArgs)
                : null;
            if (collectionType != null)
            {
                elementType = prop.GenericArgs.Length == 1
                    ? prop.GenericArgs[0]
                    : prop.GenericArgs.Length == 2
                        ? $"KeyValuePair<{string.Join(',', prop.GenericArgs)}>"
                        : null;
                if (collectionType.IndexOf("Dictionary", StringComparison.Ordinal) >= 0)
                    return null; //not supported
            }
            else if (isArray)
            {
                collectionType = useType;
            }

            if (collectionType != null)
            {
                if (collectionType == "Byte[]")
                {
                    var intList = value.IndexOf(',') >= 0 || byte.TryParse(value, out _);
                    var bytes = intList 
                        ? value.FromJsv<byte[]>() 
                        : Convert.FromBase64String(value);
                    return GetByteArrayLiteral(bytes);
                }
                
                var items = value.FromJsv<List<string>>();
                var sb = StringBuilderCacheAlt.Allocate();
                foreach (var item in items)
                {
                    var itemProp = new MetadataPropertyType {
                        Type = elementType,
                        TypeNamespace = "System",
                    };
                    var literalValue = GetLiteralValue(item, itemProp, types);
                    literalValue = Value(elementType, literalValue);
                    if (string.IsNullOrEmpty(literalValue))
                        continue;
                    if (sb.Length > 0)
                        sb.Append($"{ItemsSep} ");
                    sb.Append(literalValue);
                }

                var collectionBody = StringBuilderCacheAlt.ReturnAndFree(sb);
                return GetCollectionLiteral(collectionBody, collectionType, elementType);
            }

            if (enumType != null)
                return GetEnumLiteral(value, enumType);
            if (useType == nameof(String))
                return GetStringLiteral(value);
            if (useType.IsNumericType())
                return GetNumericTypeLiteral(value);
            if (useType == nameof(DateTime))
                return GetDateTimeLiteral(value);
            if (useType == nameof(TimeSpan))
                return GetTimeSpanLiteral(value);
            if (useType == nameof(Boolean))
                return GetBoolLiteral(value);
            if (useType == nameof(Guid))
                return GetGuidLiteral(value);
            if (useType == nameof(Char))
                return GetCharLiteral(value);

            return null;
        }

        public virtual string GetEnumLiteral(string value, MetadataType enumType)
        {
            if (enumType.EnumNames == null)
                return null;
            var enumName = enumType.EnumNames.FirstOrDefault(x => x == value) ??
                           enumType.EnumNames.FirstOrDefault(x =>
                               string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
            if (enumName == null)
                return null;
            return $"{enumType.Name}.{enumName}";
        }

        public virtual string GetStringLiteral(string value) => value.ToJson();

        public virtual string GetNumericTypeLiteral(string value) => value;

        public virtual string GetBoolLiteral(string value)
        {
            var boolValue = value.ConvertTo<bool>();
            return boolValue ? "true" : "false";
        }

        public virtual string New(string ctor) => "new " + ctor;
        public abstract string Value(string typeName, string value);

        public virtual string Float(string propValue) => propValue + (propValue.IndexOf('.') >= 0 ? "" : ".0");

        public virtual string ISO8601(string value) =>
            value.ConvertTo<DateTime>().ToUniversalTime().ToString("u")
                .Replace(' ', 'T'); //"O" generates unwanted fractions

        public virtual string XsdDuration(string value) => TimeSpanConverter.ToXsdDuration(value.ConvertTo<TimeSpan>());
        public virtual string UUID(string value) => value.ConvertTo<Guid>().ToString("D");
        
        public abstract string GetTypeName(string typeName, string[] genericArgs);

        public virtual string GetResponse(MetadataOperationType op)
        {
            if (op?.Response != null)
            {
                var genericArgs = op.Response.Name.IndexOf('`') >= 0 && op.Response.GenericArgs[0] == "'T" &&
                    (op.ViewModel != null || op.DataModel != null)
                    ? new[] { op.ViewModel?.Name ?? op.DataModel.Name }
                    : op.Response.GenericArgs;
                var typeName = GetTypeName(op.Response.Name, genericArgs);
                return typeName;
            }

            return "var";
        }

        public virtual string GetDateTimeLiteral(string value)
        {
            var dateValue = value.ConvertTo<DateTime>().ToUniversalTime();
            if (dateValue.Hour + dateValue.Minute + dateValue.Second + dateValue.Millisecond == 0)
                return New($"DateTime({dateValue.Year},{dateValue.Month},{dateValue.Day},DateTimeKind.Utc)");
            if (dateValue.Millisecond == 0)
                return New(
                    $"DateTime({dateValue.Year},{dateValue.Month},{dateValue.Day},{dateValue.Hour},{dateValue.Minute},{dateValue.Second},DateTimeKind.Utc)");
            return New(
                $"DateTime({dateValue.Year},{dateValue.Month},{dateValue.Day},{dateValue.Hour},{dateValue.Minute},{dateValue.Second},{dateValue.Millisecond},DateTimeKind.Utc)");
        }

        public virtual string GetTimeSpanLiteral(string value)
        {
            var from = value.ConvertTo<TimeSpan>();
            var sb = StringBuilderCache.Allocate();
            if (from.Days > 0)
                sb.Append(from.Days);
            if (from.Hours > 0)
                sb.Append(sb.Length > 0 ? "," : "").Append(from.Hours);
            if (from.Minutes > 0)
                sb.Append(sb.Length > 0 ? "," : "").Append(from.Minutes);
            if (from.Seconds > 0)
                sb.Append(sb.Length > 0 ? "," : "").Append(from.Seconds);
            if (from.Milliseconds > 0)
                sb.Append(sb.Length > 0 ? "," : "").Append(from.Milliseconds);
            var to = New($"TimeSpan({StringBuilderCache.ReturnAndFree(sb)})");
            return to;
        }

        public bool IsArray(string collectionType) => collectionType.EndsWith("[]");

        public virtual string GetCollectionLiteral(string collectionBody, string collectionType, string elementType) =>
            IsArray(collectionType)
                ? "new[] { " + collectionBody + " }"
                : "new " + collectionType + " { " + collectionBody + " }";

        public virtual string GetByteArrayLiteral(byte[] bytes) =>
            $"Convert.FromBase64String(\"{Convert.ToBase64String(bytes)}\")";

        public virtual string GetCharLiteral(string value) => $"'{value.ConvertTo<Char>()}'";
        public virtual string GetGuidLiteral(string value) => New($"Guid(\"{value.ConvertTo<Guid>():D}\")");

        public virtual JupyterNotebook CreateNotebook(SiteInfo site, string langContent, string requestDto, string requestArgs)
        {
            throw new Exception($"{Name} does not support Jupyter Notebooks");
        }
        
        public static JupyterCell CreateCodeCell(string src) => new() {
            CellType = "code",
            Source = ConvertToSourceLines(src),
            Metadata = new Dictionary<string, string>(),
            Outputs = new List<JupyterOutput>(),
            ExecutionCount = 0,
        };

        public static List<string> ConvertToSourceLines(string src) => string.IsNullOrEmpty(src) 
            ? new List<string>() 
            : src.ReadLines().Map(x => x + "\n");
        
        public static Dictionary<string, object> ParseJsRequest(string requestArgs)
        {
            if (!string.IsNullOrEmpty(requestArgs))
            {
                try
                {
                    var ret = JS.eval(requestArgs);
                    return (Dictionary<string, object>)ret;
                }
                catch (Exception e)
                {
                    throw new Exception("Request args should be a valid JavaScript Object literal");
                }
            }
            return null;
        }

        public static readonly HashSet<string> AutoQueryDtoNames = new() {"QueryDb`1", "QueryDb`2", "QueryData`1", "QueryData`2"};
    }
    
    public static class LangInfoExtensions
    {
        public static MetadataType FindType(this MetadataTypes types, string typeName, string typeNs) =>
            types.Types.FirstOrDefault(x => x.Name == typeName) ??
            types.Types.FirstOrDefault(x => string.Equals(x.Name, typeName, StringComparison.OrdinalIgnoreCase)) ??
            (typeName == "QueryBase" || typeName.StartsWith("QueryDb`") || typeName.StartsWith("QueryData`")
                ? QueryBaseType
                : typeName == "QueryResponse`1"
                    ? QueryResponseType
                    : null);

        private static readonly MetadataTypesGenerator MetaGen =
            new(new ServiceMetadata(new List<RestPath>()), new NativeTypesFeature().MetadataTypesConfig);
        
        private static MetadataType queryBaseType;
        private static MetadataType QueryBaseType => queryBaseType ??= MetaGen.ToType(typeof(QueryBase));  
        private static MetadataType QueryResponseType => queryBaseType ??= MetaGen.ToType(typeof(QueryResponse<>));  

        public static List<MetadataPropertyType> GetFlattenedProperties(this MetadataType type, MetadataTypes types)
        {
            var to = new List<MetadataPropertyType>();
            if (type == null) 
                return to;

            do
            {
                if (type.Properties != null)
                {
                    foreach (var metaProp in type.Properties)
                    {
                        to.Add(metaProp);
                    }
                }

                type = type.Inherits != null 
                    ? types.FindType(type.Inherits.Name, type.Inherits.Namespace) 
                    : null;
            } while (type != null);
            return to;
        }

        public static bool IsNumericType(this string typeName) => typeName switch {
            nameof(Byte) => true,
            nameof(SByte) => true,
            nameof(Int16) => true,
            nameof(Int32) => true,
            nameof(Int64) => true,
            nameof(UInt16) => true,
            nameof(UInt32) => true,
            nameof(UInt64) => true,
            nameof(Single) => true,
            nameof(Double) => true,
            nameof(Decimal) => true,
            _ => false,
        };
    }
    
    [DataContract]
    public class JupyterNotebook
    {
        [DataMember(Name = "cells")]
        public List<JupyterCell> Cells { get; set; }

        [DataMember(Name = "metadata")]
        public JupyterMetadata Metadata { get; set; }

        [DataMember(Name = "nbformat")]
        public int Nbformat { get; set; }

        [DataMember(Name = "nbformat_minor")]
        public int NbformatMinor { get; set; }

        public static JupyterNotebook CreateForPython2() => new() {
            Metadata = new JupyterMetadata {
                Kernelspec = new() {
                    DisplayName = "Python 3",
                    Language = "python",
                    Name = "python3",
                },
                LanguageInfo = new JupyterLanguageInfo {
                    CodemirrorMode = new JupyterCodemirrorMode {
                        Name = "ipython",
                        Version = 2,
                    },
                    FileExtension = ".py",
                    Mimetype = "text/x-python",
                    Name = "python",
                    NbconvertExporter = "python",
                    PygmentsLexer = "ipython2",
                    Version = "2.7.6",
                }
            },
            Nbformat = 4,
            NbformatMinor = 0,
        };
        
        public static JupyterNotebook CreateForPython3() => new() {
            Metadata = new JupyterMetadata {
                Kernelspec = new() {
                    DisplayName = "Python 3",
                    Language = "python",
                    Name = "python3",
                },
                LanguageInfo = new JupyterLanguageInfo {
                    CodemirrorMode = new JupyterCodemirrorMode {
                        Name = "ipython",
                        Version = 3,
                    },
                    FileExtension = ".py",
                    Mimetype = "text/x-python",
                    Name = "python",
                    NbconvertExporter = "python",
                    PygmentsLexer = "ipython3",
                    Version = "3.9.6",
                }
            },
            Nbformat = 4,
            NbformatMinor = 0,
        };
        
        public static JupyterNotebook CreateForCSharp() => new() {
            Metadata = new JupyterMetadata {
                OrigNbformat = 4,
                LanguageInfo = new JupyterLanguageInfo {
                    Name = "C#"
                },
                Kernelspec = new() {
                    Name = ".net-csharp",
                    DisplayName = ".NET (C#)",
                },
            },
            Nbformat = 4,
            NbformatMinor = 2,
        };
        
        public static JupyterNotebook CreateForFSharp() => new() {
            Metadata = new JupyterMetadata {
                OrigNbformat = 4,
                LanguageInfo = new JupyterLanguageInfo {
                    Name = "F#"
                },
                Kernelspec = new() {
                    Name = ".net-fsharp",
                    DisplayName = ".NET (F#)",
                },
            },
            Nbformat = 4,
            NbformatMinor = 2,
        };
    }

    [DataContract]
    public class JupyterOutput
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }// = "stdout";

        [DataMember(Name = "output_type")]
        public string OutputType { get; set; }// = "stream"; // display_data

        [DataMember(Name = "text")]
        public List<string> Text { get; set; }

        [DataMember(Name = "data")]
        public Dictionary<string, List<string>>
            Data { get; set; } //= text/html => [src_lines], text/plain => [src_lines]

        [DataMember(Name = "metadata")]
        public Dictionary<string, string> Metadata { get; set; }
    }

    [DataContract]
    public class JupyterMetadata
    {
        [DataMember(Name = "interpreter")]
        public JupyterInterpreter Interpreter { get; set; }

        [DataMember(Name = "kernelspec")]
        public JupyterKernel Kernelspec { get; set; }

        [DataMember(Name = "language_info")]
        public JupyterLanguageInfo LanguageInfo { get; set; }

        [DataMember(Name = "orig_nbformat")]
        public int? OrigNbformat { get; set; }
    }

    [DataContract]
    public class JupyterInterpreter
    {
        [DataMember(Name = "hash")]
        public string Hash { get; set; }
    }

    [DataContract]
    public class JupyterKernel
    {
        [DataMember(Name = "display_name")]
        public string DisplayName { get; set; }

        [DataMember(Name = "language")]
        public string Language { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }
    }

    [DataContract]
    public class JupyterLanguageInfo
    {
        [DataMember(Name = "codemirror_mode")]
        public JupyterCodemirrorMode CodemirrorMode { get; set; }

        [DataMember(Name = "file_extension")]
        public string FileExtension { get; set; }

        [DataMember(Name = "mimetype")]
        public string Mimetype { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "nbconvert_exporter")]
        public string NbconvertExporter { get; set; }

        [DataMember(Name = "pygments_lexer")]
        public string PygmentsLexer { get; set; }

        [DataMember(Name = "version")]
        public string Version { get; set; }
    }

    [DataContract]
    public class JupyterCodemirrorMode
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "version")]
        public int Version { get; set; }
    }

    [DataContract]
    public class JupyterCell
    {
        [DataMember(Name = "cell_type")]
        public string CellType { get; set; }

        [DataMember(Name = "execution_count")]
        public int? ExecutionCount { get; set; }

        [DataMember(Name = "metadata")]
        public Dictionary<string, string> Metadata { get; set; }

        [DataMember(Name = "outputs")]
        public List<JupyterOutput> Outputs { get; set; }

        [DataMember(Name = "source")]
        public List<string> Source { get; set; } = new();
    }
    
}