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
    ///     自动设置最大线程数
    /// </summary>
    /// <returns></returns>
    public static bool SetMaxThreads()
    {
        ThreadPool.GetMaxThreads(out var maxWorkerThreads,
            out var maxConcurrentActiveRequests);

        var changeSucceeded = ThreadPool.SetMaxThreads(
            maxWorkerThreads, maxConcurrentActiveRequests);

        return changeSucceeded;
    }

    /// <summary>
    /// 尝试获取进程的退出码
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