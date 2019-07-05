using ServiceStack;
using System.Linq;
using System;
using ServiceStack.Redis;
using ServiceStack.OrmLite;
using System.Reflection;
using ServiceStack.Script;

namespace ScriptInfo
{
    public class ScriptInfoMethods : ScriptMethods
    {
        Type GetFilterType(string name)
        {
            switch(name)
            {
                case nameof(DefaultScripts):
                    return typeof(DefaultScripts);
                case nameof(HtmlScripts):
                    return typeof(HtmlScripts);
                case nameof(ProtectedScripts):
                    return typeof(ProtectedScripts);
                case nameof(InfoScripts):
                    return typeof(InfoScripts);
                case nameof(RedisScripts):
                    return typeof(RedisScripts);
                case nameof(DbScripts):
                    return typeof(DbScripts);
                case nameof(DbScriptsAsync):
                    return typeof(DbScriptsAsync);
                case nameof(ServiceStackScripts):
                    return typeof(ServiceStackScripts);
                case nameof(AutoQueryScripts):
                    return typeof(AutoQueryScripts);
            }

            throw new NotSupportedException("Unknown Filter: " + name);
        }

        public IRawString methodLinkToSrc(string name)
        {
            const string prefix = "https://github.com/ServiceStack/ServiceStack/blob/master/src/ServiceStack.Common/Script/Methods/";

            var type = GetFilterType(name);
            var url = type == typeof(DefaultScripts)
                ? prefix
                : type == typeof(HtmlScripts) || type == typeof(ProtectedScripts)
                    ? $"{prefix}{type.Name}.cs"
                    : type == typeof(InfoScripts)
                    ? "https://github.com/ServiceStack/ServiceStack/blob/master/src/ServiceStack/InfoScripts.cs"
                    : type == typeof(RedisScripts)
                    ? "https://github.com/ServiceStack/ServiceStack.Redis/blob/master/src/ServiceStack.Redis/RedisScripts.cs"
                    : type == typeof(DbScripts) || type == typeof(DbScriptsAsync)
                    ? $"https://github.com/ServiceStack/ServiceStack.OrmLite/tree/master/src/ServiceStack.OrmLite/{type.Name}.cs"
                    : type == typeof(ServiceStackScripts)
                    ? "https://github.com/ServiceStack/ServiceStack/blob/master/src/ServiceStack/ServiceStackScripts.cs"
                    : prefix;

            return new RawString($"<a href='{url}'>{type.Name}.cs</a>");
        }

        public ScriptMethodInfo[] methodsAvailable(string name)
        {
            var filterType = GetFilterType(name);
            var filters = filterType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            var to = filters
                .OrderBy(x => x.Name)
                .ThenBy(x => x.GetParameters().Count())
                .Where(x => x.DeclaringType != typeof(ScriptMethods) && x.DeclaringType != typeof(object))
                .Where(m => !m.IsSpecialName)                
                .Select(ScriptMethodInfo.Create);

            return to.ToArray();
        }

    }

    public class ScriptMethodInfo
    {
        public string Name { get; set; }
        public string FirstParam { get; set; }
        public string ReturnType { get; set; }
        public int ParamCount { get; set; }
        public string[] RemainingParams { get; set; }

        public static ScriptMethodInfo Create(MethodInfo mi)
        {
            var paramNames = mi.GetParameters()
                .Where(x => x.ParameterType != typeof(ScriptScopeContext))
                .Select(x => x.Name)
                .ToArray();

            var to = new ScriptMethodInfo {
                Name = mi.Name,
                FirstParam = paramNames.FirstOrDefault(),
                ParamCount = paramNames.Length,
                RemainingParams = paramNames.Length > 1 ? paramNames.Skip(1).ToArray() : new string[]{},
                ReturnType = mi.ReturnType?.Name,
            };

            return to;
        }

        public string Return => ReturnType != null && ReturnType != nameof(StopExecution) ? " -> " + ReturnType : "";

        public string Body => ParamCount == 0
            ? $"{Name}"
            : ParamCount == 1
                ? $"| {Name}"
                : $"| {Name}(" + string.Join(", ", RemainingParams) + $")";

        public string Display => ParamCount == 0
            ? $"{Name}{Return}"
            : ParamCount == 1
                ? $"{FirstParam} | {Name}{Return}"
                : $"{FirstParam} | {Name}(" + string.Join(", ", RemainingParams) + $"){Return}";
    }
    
}
