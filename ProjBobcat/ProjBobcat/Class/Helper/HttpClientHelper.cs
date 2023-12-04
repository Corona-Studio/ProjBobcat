using System;
using System.Net.Http;
using ProjBobcat.Handler;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     HttpClient实例化助手
/// </summary>
public static class HttpClientHelper
{
    static readonly Lazy<HttpClient> DefaultClientFactory = new(HttpClientFactory);
    static readonly Lazy<HttpClient> DataClientFactory = new(HttpClientFactory);
    static readonly Lazy<HttpClient> HeadClientFactory = new(HttpClientFactory);

    static readonly Lazy<HttpClient> MultiPartClientFactory = new(() =>
    {
        var client = HttpClientFactory();

        client.DefaultRequestHeaders.ConnectionClose = false;

        return client;
    });

    public static HttpClient DefaultClient => DefaultClientFactory.Value;
    public static HttpClient DataClient => DataClientFactory.Value;
    public static HttpClient HeadClient => HeadClientFactory.Value;
    public static HttpClient MultiPartClient => MultiPartClientFactory.Value;

    /// <summary>
    ///     获取或设置用户代理信息。
    /// </summary>
    public static string Ua { get; set; } = "ProjBobcat";

    static HttpClient HttpClientFactory()
    {
        var handlers = new RedirectHandler(new RetryHandler(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            AllowAutoRedirect = false,
            Proxy = HttpClient.DefaultProxy
        }));
        var httpClient = new HttpClient(handlers);

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Ua);

        return httpClient;
    }
}