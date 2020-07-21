using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjBobcat.Exceptions
{
    /// <summary>
    /// 在试图使用一个未知的游戏名称时引发的异常。
    /// </summary>
    [Serializable]
    public class UnknownGameNameException : Exception
    {
        /// <summary>
        /// 获取导致导致当前异常的未知游戏名称。
        /// </summary>
        public string GameName { get; }
        /// <summary>
        /// 创建一个 <see cref="UnknownGameNameException"/> 的新实例。
        /// </summary>
        public UnknownGameNameException()
        {

        }
        /// <summary>
        /// 创建一个 <see cref="UnknownGameNameException"/> 的新实例。
        /// </summary>
        /// <param name="message">解释异常原因的错误消息。</param>
        /// <param name="gameName">导致当前异常的未知游戏名称。</param>
        /// <param name="innerException">导致当前异常的异常。</param>
        public UnknownGameNameException(string message, string gameName, Exception innerException)
            : base(message, innerException)
        {
            this.GameName = gameName;
        }
        /// <summary>
        /// 创建一个 <see cref="UnknownGameNameException"/> 的新实例。
        /// </summary>
        /// <param name="gameName">导致当前异常的未知游戏名称。</param>
        /// <param name="innerException">导致当前异常的异常。</param>
        public UnknownGameNameException(string gameName, Exception innerException = null)
            : this($"游戏名 {gameName} 是未知的。", gameName, innerException) { }
    }
}