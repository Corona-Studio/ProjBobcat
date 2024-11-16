using System;
using System.Diagnostics;
using System.Threading;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     处理器工具类
/// </summary>
public static class ProcessorHelper
{
    /// <summary>
    ///     尝试获取进程的退出码
    /// </summary>
    /// <param name="proc"></param>
    /// <param name="code"></param>
    /// <returns></returns>
    public static bool TryGetProcessExitCode(Process proc, out int code)
    {
        try
        {
            code = proc.ExitCode;
            return true;
        }
        catch (InvalidOperationException)
        {
            code = 0;
            return false;
        }
    }
}