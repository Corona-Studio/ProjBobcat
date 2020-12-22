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
        public static HttpClient GetClient(int retryCount = 5)
        {
            return new(new RetryHandler(new RedirectHandler(new HttpClientHandler
            {
                AllowAutoRedirect = false
            }), retryCount));
        }
    }
}