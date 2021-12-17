using System;
using System.Runtime.Serialization;

namespace ProjBobcat.Exceptions;

/// <summary>
///     在试图使用一个未知的游戏名称时引发的异常。
/// </summary>
[Serializable]
public class UnknownGameNameException : Exception
{
    /// <summary>
    ///     创建一个 <see cref="UnknownGameNameException" /> 的新实例。
    /// </summary>
    public UnknownGameNameException()
    {
    }

    /// <summary>
    ///     创建一个 <see cref="UnknownGameNameException" /> 的新实例。
    /// </summary>
    /// <param name="message">解释异常原因的错误消息。</param>
    /// <param name="gameName">导致当前异常的未知游戏名称。</param>
    /// <param name="innerException">导致当前异常的异常。</param>
    public UnknownGameNameException(string message, string gameName, Exception innerException)
        : base(message, innerException)
    {
        GameName = gameName;
    }

    /// <summary>
    ///     创建一个 <see cref="UnknownGameNameException" /> 的新实例。
    /// </summary>
    /// <param name="gameName">导致当前异常的未知游戏名称。</param>
    /// <param name="innerException">导致当前异常的异常。</param>
    public UnknownGameNameException(string gameName, Exception innerException = null)
        : this($"Unknown game name: {gameName}.", gameName, innerException)
    {
    }

    /// <summary>
    ///     用序列化数据初始化 <see cref="UnknownGameNameException" /> 类的新实例。
    /// </summary>
    /// <param name="serializationInfo">包含有关所引发异常的序列化对象数据。</param>
    /// <param name="context">包含关于源或目标的上下文信息。</param>
    protected UnknownGameNameException(SerializationInfo serializationInfo, StreamingContext context)
        : base(serializationInfo, context)
    {
    }

    /// <summary>
    ///     获取导致导致当前异常的未知游戏名称。
    /// </summary>
    public string GameName { get; }
}