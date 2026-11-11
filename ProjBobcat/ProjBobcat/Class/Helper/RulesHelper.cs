using System;
using System.Collections.Generic;
using System.Linq;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     MineCraft Lib Json Rule工具类
/// </summary>
public static class RulesHelper
{
    extension(Library lib)
    {
        public bool IsNewNativeLib()
        {
            return lib.Name.Contains("natives", StringComparison.OrdinalIgnoreCase) &&
                   lib.Downloads?.Artifact != null;
        }
    }

    extension(IEnumerable<JvmRules?>? rules)
    {
        /// <summary>
        ///     检查该Rule是否被允许
        /// </summary>
        /// <returns></returns>
        public bool CheckAllow()
        {
            if (rules == null) return true;

            var jvmRules = rules
                .OfType<JvmRules>()
                .ToList();

            if (jvmRules.Count == 0) return true;

            var ruleFlag = false;
            List<JvmRules> orderedRules =
            [
                with(capacity: jvmRules.Count),
                .. jvmRules.Where(r => r.Action == "disallow"),
                .. jvmRules.Where(r => r.Action == "allow")
            ];

            foreach (var rule in orderedRules)
            {
                if (rule.OperatingSystem == null) return rule.Action == "allow";
                if (rule.Action == "disallow")
                {
                    if (rule.OperatingSystem.Name?.Equals(Constants.OsSymbol, StringComparison.OrdinalIgnoreCase) ?? false)
                        return false;

                    continue;
                }

                ruleFlag = ruleFlag || rule.OperatingSystem.IsAllow();
            }

            return ruleFlag;
        }
    }
}
