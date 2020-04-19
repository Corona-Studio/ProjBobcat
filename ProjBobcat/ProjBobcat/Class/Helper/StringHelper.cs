using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Helper
{
    public static class StringHelper
    {
        public static string FixArgument(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg) || !arg.Contains('='))
                return arg;

            var para = arg.Split('=');
            if (para[1].Contains(' '))
                para[1] = $"\"{para[1]}\"";

            return string.Join("=", para);
        }

        public static string ReplaceByDic(string str, Dictionary<string, string> dic)
        {
            return str == null ? null : dic.Aggregate(str, (a, b) => a.Replace(b.Key, b.Value));
        }

        public static string ResolveUserProperties(this List<PropertyModel> properties)
        {
            if (!(properties?.Any() ?? false))
                return "{}";

            var sb = new StringBuilder();
            sb.Append('{');
            foreach (var item in properties) sb.AppendFormat("\"{0}\":[\"{1}\"],", item.Name, item.Value);

            var totalSb = new StringBuilder();
            totalSb.Append(sb.ToString().TrimEnd(',').Trim()).Append('}');
            return totalSb.ToString();
        }
    }
}