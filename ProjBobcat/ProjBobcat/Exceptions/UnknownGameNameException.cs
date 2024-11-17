using System;

namespace ProjBobcat.Exceptions;

/// <summary>
///     在试图使用一个未知的游戏名称时引发的异常。
/// </summary>
/// <remarks>
///     创建一个 <see cref="UnknownGameNameException" /> 的新实例。
/// </remarks>
/// <param name="message">解释异常原因的错误消息。</param>
/// <param name="gameName">导致当前异常的未知游戏名称。</param>
/// <param name="innerException">导致当前异常的异常。</param>
public sealed class UnknownGameNameException(string message, string gameName, Exception? innerException)
    : Exception(message, innerException)
{
    /// <summary>
    ///     创建一个 <see cref="UnknownGameNameException" /> 的新实例。
    /// </summary>
    /// <param name="gameName">导致当前异常的未知游戏名称。</param>
    /// <param name="innerException">导致当前异常的异常。</param>
    public UnknownGameNameException(
        string gameName,
        Exception? innerException = null)
        : this($"Unknown game name: {gameName}.", gameName, innerException)
    {
    }

    /// <summary>
    ///     获取导致导致当前异常的未知游戏名称。
    /// </summary>
    public string GameName { get; } = gameName;
}