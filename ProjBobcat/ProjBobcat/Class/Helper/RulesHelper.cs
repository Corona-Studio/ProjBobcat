using System;
using System.Collections.Generic;
using System.Linq;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class.Helper
{
    public static class RulesHelper
    {
        public static bool CheckAllow(this IEnumerable<JvmRules> rules)
        {
            if (rules == null) return true;

            var jvmRules = rules.ToList();

            if (!jvmRules.Any()) return false;

            var ruleFlag = false;

            foreach (var rule in jvmRules.Where(rule => rule.Action.Equals("allow", StringComparison.Ordinal)))
                if (rule.OperatingSystem == null)
                    ruleFlag = rule.Action.Equals("allow", StringComparison.Ordinal);
                else if (rule.OperatingSystem.ContainsKey("name"))
                    if (rule.OperatingSystem["name"].Equals("windows", StringComparison.Ordinal))
                        ruleFlag = rule.Action.Equals("allow", StringComparison.Ordinal);

            return ruleFlag;
        }
    }
}