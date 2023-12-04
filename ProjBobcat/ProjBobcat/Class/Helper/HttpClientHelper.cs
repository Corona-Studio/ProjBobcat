using System;
using System.Net.Http;
using ProjBobcat.Handler;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     HttpClient实例化助手
/// </summary>
public static class HttpClientHelper
{
    static readonly Lazy<HttpClient> DefaultClientFactory = new(CreateInstance);

    public static HttpClient DefaultClient => DefaultClientFactory.Value;

    /// <summary>
    ///     获取或设置用户代理信息。
    /// </summary>
    public static string Ua { get; set; } = "ProjBobcat";

    public static HttpClient CreateInstance()
    {
        var handlers = new RedirectHandler(new RetryHandler(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            Proxy = HttpClient.DefaultProxy
        }));
        var httpClient = new HttpClient(handlers);

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Ua);

        return httpClient;
    }
}