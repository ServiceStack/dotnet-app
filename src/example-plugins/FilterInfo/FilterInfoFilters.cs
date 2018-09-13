using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.Templates;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System;
using ServiceStack.Redis;
using ServiceStack.OrmLite;
using System.Reflection;

namespace FilterInfo
{
    public class FilterInfoFilters : TemplateFilter
    {
        Type GetFilterType(string name)
        {
            switch(name)
            {
                case nameof(TemplateDefaultFilters):
                    return typeof(TemplateDefaultFilters);
                case nameof(TemplateHtmlFilters):
                    return typeof(TemplateHtmlFilters);
                case nameof(TemplateProtectedFilters):
                    return typeof(TemplateProtectedFilters);
                case nameof(TemplateInfoFilters):
                    return typeof(TemplateInfoFilters);
                case nameof(TemplateRedisFilters):
                    return typeof(TemplateRedisFilters);
                case nameof(TemplateDbFilters):
                    return typeof(TemplateDbFilters);
                case nameof(TemplateDbFiltersAsync):
                    return typeof(TemplateDbFiltersAsync);
                case nameof(TemplateServiceStackFilters):
                    return typeof(TemplateServiceStackFilters);
                case nameof(TemplateAutoQueryFilters):
                    return typeof(TemplateAutoQueryFilters);
            }

            throw new NotSupportedException("Unknown Filter: " + name);
        }

        public IRawString filterLinkToSrc(string name)
        {
            const string prefix = "https://github.com/ServiceStack/ServiceStack/blob/master/src/ServiceStack.Common/Templates/Filters/";

            var type = GetFilterType(name);
            var url = type == typeof(TemplateDefaultFilters)
                ? prefix
                : type == typeof(TemplateHtmlFilters) || type == typeof(TemplateProtectedFilters)
                    ? $"{prefix}{type.Name}.cs"
                    : type == typeof(TemplateInfoFilters)
                    ? "https://github.com/ServiceStack/ServiceStack/blob/master/src/ServiceStack/TemplateInfoFilters.cs"
                    : type == typeof(TemplateRedisFilters)
                    ? "https://github.com/ServiceStack/ServiceStack.Redis/blob/master/src/ServiceStack.Redis/TemplateRedisFilters.cs"
                    : type == typeof(TemplateDbFilters) || type == typeof(TemplateDbFiltersAsync)
                    ? $"https://github.com/ServiceStack/ServiceStack.OrmLite/tree/master/src/ServiceStack.OrmLite/{type.Name}.cs"
                    : type == typeof(TemplateServiceStackFilters)
                    ? "https://github.com/ServiceStack/ServiceStack/blob/master/src/ServiceStack/TemplateServiceStackFilters.cs"
                    : prefix;

            return new RawString($"<a href='{url}'>{type.Name}.cs</a>");
        }

        public FilterInfo[] filtersAvailable(string name)
        {
            var filterType = GetFilterType(name);
            var filters = filterType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            var to = filters
                .OrderBy(x => x.Name)
                .ThenBy(x => x.GetParameters().Count())
                .Where(x => x.DeclaringType != typeof(TemplateFilter) && x.DeclaringType != typeof(object))
                .Where(m => !m.IsSpecialName)                
                .Select(x => FilterInfo.Create(x));

            return to.ToArray();
        }
    }

    public class FilterInfo
    {
        public string Name { get; set; }
        public string FirstParam { get; set; }
        public string ReturnType { get; set; }
        public int ParamCount { get; set; }
        public string[] RemainingParams { get; set; }

        public static FilterInfo Create(MethodInfo mi)
        {
            var paramNames = mi.GetParameters()
                .Where(x => x.ParameterType != typeof(TemplateScopeContext))
                .Select(x => x.Name)
                .ToArray();

            var to = new FilterInfo {
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
