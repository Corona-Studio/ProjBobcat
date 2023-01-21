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
    /// <summary>
    ///     检查该Rule是否被允许
    /// </summary>
    /// <param name="rules"></param>
    /// <returns></returns>
    public static bool CheckAllow(this IEnumerable<JvmRules?>? rules)
    {
        if (rules == null) return true;

        var jvmRules = rules.Where(r => r != null).Select(r => r!).ToList();

        if (!jvmRules.Any()) return false;

        var ruleFlag = false;
        var orderedRules = new List<JvmRules>();

        orderedRules.AddRange(jvmRules.Where(r => r.Action == "disallow"));
        orderedRules.AddRange(jvmRules.Where(r => r.Action == "allow"));

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