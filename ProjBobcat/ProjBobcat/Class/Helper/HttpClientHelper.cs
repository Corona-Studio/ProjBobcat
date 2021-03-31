using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using ProjBobcat.Handler;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    ///     HttpClient实例化助手
    /// </summary>
    public static class HttpClientHelper
    {
        public const string DefaultClientName = "DefaultClient";
        public const string DataClientName = "DataClient";
        public const string HeadClientName = "HeadClient";
        public const string MultiPartClientName = "MultiPartClient";

        public static IHttpClientFactory HttpClientFactory { get; private set; }

        public static void Init()
        {
            var arr = new[] {DefaultClientName, DataClientName, HeadClientName, MultiPartClientName};
            foreach (var name in arr)
                ServiceHelper.ServiceCollection
                    .AddHttpClient(name)
                    .ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
                    {
                        AllowAutoRedirect = false
                    })
                    .AddHttpMessageHandler<RedirectHandler>()
                    .AddHttpMessageHandler<RetryHandler>();

            ServiceHelper.UpdateServiceProvider();

            HttpClientFactory = ServiceHelper.ServiceProvider.GetService<IHttpClientFactory>();
        }

        /// <summary>
        ///     获取一个HttpClient实例
        /// </summary>
        /// <returns></returns>
        public static HttpClient GetNewClient(string name)
        {
            return HttpClientFactory.CreateClient(name);
        }
    }
}