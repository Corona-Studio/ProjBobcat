using System.Net.Http;
using ProjBobcat.Handler;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    ///     HttpClient实例化助手
    /// </summary>
    public static class HttpClientHelper
    {
        /// <summary>
        ///     获取一个HttpClient实例
        /// </summary>
        /// <returns></returns>
        public static HttpClient GetNewClient()
        {
            return new(new RedirectHandler(new RetryHandler(new HttpClientHandler
            {
                AllowAutoRedirect = false
            })));
        }
    }
}