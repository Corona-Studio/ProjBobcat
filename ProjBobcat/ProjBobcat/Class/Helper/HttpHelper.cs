using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     Http工具方法类
/// </summary>
public static partial class HttpHelper
{
    const string UriRegexStr =
        "((([A-Za-z]{3,9}:(?:\\/\\/)?)(?:[-;:&=\\+$,\\w]+@)?[A-Za-z0-9.-]+(:[0-9]+)?|(?:ww‌​w.|[-;:&=\\+$,\\w]+@)[A-Za-z0-9.-]+)((?:\\/[\\+~%\\/.\\w-_]*)?\\??(?:[-\\+=&;%@.\\w_]*)#?‌​(?:[\\w]*))?)";

    static HttpClient Client => HttpClientHelper.DefaultClient;

    [GeneratedRegex(UriRegexStr)]
    private static partial Regex UriRegex();

    /// <summary>
    ///     正则匹配Uri
    /// </summary>
    /// <param name="uri">待处理Uri</param>
    /// <returns>匹配的Uri</returns>
    public static string RegexMatchUri(string uri)
    {
        return UriRegex().Match(uri).Value;
    }

    /// <summary>
    ///     Http Delete方法
    /// </summary>
    /// <param name="address">Post地址</param>
    /// <param name="data">数据</param>
    /// <param name="contentType">ContentType</param>
    /// <param name="auth">Auth 字段</param>
    /// <returns></returns>
    public static async Task<HttpResponseMessage> Delete(
        string address,
        string data,
        string contentType = "application/json",
        ValueTuple<string, string> auth = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, new Uri(address))
        {
            Content = new StringContent(data, Encoding.UTF8, contentType)
        };

        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));

        if (auth != default &&
            !string.IsNullOrEmpty(auth.Item1) &&
            !string.IsNullOrEmpty(auth.Item2))
            req.Headers.Authorization = new AuthenticationHeaderValue(auth.Item1, auth.Item2);

        var acceptLanguage = new StringWithQualityHeaderValue(CultureInfo.CurrentCulture.Name);

        req.Headers.AcceptLanguage.Add(acceptLanguage);
        var response = await Client.SendAsync(req);

        return response;
    }

    /// <summary>
    ///     Http Get方法
    /// </summary>
    /// <param name="address">Get地址</param>
    /// <param name="auth">Auth 字段</param>
    /// <returns>获取到的字符串</returns>
    public static async Task<HttpResponseMessage> Get(
        string address,
        ValueTuple<string, string> auth = default,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(address));

        var acceptLanguage = new StringWithQualityHeaderValue(CultureInfo.CurrentCulture.Name);
        req.Headers.AcceptLanguage.Add(acceptLanguage);

        if (auth != default &&
            !string.IsNullOrEmpty(auth.Item1) &&
            !string.IsNullOrEmpty(auth.Item2))
            req.Headers.Authorization = new AuthenticationHeaderValue(auth.Item1, auth.Item2);

        var res = await Client.SendAsync(req, ct);
        return res;
    }

    /// <summary>
    ///     Http Post Form 数据
    /// </summary>
    /// <param name="address">Post 地址</param>
    /// <param name="data">数据</param>
    /// <returns>服务器返回数据</returns>
    public static async Task<HttpResponseMessage> PostFormData(string address,
        IEnumerable<KeyValuePair<string, string>> data)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, address)
        {
            Content = new FormUrlEncodedContent(data)
        };

        var response = await Client.SendAsync(req);
        return response;
    }

    /// <summary>
    ///     Http Post方法
    /// </summary>
    /// <param name="address">Post地址</param>
    /// <param name="data">数据</param>
    /// <param name="contentType">ContentType</param>
    /// <param name="auth">Auth 字段</param>
    /// <returns></returns>
    public static async Task<HttpResponseMessage> Post(
        string address,
        string data,
        string contentType = "application/json",
        ValueTuple<string, string> auth = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(address))
        {
            Content = new StringContent(data, Encoding.UTF8, contentType)
        };

        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));

        if (auth != default &&
            !string.IsNullOrEmpty(auth.Item1) &&
            !string.IsNullOrEmpty(auth.Item2))
            req.Headers.Authorization = new AuthenticationHeaderValue(auth.Item1, auth.Item2);

        var acceptLanguage = new StringWithQualityHeaderValue(CultureInfo.CurrentCulture.Name);

        req.Headers.AcceptLanguage.Add(acceptLanguage);
        var response = await Client.SendAsync(req);

        return response;
    }

    /// <summary>
    ///     Http Post方法（带参数）
    /// </summary>
    /// <param name="address">Post地址</param>
    /// <param name="param">参数</param>
    /// <param name="contentType">ContentType</param>
    /// <returns></returns>
    public static async Task<HttpResponseMessage> PostWithParams(
        string address,
        IEnumerable<KeyValuePair<string, string>> param,
        string contentType = "application/json")
    {
        using var content = new FormUrlEncodedContent(param);
        content.Headers.ContentType = new MediaTypeWithQualityHeaderValue(contentType);

        var acceptLanguage = new StringWithQualityHeaderValue(CultureInfo.CurrentCulture.Name);

        using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(address))
        {
            Content = content
        };

        req.Headers.AcceptLanguage.Add(acceptLanguage);
        var response = await Client.SendAsync(req);

        return response;
    }
}