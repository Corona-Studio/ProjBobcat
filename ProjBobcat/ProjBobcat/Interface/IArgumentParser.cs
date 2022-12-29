using System.Collections.Generic;
using ProjBobcat.Class.Model.Auth;

namespace ProjBobcat.Interface;

public interface IArgumentParser
{
    /// <summary>
    ///     解析 Log4J 日志配置文件相关参数
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> ParseGameLoggingArguments();

    /// <summary>
    ///     解析JVM核心启动参数（内存大小、Agent等）
    /// </summary>
    /// <returns>解析好的JVM核心启动参数</returns>
    IEnumerable<string> ParseJvmHeadArguments();

    /// <summary>
    ///     解析游戏JVM参数
    /// </summary>
    /// <returns>解析好的游戏JVM参数</returns>
    IEnumerable<string> ParseJvmArguments();

    /// <summary>
    ///     解析游戏参数
    /// </summary>
    /// <param name="authResult"></param>
    /// <returns>解析好的游戏参数</returns>
    IEnumerable<string> ParseGameArguments(AuthResultBase authResult);

    /// <summary>
    ///     解析部分总成
    /// </summary>
    /// <returns>解析好的全部参数</returns>
    List<string> GenerateLaunchArguments();
}